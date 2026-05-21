using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SuperviseSoft.Auth;
using SuperviseSoft.Core;
using UnityEngine;

namespace SuperviseSoft.Upload
{
  public sealed class FileUploadService : MonoBehaviour
  {
    private static FileUploadService _instance;

    public static FileUploadService Instance
    {
      get
      {
        if (_instance != null)
        {
          return _instance;
        }

        var existing = FindObjectOfType<FileUploadService>();
        if (existing != null)
        {
          _instance = existing;
          return _instance;
        }

        var obj = new GameObject("FileUploadService");
        _instance = obj.AddComponent<FileUploadService>();
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

    public IEnumerator GetUploadInfo(
      string taskId,
      string fileName,
      string fileType,
      long fileSize,
      Action<ApiResponse<UploadInfoResult>> onCompleted)
    {
      if (!EnsureLoggedIn(onCompleted))
      {
        yield break;
      }

      var request = new GetUploadInfoRequest
      {
        taskId = taskId,
        fileName = fileName,
        fileType = fileType,
        fileSize = fileSize,
      };

      yield return CloudApiClient.Instance.PostJson<GetUploadInfoRequest, UploadInfoResult>(
        "/getUploadInfo",
        request,
        response => Complete(response, onCompleted, "获取上传信息失败"));
    }

    public IEnumerator ConfirmFileUploaded(
      ConfirmFileUploadedRequest request,
      Action<ApiResponse<ConfirmFileUploadedResult>> onCompleted)
    {
      if (!EnsureLoggedIn(onCompleted))
      {
        yield break;
      }

      yield return CloudApiClient.Instance.PostJson<ConfirmFileUploadedRequest, ConfirmFileUploadedResult>(
        "/confirmFileUploaded",
        request,
        response => Complete(response, onCompleted, "确认文件上传失败"));
    }

    public IEnumerator UploadImageForTask(
      string taskId,
      string localPath,
      Action<FileUploadProgress> onStatus,
      Action<ApiResponse<ConfirmFileUploadedResult>> onCompleted)
    {
      if (!EnsureLoggedIn(onCompleted))
      {
        yield break;
      }

      Notify(onStatus, FileUploadPhase.Started, 0f, "开始读取图片");

      if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
      {
        FailUpload(40003, "图片路径不存在", onStatus, onCompleted);
        yield break;
      }

      var fileName = Path.GetFileName(localPath);
      var fileType = GetMimeType(localPath);
      if (string.IsNullOrWhiteSpace(fileType))
      {
        FailUpload(40004, "第一版只支持 JPG/PNG 图片", onStatus, onCompleted);
        yield break;
      }

      byte[] bytes;
      try
      {
        bytes = File.ReadAllBytes(localPath);
      }
      catch (Exception exception)
      {
        FailUpload(40005, $"读取图片失败：{exception.Message}", onStatus, onCompleted);
        yield break;
      }

      ApiResponse<UploadInfoResult> infoResponse = null;
      yield return GetUploadInfo(taskId, fileName, fileType, bytes.LongLength, response => infoResponse = response);
      if (infoResponse == null || !infoResponse.success || infoResponse.data == null)
      {
        FailUpload(infoResponse, onStatus, onCompleted, "获取上传信息失败");
        yield break;
      }

      if (infoResponse.data.maxBytes > 0 && bytes.LongLength > infoResponse.data.maxBytes)
      {
        FailUpload(40006, "图片超过当前上传大小限制", onStatus, onCompleted);
        yield break;
      }

      Notify(onStatus, FileUploadPhase.Uploading, 0f, "正在上传图片");
      var headers = new Dictionary<string, string>
      {
        { "x-task-id", taskId },
        { "x-storage-path", infoResponse.data.storagePath },
        { "x-file-type", fileType },
      };

      ApiResponse<UploadFileResult> uploadResponse = null;
      yield return CloudApiClient.Instance.PostBytes<UploadFileResult>(
        string.IsNullOrWhiteSpace(infoResponse.data.uploadUrl) ? "/uploadFile" : infoResponse.data.uploadUrl,
        bytes,
        fileType,
        progress => Notify(onStatus, FileUploadPhase.Uploading, progress, $"正在上传图片 {Mathf.RoundToInt(progress * 100f)}%"),
        response =>
        {
          Complete(response, value => uploadResponse = value, "上传图片失败");
        },
        headers);

      if (uploadResponse == null || !uploadResponse.success || uploadResponse.data == null)
      {
        FailUpload(uploadResponse, onStatus, onCompleted, "上传图片失败");
        yield break;
      }

      var confirmRequest = new ConfirmFileUploadedRequest
      {
        taskId = taskId,
        fileName = fileName,
        fileType = fileType,
        storagePath = uploadResponse.data.storagePath,
        fileId = uploadResponse.data.fileId,
        fileUrl = uploadResponse.data.fileUrl,
        fileSize = bytes.LongLength,
      };

      ApiResponse<ConfirmFileUploadedResult> confirmResponse = null;
      yield return ConfirmFileUploaded(confirmRequest, response => confirmResponse = response);
      if (confirmResponse != null && confirmResponse.success)
      {
        Notify(onStatus, FileUploadPhase.Success, 1f, "图片上传成功");
      }
      else
      {
        Notify(onStatus, FileUploadPhase.Failed, 1f, confirmResponse?.message ?? "确认文件上传失败");
      }

      onCompleted?.Invoke(confirmResponse);
    }

    private static bool EnsureLoggedIn<T>(Action<ApiResponse<T>> onCompleted)
    {
      if (AuthManager.Instance.IsLoggedIn)
      {
        return true;
      }

      var response = ApiResponse<T>.Fail(40101, "请先登录", ApiErrorKind.BusinessError);
      Debug.LogError($"业务接口被拦截：{response.message}");
      onCompleted?.Invoke(response);
      return false;
    }

    private static void Complete<T>(ApiResponse<T> response, Action<ApiResponse<T>> onCompleted, string fallbackLog)
    {
      if (response == null)
      {
        response = ApiResponse<T>.Fail(40001, fallbackLog, ApiErrorKind.BusinessError);
      }

      if (!response.success)
      {
        Debug.LogError($"{fallbackLog}：{response.message} kind={response.errorKind} http={response.httpStatus}");
      }

      onCompleted?.Invoke(response);
    }

    private static void FailUpload(
      ApiResponse<UploadInfoResult> response,
      Action<FileUploadProgress> onStatus,
      Action<ApiResponse<ConfirmFileUploadedResult>> onCompleted,
      string fallback)
    {
      var failed = ApiResponse<ConfirmFileUploadedResult>.Fail(
        response?.code ?? 40001,
        response?.message ?? fallback,
        response?.errorKind ?? ApiErrorKind.BusinessError,
        response?.httpStatus ?? 0,
        response?.rawBody);
      Debug.LogError($"{fallback}：{failed.message}");
      Notify(onStatus, FileUploadPhase.Failed, 0f, failed.message);
      onCompleted?.Invoke(failed);
    }

    private static void FailUpload(
      ApiResponse<UploadFileResult> response,
      Action<FileUploadProgress> onStatus,
      Action<ApiResponse<ConfirmFileUploadedResult>> onCompleted,
      string fallback)
    {
      var failed = ApiResponse<ConfirmFileUploadedResult>.Fail(
        response?.code ?? 40001,
        response?.message ?? fallback,
        response?.errorKind ?? ApiErrorKind.BusinessError,
        response?.httpStatus ?? 0,
        response?.rawBody);
      Debug.LogError($"{fallback}：{failed.message}");
      Notify(onStatus, FileUploadPhase.Failed, 0f, failed.message);
      onCompleted?.Invoke(failed);
    }

    private static void FailUpload(
      int code,
      string message,
      Action<FileUploadProgress> onStatus,
      Action<ApiResponse<ConfirmFileUploadedResult>> onCompleted)
    {
      var failed = ApiResponse<ConfirmFileUploadedResult>.Fail(code, message, ApiErrorKind.BusinessError);
      Debug.LogError($"上传图片失败：{failed.message}");
      Notify(onStatus, FileUploadPhase.Failed, 0f, failed.message);
      onCompleted?.Invoke(failed);
    }

    private static void Notify(Action<FileUploadProgress> onStatus, FileUploadPhase phase, float progress, string message)
    {
      onStatus?.Invoke(new FileUploadProgress
      {
        phase = phase,
        progress = Mathf.Clamp01(progress),
        message = message,
      });
    }

    private static string GetMimeType(string path)
    {
      var extension = Path.GetExtension(path)?.ToLowerInvariant();
      if (extension == ".jpg" || extension == ".jpeg")
      {
        return "image/jpeg";
      }

      if (extension == ".png")
      {
        return "image/png";
      }

      return string.Empty;
    }
  }
}
