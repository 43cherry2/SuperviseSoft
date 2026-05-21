using System;
using System.Collections;
using System.IO;
using System.Text;
using SuperviseSoft.AI;
using SuperviseSoft.Core;
using SuperviseSoft.Tasks;
using SuperviseSoft.Upload;
using UnityEngine;
using UnityEngine.UI;

namespace SuperviseSoft.UI
{
  public sealed class TaskDetailPanel : MonoBehaviour
  {
    public GameObject root;
    public Text titleText;
    public Text descriptionText;
    public Text statusText;
    public Text currentItemText;
    public Text dropZoneText;
    public InputField actualMinutesInput;
    public InputField textInput;
    public RawImage imagePreview;
    public ImageDropZone imageDropZone;
    public Button refreshButton;
    public Button analyzeTextButton;
    public Button startCameraButton;
    public Button capturePhotoButton;
    public Button previousItemButton;
    public Button nextItemButton;
    public Button finishTaskButton;
    public Button backListButton;
    public Text messageText;
    public TaskListPanel taskListPanel;
    public AiResultPanel aiResultPanel;

    private string _taskId;
    private StudyTask _task;
    private UploadedFileRecord _latestFile;
    private AiJob _latestJob;
    private AiResult[] _aiResults = new AiResult[0];
    private int _currentResultIndex;
    private WebCamTexture _cameraTexture;
    private bool _bound;

    private void Awake()
    {
      Bind();
    }

    private void Start()
    {
      Bind();
    }

    private void OnDisable()
    {
      StopCamera();
    }

    public void Bind()
    {
      EnsurePanelReferences();
      if (_bound)
      {
        return;
      }

      if (refreshButton == null &&
          analyzeTextButton == null &&
          startCameraButton == null &&
          capturePhotoButton == null &&
          previousItemButton == null &&
          nextItemButton == null &&
          finishTaskButton == null &&
          backListButton == null)
      {
        return;
      }

      _bound = true;
      refreshButton?.onClick.AddListener(() => Load(_taskId));
      analyzeTextButton?.onClick.AddListener(AnalyzeText);
      startCameraButton?.onClick.AddListener(StartCamera);
      capturePhotoButton?.onClick.AddListener(CapturePhotoAndAnalyze);
      previousItemButton?.onClick.AddListener(() => MoveResult(-1));
      nextItemButton?.onClick.AddListener(() => MoveResult(1));
      finishTaskButton?.onClick.AddListener(FinishTask);
      backListButton?.onClick.AddListener(() =>
      {
        EnsurePanelReferences();
        StopCamera();
        Hide();
        taskListPanel?.ShowAndRefresh();
      });

      if (imageDropZone != null)
      {
        imageDropZone.ImagePathDropped = AcceptDraggedImage;
        imageDropZone.SetHint("拖拽 JPG/PNG 到这里，松开后自动分析");
      }
    }

    public void Show()
    {
      PanelVisibility.Show(root, gameObject);
      SetMessage(string.Empty);
    }

    public void ShowAndLoad(string taskId)
    {
      Show();
      Load(taskId);
    }

    public void Hide()
    {
      PanelVisibility.Hide(root, gameObject);
    }

    public void Load(string taskId)
    {
      if (string.IsNullOrWhiteSpace(taskId))
      {
        SetMessage("缺少任务 ID。");
        return;
      }

      _taskId = taskId;
      SetMessage("正在加载本次任务...");
      SetButtons(false);
      StartCoroutine(StudyTaskService.Instance.GetTaskDetail(taskId, response =>
      {
        SetButtons(true);
        if (response.success && response.data != null)
        {
          _task = response.data.task;
          _latestFile = response.data.uploadedFiles != null && response.data.uploadedFiles.Length > 0
            ? response.data.uploadedFiles[0]
            : null;
          _latestJob = response.data.aiJobs != null && response.data.aiJobs.Length > 0
            ? response.data.aiJobs[0]
            : null;
          _aiResults = response.data.aiResults ?? new AiResult[0];
          _currentResultIndex = Mathf.Clamp(_currentResultIndex, 0, Mathf.Max(0, _aiResults.Length - 1));
          StudyTaskController.Instance.SetCurrentFile(_latestFile);
          StudyTaskController.Instance.SetCurrentAiResult(_latestJob, response.data.latestAiResult);
          Render();
          SetMessage("本次任务已更新。");
          return;
        }

        SetMessage(response.message);
      }));
    }

    public void AcceptDraggedImage(string imagePath)
    {
      if (string.IsNullOrWhiteSpace(_taskId))
      {
        SetMessage("请先打开一个任务组。");
        return;
      }

      if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
      {
        SetMessage("拖拽的图片不存在。");
        return;
      }

      byte[] bytes;
      try
      {
        bytes = File.ReadAllBytes(imagePath);
      }
      catch (Exception exception)
      {
        SetMessage($"读取拖拽图片失败：{exception.Message}");
        return;
      }

      PreviewImage(bytes);
      StartCoroutine(UploadAndAnalyzeImage(bytes, Path.GetFileName(imagePath), MimeFromPath(imagePath)));
    }

