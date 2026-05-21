using SuperviseSoft.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace SuperviseSoft.UI
{
  public sealed class CreateTaskPanel : MonoBehaviour
  {
    public GameObject root;
    public InputField titleInput;
    public InputField descriptionInput;
    public InputField estimatedMinutesInput;
    public Button createButton;
    public Button backButton;
    public Text messageText;
    public TaskListPanel taskListPanel;
    public TaskDetailPanel taskDetailPanel;

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

      if (createButton == null && backButton == null)
      {
        return;
      }

      _bound = true;
      createButton?.onClick.AddListener(OnCreateClicked);
      backButton?.onClick.AddListener(() =>
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

    public void Hide()
    {
      PanelVisibility.Hide(root, gameObject);
    }

    private void OnCreateClicked()
    {
      var title = titleInput == null ? string.Empty : titleInput.text.Trim();
      var description = descriptionInput == null ? string.Empty : descriptionInput.text.Trim();
      var estimatedMinutes = ParseMinutes(estimatedMinutesInput == null ? string.Empty : estimatedMinutesInput.text);

      if (string.IsNullOrWhiteSpace(title))
      {
        SetMessage("请输入本次任务名称。");
        return;
      }

      if (estimatedMinutes <= 0)
      {
        SetMessage("请输入预计分钟数。");
        return;
      }

      SetInteractable(false);
      SetMessage("正在创建本次任务...");
      StartCoroutine(StudyTaskService.Instance.CreateTask(title, description, estimatedMinutes, response =>
      {
        SetInteractable(true);
        if (response.success && response.data?.task != null)
        {
          EnsurePanelReferences();
          SetMessage("本次任务已创建。");
          Hide();
          taskDetailPanel?.ShowAndLoad(response.data.task._id);
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

      if (taskDetailPanel == null)
      {
        taskDetailPanel = Object.FindObjectOfType<TaskDetailPanel>(true);
      }
    }

    private void SetInteractable(bool value)
    {
      if (createButton != null)
      {
        createButton.interactable = value;
      }
    }

    private void SetMessage(string message)
    {
      if (messageText != null)
      {
        messageText.text = message ?? string.Empty;
      }
    }

    private static int ParseMinutes(string text)
    {
      return int.TryParse(text, out var value) ? Mathf.Max(0, value) : 0;
    }
  }
}
