using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using SuperviseSoft.Auth;
using UnityEngine;
using UnityEngine.Networking;

namespace SuperviseSoft.Core
{
  public sealed class CloudApiClient : MonoBehaviour
  {
    private const string DeviceIdKey = "SuperviseSoft.Auth.DeviceId";
    private static CloudApiClient _instance;

    public static CloudApiClient Instance
    {
      get
      {
        if (_instance != null)
        {
          return _instance;
        }

        var existing = FindObjectOfType<CloudApiClient>();
        if (existing != null)
        {
          _instance = existing;
          return _instance;
        }

        var obj = new GameObject("CloudApiClient");
        _instance = obj.AddComponent<CloudApiClient>();
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

    public IEnumerator GetJson<TResponse>(
      string path,
      Action<ApiResponse<TResponse>> onCompleted,
      Dictionary<string, string> headers = null)
    {
      var url = BuildUrl(path);
      using var request = UnityWebRequest.Get(url);
      ConfigureRequest(request, headers);
      yield return Send<TResponse>(request, onCompleted);
    }

    public IEnumerator PostJson<TRequest, TResponse>(
      string path,
      TRequest body,
      Action<ApiResponse<TResponse>> onCompleted,
      Dictionary<string, string> headers = null)
    {
      var json = body == null ? "{}" : JsonUtility.ToJson(body);
      yield return PostJson<TResponse>(path, json, onCompleted, headers);
    }

    public IEnumerator PostJson<TResponse>(
      string path,
      string json,
      Action<ApiResponse<TResponse>> onCompleted,
      Dictionary<string, string> headers = null)
    {
      var url = BuildUrl(path);
      using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
      var payload = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(json) ? "{}" : json);
      request.uploadHandler = new UploadHandlerRaw(payload);
      request.downloadHandler = new DownloadHandlerBuffer();
      ConfigureRequest(request, headers);
      yield return Send<TResponse>(request, onCompleted);
    }

    public IEnumerator PostBytes<TResponse>(
      string path,
      byte[] bytes,
      string contentType,
      Action<float> onProgress,
      Action<ApiResponse<TResponse>> onCompleted,
      Dictionary<string, string> headers = null)
    {
      var url = BuildUrl(path);
      using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
      request.uploadHandler = new UploadHandlerRaw(bytes ?? Array.Empty<byte>());
      request.downloadHandler = new DownloadHandlerBuffer();
      ConfigureRequest(request, headers);
      request.SetRequestHeader("Content-Type", string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
      yield return Send<TResponse>(request, onCompleted, onProgress);
    }

    private static string BuildUrl(string path)
    {
      if (!string.IsNullOrWhiteSpace(path) &&
          (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
      {
        return path;
      }

      var baseUrl = AppConfig.ApiBaseUrl?.TrimEnd('/') ?? string.Empty;
      var normalizedPath = string.IsNullOrWhiteSpace(path) ? string.Empty : path.TrimStart('/');
      return $"{baseUrl}/{normalizedPath}";
    }

    private static void ConfigureRequest(UnityWebRequest request, Dictionary<string, string> headers)
    {
      request.timeout = Mathf.Max(1, AppConfig.TimeoutSeconds);
      request.SetRequestHeader("Content-Type", "application/json");
      request.SetRequestHeader("Accept", "application/json");
      request.SetRequestHeader("x-device-id", GetOrCreateDeviceId());

      var authHeader = AuthManager.Instance.GetAuthHeader();
      if (!string.IsNullOrWhiteSpace(authHeader))
      {
        request.SetRequestHeader("Authorization", authHeader);
      }

      if (headers == null)
      {
        return;
      }

      foreach (var item in headers)
      {
        if (!string.IsNullOrWhiteSpace(item.Key) && item.Value != null)
        {
          request.SetRequestHeader(item.Key, item.Value);
        }
      }
    }

    private static IEnumerator Send<TResponse>(
      UnityWebRequest request,
      Action<ApiResponse<TResponse>> onCompleted,
      Action<float> onProgress = null)
    {
      var operation = request.SendWebRequest();
      while (!operation.isDone)
      {
        onProgress?.Invoke(request.uploadProgress < 0f ? 0f : request.uploadProgress);
        yield return null;
      }

      onProgress?.Invoke(1f);

      var body = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;

      if (request.result == UnityWebRequest.Result.ConnectionError ||
          request.result == UnityWebRequest.Result.DataProcessingError)
      {
        onCompleted?.Invoke(ApiResponse<TResponse>.Fail(
          -10001,
          request.error,
          ApiErrorKind.NetworkError,
          request.responseCode,
          body));
        yield break;
      }

      if (request.result == UnityWebRequest.Result.ProtocolError)
      {
        var message = TryReadErrorMessage(body, request.error);
        onCompleted?.Invoke(ApiResponse<TResponse>.Fail(
          -10002,
          message,
          ApiErrorKind.HttpError,
          request.responseCode,
          body));
        yield break;
      }

      if (string.IsNullOrWhiteSpace(body))
      {
        onCompleted?.Invoke(ApiResponse<TResponse>.Fail(
          -10003,
          "服务器返回为空",
          ApiErrorKind.JsonParseError,
          request.responseCode,
          body));
        yield break;
      }

      ApiResponse<TResponse> parsed;
      try
      {
        parsed = JsonUtility.FromJson<ApiResponse<TResponse>>(body);
        if (parsed != null &&
            !parsed.success &&
            string.IsNullOrEmpty(parsed.message) &&
            body.Contains("\"result\""))
        {
          var envelope = JsonUtility.FromJson<CloudFunctionEnvelope<TResponse>>(body);
          if (envelope?.result != null)
          {
            parsed = envelope.result;
          }
        }
      }
      catch (Exception exception)
      {
        onCompleted?.Invoke(ApiResponse<TResponse>.Fail(
          -10004,
          $"JSON 解析失败：{exception.Message}",
          ApiErrorKind.JsonParseError,
          request.responseCode,
          body));
        yield break;
      }

      if (parsed == null)
      {
        onCompleted?.Invoke(ApiResponse<TResponse>.Fail(
          -10004,
          "JSON 解析失败",
          ApiErrorKind.JsonParseError,
          request.responseCode,
          body));
        yield break;
      }

      parsed.httpStatus = request.responseCode;
      parsed.rawBody = body;
      parsed.errorKind = parsed.success ? ApiErrorKind.None : ApiErrorKind.BusinessError;
      onCompleted?.Invoke(parsed);
    }

    private static string TryReadErrorMessage(string body, string fallback)
    {
      if (string.IsNullOrWhiteSpace(body))
      {
        return string.IsNullOrWhiteSpace(fallback) ? "HTTP 请求错误" : fallback;
      }

      try
      {
        var error = JsonUtility.FromJson<ApiResponse<EmptyResponse>>(body);
        if (!string.IsNullOrWhiteSpace(error?.message))
        {
          return error.message;
        }
      }
      catch
      {
        // Keep the transport error visible when the body is not our ApiResponse shape.
      }

      return string.IsNullOrWhiteSpace(fallback) ? body : fallback;
    }

    private static string GetOrCreateDeviceId()
    {
      var deviceId = PlayerPrefs.GetString(DeviceIdKey, string.Empty);
      if (!string.IsNullOrWhiteSpace(deviceId))
      {
        return deviceId;
      }

      deviceId = Guid.NewGuid().ToString("N");
      PlayerPrefs.SetString(DeviceIdKey, deviceId);
      PlayerPrefs.Save();
      return deviceId;
    }
  }
}