    private void AnalyzeText()
    {
      if (string.IsNullOrWhiteSpace(_taskId))
      {
        SetMessage("请先打开一个任务组。");
        return;
      }

      var text = textInput == null ? string.Empty : textInput.text.Trim();
      if (string.IsNullOrWhiteSpace(text))
      {
        SetMessage("请输入要分析的文字。");
        return;
      }

      SetButtons(false);
      StartCoroutine(AiAnalyzeService.Instance.AnalyzeStudyText(
        _taskId,
        text,
        status => SetMessage($"AI 状态：{status}"),
        response =>
        {
          SetButtons(true);
          if (response.success && response.data != null)
          {
            _latestJob = response.data.job;
            StudyTaskController.Instance.SetCurrentAiResult(response.data.job, response.data.result);
            aiResultPanel?.ShowResult(response.data.result, response.data.job);
            _currentResultIndex = 0;
            Load(_taskId);
            SetMessage("文字分析完成。");
            return;
          }

          SetMessage(response.message);
        }));
    }

    private void StartCamera()
    {
      StartCoroutine(StartCameraRoutine());
    }

    private IEnumerator StartCameraRoutine()
    {
      if (_cameraTexture != null && _cameraTexture.isPlaying)
      {
        SetMessage("相机已经打开。");
        yield break;
      }

      if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
      {
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
      }

      if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
      {
        SetMessage("没有相机权限，请在系统设置里允许相机访问。");
        yield break;
      }

      if (WebCamTexture.devices == null || WebCamTexture.devices.Length == 0)
      {
        SetMessage("没有检测到可用相机。");
        yield break;
      }

      _cameraTexture = new WebCamTexture();
      _cameraTexture.Play();
      if (imagePreview != null)
      {
        imagePreview.texture = _cameraTexture;
      }

      SetMessage("相机已打开，点击“拍照并分析”。");
    }

    private void CapturePhotoAndAnalyze()
    {
      if (string.IsNullOrWhiteSpace(_taskId))
      {
        SetMessage("请先打开一个任务组。");
        return;
      }

      if (_cameraTexture == null || !_cameraTexture.isPlaying)
      {
        SetMessage("请先打开相机。");
        return;
      }

      if (_cameraTexture.width <= 16 || _cameraTexture.height <= 16)
      {
        SetMessage("相机还在初始化，请稍后再拍。");
        return;
      }

      var texture = new Texture2D(_cameraTexture.width, _cameraTexture.height, TextureFormat.RGB24, false);
      texture.SetPixels(_cameraTexture.GetPixels());
      texture.Apply();
      var bytes = texture.EncodeToJPG(86);
      if (imagePreview != null)
      {
        imagePreview.texture = texture;
      }

      StopCamera(false);
      StartCoroutine(UploadAndAnalyzeImage(bytes, $"camera_{DateTime.Now:yyyyMMdd_HHmmss}.jpg", "image/jpeg"));
    }

    private IEnumerator UploadAndAnalyzeImage(byte[] bytes, string fileName, string fileType)
    {
      SetButtons(false);
      ApiResponse<ConfirmFileUploadedResult> uploadResponse = null;
      yield return FileUploadService.Instance.UploadImageBytesForTask(
        _taskId,
        bytes,
        fileName,
        fileType,
        progress => SetMessage(progress.message),
        response => uploadResponse = response);

      if (uploadResponse == null || !uploadResponse.success || uploadResponse.data?.file == null)
      {
        SetButtons(true);
        SetMessage(uploadResponse?.message ?? "图片上传失败。");
        yield break;
      }

      _latestFile = uploadResponse.data.file;
      StudyTaskController.Instance.SetCurrentFile(_latestFile);
      SetMessage("图片已上传，正在 AI 分析...");

      ApiResponse<AnalyzeHomeworkImageResult> analyzeResponse = null;
      yield return AiAnalyzeService.Instance.AnalyzeHomeworkImage(
        _taskId,
        _latestFile._id,
        status => SetMessage($"AI 状态：{status}"),
        response => analyzeResponse = response);

      SetButtons(true);
      if (analyzeResponse != null && analyzeResponse.success && analyzeResponse.data != null)
      {
        _latestJob = analyzeResponse.data.job;
        StudyTaskController.Instance.SetCurrentAiResult(analyzeResponse.data.job, analyzeResponse.data.result);
        aiResultPanel?.ShowResult(analyzeResponse.data.result, analyzeResponse.data.job);
        _currentResultIndex = 0;
        Load(_taskId);
        SetMessage("图片分析完成。");
        yield break;
      }

      SetMessage(analyzeResponse?.message ?? "图片分析失败。");
    }

    private void FinishTask()
    {
      if (string.IsNullOrWhiteSpace(_taskId))
      {
        SetMessage("请先打开一个任务组。");
        return;
      }

      var actualMinutes = ParseMinutes(actualMinutesInput == null ? string.Empty : actualMinutesInput.text);
      SetButtons(false);
      SetMessage("正在结束本任务并清理明细...");
      StartCoroutine(StudyTaskService.Instance.FinishTask(_taskId, actualMinutes, response =>
      {
        SetButtons(true);
        if (response.success && response.data != null)
        {
          StopCamera();
          aiResultPanel?.ShowFinishSummary(response.data.summary);
          SetMessage("本次任务已结束，任务明细已清理。");
          Hide();
          taskListPanel?.ShowAndRefresh();
          return;
        }

        SetMessage(response.message);
      }));
    }

