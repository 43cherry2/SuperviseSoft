using System;
using System.Collections;
using System.Text;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SuperviseSoft.Mediapipe
{
  public sealed class TestGpuDiagnosticsController : MonoBehaviour
  {
    private const string CrashStepKey = "SuperviseSoft.TestGpu.LastStartedStep";
    private const string CompletedStepKey = "SuperviseSoft.TestGpu.LastCompletedStep";
    private const string StepTimeKey = "SuperviseSoft.TestGpu.LastStepTime";

    [Header("Models")]
    public TextAsset faceModel;

    private Text _reportText;
    private RawImage _cameraPreview;
    private WebCamTexture _webCamTexture;
    private Coroutine _cameraCoroutine;
    private string _lastAction = "等待操作";
    private string _lastResult = "尚未测试";
    private bool _isBusy;

    private void Awake()
    {
      Application.targetFrameRate = 60;
      BuildUi();
      RefreshReport();
    }

    private void OnDisable()
    {
      if (_cameraCoroutine != null)
      {
        StopCoroutine(_cameraCoroutine);
        _cameraCoroutine = null;
      }

      if (_webCamTexture != null)
      {
        if (_webCamTexture.isPlaying)
        {
          _webCamTexture.Stop();
        }

        _webCamTexture = null;
      }

      if (GpuManager.IsInitialized)
      {
        GpuManager.Shutdown();
      }
    }

    private void BuildUi()
    {
      var canvasObject = new GameObject("GPU Diagnostics Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
      canvasObject.transform.SetParent(transform, false);

      var canvas = canvasObject.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;

      var scaler = canvasObject.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1280f, 720f);
      scaler.matchWidthOrHeight = 0.5f;

      var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      var root = CreatePanel("Root", canvasObject.transform, new Color(0.045f, 0.055f, 0.065f, 1f));
      Stretch(root.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

      var title = CreateText("Title", root.transform, font, "TestGPU - MediaPipe GPU 诊断", 28, TextAnchor.MiddleLeft, Color.white);
      Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -58f), new Vector2(-24f, -12f));

      var buttonBar = CreatePanel("Button Bar", root.transform, new Color(0.10f, 0.12f, 0.14f, 1f));
      Stretch(buttonBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -112f), new Vector2(-20f, -64f));

      AddButton(buttonBar.transform, font, "刷新环境", 12f, () =>
      {
        _lastAction = "刷新环境";
        _lastResult = "已刷新";
        RefreshReport();
      });

      AddButton(buttonBar.transform, font, "相机预览", 176f, () =>
      {
        if (_cameraCoroutine != null)
        {
          StopCoroutine(_cameraCoroutine);
        }

        _cameraCoroutine = StartCoroutine(StartCameraPreview());
      });

      AddButton(buttonBar.transform, font, "CPU任务对照", 340f, () => RunAction("CPU FaceLandmarker 任务创建", TestCpuTaskCreation));
      AddButton(buttonBar.transform, font, "危险: GPU初始化", 504f, () => StartCoroutine(RunGpuInitialize()));
      AddButton(buttonBar.transform, font, "危险: GPU任务", 700f, () => RunAction("GPU FaceLandmarker 任务创建", TestGpuTaskCreation));
      AddButton(buttonBar.transform, font, "清除崩溃记录", 880f, () =>
      {
        PlayerPrefs.DeleteKey(CrashStepKey);
        PlayerPrefs.DeleteKey(CompletedStepKey);
        PlayerPrefs.DeleteKey(StepTimeKey);
        PlayerPrefs.Save();
        _lastAction = "清除崩溃记录";
        _lastResult = "已清除";
        RefreshReport();
      });

      var reportPanel = CreatePanel("Report Panel", root.transform, new Color(0.075f, 0.085f, 0.095f, 1f));
      Stretch(reportPanel.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(20f, 20f), new Vector2(-10f, -124f));

      _reportText = CreateText("Report Text", reportPanel.transform, font, string.Empty, 18, TextAnchor.UpperLeft, new Color(0.9f, 0.94f, 0.96f));
      Stretch(_reportText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 14f), new Vector2(-16f, -14f));
      _reportText.horizontalOverflow = HorizontalWrapMode.Wrap;
      _reportText.verticalOverflow = VerticalWrapMode.Overflow;

      var previewPanel = CreatePanel("Camera Preview Panel", root.transform, new Color(0.02f, 0.025f, 0.03f, 1f));
      Stretch(previewPanel.rectTransform, new Vector2(0.62f, 0f), Vector2.one, new Vector2(10f, 20f), new Vector2(-20f, -124f));

      _cameraPreview = new GameObject("Camera Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
      _cameraPreview.transform.SetParent(previewPanel.transform, false);
      _cameraPreview.color = new Color(0.2f, 0.22f, 0.24f, 1f);
      Stretch(_cameraPreview.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private IEnumerator StartCameraPreview()
    {
      if (_isBusy)
      {
        yield break;
      }

      _isBusy = true;
      _lastAction = "启动相机预览";
      _lastResult = "正在启动...";
      RefreshReport();

      if (_webCamTexture != null)
      {
        if (_webCamTexture.isPlaying)
        {
          _webCamTexture.Stop();
        }

        _webCamTexture = null;
      }

      var devices = WebCamTexture.devices;
      if (devices.Length == 0)
      {
        _lastResult = "未找到相机";
        _isBusy = false;
        RefreshReport();
        yield break;
      }

      var device = devices[0];
      _webCamTexture = new WebCamTexture(device.name, 640, 480, 30);
      _webCamTexture.Play();
      _cameraPreview.texture = _webCamTexture;

      var start = Time.realtimeSinceStartup;
      while (_webCamTexture.width <= 16 && Time.realtimeSinceStartup - start < 5f)
      {
        yield return null;
      }

      _lastResult = _webCamTexture.width > 16
        ? $"相机已启动：{device.name}，{_webCamTexture.width}x{_webCamTexture.height}，旋转={_webCamTexture.videoRotationAngle}，竖向镜像={_webCamTexture.videoVerticallyMirrored}"
        : "相机启动超时，可能是权限或设备占用";
      _isBusy = false;
      RefreshReport();
    }

    private IEnumerator RunGpuInitialize()
    {
      if (_isBusy)
      {
        yield break;
      }

      _isBusy = true;
      _lastAction = "GpuManager.Initialize";
      _lastResult = "开始。注意：如果这里直接闪退，下次进入本场景会显示上次崩溃步骤。";
      MarkStepStarted(_lastAction);
      RefreshReport();

      yield return GpuManager.Initialize();

      MarkStepCompleted(_lastAction);
      _lastResult = GpuManager.IsInitialized
        ? $"成功：GpuManager.IsInitialized=true，GpuResources={(GpuManager.GpuResources == null ? "null" : "ready")}"
        : "失败：Initialize 返回但 IsInitialized=false。请看 Android logcat 中 MediaPipe/GpuManager 日志。";
      _isBusy = false;
      RefreshReport();
    }

    private void RunAction(string action, Action test)
    {
      if (_isBusy)
      {
        return;
      }

      _isBusy = true;
      _lastAction = action;
      _lastResult = "运行中...";
      MarkStepStarted(action);
      RefreshReport();

      try
      {
        test();
        MarkStepCompleted(action);
      }
      catch (Exception exception)
      {
        _lastResult = $"托管异常：{exception.GetType().Name}: {exception.Message}";
        MarkStepCompleted(action);
        Debug.LogException(exception);
      }
      finally
      {
        _isBusy = false;
        RefreshReport();
      }
    }

    private void TestCpuTaskCreation()
    {
      if (faceModel == null)
      {
        _lastResult = "失败：faceModel 未绑定。";
        return;
      }

      FaceLandmarker task = null;
      try
      {
        var options = new FaceLandmarkerOptions(
          new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: faceModel.bytes),
          runningMode: RunningMode.IMAGE,
          numFaces: 1,
          outputFaceBlendshapes: true,
          outputFaceTransformationMatrixes: true);

        task = FaceLandmarker.CreateFromOptions(options, null);
        _lastResult = "成功：CPU FaceLandmarker 可以创建，模型文件本身可用。";
      }
      finally
      {
        task?.Close();
      }
    }

    private void TestGpuTaskCreation()
    {
      if (faceModel == null)
      {
        _lastResult = "失败：faceModel 未绑定。";
        return;
      }

      if (!GpuManager.IsInitialized || GpuManager.GpuResources == null)
      {
        _lastResult = "未运行：GpuManager 尚未初始化。先点“危险: GPU初始化”。";
        return;
      }

      FaceLandmarker task = null;
      try
      {
        var options = new FaceLandmarkerOptions(
          new BaseOptions(BaseOptions.Delegate.GPU, modelAssetBuffer: faceModel.bytes),
          runningMode: RunningMode.IMAGE,
          numFaces: 1,
          outputFaceBlendshapes: true,
          outputFaceTransformationMatrixes: true);

        task = FaceLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
        _lastResult = "成功：GPU FaceLandmarker 任务可以创建。下一步才需要测 GPU 图像输入。";
      }
      finally
      {
        task?.Close();
      }
    }

    private void RefreshReport()
    {
      if (_reportText == null)
      {
        return;
      }

      var builder = new StringBuilder(1600);
      builder.AppendLine("【结论提示】");
      builder.AppendLine(GetImmediateConclusion());
      builder.AppendLine();
      builder.AppendLine("【设备/图形环境】");
      builder.AppendLine($"平台：{Application.platform}");
      builder.AppendLine($"Unity：{Application.unityVersion}");
      builder.AppendLine($"设备型号：{SystemInfo.deviceModel}");
      builder.AppendLine($"系统：{SystemInfo.operatingSystem}");
      builder.AppendLine($"图形后端：{SystemInfo.graphicsDeviceType}");
      builder.AppendLine($"GPU 名称：{SystemInfo.graphicsDeviceName}");
      builder.AppendLine($"GPU 版本：{SystemInfo.graphicsDeviceVersion}");
      builder.AppendLine($"Shader Level：{SystemInfo.graphicsShaderLevel}");
      builder.AppendLine($"AsyncGPUReadback：{SystemInfo.supportsAsyncGPUReadback}");
      builder.AppendLine($"处理器：{SystemInfo.processorType} / {SystemInfo.processorCount}核");
      builder.AppendLine($"内存：{SystemInfo.systemMemorySize} MB");
      builder.AppendLine();
      builder.AppendLine("【MediaPipe GPU 状态】");
      builder.AppendLine($"GpuManager.IsInitialized：{GpuManager.IsInitialized}");
      builder.AppendLine($"GpuResources：{(GpuManager.GpuResources == null ? "null" : "ready")}");
      builder.AppendLine($"Face 模型绑定：{(faceModel == null ? "否" : $"{faceModel.name} ({faceModel.bytes.Length / 1024 / 1024} MB)")}");
      builder.AppendLine();
      builder.AppendLine("【上次危险步骤记录】");
      builder.AppendLine(GetCrashMarkerSummary());
      builder.AppendLine();
      builder.AppendLine("【本次操作】");
      builder.AppendLine($"操作：{_lastAction}");
      builder.AppendLine($"结果：{_lastResult}");
      builder.AppendLine();
      builder.AppendLine("【测试顺序建议】");
      builder.AppendLine("1. 先看图形后端。如果是 Vulkan，基本可以解释主场景打开 GPU 就崩。");
      builder.AppendLine("2. 点“相机预览”，确认相机权限和画面本身正常。");
      builder.AppendLine("3. 点“CPU任务对照”，确认模型和 MediaPipe CPU 正常。");
      builder.AppendLine("4. 只在需要定位崩溃时点“危险: GPU初始化”。如果闪退，回来后看“上次危险步骤记录”。");
      builder.AppendLine("5. GPU 初始化成功后，再点“危险: GPU任务”。");

      _reportText.text = builder.ToString();
    }

    private static string GetImmediateConclusion()
    {
      if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan)
      {
        return "当前是 Vulkan。Homuler MediaPipe Unity 的 GPU/EGL 共享路径主要依赖 OpenGLES3；在 Vulkan 下启用 GPU 很可能在 GpuManager.Initialize 或 GPU delegate 创建时原生崩溃。";
      }

      if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
      {
        return "当前是 OpenGLES3，满足 MediaPipe Unity GPU 路径的基础条件。仍需测试 GpuManager.Initialize 和 GPU 任务创建。";
      }

      return "当前不是 Android 常用的 OpenGLES3/Vulkan 后端。此场景主要用于安卓真机 GPU 诊断。";
    }

    private static void MarkStepStarted(string step)
    {
      PlayerPrefs.SetString(CrashStepKey, step);
      PlayerPrefs.SetString(StepTimeKey, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
      PlayerPrefs.Save();
    }

    private static void MarkStepCompleted(string step)
    {
      PlayerPrefs.SetString(CompletedStepKey, step);
      PlayerPrefs.Save();
    }

    private static string GetCrashMarkerSummary()
    {
      var started = PlayerPrefs.GetString(CrashStepKey, string.Empty);
      var completed = PlayerPrefs.GetString(CompletedStepKey, string.Empty);
      var time = PlayerPrefs.GetString(StepTimeKey, string.Empty);

      if (string.IsNullOrEmpty(started))
      {
        return "无危险步骤记录。";
      }

      if (started != completed)
      {
        return $"疑似上次在“{started}”期间崩溃或强退。开始时间：{time}。";
      }

      return $"上次危险步骤“{started}”已完成。时间：{time}。";
    }

    private static Image CreatePanel(string name, Transform parent, Color color)
    {
      var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
      image.transform.SetParent(parent, false);
      image.color = color;
      return image;
    }

    private static Text CreateText(string name, Transform parent, Font font, string text, int size, TextAnchor anchor, Color color)
    {
      var label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
      label.transform.SetParent(parent, false);
      label.font = font;
      label.text = text;
      label.fontSize = size;
      label.alignment = anchor;
      label.color = color;
      return label;
    }

    private static void AddButton(Transform parent, Font font, string label, float x, UnityEngine.Events.UnityAction action)
    {
      var button = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
      button.transform.SetParent(parent, false);
      button.targetGraphic = button.GetComponent<Image>();
      button.GetComponent<Image>().color = new Color(0.17f, 0.25f, 0.32f, 1f);
      button.onClick.AddListener(action);

      var rect = button.GetComponent<RectTransform>();
      rect.anchorMin = new Vector2(0f, 0f);
      rect.anchorMax = new Vector2(0f, 1f);
      rect.pivot = new Vector2(0f, 0.5f);
      rect.anchoredPosition = new Vector2(x, 0f);
      rect.sizeDelta = new Vector2(label.StartsWith("危险", StringComparison.Ordinal) ? 180f : 148f, -10f);

      var text = CreateText("Text", button.transform, font, label, 15, TextAnchor.MiddleCenter, Color.white);
      Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
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
