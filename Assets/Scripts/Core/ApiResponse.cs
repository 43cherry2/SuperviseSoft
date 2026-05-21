using System;

namespace SuperviseSoft.Core
{
  public enum ApiErrorKind
  {
    None,
    NetworkError,
    HttpError,
    BusinessError,
    JsonParseError,
  }

  [Serializable]
  public class ApiResponse<T>
  {
    public bool success;
    public int code;
    public string message;
    public T data;
    public ApiErrorKind errorKind;
    public long httpStatus;
    public string rawBody;

    public static ApiResponse<T> Ok(T data)
    {
      return new ApiResponse<T>
      {
        success = true,
        code = 0,
        message = "ok",
        data = data,
        errorKind = ApiErrorKind.None,
      };
    }

    public static ApiResponse<T> Fail(int code, string message, ApiErrorKind kind, long httpStatus = 0, string rawBody = null)
    {
      return new ApiResponse<T>
      {
        success = false,
        code = code,
        message = string.IsNullOrWhiteSpace(message) ? "请求失败" : message,
        data = default,
        errorKind = kind,
        httpStatus = httpStatus,
        rawBody = rawBody,
      };
    }
  }

  [Serializable]
  public class EmptyResponse
  {
  }

  [Serializable]
  public class CloudFunctionEnvelope<T>
  {
    public ApiResponse<T> result;
    public string requestId;
    public long timestamp;
  }
}
