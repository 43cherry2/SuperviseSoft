const cloudbase = require("@cloudbase/node-sdk");
const fetch = require("node-fetch");

const app = cloudbase.init({
  env: process.env.CLOUDBASE_ENV_ID || process.env.TCB_ENV || cloudbase.SYMBOL_CURRENT_ENV,
});
const db = app.database();

exports.main = async (event, context) => {
  let authUid = "";
  let phoneMasked = "";

  try {
    const authorization = getHeader(event, "authorization");
    if (!authorization || !authorization.toLowerCase().startsWith("bearer ")) {
      throw { code: 40101, message: "缺少登录令牌" };
    }

    const profile = await callAuthApi(context, event, "/auth/v1/user/me", "GET", null, authorization);
    authUid = profile.sub || profile.user_id || "";
    phoneMasked = maskPhone(profile.phone_number || "");

    await callAuthApi(context, event, "/auth/v1/user/signout", "POST", {}, authorization);
    await writeLoginLog(authUid, phoneMasked, "logout", true, "");

    return ok({});
  } catch (error) {
    await safeWriteLoginLog(authUid, phoneMasked, "logout", false, error.message);
    return fail(error.code || 40001, error.message || "退出登录失败");
  }
};

async function writeLoginLog(authUid, phoneMasked, loginType, success, errorMessage) {
  await db.collection("auth_login_logs").add({
    authUid: authUid || "",
    phoneMasked: phoneMasked || "",
    loginType,
    success,
    errorMessage: errorMessage || "",
    createdAt: new Date().toISOString(),
  });
}

async function safeWriteLoginLog(authUid, phoneMasked, loginType, success, errorMessage) {
  try {
    await writeLoginLog(authUid, phoneMasked, loginType, success, errorMessage);
  } catch (_) {
  }
}

async function callAuthApi(context, event, path, method, data, authorization) {
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

function maskPhone(phone) {
  const digits = String(phone || "").replace(/\D/g, "");
  const local = digits.length >= 11 ? digits.slice(-11) : digits;
  return /^1[3-9]\d{9}$/.test(local) ? `${local.slice(0, 3)}****${local.slice(7)}` : "";
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
