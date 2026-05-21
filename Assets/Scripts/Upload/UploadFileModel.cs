using System;

namespace SuperviseSoft.Upload
{
  public enum FileUploadPhase
  {
    Started,
    Uploading,
    Success,
    Failed,
  }

  [Serializable]
  public class FileUploadProgress
  {
    public FileUploadPhase phase;
    public float progress;
    public string message;
  }

  [Serializable]
  public class UploadedFileRecord
  {
    public string _id;
    public string userId;
    public string taskId;
    public string fileName;
    public string fileType;
    public string storagePath;
    public string fileUrl;
    public string cloudFileId;
    public string status;
    public long fileSize;
    public string createdAt;
    public string updatedAt;
  }

  [Serializable]
  public class GetUploadInfoRequest
  {
    public string taskId;
    public string fileName;
    public string fileType;
    public long fileSize;
  }

  [Serializable]
  public class UploadInfoResult
  {
    public string taskId;
    public string fileName;
    public string fileType;
    public string storagePath;
    public string uploadUrl;
    public long maxBytes;
  }

  [Serializable]
  public class UploadFileResult
  {
    public string fileId;
    public string storagePath;
    public string fileUrl;
  }

  [Serializable]
  public class ConfirmFileUploadedRequest
  {
    public string taskId;
    public string fileName;
    public string fileType;
    public string storagePath;
    public string fileId;
    public string fileUrl;
    public long fileSize;
  }

  [Serializable]
  public class ConfirmFileUploadedResult
  {
    public UploadedFileRecord file;
  }
}
