const cloudbase = require("@cloudbase/node-sdk");
const fetch = require("node-fetch");

const app = cloudbase.init({
  env: process.env.CLOUDBASE_ENV_ID || process.env.TCB_ENV || cloudbase.SYMBOL_CURRENT_ENV,
});
const db = app.database();

exports.main = async (event, context) => {
  try {
    const authorization = getHeader(event, "authorization");
    if (!authorization || !authorization.toLowerCase().startsWith("bearer ")) {
      throw { code: 40101, message: "缺少登录令牌" };
    }

    const profile = await callAuthApi(context, event, "/auth/v1/user/me", "GET", null, authorization);
    const authUid = profile.sub || profile.user_id;
    const phone = profile.phone_number || "";
    const phoneMasked = maskPhone(phone);
    const user = await upsertUser(authUid, phone, phoneMasked, false);

    return ok(user);
  } catch (error) {
    return fail(error.code || 40001, error.message || "获取当前用户失败");
  }
};

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
    lastLoginAt: "",
  };
  await users.add(record);
  return record;
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
