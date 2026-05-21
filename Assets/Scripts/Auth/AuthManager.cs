using SuperviseSoft.Core;
using UnityEngine;

namespace SuperviseSoft.Auth
{
  public sealed class AuthManager : MonoBehaviour
  {
    private const string TokenKey = "SuperviseSoft.Auth.Token";
    private const string RefreshTokenKey = "SuperviseSoft.Auth.RefreshToken";
    private const string TokenTypeKey = "SuperviseSoft.Auth.TokenType";
    private const string AuthUidKey = "SuperviseSoft.Auth.AuthUid";
    private const string PhoneMaskedKey = "SuperviseSoft.Auth.PhoneMasked";
    private const string ExpiresAtKey = "SuperviseSoft.Auth.ExpiresAtUnix";

    private static AuthManager _instance;

    public static AuthManager Instance
    {
      get
      {
        if (_instance != null)
        {
          return _instance;
        }

        var existing = FindObjectOfType<AuthManager>();
        if (existing != null)
        {
          _instance = existing;
          return _instance;
        }

        var obj = new GameObject("AuthManager");
        _instance = obj.AddComponent<AuthManager>();
        DontDestroyOnLoad(obj);
        return _instance;
      }
    }

    public string CurrentUserId { get; private set; }
    public string CurrentToken { get; private set; }
    public string CurrentPhoneMasked { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(CurrentToken) && !string.IsNullOrWhiteSpace(CurrentUserId);

    private string _refreshToken;
    private string _tokenType = "Bearer";
    private long _expiresAtUnix;

    private void Awake()
    {
      if (_instance != null && _instance != this)
      {
        Destroy(gameObject);
        return;
      }

      _instance = this;
      DontDestroyOnLoad(gameObject);
      Initialize();
    }

    public void Initialize()
    {
      CurrentToken = PlayerPrefs.GetString(TokenKey, string.Empty);
      _refreshToken = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
      _tokenType = PlayerPrefs.GetString(TokenTypeKey, "Bearer");
      CurrentUserId = PlayerPrefs.GetString(AuthUidKey, string.Empty);
      CurrentPhoneMasked = PlayerPrefs.GetString(PhoneMaskedKey, string.Empty);
      _expiresAtUnix = ReadLong(ExpiresAtKey);

      if (!string.IsNullOrWhiteSpace(CurrentToken) &&
          _expiresAtUnix > 0 &&
          UnixNow() >= _expiresAtUnix)
      {
        ClearSession();
      }
    }

    public void SaveSession(AuthSession session)
    {
      if (session == null || string.IsNullOrWhiteSpace(session.token) || string.IsNullOrWhiteSpace(session.authUid))
      {
        return;
      }

      CurrentUserId = session.authUid;
      CurrentToken = session.token;
      _refreshToken = session.refreshToken;
      _tokenType = string.IsNullOrWhiteSpace(session.tokenType) ? "Bearer" : session.tokenType;
      CurrentPhoneMasked = session.phoneMasked;
      _expiresAtUnix = session.expiresAtUnix > 0
        ? session.expiresAtUnix
        : UnixNow() + Mathf.Max(0, session.expiresIn);

      PlayerPrefs.SetString(AuthUidKey, CurrentUserId);
      PlayerPrefs.SetString(TokenKey, CurrentToken);
      PlayerPrefs.SetString(RefreshTokenKey, _refreshToken ?? string.Empty);
      PlayerPrefs.SetString(TokenTypeKey, _tokenType);
      PlayerPrefs.SetString(PhoneMaskedKey, CurrentPhoneMasked ?? string.Empty);
      PlayerPrefs.SetString(ExpiresAtKey, _expiresAtUnix.ToString());
      PlayerPrefs.Save();
    }

    public void ClearSession()
    {
      CurrentUserId = string.Empty;
      CurrentToken = string.Empty;
      CurrentPhoneMasked = string.Empty;
      _refreshToken = string.Empty;
      _tokenType = "Bearer";
      _expiresAtUnix = 0;

      PlayerPrefs.DeleteKey(AuthUidKey);
      PlayerPrefs.DeleteKey(TokenKey);
      PlayerPrefs.DeleteKey(RefreshTokenKey);
      PlayerPrefs.DeleteKey(TokenTypeKey);
      PlayerPrefs.DeleteKey(PhoneMaskedKey);
      PlayerPrefs.DeleteKey(ExpiresAtKey);
      PlayerPrefs.Save();
    }

    public void Logout()
    {
      ClearSession();
    }

    public string GetAuthHeader()
    {
      if (string.IsNullOrWhiteSpace(CurrentToken))
      {
        return string.Empty;
      }

      return $"{(string.IsNullOrWhiteSpace(_tokenType) ? "Bearer" : _tokenType)} {CurrentToken}";
    }

    private static long ReadLong(string key)
    {
      var value = PlayerPrefs.GetString(key, "0");
      return long.TryParse(value, out var result) ? result : 0;
    }

    private static long UnixNow()
    {
      return System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
  }
}
