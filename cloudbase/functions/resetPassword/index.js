const cloudbase = require("@cloudbase/node-sdk");
const fetch = require("node-fetch");

const app = cloudbase.init({
  env: process.env.CLOUDBASE_ENV_ID || process.env.TCB_ENV || cloudbase.SYMBOL_CURRENT_ENV,
});
const db = app.database();

exports.main = async (event, context) => {
  const body = readBody(event);
  let authUid = "";
  let phoneMasked = maskPhone(body.phone);

  try {
    const phone = normalizePhone(body.phone);
    const password = normalizePassword(body.password || body.newPassword);
    const verificationId = requireString(body.verificationId, "verificationId");
    const code = requireString(body.code, "code");

    const verified = await callAuthApi(context, event, "/auth/v1/verification/verify", "POST", {
      verification_id: verificationId,
      verification_code: code,
    });

    await callAuthApi(context, event, "/auth/v1/reset", "POST", {
      phone_number: phone,
      new_password: password,
      verification_token: verified.verification_token,
    });

    const token = await callAuthApi(context, event, "/auth/v1/signin", "POST", {
      username: phone,
      password,
    });

    const profile = await getProfile(context, event, token);
    authUid = token.sub || profile.sub || profile.user_id;
    phoneMasked = maskPhone(profile.phone_number || phone);
    const user = await upsertUser(authUid, profile.phone_number || phone, phoneMasked, true);
    await writeLoginLog(authUid, phoneMasked, "reset_password", true, "");

    return ok({
      session: buildSession(token, authUid, phoneMasked),
      user,
    });
  } catch (error) {
    await safeWriteLoginLog(authUid, phoneMasked, "reset_password", false, error.message);
    return fail(error.code || 40001, error.message || "重置密码失败");
  }
};

async function getProfile(context, event, token) {
  return callAuthApi(context, event, "/auth/v1/user/me", "GET", null, `${token.token_type || "Bearer"} ${token.access_token}`);
}

async function upsertUser(authUid, phone, phoneMasked, touchLastLogin) {
  if (!authUid) throw { code: 50002, message: "缺少用户身份 ID" };
  const now = new Date().toISOString();
  const users = db.collection("users");
  const existing = await users.where({ authUid }).limit(1).get();
  const patch = {
    authUid,
    phone,
    phoneMasked,
    nickname: "student",
    role: "student",
    updatedAt: now,
    ...(touchLastLogin ? { lastLoginAt: now } : {}),
  };
  if (existing.data && existing.data.length > 0) {
    await users.doc(existing.data[0]._id).update(patch);
    return { ...existing.data[0], ...patch };
  }
  const record = {
    ...patch,
    createdAt: now,
    lastLoginAt: now,
  };
  await users.add(record);
  return record;
}

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

function buildSession(token, authUid, phoneMasked) {
  const expiresIn = Number(token.expires_in || 0);
  return {
    authUid,
    token: token.access_token,
    refreshToken: token.refresh_token || "",
    tokenType: token.token_type || "Bearer",
    expiresIn,
    expiresAtUnix: Math.floor(Date.now() / 1000) + expiresIn,
    phoneMasked,
  };
}

function readBody(event) {
  if (!event) return {};
  if (event.body) return typeof event.body === "string" ? JSON.parse(event.body || "{}") : event.body;
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

function normalizePhone(phone) {
  const digits = String(phone || "").replace(/\D/g, "");
  const local = digits.length === 13 && digits.startsWith("86") ? digits.slice(2) : digits;
  if (!/^1[3-9]\d{9}$/.test(local)) throw { code: 40003, message: "手机号格式不正确" };
  return `+86 ${local}`;
}

function maskPhone(phone) {
  const digits = String(phone || "").replace(/\D/g, "");
  const local = digits.length >= 11 ? digits.slice(-11) : digits;
  return /^1[3-9]\d{9}$/.test(local) ? `${local.slice(0, 3)}****${local.slice(7)}` : "";
}

function normalizePassword(password) {
  const value = String(password || "");
  if (value.length < 6) throw { code: 40004, message: "密码至少需要 6 位" };
  return value;
}

function requireString(value, name) {
  const text = String(value || "").trim();
  if (!text) throw { code: 40005, message: `缺少参数：${name}` };
  return text;
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
