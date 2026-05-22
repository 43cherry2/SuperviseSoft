using SuperviseSoft.AI;
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
    public Text fileText;
    public InputField actualMinutesInput;
    public InputField imagePathInput;
    public Button refreshButton;
    public Button runningButton;
    public Button pausedButton;
    public Button finishedButton;
    public Button uploadButton;
    public Button analyzeButton;
    public Button backListButton;
    public Text messageText;
    public TaskListPanel taskListPanel;
    public AiResultPanel aiResultPanel;

    private string _taskId;
    private StudyTask _task;
    private UploadedFileRecord _latestFile;
    private AiJob _latestJob;
    private bool _bound;

    private void Awake()
    {
      Bind();
    }

    private void Start()
    {
      Bind();
    }

    public void Bind()
    {
      EnsurePanelReferences();
      if (_bound)
      {
        return;
      }

      if (refreshButton == null &&
          runningButton == null &&
          pausedButton == null &&
          finishedButton == null &&
          uploadButton == null &&
          analyzeButton == null &&
          backListButton == null)
      {
        return;
      }

      _bound = true;
      refreshButton?.onClick.AddListener(() => Load(_taskId));
      runningButton?.onClick.AddListener(() => UpdateStatus(StudyTaskStatus.Running));
      pausedButton?.onClick.AddListener(() => UpdateStatus(StudyTaskStatus.Paused));
      finishedButton?.onClick.AddListener(() => UpdateStatus(StudyTaskStatus.Finished));
      uploadButton?.onClick.AddListener(UploadImage);
      analyzeButton?.onClick.AddListener(AnalyzeImage);
      backListButton?.onClick.AddListener(() =>
      {
        EnsurePanelReferences();
        Hide();
        taskListPanel?.ShowAndRefresh();
      });
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
      SetMessage("正在加载任务详情...");
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
          StudyTaskController.Instance.SetCurrentFile(_latestFile);
          StudyTaskController.Instance.SetCurrentAiResult(_latestJob, response.data.latestAiResult);
          Render();
          SetMessage("任务详情已更新。");
          return;
        }

        SetMessage(response.message);
      }));
    }

    private void UpdateStatus(string status)
    {
      if (string.IsNullOrWhiteSpace(_taskId))
      {
        SetMessage("请先打开任务详情。");
        return;
      }

      var actualMinutes = ParseMinutes(actualMinutesInput == null ? string.Empty : actualMinutesInput.text);
      SetButtons(false);
      SetMessage("正在更新任务状态...");
      StartCoroutine(StudyTaskService.Instance.UpdateTaskStatus(_taskId, status, actualMinutes, response =>
      {
        SetButtons(true);
        if (response.success && response.data?.task != null)
        {
          _task = response.data.task;
          Render();
          SetMessage("任务状态已更新。");
          return;
        }

        SetMessage(response.message);
      }));
    }

    private void UploadImage()
    {
      if (string.IsNullOrWhiteSpace(_taskId))
      {
        SetMessage("请先打开任务详情。");
        return;
      }

      var path = imagePathInput == null ? string.Empty : imagePathInput.text.Trim();
      SetButtons(false);
      StartCoroutine(FileUploadService.Instance.UploadImageForTask(
        _taskId,
        path,
        progress => SetMessage(progress.message),
        response =>
        {
          SetButtons(true);
          if (response.success && response.data?.file != null)
          {
            _latestFile = response.data.file;
            StudyTaskController.Instance.SetCurrentFile(_latestFile);
            Render();
            SetMessage("图片上传成功。");
            return;
          }

          SetMessage(response.message);
        }));
    }

    private void AnalyzeImage()
    {
      if (string.IsNullOrWhiteSpace(_taskId))
      {
        SetMessage("请先打开任务详情。");
        return;
      }

      if (_latestFile == null || string.IsNullOrWhiteSpace(_latestFile._id))
      {
        SetMessage("请先上传一张作业图片。");
        return;
      }

      EnsurePanelReferences();
      SetButtons(false);
      StartCoroutine(AiAnalyzeService.Instance.AnalyzeHomeworkImage(
        _taskId,
        _latestFile._id,
        status => SetMessage($"AI 状态：{status}"),
        response =>
        {
          SetButtons(true);
          if (response.success && response.data != null)
          {
            _latestJob = response.data.job;
            StudyTaskController.Instance.SetCurrentAiResult(response.data.job, response.data.result);
            if (aiResultPanel == null)
            {
              SetMessage("AI 分析完成，但没有找到 AI 结果界面。");
              return;
            }

            aiResultPanel.ShowResult(response.data.result, response.data.job);
            SetMessage("AI 分析完成。");
            return;
          }

          SetMessage(response.message);
        }));
    }

    private void EnsurePanelReferences()
    {
      if (taskListPanel == null)
      {
        taskListPanel = Object.FindObjectOfType<TaskListPanel>(true);
      }

      if (aiResultPanel == null)
      {
        aiResultPanel = Object.FindObjectOfType<AiResultPanel>(true);
      }
    }

    private void Render()
    {
      SetText(titleText, _task == null ? "" : $"{_task.title}（预计 {_task.estimatedMinutes} 分钟）");
      SetText(descriptionText, _task?.description ?? "");
      SetText(statusText, _task == null ? "" : $"状态：{_task.status}，实际：{_task.actualMinutes} 分钟");
      SetText(fileText, _latestFile == null ? "暂无上传图片" : $"最近文件：{_latestFile.fileName}\nFileId: {_latestFile._id}");
    }

    private void SetButtons(bool value)
    {
      if (refreshButton != null) refreshButton.interactable = value;
      if (runningButton != null) runningButton.interactable = value;
      if (pausedButton != null) pausedButton.interactable = value;
      if (finishedButton != null) finishedButton.interactable = value;
      if (uploadButton != null) uploadButton.interactable = value;
      if (analyzeButton != null) analyzeButton.interactable = value;
    }

    private void SetMessage(string message)
    {
      if (messageText != null)
      {
        messageText.text = message ?? string.Empty;
      }
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
