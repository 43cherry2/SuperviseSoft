using SuperviseSoft.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace SuperviseSoft.UI
{
  public sealed class MainEntryPanel : MonoBehaviour
  {
    public GameObject root;
    public Text userIdText;
    public Text phoneMaskedText;
    public Text messageText;
    public Button refreshUserButton;
    public Button logoutButton;
    public Button openTaskListButton;
    public LoginPanel loginPanel;
    public RegisterPanel registerPanel;
    public ResetPasswordPanel resetPasswordPanel;
    public TaskListPanel taskListPanel;

    private bool _refreshBound;
    private bool _logoutBound;
    private bool _taskListBound;

    private void Awake()
    {
      Bind();
    }

    private void Start()
    {
      AuthManager.Instance.Initialize();
      EnsurePanelReferences();
      if (AuthManager.Instance.IsLoggedIn)
      {
        loginPanel?.Hide();
        registerPanel?.Hide();
        resetPasswordPanel?.Hide();
        ShowAndRefresh();
        return;
      }

      Hide();
      registerPanel?.Hide();
      resetPasswordPanel?.Hide();
      loginPanel?.Show();
    }

    public void Bind()
    {
      EnsurePanelReferences();
      if (refreshUserButton == null && logoutButton == null && openTaskListButton == null)
      {
        return;
      }

      if (refreshUserButton != null && !_refreshBound)
      {
        _refreshBound = true;
        refreshUserButton.onClick.AddListener(RefreshCurrentUser);
      }

      if (logoutButton != null && !_logoutBound)
      {
        _logoutBound = true;
        logoutButton.onClick.AddListener(OnLogoutClicked);
      }

      if (openTaskListButton != null && !_taskListBound)
      {
        _taskListBound = true;
        openTaskListButton.onClick.AddListener(OpenTaskList);
      }
    }

    public void ShowAndRefresh()
    {
      Show();
      RefreshCurrentUser();
    }

    public void Show()
    {
      AuthManager.Instance.Initialize();
      EnsurePanelReferences();
      if (!AuthManager.Instance.IsLoggedIn)
      {
        Hide();
        registerPanel?.Hide();
        resetPasswordPanel?.Hide();
        loginPanel?.Show();
        return;
      }

      PanelVisibility.Show(root, gameObject);
      UpdateLocalSessionUi();
    }

    public void Hide()
    {
      PanelVisibility.Hide(root, gameObject);
    }

    private void OpenTaskList()
    {
      EnsurePanelReferences();
      if (taskListPanel == null)
      {
        SetMessage("没有找到学习任务界面，请把 TaskListPanel 放到 TestSessage 场景里。");
        return;
      }

      Hide();
      taskListPanel.ShowAndRefresh();
    }

    private void RefreshCurrentUser()
    {
      AuthManager.Instance.Initialize();
      EnsurePanelReferences();
      if (!AuthManager.Instance.IsLoggedIn)
      {
        Hide();
        registerPanel?.Hide();
        resetPasswordPanel?.Hide();
        loginPanel?.Show();
        return;
      }

      SetMessage("正在检查登录状态...");
      StartCoroutine(AuthService.Instance.GetCurrentUser(response =>
      {
        if (response.success && response.data != null)
        {
          SetMessage("登录状态有效。");
          SetText(userIdText, response.data.authUid);
          SetText(phoneMaskedText, response.data.phoneMasked);
          return;
        }

        AuthManager.Instance.ClearSession();
        SetMessage(response.message);
        Hide();
        registerPanel?.Hide();
        resetPasswordPanel?.Hide();
        loginPanel?.Show();
      }));
    }

    private void OnLogoutClicked()
    {
      EnsurePanelReferences();
      StartCoroutine(AuthService.Instance.Logout(response =>
      {
        SetMessage(response.success ? "已退出登录。" : response.message);
        Hide();
        registerPanel?.Hide();
        resetPasswordPanel?.Hide();
        taskListPanel?.Hide();
        loginPanel?.Show();
      }));
    }

    private void EnsurePanelReferences()
    {
      if (loginPanel == null)
      {
        loginPanel = Object.FindObjectOfType<LoginPanel>(true);
      }

      if (registerPanel == null)
      {
        registerPanel = Object.FindObjectOfType<RegisterPanel>(true);
      }

      if (resetPasswordPanel == null)
      {
        resetPasswordPanel = Object.FindObjectOfType<ResetPasswordPanel>(true);
      }

      if (taskListPanel == null)
      {
        taskListPanel = Object.FindObjectOfType<TaskListPanel>(true);
      }
    }

    private void UpdateLocalSessionUi()
    {
      SetText(userIdText, AuthManager.Instance.CurrentUserId);
      SetText(phoneMaskedText, AuthManager.Instance.CurrentPhoneMasked);
      SetMessage(string.Empty);
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
  }
}
