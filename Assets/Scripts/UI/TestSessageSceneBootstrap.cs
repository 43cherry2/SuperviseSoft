using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SuperviseSoft.Upload;

namespace SuperviseSoft.UI
{
  public static class TestSessageSceneBootstrap
  {
    private const string SceneName = "TestSessage";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
      TryBuild();
      SceneManager.sceneLoaded += (_, __) => TryBuild();
    }

    private static void TryBuild()
    {
      if (SceneManager.GetActiveScene().name != SceneName)
      {
        return;
      }

      EnsureEventSystem();
    }

    private static void Build()
    {
      var camera = Object.FindObjectOfType<Camera>();
      if (camera == null)
      {
        camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(0f, 0f, -10f);
      }

      camera.clearFlags = CameraClearFlags.SolidColor;
      camera.backgroundColor = new Color(0.06f, 0.075f, 0.09f, 1f);

      var root = new GameObject("AuthRuntimeRoot");
      var canvas = CreateCanvas(root.transform);
      var font = GetFont();

      var background = CreateImage("Background", canvas.transform, new Color(0.06f, 0.075f, 0.09f, 1f));
      Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

      var title = CreateText("Title", canvas.transform, font, "学习任务管理系统", 28, TextAnchor.MiddleLeft, Color.white);
      Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -72f), new Vector2(-32f, -20f));

      var loginPanel = BuildLoginPanel(canvas.transform, font);
      var registerPanel = BuildRegisterPanel(canvas.transform, font);
      var resetPanel = BuildResetPasswordPanel(canvas.transform, font);
      var mainPanel = BuildMainEntryPanel(canvas.transform, font);

      loginPanel.mainEntryPanel = mainPanel;
      loginPanel.registerPanel = registerPanel;
      loginPanel.resetPasswordPanel = resetPanel;

      registerPanel.loginPanel = loginPanel;
      registerPanel.mainEntryPanel = mainPanel;

      resetPanel.loginPanel = loginPanel;
      resetPanel.mainEntryPanel = mainPanel;

      mainPanel.loginPanel = loginPanel;
      mainPanel.registerPanel = registerPanel;
      mainPanel.resetPasswordPanel = resetPanel;

      EnsureSecondRoundUi();
      loginPanel.Show();
      registerPanel.Hide();
      resetPanel.Hide();
      mainPanel.Hide();
      EnsureEventSystem();
    }

    private static void EnsureSecondRoundUi()
    {
      var canvas = Object.FindObjectOfType<Canvas>();
      var mainPanel = Object.FindObjectOfType<MainEntryPanel>(true);
      if (canvas == null || mainPanel == null)
      {
        return;
      }

      var font = GetFont();
      var taskListPanel = Object.FindObjectOfType<TaskListPanel>(true);
      var createTaskPanel = Object.FindObjectOfType<CreateTaskPanel>(true);
      var taskDetailPanel = Object.FindObjectOfType<TaskDetailPanel>(true);
      var aiResultPanel = Object.FindObjectOfType<AiResultPanel>(true);

      if (taskListPanel == null)
      {
        taskListPanel = BuildTaskListPanel(canvas.transform, font);
      }

      if (createTaskPanel == null)
      {
        createTaskPanel = BuildCreateTaskPanel(canvas.transform, font);
      }

      if (taskDetailPanel == null)
      {
        taskDetailPanel = BuildTaskDetailPanel(canvas.transform, font);
      }

      if (aiResultPanel == null)
      {
        aiResultPanel = BuildAiResultPanel(canvas.transform, font);
      }

      taskListPanel.createTaskPanel = createTaskPanel;
      taskListPanel.taskDetailPanel = taskDetailPanel;
      taskListPanel.mainEntryPanel = mainPanel;

      createTaskPanel.taskListPanel = taskListPanel;
      createTaskPanel.taskDetailPanel = taskDetailPanel;

      taskDetailPanel.taskListPanel = taskListPanel;
      taskDetailPanel.aiResultPanel = aiResultPanel;

      mainPanel.taskListPanel = taskListPanel;
      if (mainPanel.openTaskListButton == null)
      {
        mainPanel.openTaskListButton = CreateButton("OpenTaskListButton", mainPanel.root == null ? mainPanel.transform : mainPanel.root.transform, font, "学习任务", 4, 0);
      }

      mainPanel.Bind();
      createTaskPanel.Bind();
      taskListPanel.Bind();
      taskDetailPanel.Bind();
      aiResultPanel.Bind();

      createTaskPanel.Hide();
      taskListPanel.Hide();
      taskDetailPanel.Hide();
      aiResultPanel.Hide();
    }

    private static LoginPanel BuildLoginPanel(Transform parent, Font font)
    {
      var panel = CreateCard("LoginPanel", parent);
      var script = panel.gameObject.AddComponent<LoginPanel>();
      script.root = panel.gameObject;
      AddHeader(panel.transform, font, "登录");
      script.phoneInput = CreateInput("PhoneInput", panel.transform, font, "手机号", 0);
      script.smsCodeInput = CreateInput("SmsCodeInput", panel.transform, font, "短信验证码", 1);
      MakeInputLeftHalf(script.smsCodeInput);
      script.passwordInput = CreateInput("PasswordInput", panel.transform, font, "密码", 2);
      script.passwordInput.contentType = InputField.ContentType.Password;
      script.sendSmsCodeButton = CreateButton("SendSmsCodeButton", panel.transform, font, "发送验证码", 1, 1);
      script.smsLoginButton = CreateButton("SmsLoginButton", panel.transform, font, "验证码登录", 3, 0);
      script.passwordLoginButton = CreateButton("PasswordLoginButton", panel.transform, font, "密码登录", 3, 1);
      script.openRegisterButton = CreateButton("OpenRegisterButton", panel.transform, font, "注册", 4, 0);
      script.openResetPasswordButton = CreateButton("OpenResetPasswordButton", panel.transform, font, "忘记密码", 4, 1);
      script.messageText = CreateMessageText("LoginMessage", panel.transform, font);
      script.Bind();
      return script;
    }

    private static RegisterPanel BuildRegisterPanel(Transform parent, Font font)
    {
      var panel = CreateCard("RegisterPanel", parent);
      var script = panel.gameObject.AddComponent<RegisterPanel>();
      script.root = panel.gameObject;
      AddHeader(panel.transform, font, "注册");
      script.phoneInput = CreateInput("PhoneInput", panel.transform, font, "手机号", 0);
      script.smsCodeInput = CreateInput("SmsCodeInput", panel.transform, font, "短信验证码", 1);
      MakeInputLeftHalf(script.smsCodeInput);
      script.passwordInput = CreateInput("PasswordInput", panel.transform, font, "密码", 2);
      script.passwordInput.contentType = InputField.ContentType.Password;
      script.confirmPasswordInput = CreateInput("ConfirmPasswordInput", panel.transform, font, "确认密码", 3);
      script.confirmPasswordInput.contentType = InputField.ContentType.Password;
      script.sendSmsCodeButton = CreateButton("SendSmsCodeButton", panel.transform, font, "发送验证码", 1, 1);
      script.registerButton = CreateButton("RegisterButton", panel.transform, font, "创建账号", 4, 0);
      script.backToLoginButton = CreateButton("BackToLoginButton", panel.transform, font, "返回登录", 4, 1);
      script.messageText = CreateMessageText("RegisterMessage", panel.transform, font);
      script.Bind();
      return script;
    }

    private static ResetPasswordPanel BuildResetPasswordPanel(Transform parent, Font font)
    {
      var panel = CreateCard("ResetPasswordPanel", parent);
      var script = panel.gameObject.AddComponent<ResetPasswordPanel>();
      script.root = panel.gameObject;
      AddHeader(panel.transform, font, "重置密码");
      script.phoneInput = CreateInput("PhoneInput", panel.transform, font, "手机号", 0);
      script.smsCodeInput = CreateInput("SmsCodeInput", panel.transform, font, "短信验证码", 1);
      MakeInputLeftHalf(script.smsCodeInput);
      script.newPasswordInput = CreateInput("NewPasswordInput", panel.transform, font, "新密码", 2);
      script.newPasswordInput.contentType = InputField.ContentType.Password;
      script.confirmPasswordInput = CreateInput("ConfirmPasswordInput", panel.transform, font, "确认密码", 3);
      script.confirmPasswordInput.contentType = InputField.ContentType.Password;
      script.sendSmsCodeButton = CreateButton("SendSmsCodeButton", panel.transform, font, "发送验证码", 1, 1);
      script.resetPasswordButton = CreateButton("ResetPasswordButton", panel.transform, font, "重置密码", 4, 0);
      script.backToLoginButton = CreateButton("BackToLoginButton", panel.transform, font, "返回登录", 4, 1);
      script.messageText = CreateMessageText("ResetMessage", panel.transform, font);
      script.Bind();
      return script;
    }

    private static MainEntryPanel BuildMainEntryPanel(Transform parent, Font font)
    {
      var panel = CreateCard("MainEntryPanel", parent);
      var script = panel.gameObject.AddComponent<MainEntryPanel>();
      script.root = panel.gameObject;
      AddHeader(panel.transform, font, "主页");
      CreateTextAt(panel.transform, font, "AuthUidLabel", "用户 ID", 0, 0, TextAnchor.MiddleLeft);
      script.userIdText = CreateTextAt(panel.transform, font, "AuthUidValue", "", 0, 1, TextAnchor.MiddleLeft);
      CreateTextAt(panel.transform, font, "PhoneLabel", "手机号", 1, 0, TextAnchor.MiddleLeft);
      script.phoneMaskedText = CreateTextAt(panel.transform, font, "PhoneValue", "", 1, 1, TextAnchor.MiddleLeft);
      script.refreshUserButton = CreateButton("RefreshUserButton", panel.transform, font, "刷新用户", 3, 0);
      script.logoutButton = CreateButton("LogoutButton", panel.transform, font, "退出登录", 3, 1);
      script.openTaskListButton = CreateButton("OpenTaskListButton", panel.transform, font, "学习任务", 4, 0);
      script.messageText = CreateMessageText("MainMessage", panel.transform, font);
      script.Bind();
      return script;
    }

    private static CreateTaskPanel BuildCreateTaskPanel(Transform parent, Font font)
    {
      var panel = CreateWideCard("CreateTaskPanel", parent);
      var script = panel.gameObject.AddComponent<CreateTaskPanel>();
      script.root = panel.gameObject;
      AddHeader(panel.transform, font, "创建学习任务");
      script.titleInput = CreateInput("TitleInput", panel.transform, font, "任务标题", 0);
      script.descriptionInput = CreateInput("DescriptionInput", panel.transform, font, "任务描述", 1);
      script.estimatedMinutesInput = CreateInput("EstimatedMinutesInput", panel.transform, font, "预计分钟数", 2);
      script.createButton = CreateButton("CreateTaskButton", panel.transform, font, "创建任务", 3, 0);
      script.backButton = CreateButton("BackTaskListButton", panel.transform, font, "返回列表", 3, 1);
      script.messageText = CreateMessageText("CreateTaskMessage", panel.transform, font);
      return script;
    }

    private static TaskListPanel BuildTaskListPanel(Transform parent, Font font)
    {
      var panel = CreateWideCard("TaskListPanel", parent);
      var script = panel.gameObject.AddComponent<TaskListPanel>();
      script.root = panel.gameObject;
      AddHeader(panel.transform, font, "学习任务列表");
      script.taskListText = CreateText("TaskListText", panel.transform, font, "暂无任务", 16, TextAnchor.UpperLeft, new Color(0.9f, 0.94f, 0.96f, 1f));
      Stretch(script.taskListText.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 120f), new Vector2(-28f, -92f));
      script.taskListText.horizontalOverflow = HorizontalWrapMode.Wrap;
      script.taskListText.verticalOverflow = VerticalWrapMode.Overflow;
      script.taskIdInput = CreateInput("TaskIdInput", panel.transform, font, "任务 ID（可不填，默认打开第一条）", 3);
      script.refreshButton = CreateButton("RefreshTasksButton", panel.transform, font, "刷新", 4, 0);
      script.createTaskButton = CreateButton("OpenCreateTaskButton", panel.transform, font, "创建任务", 4, 1);
      script.openDetailButton = CreateButton("OpenTaskDetailButton", panel.transform, font, "打开详情", 5, 0);
      script.backMainButton = CreateButton("BackMainButton", panel.transform, font, "返回主页", 5, 1);
      script.messageText = CreateMessageText("TaskListMessage", panel.transform, font);
      return script;
    }

    private static TaskDetailPanel BuildTaskDetailPanel(Transform parent, Font font)
    {
      var panel = CreateWideCard("TaskDetailPanel", parent);
      var script = panel.gameObject.AddComponent<TaskDetailPanel>();
      script.root = panel.gameObject;
      AddHeader(panel.transform, font, "本次任务");
      script.titleText = CreateTextAt(panel.transform, font, "TaskTitleText", "", 0, 0, TextAnchor.MiddleLeft);
      script.descriptionText = CreateTextAt(panel.transform, font, "TaskDescriptionText", "", 1, 0, TextAnchor.MiddleLeft);
      script.statusText = CreateTextAt(panel.transform, font, "TaskStatusText", "", 2, 0, TextAnchor.MiddleLeft);
      script.textInput = CreateInput("StudyTextInput", panel.transform, font, "输入文字任务，点击分析文字", 3);
      script.actualMinutesInput = CreateInput("ActualMinutesInput", panel.transform, font, "结束时填写实际分钟数", 4);

      var dropZoneImage = CreateImage("ImageDropZone", panel.transform, new Color(0.18f, 0.21f, 0.24f, 1f));
      Stretch(dropZoneImage.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 12f), new Vector2(-390f, -510f));
      var dropZone = dropZoneImage.gameObject.AddComponent<ImageDropZone>();
      var dropText = CreateText("DropZoneText", dropZoneImage.transform, font, "拖拽 JPG/PNG 到这里\n或打开相机拍照", 15, TextAnchor.MiddleCenter, new Color(0.9f, 0.94f, 0.96f, 1f));
      Stretch(dropText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      dropZone.hintText = dropText;
      script.imageDropZone = dropZone;
      script.dropZoneText = dropText;

      script.imagePreview = CreateRawImage("ImagePreview", panel.transform, new Color(0.08f, 0.09f, 0.1f, 1f));
      Stretch(script.imagePreview.rectTransform, Vector2.zero, Vector2.one, new Vector2(360f, 12f), new Vector2(-28f, -510f));

      script.currentItemText = CreateText("CurrentItemText", panel.transform, font, "暂无分析项", 15, TextAnchor.UpperLeft, new Color(0.9f, 0.94f, 0.96f, 1f));
      Stretch(script.currentItemText.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 150f), new Vector2(-28f, -300f));
      script.currentItemText.horizontalOverflow = HorizontalWrapMode.Wrap;
      script.currentItemText.verticalOverflow = VerticalWrapMode.Overflow;

      script.analyzeTextButton = CreateButton("AnalyzeTextButton", panel.transform, font, "分析文字", 5, 0);
      script.startCameraButton = CreateButton("StartCameraButton", panel.transform, font, "打开相机", 5, 1);
      script.capturePhotoButton = CreateButton("CapturePhotoButton", panel.transform, font, "拍照并分析", 6, 0);
      script.refreshButton = CreateButton("RefreshDetailButton", panel.transform, font, "刷新", 6, 1);
      script.previousItemButton = CreateButton("PreviousItemButton", panel.transform, font, "上一项", 7, 0);
      script.nextItemButton = CreateButton("NextItemButton", panel.transform, font, "下一项", 7, 1);
      script.finishTaskButton = CreateButton("FinishTaskButton", panel.transform, font, "结束本任务", 8, 0);
      script.backListButton = CreateButton("BackListButton", panel.transform, font, "返回列表", 8, 1);
      script.messageText = CreateMessageText("TaskDetailMessage", panel.transform, font);
      return script;
    }

    private static AiResultPanel BuildAiResultPanel(Transform parent, Font font)
    {
      var panel = CreateWideCard("AiResultPanel", parent);
      var script = panel.gameObject.AddComponent<AiResultPanel>();
      script.root = panel.gameObject;
      AddHeader(panel.transform, font, "AI 分析结果");
      script.statusText = CreateTextAt(panel.transform, font, "AiStatusText", "", 0, 0, TextAnchor.MiddleLeft);
      script.summaryText = CreateTextAt(panel.transform, font, "AiSummaryText", "", 1, 0, TextAnchor.MiddleLeft);
      script.estimatedMinutesText = CreateTextAt(panel.transform, font, "AiMinutesText", "", 2, 0, TextAnchor.MiddleLeft);
      script.suggestedStepsText = CreateText("AiStepsText", panel.transform, font, "", 16, TextAnchor.UpperLeft, new Color(0.9f, 0.94f, 0.96f, 1f));
      Stretch(script.suggestedStepsText.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 88f), new Vector2(-28f, -240f));
      script.suggestedStepsText.horizontalOverflow = HorizontalWrapMode.Wrap;
      script.suggestedStepsText.verticalOverflow = VerticalWrapMode.Overflow;
      script.closeButton = CreateButton("CloseAiResultButton", panel.transform, font, "关闭", 8, 0);
      return script;
    }

    private static RectTransform CreateCard(string name, Transform parent)
    {
      var panel = CreateImage(name, parent, new Color(0.12f, 0.145f, 0.17f, 0.98f));
      Stretch(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-280f, -230f), new Vector2(280f, 230f));
      return panel.rectTransform;
    }

    private static RectTransform CreateWideCard(string name, Transform parent)
    {
      var panel = CreateImage(name, parent, new Color(0.12f, 0.145f, 0.17f, 0.98f));
      Stretch(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360f, -310f), new Vector2(360f, 310f));
      return panel.rectTransform;
    }

    private static void AddHeader(Transform parent, Font font, string text)
    {
      var header = CreateText("Header", parent, font, text, 24, TextAnchor.MiddleLeft, Color.white);
      Stretch(header.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 388f), new Vector2(-28f, -24f));
    }

    private static InputField CreateInput(string name, Transform parent, Font font, string placeholder, int row)
    {
      var root = CreateImage(name, parent, new Color(0.94f, 0.96f, 0.98f, 1f));
      Stretch(root.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 310f - row * 64f), new Vector2(-28f, -98f - row * 64f));
      var input = root.gameObject.AddComponent<InputField>();
      var text = CreateText("Text", root.transform, font, "", 17, TextAnchor.MiddleLeft, Color.black);
      Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
      var placeholderText = CreateText("Placeholder", root.transform, font, placeholder, 17, TextAnchor.MiddleLeft, new Color(0.34f, 0.38f, 0.42f, 0.8f));
      Stretch(placeholderText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
      input.textComponent = text;
      input.placeholder = placeholderText;
      return input;
    }

    private static Button CreateButton(string name, Transform parent, Font font, string label, int row, int col)
    {
      var root = CreateImage(name, parent, new Color(0.22f, 0.47f, 0.78f, 1f));
      var left = col == 0 ? 28f : 294f;
      var right = col == 0 ? -294f : -28f;
      Stretch(root.rectTransform, Vector2.zero, Vector2.one, new Vector2(left, 310f - row * 64f), new Vector2(right, -98f - row * 64f));
      var button = root.gameObject.AddComponent<Button>();
      button.targetGraphic = root;
      var text = CreateText("Label", root.transform, font, label, 16, TextAnchor.MiddleCenter, Color.white);
      Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      return button;
    }

    private static Text CreateTextAt(Transform parent, Font font, string name, string value, int row, int col, TextAnchor anchor)
    {
      var text = CreateText(name, parent, font, value, 17, anchor, new Color(0.9f, 0.94f, 0.96f, 1f));
      var left = col == 0 ? 28f : 150f;
      var right = col == 0 ? -410f : -28f;
      Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(left, 296f - row * 64f), new Vector2(right, -112f - row * 64f));
      return text;
    }

    private static Text CreateMessageText(string name, Transform parent, Font font)
    {
      var text = CreateText(name, parent, font, "", 16, TextAnchor.UpperLeft, new Color(1f, 0.82f, 0.42f, 1f));
      Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 18f), new Vector2(-28f, -340f));
      text.horizontalOverflow = HorizontalWrapMode.Wrap;
      text.verticalOverflow = VerticalWrapMode.Overflow;
      return text;
    }

    private static void MakeInputLeftHalf(InputField input)
    {
      if (input == null)
      {
        return;
      }

      var rect = input.GetComponent<RectTransform>();
      rect.offsetMax = new Vector2(-294f, rect.offsetMax.y);
    }

    private static Canvas CreateCanvas(Transform parent)
    {
      var obj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
      obj.transform.SetParent(parent, false);
      var canvas = obj.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      var scaler = obj.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1280f, 720f);
      scaler.matchWidthOrHeight = 0.5f;
      return canvas;
    }

    private static void EnsureEventSystem()
    {
      if (Object.FindObjectOfType<EventSystem>() != null)
      {
        return;
      }

      new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static Font GetFont()
    {
      return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
      var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
      image.transform.SetParent(parent, false);
      image.color = color;
      return image;
    }

    private static RawImage CreateRawImage(string name, Transform parent, Color color)
    {
      var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
      image.transform.SetParent(parent, false);
      image.color = color;
      return image;
    }

    private static Text CreateText(string name, Transform parent, Font font, string value, int size, TextAnchor anchor, Color color)
    {
      var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
      text.transform.SetParent(parent, false);
      text.font = font;
      text.text = value;
      text.fontSize = size;
      text.alignment = anchor;
      text.color = color;
      return text;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
      rect.anchorMin = min;
      rect.anchorMax = max;
      rect.offsetMin = offsetMin;
      rect.offsetMax = offsetMax;
    }
  }
}
