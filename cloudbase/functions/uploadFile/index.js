const cloudbase = require("@cloudbase/node-sdk");
const fetch = require("node-fetch");

const app = cloudbase.init({
  env: process.env.CLOUDBASE_ENV_ID || process.env.TCB_ENV || cloudbase.SYMBOL_CURRENT_ENV,
});
const db = app.database();
const MAX_BYTES = Number(process.env.UPLOAD_MAX_BYTES || 5 * 1024 * 1024);

// MVP HTTP 上传适配层：Unity 上传图片字节流到本云函数，云函数再写入 CloudBase 云存储。
// 适合第一版和小文件。后续如果 Unity 接入 CloudBase 官方存储直传能力，应替换为客户端直传。
exports.main = async (event, context) => {
  try {
    const userId = await requireUserId(event, context);
    const taskId = getHeader(event, "x-task-id");
    const storagePath = getHeader(event, "x-storage-path");
    const fileType = requireImageType(getHeader(event, "x-file-type"));
    await requireOwnedTask(userId, taskId);
    validateStoragePath(userId, taskId, storagePath);
    const pendingFile = await requirePendingFile(userId, taskId, storagePath, fileType);
    const bytes = readBinaryBody(event);
    if (!bytes || bytes.length === 0) throw { code: 40005, message: "上传内容为空" };
    if (bytes.length > MAX_BYTES) throw { code: 40007, message: "图片超过当前上传大小限制" };

    const uploadResult = await app.uploadFile({
      cloudPath: storagePath,
      fileContent: bytes,
    });
    const cloudFileId = uploadResult.fileID || "";
    const now = new Date().toISOString();
    await db.collection("uploaded_files").doc(pendingFile._id).update({
      cloudFileId,
      fileUrl: cloudFileId,
      fileSize: bytes.length,
      updatedAt: now,
    });

    return ok({
      fileId: cloudFileId,
      storagePath,
      fileUrl: cloudFileId,
    });
  } catch (error) {
    return fail(error.code || 40001, error.message || "上传图片失败");
  }
};

async function requirePendingFile(userId, taskId, storagePath, fileType) {
  const result = await db.collection("uploaded_files").where({ userId, taskId, storagePath, fileType, status: "created" }).limit(1).get();
  if (!result.data || result.data.length === 0) throw { code: 40402, message: "上传信息不存在或已失效" };
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
  const path = String(storagePath || "");
  const prefix = `users/${userId}/tasks/${taskId}/`;
  if (!path.startsWith(prefix) || path.includes("..")) throw { code: 40006, message: "上传路径不合法" };
}

function readBinaryBody(event) {
  if (!event) return Buffer.alloc(0);
  if (event.isBase64Encoded && event.body) return Buffer.from(event.body, "base64");
  if (event.body && typeof event.body === "string") {
    const text = event.body;
    if (text.trim().startsWith("{")) {
      const json = JSON.parse(text);
      return Buffer.from(json.fileBase64 || "", "base64");
    }
    return Buffer.from(text, "binary");
  }
  if (event.rawBody) return Buffer.isBuffer(event.rawBody) ? event.rawBody : Buffer.from(event.rawBody, "binary");
  return Buffer.alloc(0);
}

function requireImageType(fileType) {
  const value = String(fileType || "").toLowerCase();
  if (value === "image/jpeg" || value === "image/jpg") return "image/jpeg";
  if (value === "image/png") return "image/png";
  throw { code: 40004, message: "第一版只支持 JPG/PNG 图片" };
}

function getAuthBaseUrl(context) {
  if (process.env.CLOUDBASE_AUTH_BASE_URL) return process.env.CLOUDBASE_AUTH_BASE_URL.replace(/\/$/, "");
  const parsed = cloudbase.parseContext ? cloudbase.parseContext(context) : {};
  const envId = process.env.CLOUDBASE_ENV_ID || process.env.TCB_ENV || (parsed && parsed.environ && parsed.environ.TCB_ENV);
  if (!envId) throw { code: 50001, message: "缺少 CloudBase 环境 ID" };
  const intl = process.env.CLOUDBASE_AUTH_REGION === "intl" ? ".intl" : "";
  return `https://${envId}.api${intl}.tcloudbasegateway.com`;
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
