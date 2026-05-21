using System;
using System.Collections;
using System.Collections.Generic;
using SuperviseSoft.Core;
using UnityEngine;

namespace SuperviseSoft.Auth
{
  public sealed class AuthService : MonoBehaviour
  {
    private static AuthService _instance;
    private readonly Dictionary<string, string> _verificationIds = new Dictionary<string, string>();

    public static AuthService Instance
    {
      get
      {
        if (_instance != null)
        {
          return _instance;
        }

        var existing = FindObjectOfType<AuthService>();
        if (existing != null)
        {
          _instance = existing;
          return _instance;
        }

        var obj = new GameObject("AuthService");
        _instance = obj.AddComponent<AuthService>();
        DontDestroyOnLoad(obj);
        return _instance;
      }
    }

    private void Awake()
    {
      if (_instance != null && _instance != this)
      {
        Destroy(gameObject);
        return;
      }

      _instance = this;
      DontDestroyOnLoad(gameObject);
    }

    public IEnumerator SendSmsCode(string phone, string scene, Action<ApiResponse<SmsCodeResult>> onCompleted)
    {
      var request = new SendSmsCodeRequest
      {
        phone = phone,
        scene = scene,
      };

      yield return CloudApiClient.Instance.PostJson<SendSmsCodeRequest, SmsCodeResult>(
        "/sendSmsCode",
        request,
        response =>
        {
          if (response.success && response.data != null && !string.IsNullOrWhiteSpace(response.data.verificationId))
          {
            _verificationIds[CacheKey(phone, scene)] = response.data.verificationId;
          }

          onCompleted?.Invoke(response);
        });
    }

    public IEnumerator RegisterWithPhoneCodeAndPassword(
      string phone,
      string code,
      string password,
      Action<ApiResponse<AuthResult>> onCompleted)
    {
      var request = new PhoneCodePasswordRequest
      {
        phone = phone,
        code = code,
        password = password,
        verificationId = GetVerificationId(phone, "register"),
      };

      yield return CloudApiClient.Instance.PostJson<PhoneCodePasswordRequest, AuthResult>(
        "/registerWithPhone",
        request,
        response =>
        {
          SaveSessionIfPresent(response);
          onCompleted?.Invoke(response);
        });
    }

    public IEnumerator LoginWithSmsCode(string phone, string code, Action<ApiResponse<AuthResult>> onCompleted)
    {
      var request = new PhoneCodeRequest
      {
        phone = phone,
        code = code,
        verificationId = GetVerificationId(phone, "login"),
      };

      yield return CloudApiClient.Instance.PostJson<PhoneCodeRequest, AuthResult>(
        "/loginWithSmsCode",
        request,
        response =>
        {
          SaveSessionIfPresent(response);
          onCompleted?.Invoke(response);
        });
    }

    public IEnumerator LoginWithPassword(string phone, string password, Action<ApiResponse<AuthResult>> onCompleted)
    {
      var request = new PhonePasswordRequest
      {
        phone = phone,
        password = password,
      };

      yield return CloudApiClient.Instance.PostJson<PhonePasswordRequest, AuthResult>(
        "/loginWithPassword",
        request,
        response =>
        {
          SaveSessionIfPresent(response);
          onCompleted?.Invoke(response);
        });
    }

    public IEnumerator ResetPasswordWithSmsCode(
      string phone,
      string code,
      string newPassword,
      Action<ApiResponse<AuthResult>> onCompleted)
    {
      var request = new PhoneCodePasswordRequest
      {
        phone = phone,
        code = code,
        password = newPassword,
        verificationId = GetVerificationId(phone, "reset_password"),
      };

      yield return CloudApiClient.Instance.PostJson<PhoneCodePasswordRequest, AuthResult>(
        "/resetPassword",
        request,
        response =>
        {
          SaveSessionIfPresent(response);
          onCompleted?.Invoke(response);
        });
    }

    public IEnumerator Logout(Action<ApiResponse<EmptyResponse>> onCompleted)
    {
      yield return CloudApiClient.Instance.PostJson<EmptyResponse>(
        "/logout",
        "{}",
        response =>
        {
          AuthManager.Instance.ClearSession();
          onCompleted?.Invoke(response);
        });
    }

    public IEnumerator GetCurrentUser(Action<ApiResponse<AuthUser>> onCompleted)
    {
      yield return CloudApiClient.Instance.GetJson<AuthUser>("/getCurrentUser", onCompleted);
    }

    public bool IsLoggedIn()
    {
      return AuthManager.Instance.IsLoggedIn;
    }

    private static void SaveSessionIfPresent(ApiResponse<AuthResult> response)
    {
      if (response.success && response.data?.session != null)
      {
        AuthManager.Instance.SaveSession(response.data.session);
      }
    }

    private string GetVerificationId(string phone, string scene)
    {
      _verificationIds.TryGetValue(CacheKey(phone, scene), out var value);
      return value;
    }

    private static string CacheKey(string phone, string scene)
    {
      return $"{NormalizePhoneKey(phone)}:{scene ?? string.Empty}";
    }

    private static string NormalizePhoneKey(string phone)
    {
      if (string.IsNullOrWhiteSpace(phone))
      {
        return string.Empty;
      }

      return phone.Replace(" ", string.Empty).Replace("+86", string.Empty).Trim();
    }
  }
}
