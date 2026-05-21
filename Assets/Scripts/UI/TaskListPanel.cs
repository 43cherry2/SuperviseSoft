using SuperviseSoft.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace SuperviseSoft.UI
{
  public sealed class TaskListPanel : MonoBehaviour
  {
    public GameObject root;
    public Text taskListText;
    public InputField taskIdInput;
    public Button refreshButton;
    public Button createTaskButton;
    public Button openDetailButton;
    public Button backMainButton;
    public Text messageText;
    public CreateTaskPanel createTaskPanel;
    public TaskDetailPanel taskDetailPanel;
    public MainEntryPanel mainEntryPanel;

    private StudyTask[] _tasks;
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

      if (refreshButton == null && createTaskButton == null && openDetailButton == null && backMainButton == null)
      {
        return;
      }

      _bound = true;
      refreshButton?.onClick.AddListener(Refresh);
      createTaskButton?.onClick.AddListener(OpenCreateTask);
      openDetailButton?.onClick.AddListener(OpenSelectedDetail);
      backMainButton?.onClick.AddListener(() =>
      {
        EnsurePanelReferences();
        Hide();
        mainEntryPanel?.ShowAndRefresh();
      });
    }

    public void Show()
    {
      PanelVisibility.Show(root, gameObject);
      SetMessage(string.Empty);
    }

    public void ShowAndRefresh()
    {
      Show();
      Refresh();
    }

    public void Hide()
    {
      PanelVisibility.Hide(root, gameObject);
    }

    public void Refresh()
    {
      SetMessage("正在加载进行中的任务组...");
      SetButtons(false);
      StartCoroutine(StudyTaskService.Instance.GetTaskList(response =>
      {
        SetButtons(true);
        if (response.success && response.data != null)
        {
          _tasks = response.data.tasks ?? new StudyTask[0];
          RenderTasks();
          SetMessage(_tasks.Length == 0 ? "暂无进行中的任务组，请先创建一个本次任务。" : "任务组列表已更新。");
          return;
        }

        SetMessage(response.message);
      }));
    }

    private void OpenCreateTask()
    {
      EnsurePanelReferences();
      if (createTaskPanel == null)
      {
        SetMessage("没有找到创建任务界面，请把 CreateTaskPanel 放到 TestSessage 场景里。");
        return;
      }

      Hide();
      createTaskPanel.Show();
    }

    private void OpenSelectedDetail()
    {
      EnsurePanelReferences();
      var taskId = taskIdInput == null ? string.Empty : taskIdInput.text.Trim();
      if (string.IsNullOrWhiteSpace(taskId) && _tasks != null && _tasks.Length > 0)
      {
        taskId = _tasks[0]._id;
      }

      if (string.IsNullOrWhiteSpace(taskId))
      {
        SetMessage("请输入任务 ID，或先创建任务组。");
        return;
      }

      if (taskDetailPanel == null)
      {
        SetMessage("没有找到任务详情界面，请把 TaskDetailPanel 放到 TestSessage 场景里。");
        return;
      }

      Hide();
      taskDetailPanel.ShowAndLoad(taskId);
    }

    private void EnsurePanelReferences()
    {
      if (createTaskPanel == null)
      {
        createTaskPanel = Object.FindObjectOfType<CreateTaskPanel>(true);
      }

      if (taskDetailPanel == null)
      {
        taskDetailPanel = Object.FindObjectOfType<TaskDetailPanel>(true);
      }

      if (mainEntryPanel == null)
      {
        mainEntryPanel = Object.FindObjectOfType<MainEntryPanel>(true);
      }
    }

    private void RenderTasks()
    {
      if (taskListText == null)
      {
        return;
      }

      if (_tasks == null || _tasks.Length == 0)
      {
        taskListText.text = "暂无进行中的任务组";
        return;
      }

      var lines = new System.Text.StringBuilder();
      for (var i = 0; i < _tasks.Length; i++)
      {
        var task = _tasks[i];
        lines.Append(i + 1)
          .Append(". ")
          .Append(task.title)
          .Append(" | ")
          .Append(task.status)
          .Append(" | 预计 ")
          .Append(task.estimatedMinutes)
          .Append(" 分钟")
          .AppendLine();
        lines.Append("ID: ").Append(task._id).AppendLine();
      }

      taskListText.text = lines.ToString();
    }

    private void SetButtons(bool value)
    {
      if (refreshButton != null)
      {
        refreshButton.interactable = value;
      }

      if (openDetailButton != null)
      {
        openDetailButton.interactable = value;
      }
    }

    private void SetMessage(string message)
    {
      if (messageText != null)
      {
        messageText.text = message ?? string.Empty;
      }
    }
  }
}
