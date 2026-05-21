using System;

namespace SuperviseSoft.AI
{
  [Serializable]
  public class SuggestedStep
  {
    public string title;
    public int minutes;
    public string description;
  }

  [Serializable]
  public class AiResult
  {
    public string _id;
    public string userId;
    public string taskId;
    public string fileId;
    public string jobId;
    public string inputType;
    public string inputText;
    public string fileName;
    public string summary;
    public int estimatedMinutes;
    public SuggestedStep[] suggestedSteps;
    public string rawResponse;
    public string createdAt;
  }

  [Serializable]
  public class GetAiResultResponse
  {
    public AiJob job;
    public AiResult result;
  }

  public static class AiInputType
  {
    public const string Image = "image";
    public const string Text = "text";
  }
}
