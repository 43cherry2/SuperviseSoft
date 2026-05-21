const cloudbase = require("@cloudbase/node-sdk");
const fetch = require("node-fetch");

const app = cloudbase.init({
  env: process.env.CLOUDBASE_ENV_ID || process.env.TCB_ENV || cloudbase.SYMBOL_CURRENT_ENV,
});
const db = app.database();
const MAX_BYTES = Number(process.env.UPLOAD_MAX_BYTES || 5 * 1024 * 1024);

exports.main = async (event, context) => {
  try {
    const userId = await requireUserId(event, context);
    const body = readBody(event);
    const task = await requireOwnedTask(userId, body.taskId);
    const fileType = requireImageType(body.fileType);
    const fileName = sanitizeFileName(body.fileName, fileType);
    const fileSize = Math.max(0, Number(body.fileSize || 0));
    if (fileSize > MAX_BYTES) throw { code: 40007, message: "图片超过当前上传大小限制" };

    const storagePath = `users/${userId}/tasks/${task._id}/${Date.now()}_${fileName}`;
    const now = new Date().toISOString();
    await db.collection("uploaded_files").add({
      userId,
      taskId: task._id,
      fileName,
      fileType,
      storagePath,
      fileUrl: "",
      cloudFileId: "",
      status: "created",
      fileSize,
      createdAt: now,
      updatedAt: now,
    });

    return ok({
      taskId: task._id,
      fileName,
      fileType,
      storagePath,
      uploadUrl: "/uploadFile",
      maxBytes: MAX_BYTES,
    });
  } catch (error) {
    return fail(error.code || 40001, error.message || "获取上传信息失败");
  }
};

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

function requireImageType(fileType) {
  const value = String(fileType || "").toLowerCase();
  if (value === "image/jpeg" || value === "image/jpg") return "image/jpeg";
  if (value === "image/png") return "image/png";
  throw { code: 40004, message: "第一版只支持 JPG/PNG 图片" };
}

function sanitizeFileName(fileName, fileType) {
  const fallback = fileType === "image/png" ? "homework.png" : "homework.jpg";
  const raw = String(fileName || fallback).trim() || fallback;
  const cleaned = raw.replace(/[^\w.\-\u4e00-\u9fa5]/g, "_").replace(/_+/g, "_").slice(0, 80);
  const hasExtension = /\.(jpg|jpeg|png)$/i.test(cleaned);
  if (hasExtension) return cleaned;
  return `${cleaned}${fileType === "image/png" ? ".png" : ".jpg"}`;
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
