const cloudbase = require("@cloudbase/node-sdk");
const fetch = require("node-fetch");

const app = cloudbase.init({
  env: process.env.CLOUDBASE_ENV_ID || process.env.TCB_ENV || cloudbase.SYMBOL_CURRENT_ENV,
});
const db = app.database();

exports.main = async (event, context) => {
  let job = null;
  let userId = "";
  let taskId = "";

  try {
    userId = await requireUserId(event, context);
    const body = readBody(event);
    taskId = String(body.taskId || "").trim();
    const inputText = requireText(body.text);
    const task = await requireOwnedTask(userId, taskId);
    const now = new Date().toISOString();
    const model = getProviderModel();

    job = {
      userId,
      taskId: task._id,
      fileId: "",
      inputType: "text",
      status: "processing",
      model,
      errorMessage: "",
      createdAt: now,
      updatedAt: now,
    };
    const jobAdd = await db.collection("ai_jobs").add(job);
    job._id = jobAdd.id;

    const ai = await analyzeWithProvider({ task, inputText, model });
    const resultRecord = {
      userId,
      taskId: task._id,
      fileId: "",
      jobId: job._id,
      inputType: "text",
      inputText,
      fileName: "",
      summary: ai.summary,
      estimatedMinutes: ai.estimatedMinutes,
      suggestedSteps: ai.suggestedSteps,
      rawResponse: JSON.stringify(ai.rawResponse || ai),
      createdAt: new Date().toISOString(),
    };
    const resultAdd = await db.collection("ai_results").add(resultRecord);
    resultRecord._id = resultAdd.id;

    const successPatch = { status: "success", errorMessage: "", updatedAt: new Date().toISOString() };
    await db.collection("ai_jobs").doc(job._id).update(successPatch);
    await writeUsageLog(userId, "ai_analyze_text", task._id, "", job._id, model, ai.tokenUsage || null);
    return ok({ job: { ...job, ...successPatch }, result: resultRecord });
  } catch (error) {
    const message = error.message || "AI 文字分析失败";
    if (job && job._id) {
      await safeUpdateJobFailed(job._id, message);
      await safeWriteUsageLog(userId, "ai_analyze_text", taskId, "", job._id, job.model || getProviderModel(), null);
    }
    return fail(error.code || 40001, message);
  }
};

async function analyzeWithProvider({ task, inputText, model }) {
  if (!process.env.AI_API_KEY || !process.env.AI_API_URL) {
    return mockAnalyze(task, inputText, model);
  }
  return realAnalyze(task, inputText, model);
}

function mockAnalyze(task, inputText, model) {
  const estimatedMinutes = Math.max(15, Math.min(90, Math.ceil(inputText.length / 25)));
  return {
    summary: `已分析文字任务。内容重点是：${inputText.slice(0, 80)}${inputText.length > 80 ? "..." : ""}`,
    estimatedMinutes,
    suggestedSteps: [
      { title: "拆分要求", minutes: 5, description: "先圈出题目要求、交付物和限制条件。" },
      { title: "集中完成", minutes: Math.max(5, estimatedMinutes - 10), description: "按题目顺序完成主要内容，卡住的地方先标记。" },
      { title: "检查修正", minutes: 5, description: "检查遗漏、错字、单位和答题格式。" },
    ],
    rawResponse: { provider: "MockAiProvider", model },
    tokenUsage: { promptTokens: 0, completionTokens: 0, totalTokens: 0 },
  };
}

