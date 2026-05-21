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
    const storagePath = String(body.storagePath || "").trim();
    validateStoragePath(userId, task._id, storagePath);
    const pendingFile = await requirePendingFile(userId, task._id, storagePath);
    if (!pendingFile.cloudFileId) throw { code: 40008, message: "云存储文件尚未上传成功" };

    const patch = {
      status: "uploaded",
      updatedAt: new Date().toISOString(),
    };
    await db.collection("uploaded_files").doc(pendingFile._id).update(patch);
    return ok({ file: { ...pendingFile, ...patch } });
  } catch (error) {
    return fail(error.code || 40001, error.message || "确认文件上传失败");
  }
};

async function requirePendingFile(userId, taskId, storagePath) {
  const result = await db.collection("uploaded_files").where({ userId, taskId, storagePath, status: "created" }).limit(1).get();
  if (!result.data || result.data.length === 0) throw { code: 40402, message: "上传信息不存在或已确认" };
  return result.data[0];
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

function validateStoragePath(userId, taskId, storagePath) {
  const prefix = `users/${userId}/tasks/${taskId}/`;
  if (!storagePath.startsWith(prefix) || storagePath.includes("..")) throw { code: 40006, message: "上传路径不合法" };
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
