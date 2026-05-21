const cloudbase = require("@cloudbase/node-sdk");
const fetch = require("node-fetch");

const app = cloudbase.init({
  env: process.env.CLOUDBASE_ENV_ID || process.env.TCB_ENV || cloudbase.SYMBOL_CURRENT_ENV,
});
const db = app.database();

exports.main = async (event, context) => {
  try {
    const userId = await requireUserId(event, context);
    const body = readBody(event);
    const task = await requireOwnedTask(userId, body.taskId);
    const actualMinutes = Math.max(0, Math.floor(Number(body.actualMinutes || 0)));

    const results = await db.collection("ai_results").where({ userId, taskId: task._id }).limit(100).get();
    const files = await db.collection("uploaded_files").where({ userId, taskId: task._id }).limit(100).get();
    const aiResults = results.data || [];
    const uploadedFiles = files.data || [];
    const finishedAt = new Date().toISOString();
    const summary = buildSummary(task, aiResults, actualMinutes, finishedAt);

    await db.collection("usage_logs").add({
      userId,
      type: "task_finish",
      taskId: task._id,
      fileId: "",
      jobId: "",
      model: "",
      tokenUsage: null,
      summary: {
        itemCount: summary.itemCount,
        imageItemCount: summary.imageItemCount,
        textItemCount: summary.textItemCount,
        estimatedMinutes: summary.estimatedMinutes,
        aiEstimatedMinutes: summary.aiEstimatedMinutes,
        actualMinutes: summary.actualMinutes,
        durationMinutes: summary.durationMinutes,
      },
      createdAt: finishedAt,
    });

    await deleteCloudFiles(uploadedFiles);
    await removeWhere("ai_results", { userId, taskId: task._id });
    await removeWhere("ai_jobs", { userId, taskId: task._id });
    await removeWhere("uploaded_files", { userId, taskId: task._id });
    await db.collection("study_tasks").doc(task._id).remove();

    return ok({ summary });
  } catch (error) {
    return fail(error.code || 40001, error.message || "结束本任务失败");
  }
};

function buildSummary(task, aiResults, actualMinutes, finishedAt) {
  const imageItemCount = aiResults.filter(item => item.inputType === "image").length;
  const textItemCount = aiResults.filter(item => item.inputType === "text").length;
  const aiEstimatedMinutes = aiResults.reduce((sum, item) => sum + Math.max(0, Number(item.estimatedMinutes || 0)), 0);
  const startedAt = task.createdAt || "";
  return {
    taskId: task._id,
    title: task.title || "本次任务",
    itemCount: aiResults.length,
    imageItemCount,
    textItemCount,
    estimatedMinutes: Number(task.estimatedMinutes || 0),
    aiEstimatedMinutes,
    actualMinutes,
    startedAt,
    finishedAt,
    durationMinutes: estimateDurationMinutes(startedAt, finishedAt),
  };
}

function estimateDurationMinutes(startedAt, finishedAt) {
  const start = Date.parse(startedAt);
  const end = Date.parse(finishedAt);
  if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start) return 0;
  return Math.max(1, Math.round((end - start) / 60000));
}

async function deleteCloudFiles(uploadedFiles) {
  const fileList = uploadedFiles
    .map(file => file.cloudFileId || file.fileUrl)
    .filter(value => value && /^cloud:\/\//.test(value));
  if (fileList.length === 0) {
    return;
  }

  try {
    await app.deleteFile({ fileList });
  } catch (_) {
    // 数据库明细仍会清理；云存储删除失败时交给 CloudBase 控制台或后续定时清理兜底。
  }
}

async function removeWhere(collectionName, where) {
  try {
    await db.collection(collectionName).where(where).remove();
  } catch (_) {
    const result = await db.collection(collectionName).where(where).limit(100).get();
    const rows = result.data || [];
    for (const row of rows) {
      if (row._id) {
        await db.collection(collectionName).doc(row._id).remove();
      }
    }
  }
}

async function requireOwnedTask(userId, taskId) {
  const id = String(taskId || "").trim();
  if (!id) throw { code: 40003, message: "缺少任务 ID" };
  const result = await db.collection("study_tasks").where({ _id: id, userId }).limit(1).get();
  if (!result.data || result.data.length === 0) throw { code: 40401, message: "任务不存在或无权访问" };
  return result.data[0];
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
    headers: {
      "Content-Type": "application/json",
      Accept: "application/json",
      "x-device-id": getHeader(event, "x-device-id") || "unity-device",
      ...(authorization ? { Authorization: authorization } : {}),
    },
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
