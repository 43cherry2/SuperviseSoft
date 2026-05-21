using System;
using SuperviseSoft.AI;
using SuperviseSoft.Upload;

namespace SuperviseSoft.Tasks
{
  [Serializable]
  public class StudyTask
  {
    public string _id;
    public string userId;
    public string title;
    public string description;
    public int estimatedMinutes;
    public int actualMinutes;
    public string status;
    public string createdAt;
    public string updatedAt;
  }

  [Serializable]
  public class CreateTaskRequest
  {
    public string title;
    public string description;
    public int estimatedMinutes;
  }

  [Serializable]
  public class CreateTaskResult
  {
    public StudyTask task;
  }

  [Serializable]
  public class GetTaskListResult
  {
    public StudyTask[] tasks;
  }

  [Serializable]
  public class GetTaskDetailResult
  {
    public StudyTask task;
    public UploadedFileRecord[] uploadedFiles;
    public AiJob[] aiJobs;
    public AiResult latestAiResult;
  }

  [Serializable]
  public class UpdateTaskStatusRequest
  {
    public string taskId;
    public string status;
    public int actualMinutes;
  }

  [Serializable]
  public class UpdateTaskStatusResult
  {
    public StudyTask task;
  }

  public static class StudyTaskStatus
  {
    public const string Created = "created";
    public const string Running = "running";
    public const string Paused = "paused";
    public const string Finished = "finished";
    public const string Cancelled = "cancelled";

    public static bool IsValid(string status)
    {
      return status == Created ||
             status == Running ||
             status == Paused ||
             status == Finished ||
             status == Cancelled;
    }
  }
}
