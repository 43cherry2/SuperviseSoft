const cloudbase = require("@cloudbase/node-sdk");
const fetch = require("node-fetch");

const app = cloudbase.init({
  env: process.env.CLOUDBASE_ENV_ID || process.env.TCB_ENV || cloudbase.SYMBOL_CURRENT_ENV,
});

exports.main = async (event, context) => {
  try {
    const body = readBody(event);
    const phone = normalizePhone(body.phone);
    const scene = normalizeScene(body.scene);
    const target = scene === "register" ? "ANY" : "USER";

    const result = await callAuthApi(context, event, "/auth/v1/verification", "POST", {
      phone_number: phone,
      target,
    });

    return ok({
      verificationId: result.verification_id,
      expiresIn: result.expires_in || 600,
      isUser: !!result.is_user,
      scene,
    });
  } catch (error) {
    return fail(error.code || 40001, error.message || "发送验证码失败");
  }
};

async function callAuthApi(context, event, path, method, data, authorization) {
  // CloudBase Auth HTTP API uses verification_id -> verification_token -> signin/signup.
  // If CloudBase changes the HTTP flow later, replace this adapter without changing Unity.
  const response = await fetch(`${getAuthBaseUrl(context)}${path}`, {
    method,
    headers: {
      "Content-Type": "application/json",
      Accept: "application/json",
      "x-device-id": getHeader(event, "x-device-id") || "unity-auth-device",
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

function readBody(event) {
  if (!event) return {};
  if (event.body) {
    return typeof event.body === "string" ? JSON.parse(event.body || "{}") : event.body;
  }
  if (event.rawBody) return JSON.parse(event.rawBody || "{}");
  return event;
}

function getAuthBaseUrl(context) {
  if (process.env.CLOUDBASE_AUTH_BASE_URL) return process.env.CLOUDBASE_AUTH_BASE_URL.replace(/\/$/, "");
  const parsed = cloudbase.parseContext ? cloudbase.parseContext(context) : {};
  const envId =
    process.env.CLOUDBASE_ENV_ID ||
    process.env.TCB_ENV ||
    (parsed && parsed.environ && parsed.environ.TCB_ENV);
  if (!envId) throw { code: 50001, message: "缺少 CloudBase 环境 ID" };
  const intl = process.env.CLOUDBASE_AUTH_REGION === "intl" ? ".intl" : "";
  return `https://${envId}.api${intl}.tcloudbasegateway.com`;
}

function normalizeScene(scene) {
  const value = (scene || "").trim();
  if (["register", "login", "reset_password"].includes(value)) return value;
  throw { code: 40002, message: "验证码场景不正确" };
}

function normalizePhone(phone) {
  const digits = String(phone || "").replace(/\D/g, "");
  const local = digits.length === 13 && digits.startsWith("86") ? digits.slice(2) : digits;
  if (!/^1[3-9]\d{9}$/.test(local)) throw { code: 40003, message: "手机号格式不正确" };
  return `+86 ${local}`;
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
