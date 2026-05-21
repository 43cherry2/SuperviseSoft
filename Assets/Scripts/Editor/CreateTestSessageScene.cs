using System.Reflection;
using SuperviseSoft.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuperviseSoft.Editor
{
  public static class CreateTestSessageScene
  {
    private const string ScenePath = "Assets/Scenes/TestSessage.unity";

    [MenuItem("SuperviseSoft/Create TestSessage Auth Scene")]
    public static void CreateScene()
    {
      var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
      SceneManager.SetActiveScene(scene);

      InvokeBootstrapMethod("Build");

      EditorSceneManager.SaveScene(scene, ScenePath);
      AssetDatabase.Refresh();
      Debug.Log($"Created {ScenePath}");
    }

    [MenuItem("SuperviseSoft/Update TestSessage Panels")]
    public static void UpdatePanelsInActiveScene()
    {
      var scene = SceneManager.GetActiveScene();
      if (scene.name != "TestSessage" && scene.path != ScenePath)
      {
        Debug.LogWarning("请先打开 TestSessage 场景，再执行 SuperviseSoft/Update TestSessage Panels。");
        return;
      }

      if (Object.FindObjectOfType<Canvas>(true) == null ||
          Object.FindObjectOfType<MainEntryPanel>(true) == null)
      {
        InvokeBootstrapMethod("Build");
      }
      else
      {
        InvokeBootstrapMethod("EnsureSecondRoundUi");
      }

      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
      AssetDatabase.Refresh();
      Debug.Log($"Updated {ScenePath} panels.");
    }

    internal static void AutoUpdateOpenSceneIfNeeded()
    {
      var scene = SceneManager.GetActiveScene();
      if (scene.name != "TestSessage" && scene.path != ScenePath)
      {
        return;
      }

      var hasTaskList = Object.FindObjectOfType<TaskListPanel>(true) != null;
      var hasCreateTask = Object.FindObjectOfType<CreateTaskPanel>(true) != null;
      var hasTaskDetail = Object.FindObjectOfType<TaskDetailPanel>(true) != null;
      var hasAiResult = Object.FindObjectOfType<AiResultPanel>(true) != null;
      var mainPanel = Object.FindObjectOfType<MainEntryPanel>(true);
      var hasTaskButton = mainPanel != null && mainPanel.openTaskListButton != null;

      if (hasTaskList && hasCreateTask && hasTaskDetail && hasAiResult && hasTaskButton)
      {
        return;
      }

      UpdatePanelsInActiveScene();
    }

    private static void InvokeBootstrapMethod(string methodName)
    {
      var method = typeof(TestSessageSceneBootstrap).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
      if (method == null)
      {
        Debug.LogError($"找不到 TestSessageSceneBootstrap.{methodName}。");
        return;
      }

      method.Invoke(null, null);
    }
  }

  [InitializeOnLoad]
  public static class TestSessageSceneAutoUpdater
  {
    static TestSessageSceneAutoUpdater()
    {
      EditorApplication.delayCall += CreateTestSessageScene.AutoUpdateOpenSceneIfNeeded;
    }
  }
}