    private void MoveResult(int delta)
    {
      if (_aiResults == null || _aiResults.Length == 0)
      {
        return;
      }

      _currentResultIndex = (_currentResultIndex + delta + _aiResults.Length) % _aiResults.Length;
      RenderCurrentItem();
    }

    private void Render()
    {
      SetText(titleText, _task == null ? "" : $"{_task.title}（预计 {_task.estimatedMinutes} 分钟）");
      SetText(descriptionText, _task?.description ?? "");
      SetText(statusText, _task == null ? "" : $"状态：{_task.status} | 已分析 {_aiResults.Length} 项");
      RenderCurrentItem();
    }

    private void RenderCurrentItem()
    {
      if (currentItemText == null)
      {
        return;
      }

      if (_aiResults == null || _aiResults.Length == 0)
      {
        currentItemText.text = "本次任务组还没有分析项。\n可以输入文字点击分析，或拖拽图片/拍照后自动分析。";
        return;
      }

      var result = _aiResults[Mathf.Clamp(_currentResultIndex, 0, _aiResults.Length - 1)];
      currentItemText.text = BuildResultText(result, _currentResultIndex + 1, _aiResults.Length);
    }

    private void PreviewImage(byte[] bytes)
    {
      if (imagePreview == null || bytes == null || bytes.Length == 0)
      {
        return;
      }

      var texture = new Texture2D(2, 2);
      if (texture.LoadImage(bytes))
      {
        imagePreview.texture = texture;
      }
    }

    private void EnsurePanelReferences()
    {
      if (taskListPanel == null)
      {
        taskListPanel = UnityEngine.Object.FindObjectOfType<TaskListPanel>(true);
      }

      if (aiResultPanel == null)
      {
        aiResultPanel = UnityEngine.Object.FindObjectOfType<AiResultPanel>(true);
      }
    }

    private void StopCamera(bool clearPreview = true)
    {
      if (_cameraTexture != null)
      {
        if (_cameraTexture.isPlaying)
        {
          _cameraTexture.Stop();
        }

        _cameraTexture = null;
      }

      if (clearPreview && imagePreview != null)
      {
        imagePreview.texture = null;
      }
    }

    private void SetButtons(bool value)
    {
      if (refreshButton != null) refreshButton.interactable = value;
      if (analyzeTextButton != null) analyzeTextButton.interactable = value;
      if (startCameraButton != null) startCameraButton.interactable = value;
      if (capturePhotoButton != null) capturePhotoButton.interactable = value;
      if (previousItemButton != null) previousItemButton.interactable = value;
      if (nextItemButton != null) nextItemButton.interactable = value;
      if (finishTaskButton != null) finishTaskButton.interactable = value;
    }

    private void SetMessage(string message)
    {
      if (messageText != null)
      {
        messageText.text = message ?? string.Empty;
      }
    }

    private static string BuildResultText(AiResult result, int index, int total)
    {
      var builder = new StringBuilder();
      builder.Append("第 ").Append(index).Append(" / ").Append(total).Append(" 项");
      builder.Append(result.inputType == AiInputType.Text ? " | 文字" : " | 图片");
      if (!string.IsNullOrWhiteSpace(result.fileName))
      {
        builder.Append(" | ").Append(result.fileName);
      }

      builder.AppendLine();
      if (!string.IsNullOrWhiteSpace(result.inputText))
      {
        builder.Append("原文：").Append(TrimPreview(result.inputText, 80)).AppendLine();
      }

      builder.Append("预计：").Append(result.estimatedMinutes).AppendLine(" 分钟");
      builder.AppendLine(result.summary);

      if (result.suggestedSteps != null && result.suggestedSteps.Length > 0)
      {
        builder.AppendLine();
        for (var i = 0; i < result.suggestedSteps.Length; i++)
        {
          var step = result.suggestedSteps[i];
          builder.Append(i + 1).Append(". ").Append(step.title).Append(" - ").Append(step.minutes).AppendLine(" 分钟");
          builder.AppendLine(step.description);
        }
      }

      return builder.ToString();
    }

    private static string TrimPreview(string value, int maxLength)
    {
      if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
      {
        return value ?? string.Empty;
      }

      return value.Substring(0, maxLength) + "...";
    }

    private static string MimeFromPath(string path)
    {
      var extension = Path.GetExtension(path)?.ToLowerInvariant();
      if (extension == ".png")
      {
        return "image/png";
      }

      return "image/jpeg";
    }

    private static void SetText(Text text, string value)
    {
      if (text != null)
      {
        text.text = value ?? string.Empty;
      }
    }

    private static int ParseMinutes(string text)
    {
      return int.TryParse(text, out var value) ? Mathf.Max(0, value) : 0;
    }
  }
}
