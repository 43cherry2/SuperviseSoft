using System.Collections.Generic;
using SuperviseSoft.Mediapipe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SuperviseSoft.Editor
{
  public static class CreateTestMediapipeScene
  {
    private const string ScenePath = "Assets/Scenes/TestMediapipe.unity";
    private const string PoseModelPath = "Packages/com.github.homuler.mediapipe/PackageResources/MediaPipe/pose_landmarker_full.bytes";
    private const string FaceModelPath = "Packages/com.github.homuler.mediapipe/PackageResources/MediaPipe/face_landmarker_v2_with_blendshapes.bytes";
    private const string HandModelPath = "Packages/com.github.homuler.mediapipe/PackageResources/MediaPipe/hand_landmarker.bytes";
    private const string ObjectModelPath = "Packages/com.github.homuler.mediapipe/PackageResources/MediaPipe/efficientdet_lite0_float16.bytes";

    [MenuItem("SuperviseSoft/Create Test Mediapipe Scene")]
    public static void CreateScene()
    {
      var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
      SceneManager.SetActiveScene(scene);

      var mainCamera = new GameObject("Main Camera").AddComponent<Camera>();
      mainCamera.clearFlags = CameraClearFlags.SolidColor;
      mainCamera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
      mainCamera.transform.position = new Vector3(0f, 0f, -10f);

      var canvas = CreateCanvas();
      var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

      var background = CreatePanel("Background", canvas.transform, new Color(0.06f, 0.07f, 0.075f, 1f));
      Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

      var title = CreateText("Title", canvas.transform, font, "TestMediapipe 学习监督测试", 26, TextAnchor.MiddleLeft, Color.white);
      Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -68f), new Vector2(-24f, -16f));

      var toolbar = CreatePanel("Toolbar", canvas.transform, new Color(0.13f, 0.15f, 0.16f, 0.96f));
      Stretch(toolbar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -124f), new Vector2(-20f, -76f));

      var showCameraToggle = CreateToggle("Show Camera Toggle", toolbar.transform, font, "显示相机画面");
      Stretch(showCameraToggle.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 8f), new Vector2(188f, -8f));
      showCameraToggle.isOn = true;

      var cameraDropdown = CreateDropdown("Camera Dropdown", toolbar.transform, font, 17);
      Stretch(cameraDropdown.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(208f, 6f), new Vector2(440f, -6f));

      var nameInput = CreateInputField("Enroll Name Input", toolbar.transform, font, "输入姓名");
      Stretch(nameInput.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(460f, 8f), new Vector2(640f, -8f));

      var enrollButton = CreateButton("Enroll Face Button", toolbar.transform, font, "录入当前人脸");
      Stretch(enrollButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(660f, 8f), new Vector2(804f, -8f));

      var identityText = CreateText("Identity Text", toolbar.transform, font, "当前身份：未知", 18, TextAnchor.MiddleLeft, new Color(0.84f, 0.95f, 1f));
      Stretch(identityText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(824f, 8f), new Vector2(-16f, -8f));

      var previewArea = CreatePanel("Preview Area", canvas.transform, new Color(0.04f, 0.045f, 0.05f, 1f));
      Stretch(previewArea.rectTransform, new Vector2(0f, 0f), new Vector2(0.72f, 1f), new Vector2(20f, 84f), new Vector2(-16f, -144f));

      var cameraView = CreateRawImage("Camera View", previewArea.transform, Color.white);
      Stretch(cameraView.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

      var hiddenCameraImage = CreateImage("Hidden Camera Green Image", previewArea.transform, Color.green);
      Stretch(hiddenCameraImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      hiddenCameraImage.gameObject.SetActive(false);

      var hiddenLabel = CreateText("Hidden Camera Label", hiddenCameraImage.transform, font, "相机画面已隐藏", 28, TextAnchor.MiddleCenter, Color.white);
      Stretch(hiddenLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

      var reportPanel = CreatePanel("Report Panel", canvas.transform, new Color(0.11f, 0.13f, 0.14f, 0.96f));
      Stretch(reportPanel.rectTransform, new Vector2(0.72f, 0f), Vector2.one, new Vector2(0f, 84f), new Vector2(-20f, -144f));

      var reportTitle = CreateText("Report Title", reportPanel.transform, font, "实时状态", 22, TextAnchor.MiddleLeft, Color.white);
      Stretch(reportTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -54f), new Vector2(-18f, -14f));

      var reportText = CreateText("Report Text", reportPanel.transform, font, "等待检测...", 20, TextAnchor.UpperLeft, new Color(0.88f, 0.92f, 0.94f));
      Stretch(reportText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 18f), new Vector2(-18f, -70f));
      reportText.horizontalOverflow = HorizontalWrapMode.Wrap;
      reportText.verticalOverflow = VerticalWrapMode.Overflow;

      var bottomBar = CreatePanel("Bottom Status Bar", canvas.transform, new Color(0.02f, 0.025f, 0.03f, 0.98f));
      Stretch(bottomBar.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 70f));

      var bottomStatusText = CreateText("Bottom Status Text", bottomBar.transform, font, "人物：等待相机启动", 22, TextAnchor.MiddleLeft, Color.white);
      Stretch(bottomStatusText.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 0f), new Vector2(-24f, 0f));

      var controllerObject = new GameObject("Study Monitor Mediapipe Controller");
      var controller = controllerObject.AddComponent<StudyMonitorMediapipeController>();
      controller.poseModel = AssetDatabase.LoadAssetAtPath<TextAsset>(PoseModelPath);
      controller.faceModel = AssetDatabase.LoadAssetAtPath<TextAsset>(FaceModelPath);
      controller.handModel = AssetDatabase.LoadAssetAtPath<TextAsset>(HandModelPath);
      controller.objectModel = AssetDatabase.LoadAssetAtPath<TextAsset>(ObjectModelPath);
      controller.requestedWidth = 640;
      controller.requestedHeight = 480;
      controller.detectionIntervalSeconds = 0f;
      controller.mirrorLandmarksWithCameraPreview = true;
      controller.mirrorLandmarksHorizontallyInEditor = true;
      controller.mirrorLandmarksHorizontallyOnAndroid = false;
      controller.blinkThreshold = 0.22f;
      controller.blinkOpenThreshold = 0.2f;
      controller.blinkMinimumClosedSeconds = 0f;
      controller.invertHeadPitch = false;
      controller.invertHeadDownScoreOnAndroid = true;
      controller.enableDeskAwarePosture = true;
      controller.deskObjectsOverrideFullBodyPosture = true;
      controller.enableObjectDetectionPostureEvidence = true;
      controller.useOcclusionPostureEvidence = true;
      controller.objectDetectionScoreThreshold = 0.25f;
      controller.handDeskEvidenceMinY = 0.52f;
      controller.cameraView = cameraView;
      controller.hiddenCameraImage = hiddenCameraImage;
      controller.showCameraToggle = showCameraToggle;
      controller.cameraDropdown = cameraDropdown;
      controller.bottomStatusText = bottomStatusText;
      controller.reportText = reportText;
      controller.identityText = identityText;
      controller.enrollNameInput = nameInput;
      controller.enrollFaceButton = enrollButton;

      if (controller.poseModel == null)
      {
        Debug.LogWarning($"Pose model was not found: {PoseModelPath}");
      }

      if (controller.faceModel == null)
      {
        Debug.LogWarning($"Face model was not found: {FaceModelPath}");
      }

      if (controller.handModel == null)
      {
        Debug.LogWarning($"Hand model was not found: {HandModelPath}");
      }

      if (controller.objectModel == null)
      {
        Debug.LogWarning($"Object model was not found: {ObjectModelPath}");
      }

      CreateEventSystem();
      EditorSceneManager.SaveScene(scene, ScenePath);
      AssetDatabase.Refresh();
      Debug.Log($"Created {ScenePath}");
    }

    private static Canvas CreateCanvas()
    {
      var canvasObject = new GameObject("Canvas");
      var canvas = canvasObject.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);
      canvasObject.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;
      canvasObject.AddComponent<GraphicRaycaster>();
      return canvas;
    }

    private static void CreateEventSystem()
    {
      var eventSystemObject = new GameObject("EventSystem");
      eventSystemObject.AddComponent<EventSystem>();
      eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static Image CreatePanel(string name, Transform parent, Color color)
    {
      var image = CreateImage(name, parent, color);
      image.raycastTarget = false;
      return image;
    }

    private static RawImage CreateRawImage(string name, Transform parent, Color color)
    {
      var gameObject = new GameObject(name);
      gameObject.transform.SetParent(parent, false);
      var rawImage = gameObject.AddComponent<RawImage>();
      rawImage.color = color;
      return rawImage;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
      var gameObject = new GameObject(name);
      gameObject.transform.SetParent(parent, false);
      var image = gameObject.AddComponent<Image>();
      image.color = color;
      return image;
    }

    private static Text CreateText(string name, Transform parent, Font font, string text, int fontSize, TextAnchor alignment, Color color)
    {
      var gameObject = new GameObject(name);
      gameObject.transform.SetParent(parent, false);
      var textComponent = gameObject.AddComponent<Text>();
      textComponent.font = font;
      textComponent.text = text;
      textComponent.fontSize = fontSize;
      textComponent.alignment = alignment;
      textComponent.color = color;
      return textComponent;
    }

    private static Toggle CreateToggle(string name, Transform parent, Font font, string label)
    {
      var root = new GameObject(name);
      root.transform.SetParent(parent, false);
      var toggle = root.AddComponent<Toggle>();

      var background = CreateImage("Background", root.transform, new Color(0.9f, 0.95f, 1f, 1f));
      Stretch(background.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -12f), new Vector2(24f, 12f));

      var checkmark = CreateImage("Checkmark", background.transform, new Color(0.1f, 0.72f, 0.28f, 1f));
      Stretch(checkmark.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

      var text = CreateText("Label", root.transform, font, label, 18, TextAnchor.MiddleLeft, Color.white);
      Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(34f, 0f), Vector2.zero);

      toggle.targetGraphic = background;
      toggle.graphic = checkmark;
      return toggle;
    }

    private static InputField CreateInputField(string name, Transform parent, Font font, string placeholder)
    {
      var root = new GameObject(name);
      root.transform.SetParent(parent, false);
      var image = root.AddComponent<Image>();
      image.color = new Color(0.95f, 0.97f, 0.98f, 1f);

      var input = root.AddComponent<InputField>();
      var text = CreateText("Text", root.transform, font, string.Empty, 17, TextAnchor.MiddleLeft, Color.black);
      Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));

      var placeholderText = CreateText("Placeholder", root.transform, font, placeholder, 17, TextAnchor.MiddleLeft, new Color(0.35f, 0.39f, 0.42f, 0.75f));
      Stretch(placeholderText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));

      input.textComponent = text;
      input.placeholder = placeholderText;
      return input;
    }

    private static Button CreateButton(string name, Transform parent, Font font, string label)
    {
      var root = new GameObject(name);
      root.transform.SetParent(parent, false);
      var image = root.AddComponent<Image>();
      image.color = new Color(0.18f, 0.52f, 0.95f, 1f);
      var button = root.AddComponent<Button>();
      button.targetGraphic = image;

      var text = CreateText("Label", root.transform, font, label, 17, TextAnchor.MiddleCenter, Color.white);
      Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      return button;
    }

    private static Dropdown CreateDropdown(string name, Transform parent, Font font, int fontSize)
    {
      var root = new GameObject(name);
      root.transform.SetParent(parent, false);
      var image = root.AddComponent<Image>();
      image.color = new Color(0.22f, 0.25f, 0.28f, 1f);
      var dropdown = root.AddComponent<Dropdown>();

      // Label
      var label = CreateText("Label", root.transform, font, "选择相机", fontSize, TextAnchor.MiddleLeft, Color.white);
      Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-28f, -2f));

      // Arrow
      var arrow = CreateText("Arrow", root.transform, font, "▼", 12, TextAnchor.MiddleCenter, Color.white);
      Stretch(arrow.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-24f, 2f), new Vector2(-6f, -2f));

      // Template
      var template = new GameObject("Template");
      template.transform.SetParent(root.transform, false);
      var templateImage = template.AddComponent<Image>();
      templateImage.color = new Color(0.18f, 0.2f, 0.22f, 1f);
      var scrollRect = template.AddComponent<ScrollRect>();
      template.SetActive(false);

      var templateRect = template.GetComponent<RectTransform>();
      templateRect.anchorMin = new Vector2(0f, 0f);
      templateRect.anchorMax = new Vector2(1f, 0f);
      templateRect.pivot = new Vector2(0.5f, 1f);
      templateRect.anchoredPosition = new Vector2(0f, -2f);
      templateRect.sizeDelta = new Vector2(0f, 160f);

      // Viewport
      var viewport = new GameObject("Viewport");
      viewport.transform.SetParent(template.transform, false);
      var viewportImage = viewport.AddComponent<Image>();
      viewportImage.color = new Color(0.18f, 0.2f, 0.22f, 1f);
      var viewportMask = viewport.AddComponent<Mask>();
      viewportMask.showMaskGraphic = false;
      var viewportRect = viewport.GetComponent<RectTransform>();
      Stretch(viewportRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

      // Content
      var content = new GameObject("Content");
      content.transform.SetParent(viewport.transform, false);
      var contentRect = content.AddComponent<RectTransform>();
      contentRect.anchorMin = new Vector2(0f, 1f);
      contentRect.anchorMax = new Vector2(1f, 1f);
      contentRect.pivot = new Vector2(0.5f, 1f);
      contentRect.sizeDelta = new Vector2(0f, 160f);

      var layoutGroup = content.AddComponent<VerticalLayoutGroup>();
      layoutGroup.childControlHeight = true;
      layoutGroup.childControlWidth = true;
      layoutGroup.childForceExpandHeight = false;
      layoutGroup.childForceExpandWidth = true;
      layoutGroup.spacing = 0;
      layoutGroup.padding = new RectOffset(0, 0, 0, 0);

      var contentFitter = content.AddComponent<ContentSizeFitter>();
      contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

      // Item
      var item = new GameObject("Item");
      item.transform.SetParent(content.transform, false);
      var itemRect = item.AddComponent<RectTransform>();
      itemRect.anchorMin = new Vector2(0f, 1f);
      itemRect.anchorMax = new Vector2(1f, 1f);
      itemRect.pivot = new Vector2(0.5f, 0.5f);
      itemRect.sizeDelta = new Vector2(0f, 30f);
      var itemImage = item.AddComponent<Image>();
      itemImage.color = new Color(0.22f, 0.25f, 0.28f, 1f);

      var itemToggle = item.AddComponent<Toggle>();
      itemToggle.targetGraphic = itemImage;

      var itemBg = CreateImage("Item Background", item.transform, new Color(0.28f, 0.62f, 0.95f, 1f));
      Stretch(itemBg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      itemBg.gameObject.SetActive(false);

      var itemCheckmark = CreateImage("Item Checkmark", item.transform, new Color(0.84f, 0.95f, 1f, 1f));
      Stretch(itemCheckmark.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -8f), new Vector2(24f, 8f));

      var itemLabel = CreateText("Item Label", item.transform, font, "Option", fontSize, TextAnchor.MiddleLeft, Color.white);
      Stretch(itemLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(34f, 2f), new Vector2(-8f, -2f));

      itemToggle.graphic = itemCheckmark;

      scrollRect.viewport = viewportRect;
      scrollRect.content = contentRect;
      scrollRect.horizontal = false;

      dropdown.template = templateRect;
      dropdown.captionText = label;
      dropdown.itemText = itemLabel;
      dropdown.options = new List<Dropdown.OptionData> { new("选项") };

      return dropdown;
    }

    private static void Stretch(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
      rectTransform.anchorMin = anchorMin;
      rectTransform.anchorMax = anchorMax;
      rectTransform.offsetMin = offsetMin;
      rectTransform.offsetMax = offsetMax;
    }
  }
}
