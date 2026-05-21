using SuperviseSoft.Auth;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuperviseSoft.UI
{
  public static class AuthSceneStartup
  {
    private const string SceneName = "TestSessage";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
      ApplyInitialPanelState();
      SceneManager.sceneLoaded += (_, __) => ApplyInitialPanelState();
    }

    private static void ApplyInitialPanelState()
    {
      if (SceneManager.GetActiveScene().name != SceneName)
      {
        return;
      }

      var loginPanel = Object.FindObjectOfType<LoginPanel>(true);
      var registerPanel = Object.FindObjectOfType<RegisterPanel>(true);
      var resetPasswordPanel = Object.FindObjectOfType<ResetPasswordPanel>(true);
      var mainEntryPanel = Object.FindObjectOfType<MainEntryPanel>(true);
      var createTaskPanel = Object.FindObjectOfType<CreateTaskPanel>(true);
      var taskListPanel = Object.FindObjectOfType<TaskListPanel>(true);
      var taskDetailPanel = Object.FindObjectOfType<TaskDetailPanel>(true);
      var aiResultPanel = Object.FindObjectOfType<AiResultPanel>(true);

      if (loginPanel == null && registerPanel == null && resetPasswordPanel == null && mainEntryPanel == null)
      {
        return;
      }

      AuthManager.Instance.Initialize();
      if (AuthManager.Instance.IsLoggedIn)
      {
        loginPanel?.Hide();
        registerPanel?.Hide();
        resetPasswordPanel?.Hide();
        createTaskPanel?.Hide();
        taskListPanel?.Hide();
        taskDetailPanel?.Hide();
        aiResultPanel?.Hide();
        mainEntryPanel?.ShowAndRefresh();
        return;
      }

      mainEntryPanel?.Hide();
      registerPanel?.Hide();
      resetPasswordPanel?.Hide();
      createTaskPanel?.Hide();
      taskListPanel?.Hide();
      taskDetailPanel?.Hide();
      aiResultPanel?.Hide();
      loginPanel?.Show();
    }
  }
}
