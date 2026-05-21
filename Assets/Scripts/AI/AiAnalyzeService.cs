using System;
using System.Collections;
using SuperviseSoft.Auth;
using SuperviseSoft.Core;
using UnityEngine;

namespace SuperviseSoft.AI
{
  public sealed class AiAnalyzeService : MonoBehaviour
  {
    private static AiAnalyzeService _instance;

    public static AiAnalyzeService Instance
    {
      get
      {
        if (_instance != null)
        {
          return _instance;
        }

        var existing = FindObjectOfType<AiAnalyzeService>();
        if (existing != null)
        {
          _instance = existing;
          return _instance;
        }

        var obj = new GameObject("AiAnalyzeService");
        _instance = obj.AddComponent<AiAnalyzeService>();
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

    public IEnumerator AnalyzeHomeworkImage(
      string taskId,
      string fileId,
      Action<string> onStatus,
      Action<ApiResponse<AnalyzeHomeworkImageResult>> onCompleted)
    {
      if (!EnsureLoggedIn(onCompleted))
      {
        yield break;
      }

      onStatus?.Invoke(AiJobStatus.Pending);
      var request = new AnalyzeHomeworkImageRequest
      {
        taskId = taskId,
        fileId = fileId,
      };

      onStatus?.Invoke(AiJobStatus.Processing);
      yield return CloudApiClient.Instance.PostJson<AnalyzeHomeworkImageRequest, AnalyzeHomeworkImageResult>(
        "/analyzeHomeworkImage",
        request,
        response =>
        {
          if (response.success)
          {
            onStatus?.Invoke(response.data?.job?.status ?? AiJobStatus.Success);
          }
          else
          {
            onStatus?.Invoke(AiJobStatus.Failed);
          }

          Complete(response, onCompleted, "AI 分析失败");
        });
    }

    public IEnumerator GetAiResult(
      string jobId,
      Action<ApiResponse<GetAiResultResponse>> onCompleted)
    {
      if (!EnsureLoggedIn(onCompleted))
      {
        yield break;
      }

      if (string.IsNullOrWhiteSpace(jobId))
      {
        Complete(ApiResponse<GetAiResultResponse>.Fail(40003, "缺少 AI 任务 ID", ApiErrorKind.BusinessError), onCompleted, "获取 AI 结果失败");
        yield break;
      }

      yield return CloudApiClient.Instance.GetJson<GetAiResultResponse>(
        $"/getAiResult?jobId={Uri.EscapeDataString(jobId)}",
        response => Complete(response, onCompleted, "获取 AI 结果失败"));
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
  }
}