async function realAnalyze(task, inputText, model) {
  const response = await fetch(process.env.AI_API_URL, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${process.env.AI_API_KEY}` },
    body: JSON.stringify({
      model,
      messages: [
        { role: "system", content: "你是学习任务规划助手。请根据文字任务输出 JSON：summary、estimatedMinutes、suggestedSteps。" },
        { role: "user", content: `任务：${task.title}\n描述：${task.description || ""}\n文字内容：${inputText}` },
      ],
      temperature: 0.2,
    }),
  });
  const text = await response.text();
  if (!response.ok) throw { code: response.status, message: text || response.statusText };
  const json = text ? JSON.parse(text) : {};
  const content = json.choices && json.choices[0] && json.choices[0].message ? json.choices[0].message.content : "";
  const parsed = safeParseAiJson(content);
  return {
    summary: parsed.summary || content || "AI 已完成文字分析。",
    estimatedMinutes: Number(parsed.estimatedMinutes || task.estimatedMinutes || 30),
    suggestedSteps: Array.isArray(parsed.suggestedSteps) ? parsed.suggestedSteps : mockAnalyze(task, inputText, model).suggestedSteps,
    rawResponse: json,
    tokenUsage: json.usage || null,
  };
}

function safeParseAiJson(content) {
  try {
    const start = content.indexOf("{");
    const end = content.lastIndexOf("}");
    if (start >= 0 && end > start) return JSON.parse(content.slice(start, end + 1));
    return JSON.parse(content);
  } catch (_) {
    return {};
  }
}

function requireText(value) {
  const text = String(value || "").trim();
  if (!text) throw { code: 40003, message: "请输入要分析的文字" };
  if (text.length > 4000) throw { code: 40004, message: "文字内容过长" };
  return text;
}

function getProviderModel() {
  if (!process.env.AI_API_KEY || !process.env.AI_API_URL) return "mock-study-text-v1";
  return process.env.AI_MODEL || "replaceable-study-model";
}

async function requireOwnedTask(userId, taskId) {
  const id = String(taskId || "").trim();
  if (!id) throw { code: 40003, message: "缺少任务 ID" };
  const result = await db.collection("study_tasks").where({ _id: id, userId }).limit(1).get();
  if (!result.data || result.data.length === 0) throw { code: 40401, message: "任务不存在或无权访问" };
  return result.data[0];
}

async function writeUsageLog(userId, type, taskId, fileId, jobId, model, tokenUsage) {
  await db.collection("usage_logs").add({ userId, type, taskId, fileId, jobId, model, tokenUsage: tokenUsage || null, createdAt: new Date().toISOString() });
}

async function safeWriteUsageLog(userId, type, taskId, fileId, jobId, model, tokenUsage) {
  try { await writeUsageLog(userId, type, taskId, fileId, jobId, model, tokenUsage); } catch (_) {}
}

async function safeUpdateJobFailed(jobId, message) {
  try { await db.collection("ai_jobs").doc(jobId).update({ status: "failed", errorMessage: message, updatedAt: new Date().toISOString() }); } catch (_) {}
}

async function requireUserId(event, context) {
  const authorization = getHeader(event, "authorization");
  if (!authorization || !authorization.toLowerCase().startsWith("bearer ")) throw { code: 40101, message: "请先登录" };
  const profile = await callAuthApi(context, event, "/auth/v1/user/me", "GET", null, authorization);
  const userId = profile.sub || profile.user_id;
  if (!userId) throw { code: 40102, message: "登录态无效" };
  return userId;
}

async function callAuthApi(context, event, path, method, data, authorization) {
  const response = await fetch(`${getAuthBaseUrl(context)}${path}`, {
    method,
    headers: { "Content-Type": "application/json", Accept: "application/json", "x-device-id": getHeader(event, "x-device-id") || "unity-device", ...(authorization ? { Authorization: authorization } : {}) },
    body: method === "GET" ? undefined : JSON.stringify(data || {}),
  });
  const text = await response.text();
  const json = text ? JSON.parse(text) : {};
  if (!response.ok || json.error || json.error_code) throw { code: json.error_code || response.status || 40001, message: json.error_description || json.message || json.error || response.statusText };
  return json;
}

function getAuthBaseUrl(context) {
  if (process.env.CLOUDBASE_AUTH_BASE_URL) return process.env.CLOUDBASE_AUTH_BASE_URL.replace(/\/$/, "");
  const parsed = cloudbase.parseContext ? cloudbase.parseContext(context) : {};
  const envId = process.env.CLOUDBASE_ENV_ID || process.env.TCB_ENV || (parsed && parsed.environ && parsed.environ.TCB_ENV);
  if (!envId) throw { code: 50001, message: "缺少 CloudBase 环境 ID" };
  const intl = process.env.CLOUDBASE_AUTH_REGION === "intl" ? ".intl" : "";
  return `https://${envId}.api${intl}.tcloudbasegateway.com`;
}

function readBody(event) {
  if (!event) return {};
  if (event.body) return typeof event.body === "string" ? JSON.parse(event.body || "{}") : event.body;
  if (event.rawBody) return JSON.parse(event.rawBody || "{}");
  return event;
}

function getHeader(event, name) {
  const headers = event && (event.headers || event.header || {});
  const lower = name.toLowerCase();
  for (const key of Object.keys(headers)) {
    if (key.toLowerCase() === lower) return headers[key];
  }
  return "";
}

function ok(data) {
  return { success: true, code: 0, message: "ok", data };
}

function fail(code, message) {
  return { success: false, code, message, data: null };
}
