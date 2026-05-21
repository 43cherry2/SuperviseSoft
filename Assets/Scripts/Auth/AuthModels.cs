using System;

namespace SuperviseSoft.Auth
{
  [Serializable]
  public class SendSmsCodeRequest
  {
    public string phone;
    public string scene;
  }

  [Serializable]
  public class PhoneCodePasswordRequest
  {
    public string phone;
    public string code;
    public string password;
    public string verificationId;
  }

  [Serializable]
  public class PhoneCodeRequest
  {
    public string phone;
    public string code;
    public string verificationId;
  }

  [Serializable]
  public class PhonePasswordRequest
  {
    public string phone;
    public string password;
  }

  [Serializable]
  public class SmsCodeResult
  {
    public string verificationId;
    public int expiresIn;
    public bool isUser;
    public string scene;
  }

  [Serializable]
  public class AuthSession
  {
    public string authUid;
    public string token;
    public string refreshToken;
    public string tokenType;
    public int expiresIn;
    public long expiresAtUnix;
    public string phoneMasked;
  }

  [Serializable]
  public class AuthUser
  {
    public string authUid;
    public string phoneMasked;
    public string nickname;
    public string role;
    public string createdAt;
    public string updatedAt;
    public string lastLoginAt;
  }

  [Serializable]
  public class AuthResult
  {
    public AuthSession session;
    public AuthUser user;
  }
}
