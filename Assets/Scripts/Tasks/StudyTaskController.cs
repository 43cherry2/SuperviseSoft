using System;
using SuperviseSoft.AI;
using SuperviseSoft.Core;
using SuperviseSoft.Upload;
using UnityEngine;

namespace SuperviseSoft.Tasks
{
  public sealed class StudyTaskController : MonoBehaviour
  {
    private static StudyTaskController _instance;

    public static StudyTaskController Instance
    {
      get
      {
        if (_instance != null)
        {
          return _instance;
        }

        var existing = FindObjectOfType<StudyTaskController>();
        if (existing != null)
        {
          _instance = existing;
          return _instance;
        }

        var obj = new GameObject("StudyTaskController");
        _instance = obj.AddComponent<StudyTaskController>();
        DontDestroyOnLoad(obj);
        return _instance;
      }
    }

    public StudyTask CurrentTask { get; private set; }
    public UploadedFileRecord CurrentFile { get; private set; }
    public AiJob CurrentAiJob { get; private set; }
    public AiResult CurrentAiResult { get; private set; }

    public event Action<ApiResponse<GetTaskListResult>> TaskListLoaded;
    public event Action<ApiResponse<GetTaskDetailResult>> TaskDetailLoaded;
    public event Action<ApiResponse<CreateTaskResult>> TaskCreated;
    public event Action<ApiResponse<UpdateTaskStatusResult>> TaskStatusUpdated;

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

    public void CreateTask(string title, string description, int estimatedMinutes)
    {
      StartCoroutine(StudyTaskService.Instance.CreateTask(title, description, estimatedMinutes, response =>
      {
        if (response.success && response.data?.task != null)
        {
          CurrentTask = response.data.task;
        }

        TaskCreated?.Invoke(response);
      }));
    }

    public void LoadTaskList()
    {
      StartCoroutine(StudyTaskService.Instance.GetTaskList(TaskListLoaded));
    }

    public void LoadTaskDetail(string taskId)
    {
      StartCoroutine(StudyTaskService.Instance.GetTaskDetail(taskId, response =>
      {
        if (response.success && response.data != null)
        {
          CurrentTask = response.data.task;
          CurrentFile = response.data.uploadedFiles != null && response.data.uploadedFiles.Length > 0
            ? response.data.uploadedFiles[0]
            : null;
          CurrentAiJob = response.data.aiJobs != null && response.data.aiJobs.Length > 0
            ? response.data.aiJobs[0]
            : null;
          CurrentAiResult = response.data.latestAiResult;
        }

        TaskDetailLoaded?.Invoke(response);
      }));
    }

    public void UpdateTaskStatus(string taskId, string status, int actualMinutes)
    {
      StartCoroutine(StudyTaskService.Instance.UpdateTaskStatus(taskId, status, actualMinutes, response =>
      {
        if (response.success && response.data?.task != null)
        {
          CurrentTask = response.data.task;
        }

        TaskStatusUpdated?.Invoke(response);
      }));
    }

    public void SetCurrentFile(UploadedFileRecord file)
    {
      CurrentFile = file;
    }

    public void SetCurrentAiResult(AiJob job, AiResult result)
    {
      CurrentAiJob = job;
      CurrentAiResult = result;
    }
  }
}
