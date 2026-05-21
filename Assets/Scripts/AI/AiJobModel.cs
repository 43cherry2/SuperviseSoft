using System;

namespace SuperviseSoft.AI
{
  [Serializable]
  public class AiJob
  {
    public string _id;
    public string userId;
    public string taskId;
    public string fileId;
    public string inputType;
    public string status;
    public string model;
    public string errorMessage;
    public string createdAt;
    public string updatedAt;
  }

  [Serializable]
  public class AnalyzeHomeworkImageRequest
  {
    public string taskId;
    public string fileId;
  }

  [Serializable]
  public class AnalyzeHomeworkImageResult
  {
    public AiJob job;
    public AiResult result;
  }

  [Serializable]
  public class AnalyzeStudyTextRequest
  {
    public string taskId;
    public string text;
  }

  [Serializable]
  public class AnalyzeStudyTextResult
  {
    public AiJob job;
    public AiResult result;
  }

  public static class AiJobStatus
  {
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Success = "success";
    public const string Failed = "failed";
  }
}
