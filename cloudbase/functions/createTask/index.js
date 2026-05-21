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
    const title = requireString(body.title, "title", 80);
    const description = optionalString(body.description, 500);
    const estimatedMinutes = requirePositiveInt(body.estimatedMinutes, "estimatedMinutes");
    const now = new Date().toISOString();

    const task = {
      userId,
      title,
      description,
      estimatedMinutes,
      actualMinutes: 0,
      status: "created",
      createdAt: now,
      updatedAt: now,
    };

    const result = await db.collection("study_tasks").add(task);
    task._id = result.id;
    return ok({ task });
  } catch (error) {
    return fail(error.code || 40001, error.message || "创建学习任务失败");
  }
};

async function requireUserId(event, context) {
  const authorization = getHeader(event, "authorization");
  if (!authorization || !authorization.toLowerCase().startsWith("bearer ")) {
    throw { code: 40101, message: "请先登录" };
  }

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
  if (!response.ok || json.error || json.error_code) {
    throw {
      code: json.error_code || response.status || 40001,
      message: json.error_description || json.message || json.error || response.statusText,
    };
  }
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

function requireString(value, name, maxLength) {
  const text = String(value || "").trim();
  if (!text) throw { code: 40003, message: `缺少参数：${name}` };
  if (maxLength && text.length > maxLength) throw { code: 40004, message: `${name} 过长` };
  return text;
}

function optionalString(value, maxLength) {
  const text = String(value || "").trim();
  return maxLength && text.length > maxLength ? text.slice(0, maxLength) : text;
}

function requirePositiveInt(value, name) {
  const number = Number(value);
  if (!Number.isFinite(number) || number <= 0) throw { code: 40005, message: `${name} 必须大于 0` };
  return Math.floor(number);
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
