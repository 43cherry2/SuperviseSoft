using System;
using System.Collections;
using SuperviseSoft.Auth;
using SuperviseSoft.Core;
using UnityEngine;

namespace SuperviseSoft.Tasks
{
  public sealed class StudyTaskService : MonoBehaviour
  {
    private static StudyTaskService _instance;

    public static StudyTaskService Instance
    {
      get
      {
        if (_instance != null)
        {
          return _instance;
        }

        var existing = FindObjectOfType<StudyTaskService>();
        if (existing != null)
        {
          _instance = existing;
          return _instance;
        }

        var obj = new GameObject("StudyTaskService");
        _instance = obj.AddComponent<StudyTaskService>();
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

    public IEnumerator CreateTask(
      string title,
      string description,
      int estimatedMinutes,
      Action<ApiResponse<CreateTaskResult>> onCompleted)
    {
      if (!EnsureLoggedIn(onCompleted))
      {
        yield break;
      }

      var request = new CreateTaskRequest
      {
        title = title,
        description = description,
        estimatedMinutes = estimatedMinutes,
      };

      yield return CloudApiClient.Instance.PostJson<CreateTaskRequest, CreateTaskResult>(
        "/createTask",
        request,
        response => Complete(response, onCompleted, "创建学习任务失败"));
    }

    public IEnumerator GetTaskList(Action<ApiResponse<GetTaskListResult>> onCompleted)
    {
      if (!EnsureLoggedIn(onCompleted))
      {
        yield break;
      }

      yield return CloudApiClient.Instance.GetJson<GetTaskListResult>(
        "/getTaskList",
        response => Complete(response, onCompleted, "获取任务列表失败"));
    }

    public IEnumerator GetTaskDetail(string taskId, Action<ApiResponse<GetTaskDetailResult>> onCompleted)
    {
      if (!EnsureLoggedIn(onCompleted))
      {
        yield break;
      }

      if (string.IsNullOrWhiteSpace(taskId))
      {
        Complete(ApiResponse<GetTaskDetailResult>.Fail(40003, "缺少任务 ID", ApiErrorKind.BusinessError), onCompleted, "获取任务详情失败");
        yield break;
      }

      yield return CloudApiClient.Instance.GetJson<GetTaskDetailResult>(
        $"/getTaskDetail?taskId={Uri.EscapeDataString(taskId)}",
        response => Complete(response, onCompleted, "获取任务详情失败"));
    }

    public IEnumerator UpdateTaskStatus(
      string taskId,
      string status,
      int actualMinutes,
      Action<ApiResponse<UpdateTaskStatusResult>> onCompleted)
    {
      if (!EnsureLoggedIn(onCompleted))
      {
        yield break;
      }

      if (!StudyTaskStatus.IsValid(status))
      {
        Complete(ApiResponse<UpdateTaskStatusResult>.Fail(40004, "任务状态不正确", ApiErrorKind.BusinessError), onCompleted, "更新任务状态失败");
        yield break;
      }

      var request = new UpdateTaskStatusRequest
      {
        taskId = taskId,
        status = status,
        actualMinutes = Mathf.Max(0, actualMinutes),
      };

      yield return CloudApiClient.Instance.PostJson<UpdateTaskStatusRequest, UpdateTaskStatusResult>(
        "/updateTaskStatus",
        request,
        response => Complete(response, onCompleted, "更新任务状态失败"));
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
