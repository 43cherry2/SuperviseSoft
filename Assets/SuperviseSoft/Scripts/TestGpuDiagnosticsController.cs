using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.ObjectDetector;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Experimental;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using VisionRunningMode = Mediapipe.Tasks.Vision.Core.RunningMode;

namespace SuperviseSoft.Mediapipe
{
  public sealed class TestGpuDiagnosticsController : MonoBehaviour
  {
    private const string CrashStepKey = "SuperviseSoft.TestGpu.LastStartedStep";
    private const string CompletedStepKey = "SuperviseSoft.TestGpu.LastCompletedStep";
    private const string StepTimeKey = "SuperviseSoft.TestGpu.LastStepTime";

    [Header("Models")]
    public TextAsset poseModel;
    public TextAsset faceModel;
    public TextAsset handModel;
    public TextAsset objectModel;

    private Text _reportText;
    private RawImage _cameraPreview;
    private RectTransform _rootRect;
    private RectTransform _titleRect;
    private RectTransform _buttonBarRect;
    private RectTransform _reportPanelRect;
    private RectTransform _previewPanelRect;
    private AspectRatioFitter _previewAspect;
    private GridLayoutGroup _buttonGrid;
    private WebCamTexture _webCamTexture;
    private Coroutine _cameraCoroutine;
    private int _selectedCameraIndex;
    private int _lastInferenceFrameId;
    private string _lastAction = "等待操作";
    private string _lastResult = "尚未测试";
    private string _lastInferencePath = "尚未运行图像推理";
    private bool _isBusy;
    private bool _lastPortraitLayout;

    private void Awake()
    {
      Application.targetFrameRate = 60;
      BuildUi();
      ApplyResponsiveLayout(true);
      RefreshReport();
    }

    private void Update()
    {
      ApplyResponsiveLayout(false);
      UpdateCameraPreviewTransform();
    }

    private void OnDisable()
    {
      if (_cameraCoroutine != null)
      {
        StopCoroutine(_cameraCoroutine);
        _cameraCoroutine = null;
      }

      StopCameraPreview();

      if (GpuManager.IsInitialized)
      {
        GpuManager.Shutdown();
      }
    }

    private void BuildUi()
    {
      var canvasObject = new GameObject("GPU Diagnostics Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
      canvasObject.transform.SetParent(transform, false);
      EnsureEventSystem();

      var canvas = canvasObject.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;

      var scaler = canvasObject.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1280f, 720f);
      scaler.matchWidthOrHeight = 0.5f;

      var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      var root = CreatePanel("Root", canvasObject.transform, new Color(0.045f, 0.055f, 0.065f, 1f));
      Stretch(root.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      _rootRect = root.rectTransform;

      var title = CreateText("Title", root.transform, font, "TestGPU - MediaPipe GPU 诊断", 28, TextAnchor.MiddleLeft, Color.white);
      Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -58f), new Vector2(-24f, -12f));
      _titleRect = title.rectTransform;

      var buttonBar = CreatePanel("Button Bar", root.transform, new Color(0.10f, 0.12f, 0.14f, 1f));
      Stretch(buttonBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -112f), new Vector2(-20f, -64f));
      _buttonBarRect = buttonBar.rectTransform;
      _buttonGrid = buttonBar.gameObject.AddComponent<GridLayoutGroup>();
      _buttonGrid.padding = new RectOffset(8, 8, 8, 8);
      _buttonGrid.spacing = new Vector2(8f, 8f);
      _buttonGrid.childAlignment = TextAnchor.UpperLeft;

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

      AddButton(buttonBar.transform, font, "切换相机", 340f, () =>
      {
        if (_cameraCoroutine != null)
        {
          StopCoroutine(_cameraCoroutine);
        }

        _cameraCoroutine = StartCoroutine(SwitchCameraPreview());
      });

      AddButton(buttonBar.transform, font, "CPU任务对照", 340f, () => RunAction("CPU FaceLandmarker 任务创建", TestCpuTaskCreation));
      AddButton(buttonBar.transform, font, "危险: GPU初始化", 504f, () => StartCoroutine(RunGpuInitialize()));
      AddButton(buttonBar.transform, font, "危险: GPU任务", 700f, () => RunAction("GPU FaceLandmarker 任务创建", TestGpuTaskCreation));
      AddButton(buttonBar.transform, font, "CPU单帧推理", 880f, () => StartCoroutine(RunSingleFrameInference(BaseOptions.Delegate.CPU, false)));
      AddButton(buttonBar.transform, font, "危险: GPU单帧", 1060f, () => StartCoroutine(RunSingleFrameInference(BaseOptions.Delegate.GPU, true)));
      AddButton(buttonBar.transform, font, "CPU完整诊断", 1240f, () => StartCoroutine(RunFullPipelineInference(BaseOptions.Delegate.CPU, false, VisionRunningMode.IMAGE)));
      AddButton(buttonBar.transform, font, "GPU完整诊断", 1420f, () => StartCoroutine(RunFullPipelineInference(BaseOptions.Delegate.GPU, true, VisionRunningMode.IMAGE)));
      AddButton(buttonBar.transform, font, "GPU视频诊断", 1600f, () => StartCoroutine(RunFullPipelineInference(BaseOptions.Delegate.GPU, true, VisionRunningMode.VIDEO)));
      AddButton(buttonBar.transform, font, "GPU变换扫描", 1780f, () => StartCoroutine(RunTransformScan(BaseOptions.Delegate.GPU, true)));
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
      _reportPanelRect = reportPanel.rectTransform;

      _reportText = CreateText("Report Text", reportPanel.transform, font, string.Empty, 18, TextAnchor.UpperLeft, new Color(0.9f, 0.94f, 0.96f));
      Stretch(_reportText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 14f), new Vector2(-16f, -14f));
      _reportText.horizontalOverflow = HorizontalWrapMode.Wrap;
      _reportText.verticalOverflow = VerticalWrapMode.Overflow;

      var previewPanel = CreatePanel("Camera Preview Panel", root.transform, new Color(0.02f, 0.025f, 0.03f, 1f));
      Stretch(previewPanel.rectTransform, new Vector2(0.62f, 0f), Vector2.one, new Vector2(10f, 20f), new Vector2(-20f, -124f));
      _previewPanelRect = previewPanel.rectTransform;

      _cameraPreview = new GameObject("Camera Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
      _cameraPreview.transform.SetParent(previewPanel.transform, false);
      _cameraPreview.color = new Color(0.2f, 0.22f, 0.24f, 1f);
      Stretch(_cameraPreview.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      _previewAspect = _cameraPreview.gameObject.AddComponent<AspectRatioFitter>();
      _previewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
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
      yield return EnsureCameraPreviewRunning();
      _isBusy = false;
      RefreshReport();
    }

    private IEnumerator SwitchCameraPreview()
    {
      if (_isBusy)
      {
        yield break;
      }

      _isBusy = true;
      var devices = WebCamTexture.devices;
      if (devices.Length == 0)
      {
        _lastAction = "切换相机";
        _lastResult = "未找到相机";
        _isBusy = false;
        RefreshReport();
        yield break;
      }

      _selectedCameraIndex = (_selectedCameraIndex + 1) % devices.Length;
      _lastAction = $"切换相机到 #{_selectedCameraIndex}";
      _lastResult = $"正在启动：{devices[_selectedCameraIndex].name}，前置={devices[_selectedCameraIndex].isFrontFacing}";
      RefreshReport();

      StopCameraPreview();
      yield return EnsureCameraPreviewRunning();

      _isBusy = false;
      RefreshReport();
    }

    private IEnumerator EnsureCameraPreviewRunning()
    {
      if (_webCamTexture != null && _webCamTexture.isPlaying && _webCamTexture.width > 16)
      {
        var runningDevice = GetCurrentCameraDevice();
        _lastResult = $"相机已就绪：#{_selectedCameraIndex} {runningDevice.name}，前置={runningDevice.isFrontFacing}，{_webCamTexture.width}x{_webCamTexture.height}，旋转={_webCamTexture.videoRotationAngle}，竖向镜像={_webCamTexture.videoVerticallyMirrored}";
        yield break;
      }

      StopCameraPreview();

      var devices = WebCamTexture.devices;
      if (devices.Length == 0)
      {
        _lastResult = "未找到相机";
        yield break;
      }

      _selectedCameraIndex = Mathf.Clamp(_selectedCameraIndex, 0, devices.Length - 1);
      var device = devices[_selectedCameraIndex];
      _webCamTexture = new WebCamTexture(device.name, 640, 480, 30);
      _webCamTexture.Play();
      _cameraPreview.texture = _webCamTexture;

      var start = Time.realtimeSinceStartup;
      while (_webCamTexture != null &&
             _webCamTexture.width <= 16 &&
             Time.realtimeSinceStartup - start < 5f)
      {
        yield return null;
      }

      _lastResult = _webCamTexture != null && _webCamTexture.width > 16
        ? $"相机已启动：#{_selectedCameraIndex} {device.name}，前置={device.isFrontFacing}，{_webCamTexture.width}x{_webCamTexture.height}，旋转={_webCamTexture.videoRotationAngle}，竖向镜像={_webCamTexture.videoVerticallyMirrored}"
        : "相机启动超时，可能是权限或设备占用";
      UpdateCameraPreviewTransform();
    }

    private void StopCameraPreview()
    {
      if (_webCamTexture == null)
      {
        return;
      }

      if (_webCamTexture.isPlaying)
      {
        _webCamTexture.Stop();
      }

      _webCamTexture = null;
      if (_cameraPreview != null)
      {
        _cameraPreview.texture = null;
      }
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
          runningMode: global::Mediapipe.Tasks.Vision.Core.RunningMode.IMAGE,
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
          runningMode: global::Mediapipe.Tasks.Vision.Core.RunningMode.IMAGE,
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

    private IEnumerator RunSingleFrameInference(BaseOptions.Delegate delegateCase, bool allowGpuTextureInput)
    {
      if (_isBusy)
      {
        yield break;
      }

      _isBusy = true;
      var action = delegateCase == BaseOptions.Delegate.GPU
        ? "GPU FaceLandmarker 单帧推理"
        : "CPU FaceLandmarker 单帧推理";
      _lastAction = action;
      _lastResult = "准备相机帧和模型...";
      MarkStepStarted(action);
      RefreshReport();

      FaceLandmarker task = null;
      TextureFramePool framePool = null;

      try
      {
        if (faceModel == null)
        {
          _lastResult = "失败：faceModel 未绑定。";
          yield break;
        }

        yield return EnsureCameraPreviewRunning();
        if (_webCamTexture == null || !_webCamTexture.isPlaying || _webCamTexture.width <= 16)
        {
          _lastResult = "失败：相机没有可用画面，无法做单帧推理。";
          yield break;
        }

        if (delegateCase == BaseOptions.Delegate.GPU &&
            (!GpuManager.IsInitialized || GpuManager.GpuResources == null))
        {
          _lastResult = "正在初始化 GPU...";
          RefreshReport();
          yield return GpuManager.Initialize();
        }

        if (delegateCase == BaseOptions.Delegate.GPU &&
            (!GpuManager.IsInitialized || GpuManager.GpuResources == null))
        {
          _lastResult = "失败：GpuManager 初始化后仍不可用。";
          yield break;
        }

        var options = new FaceLandmarkerOptions(
          new BaseOptions(delegateCase, modelAssetBuffer: faceModel.bytes),
          runningMode: global::Mediapipe.Tasks.Vision.Core.RunningMode.IMAGE,
          numFaces: 1,
          outputFaceBlendshapes: true,
          outputFaceTransformationMatrixes: true);

        task = FaceLandmarker.CreateFromOptions(
          options,
          delegateCase == BaseOptions.Delegate.GPU ? GpuManager.GpuResources : null);

        framePool = new TextureFramePool(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGBA32, 2);
        if (!framePool.TryGetTextureFrame(out var textureFrame))
        {
          _lastResult = "失败：TextureFramePool 暂时没有空闲帧。";
          yield break;
        }

        var imageProcessingOptions = CreateImageProcessingOptions(out var flipHorizontally, out var flipVertically);
        var useGpuTexture = delegateCase == BaseOptions.Delegate.GPU &&
                            allowGpuTextureInput &&
                            SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;
        var inputPath = useGpuTexture ? "GPU纹理输入" : "CPU相机帧输入";

        if (useGpuTexture)
        {
          using var glContext = GpuManager.GetGlContext();
          textureFrame.ReadTextureOnGPU(_webCamTexture, flipHorizontally, flipVertically);
          var inputImage = textureFrame.BuildGPUImage(glContext);
          yield return new WaitForEndOfFrame();

          var result = FaceLandmarkerResult.Alloc(1, true, true);
          var detected = task.TryDetect(inputImage, imageProcessingOptions, ref result);
          _lastInferencePath = $"{delegateCase} delegate / {inputPath}";
          _lastResult = $"成功：{_lastInferencePath} 完成，检测到人脸={detected}。";
        }
        else
        {
          var request = textureFrame.ReadTextureAsync(_webCamTexture, flipHorizontally, flipVertically);
          yield return new WaitUntil(() => request.done);
          if (request.hasError)
          {
            textureFrame.Release();
            _lastResult = "失败：读取相机帧到 CPU 失败。";
            yield break;
          }

          var inputImage = textureFrame.BuildCPUImage();
          textureFrame.Release();

          var result = FaceLandmarkerResult.Alloc(1, true, true);
          var detected = task.TryDetect(inputImage, imageProcessingOptions, ref result);
          _lastInferencePath = $"{delegateCase} delegate / {inputPath}";
          _lastResult = $"成功：{_lastInferencePath} 完成，检测到人脸={detected}。";
        }
      }
      finally
      {
        task?.Close();
        framePool?.Dispose();
        MarkStepCompleted(action);
        _isBusy = false;
        RefreshReport();
      }
    }

    private IEnumerator RunFullPipelineInference(BaseOptions.Delegate delegateCase, bool allowGpuTextureInput, VisionRunningMode runningMode)
    {
      if (_isBusy)
      {
        yield break;
      }

      _isBusy = true;
      var action = $"{delegateCase} 完整链路诊断 ({runningMode})";
      _lastAction = action;
      _lastResult = "准备相机、GPU 和四个任务...";
      MarkStepStarted(action);
      RefreshReport();

      PoseLandmarker poseTask = null;
      FaceLandmarker faceTask = null;
      HandLandmarker handTask = null;
      ObjectDetector objectTask = null;
      TextureFramePool framePool = null;

      try
      {
        yield return EnsureCameraPreviewRunning();
        if (_webCamTexture == null || !_webCamTexture.isPlaying || _webCamTexture.width <= 16)
        {
          _lastResult = "失败：相机没有可用画面，无法做完整诊断。";
          yield break;
        }

        if (delegateCase == BaseOptions.Delegate.GPU &&
            (!GpuManager.IsInitialized || GpuManager.GpuResources == null))
        {
          _lastResult = "正在初始化 GPU...";
          RefreshReport();
          yield return GpuManager.Initialize();
        }

        if (delegateCase == BaseOptions.Delegate.GPU &&
            (!GpuManager.IsInitialized || GpuManager.GpuResources == null))
        {
          _lastResult = "失败：GpuManager 初始化后仍不可用。";
          yield break;
        }

        var gpuResources = delegateCase == BaseOptions.Delegate.GPU ? GpuManager.GpuResources : null;
        var taskCreation = new StringBuilder(512);
        CreateDiagnosticTasks(delegateCase, runningMode, gpuResources, taskCreation, out poseTask, out faceTask, out handTask, out objectTask);

        framePool = new TextureFramePool(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGBA32, 8);
        var imageProcessingOptions = CreateImageProcessingOptions(
          false,
          out var flipHorizontally,
          out var flipVertically,
          out var rotationDegrees);
        var useGpuTexture = delegateCase == BaseOptions.Delegate.GPU &&
                            allowGpuTextureInput &&
                            SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;

        using var glContext = useGpuTexture ? GpuManager.GetGlContext() : null;
        var builder = new StringBuilder(1600);
        builder.AppendLine($"链路：{delegateCase} delegate / {(useGpuTexture ? "GPU纹理输入" : "CPU相机帧输入")} / {runningMode}");
        builder.AppendLine(GetCameraSummary());
        builder.AppendLine($"输入变换：rotationDegrees={rotationDegrees}, flipH={flipHorizontally}, flipV={flipVertically}");
        builder.Append(taskCreation);

        var timestamp = GetTimestampMillis();
        if (poseTask != null)
        {
          yield return RunTaskOnCurrentFrame(
            "Pose",
            useGpuTexture,
            framePool,
            glContext,
            imageProcessingOptions,
            flipHorizontally,
            flipVertically,
            builder,
            image => SummarizePose(poseTask, image, imageProcessingOptions, runningMode, timestamp++));
        }

        if (faceTask != null)
        {
          yield return RunTaskOnCurrentFrame(
            "Face",
            useGpuTexture,
            framePool,
            glContext,
            imageProcessingOptions,
            flipHorizontally,
            flipVertically,
            builder,
            image => SummarizeFace(faceTask, image, imageProcessingOptions, runningMode, timestamp++));
        }

        if (handTask != null)
        {
          yield return RunTaskOnCurrentFrame(
            "Hand",
            useGpuTexture,
            framePool,
            glContext,
            imageProcessingOptions,
            flipHorizontally,
            flipVertically,
            builder,
            image => SummarizeHand(handTask, image, imageProcessingOptions, runningMode, timestamp++));
        }

        if (objectTask != null)
        {
          yield return RunTaskOnCurrentFrame(
            "Object",
            useGpuTexture,
            framePool,
            glContext,
            imageProcessingOptions,
            flipHorizontally,
            flipVertically,
            builder,
            image => SummarizeObjects(objectTask, image, imageProcessingOptions, runningMode, timestamp++));
        }

        _lastInferenceFrameId++;
        _lastInferencePath = $"{delegateCase} / {(useGpuTexture ? "GPU纹理输入" : "CPU相机帧输入")} / {runningMode} / frame#{_lastInferenceFrameId}";
        _lastResult = builder.ToString();
      }
      finally
      {
        poseTask?.Close();
        faceTask?.Close();
        handTask?.Close();
        objectTask?.Close();
        framePool?.Dispose();
        MarkStepCompleted(action);
        _isBusy = false;
        RefreshReport();
      }
    }

    private IEnumerator RunTransformScan(BaseOptions.Delegate delegateCase, bool allowGpuTextureInput)
    {
      if (_isBusy)
      {
        yield break;
      }

      _isBusy = true;
      var action = $"{delegateCase} 变换扫描";
      _lastAction = action;
      _lastResult = "准备扫描相机旋转和镜像组合...";
      MarkStepStarted(action);
      RefreshReport();

      PoseLandmarker poseTask = null;
      FaceLandmarker faceTask = null;
      TextureFramePool framePool = null;

      try
      {
        yield return EnsureCameraPreviewRunning();
        if (_webCamTexture == null || !_webCamTexture.isPlaying || _webCamTexture.width <= 16)
        {
          _lastResult = "失败：相机没有可用画面，无法做变换扫描。";
          yield break;
        }

        if (delegateCase == BaseOptions.Delegate.GPU &&
            (!GpuManager.IsInitialized || GpuManager.GpuResources == null))
        {
          _lastResult = "正在初始化 GPU...";
          RefreshReport();
          yield return GpuManager.Initialize();
        }

        if (delegateCase == BaseOptions.Delegate.GPU &&
            (!GpuManager.IsInitialized || GpuManager.GpuResources == null))
        {
          _lastResult = "失败：GpuManager 初始化后仍不可用。";
          yield break;
        }

        var gpuResources = delegateCase == BaseOptions.Delegate.GPU ? GpuManager.GpuResources : null;
        var taskCreation = new StringBuilder(256);
        if (poseModel != null)
        {
          try
          {
            poseTask = PoseLandmarker.CreateFromOptions(
              CreatePoseOptions(delegateCase, VisionRunningMode.IMAGE),
              gpuResources);
            taskCreation.AppendLine("Pose 任务：已创建");
          }
          catch (Exception exception)
          {
            taskCreation.AppendLine($"Pose 任务：创建异常 {exception.GetType().Name}: {exception.Message}");
            Debug.LogException(exception);
          }
        }
        else
        {
          taskCreation.AppendLine("Pose 任务：跳过，poseModel 未绑定");
        }

        if (faceModel != null)
        {
          try
          {
            faceTask = FaceLandmarker.CreateFromOptions(
              CreateFaceOptions(delegateCase, VisionRunningMode.IMAGE),
              gpuResources);
            taskCreation.AppendLine("Face 任务：已创建");
          }
          catch (Exception exception)
          {
            taskCreation.AppendLine($"Face 任务：创建异常 {exception.GetType().Name}: {exception.Message}");
            Debug.LogException(exception);
          }
        }
        else
        {
          taskCreation.AppendLine("Face 任务：跳过，faceModel 未绑定");
        }

        var useGpuTexture = delegateCase == BaseOptions.Delegate.GPU &&
                            allowGpuTextureInput &&
                            SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;
        framePool = new TextureFramePool(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGBA32, 40);
        using var glContext = useGpuTexture ? GpuManager.GetGlContext() : null;

        var builder = new StringBuilder(2200);
        builder.AppendLine($"扫描链路：{delegateCase} delegate / {(useGpuTexture ? "GPU纹理输入" : "CPU相机帧输入")} / IMAGE");
        builder.AppendLine(GetCameraSummary());
        builder.AppendLine($"说明：每组只测 Face/Pose；如果某个 rotation/flip 能检测，主场景大概率是相机变换问题。");
        builder.Append(taskCreation);

        var rotations = new[] { 0, 90, 180, 270 };
        var horizontalFlips = new[] { false, true };
        var flipVertically = _webCamTexture.videoVerticallyMirrored;
        var timestamp = GetTimestampMillis();

        foreach (var rotation in rotations)
        {
          foreach (var flipHorizontally in horizontalFlips)
          {
            var options = new ImageProcessingOptions(rotationDegrees: rotation);
            builder.AppendLine($"组合 rotation={rotation}, flipH={flipHorizontally}, flipV={flipVertically}");

            if (faceTask != null)
            {
              yield return RunTaskOnCurrentFrame(
                "  Face",
                useGpuTexture,
                framePool,
                glContext,
                options,
                flipHorizontally,
                flipVertically,
                builder,
                image => SummarizeFace(faceTask, image, options, VisionRunningMode.IMAGE, timestamp++));
            }

            if (poseTask != null)
            {
              yield return RunTaskOnCurrentFrame(
                "  Pose",
                useGpuTexture,
                framePool,
                glContext,
                options,
                flipHorizontally,
                flipVertically,
                builder,
                image => SummarizePose(poseTask, image, options, VisionRunningMode.IMAGE, timestamp++));
            }
          }
        }

        _lastInferenceFrameId++;
        _lastInferencePath = $"{delegateCase} 变换扫描 / {(useGpuTexture ? "GPU纹理输入" : "CPU相机帧输入")} / frame#{_lastInferenceFrameId}";
        _lastResult = builder.ToString();
      }
      finally
      {
        poseTask?.Close();
        faceTask?.Close();
        framePool?.Dispose();
        MarkStepCompleted(action);
        _isBusy = false;
        RefreshReport();
      }
    }

    private void CreateDiagnosticTasks(
      BaseOptions.Delegate delegateCase,
      VisionRunningMode runningMode,
      global::Mediapipe.GpuResources gpuResources,
      StringBuilder builder,
      out PoseLandmarker poseTask,
      out FaceLandmarker faceTask,
      out HandLandmarker handTask,
      out ObjectDetector objectTask)
    {
      poseTask = null;
      faceTask = null;
      handTask = null;
      objectTask = null;

      if (poseModel == null)
      {
        builder.AppendLine("Pose 任务：跳过，poseModel 未绑定");
      }
      else
      {
        try
        {
          poseTask = PoseLandmarker.CreateFromOptions(CreatePoseOptions(delegateCase, runningMode), gpuResources);
          builder.AppendLine("Pose 任务：已创建");
        }
        catch (Exception exception)
        {
          builder.AppendLine($"Pose 任务：创建异常 {exception.GetType().Name}: {exception.Message}");
          Debug.LogException(exception);
        }
      }

      if (faceModel == null)
      {
        builder.AppendLine("Face 任务：跳过，faceModel 未绑定");
      }
      else
      {
        try
        {
          faceTask = FaceLandmarker.CreateFromOptions(CreateFaceOptions(delegateCase, runningMode), gpuResources);
          builder.AppendLine("Face 任务：已创建");
        }
        catch (Exception exception)
        {
          builder.AppendLine($"Face 任务：创建异常 {exception.GetType().Name}: {exception.Message}");
          Debug.LogException(exception);
        }
      }

      if (handModel == null)
      {
        builder.AppendLine("Hand 任务：跳过，handModel 未绑定");
      }
      else
      {
        try
        {
          handTask = HandLandmarker.CreateFromOptions(CreateHandOptions(delegateCase, runningMode), gpuResources);
          builder.AppendLine("Hand 任务：已创建");
        }
        catch (Exception exception)
        {
          builder.AppendLine($"Hand 任务：创建异常 {exception.GetType().Name}: {exception.Message}");
          Debug.LogException(exception);
        }
      }

      if (objectModel == null)
      {
        builder.AppendLine("Object 任务：跳过，objectModel 未绑定");
      }
      else
      {
        try
        {
          objectTask = ObjectDetector.CreateFromOptions(CreateObjectOptions(delegateCase, runningMode), gpuResources);
          builder.AppendLine("Object 任务：已创建");
        }
        catch (Exception exception)
        {
          builder.AppendLine($"Object 任务：创建异常 {exception.GetType().Name}: {exception.Message}");
          Debug.LogException(exception);
        }
      }
    }

    private PoseLandmarkerOptions CreatePoseOptions(BaseOptions.Delegate delegateCase, VisionRunningMode runningMode)
      => new PoseLandmarkerOptions(
        new BaseOptions(delegateCase, modelAssetBuffer: poseModel.bytes),
        runningMode: runningMode,
        numPoses: 1,
        minPoseDetectionConfidence: 0.5f,
        minPosePresenceConfidence: 0.5f,
        minTrackingConfidence: 0.5f,
        outputSegmentationMasks: false);

    private FaceLandmarkerOptions CreateFaceOptions(BaseOptions.Delegate delegateCase, VisionRunningMode runningMode)
      => new FaceLandmarkerOptions(
        new BaseOptions(delegateCase, modelAssetBuffer: faceModel.bytes),
        runningMode: runningMode,
        numFaces: 1,
        minFaceDetectionConfidence: 0.5f,
        minFacePresenceConfidence: 0.5f,
        minTrackingConfidence: 0.5f,
        outputFaceBlendshapes: true,
        outputFaceTransformationMatrixes: true);

    private HandLandmarkerOptions CreateHandOptions(BaseOptions.Delegate delegateCase, VisionRunningMode runningMode)
      => new HandLandmarkerOptions(
        new BaseOptions(delegateCase, modelAssetBuffer: handModel.bytes),
        runningMode: runningMode,
        numHands: 2,
        minHandDetectionConfidence: 0.5f,
        minHandPresenceConfidence: 0.5f,
        minTrackingConfidence: 0.5f);

    private ObjectDetectorOptions CreateObjectOptions(BaseOptions.Delegate delegateCase, VisionRunningMode runningMode)
      => new ObjectDetectorOptions(
        new BaseOptions(delegateCase, modelAssetBuffer: objectModel.bytes),
        runningMode: runningMode,
        maxResults: 8,
        scoreThreshold: 0.25f,
        categoryAllowList: null);

    private IEnumerator RunTaskOnCurrentFrame(
      string label,
      bool useGpuTexture,
      TextureFramePool framePool,
      global::Mediapipe.GlContext glContext,
      ImageProcessingOptions imageProcessingOptions,
      bool flipHorizontally,
      bool flipVertically,
      StringBuilder builder,
      Func<global::Mediapipe.Image, string> detect)
    {
      if (!framePool.TryGetTextureFrame(out var textureFrame))
      {
        builder.AppendLine($"{label}: 失败，TextureFramePool 暂时没有空闲帧");
        yield break;
      }

      global::Mediapipe.Image inputImage = null;
      if (useGpuTexture)
      {
        try
        {
          textureFrame.ReadTextureOnGPU(_webCamTexture, flipHorizontally, flipVertically);
          inputImage = textureFrame.BuildGPUImage(glContext);
        }
        catch (Exception exception)
        {
          textureFrame.Release();
          builder.AppendLine($"{label}: GPU图像构建异常 {exception.GetType().Name}: {exception.Message}");
          Debug.LogException(exception);
          yield break;
        }

        yield return new WaitForEndOfFrame();
      }
      else
      {
        var request = textureFrame.ReadTextureAsync(_webCamTexture, flipHorizontally, flipVertically);
        yield return new WaitUntil(() => request.done);
        if (request.hasError)
        {
          textureFrame.Release();
          builder.AppendLine($"{label}: 失败，读取相机帧到 CPU 出错");
          yield break;
        }

        inputImage = textureFrame.BuildCPUImage();
        textureFrame.Release();
      }

      try
      {
        builder.AppendLine($"{label}: {detect(inputImage)}");
      }
      catch (Exception exception)
      {
        builder.AppendLine($"{label}: 推理异常 {exception.GetType().Name}: {exception.Message}");
        Debug.LogException(exception);
      }
    }

    private static string SummarizePose(
      PoseLandmarker task,
      global::Mediapipe.Image image,
      ImageProcessingOptions imageProcessingOptions,
      VisionRunningMode runningMode,
      long timestamp)
    {
      var result = PoseLandmarkerResult.Alloc(1, false);
      var detected = runningMode == VisionRunningMode.IMAGE
        ? task.TryDetect(image, imageProcessingOptions, ref result)
        : task.TryDetectForVideo(image, timestamp, imageProcessingOptions, ref result);
      var personCount = result.poseLandmarks?.Count ?? 0;
      var pointCount = personCount > 0 ? result.poseLandmarks[0].landmarks?.Count ?? 0 : 0;
      return $"detected={detected}, 人数={personCount}, 点数={pointCount}";
    }

    private static string SummarizeFace(
      FaceLandmarker task,
      global::Mediapipe.Image image,
      ImageProcessingOptions imageProcessingOptions,
      VisionRunningMode runningMode,
      long timestamp)
    {
      var result = FaceLandmarkerResult.Alloc(1, true, true);
      var detected = runningMode == VisionRunningMode.IMAGE
        ? task.TryDetect(image, imageProcessingOptions, ref result)
        : task.TryDetectForVideo(image, timestamp, imageProcessingOptions, ref result);
      var faceCount = result.faceLandmarks?.Count ?? 0;
      var pointCount = faceCount > 0 ? result.faceLandmarks[0].landmarks?.Count ?? 0 : 0;
      var blendshapeCount = result.faceBlendshapes?.Count ?? 0;
      return $"detected={detected}, 人脸={faceCount}, 点数={pointCount}, blendshapes={blendshapeCount}";
    }

    private static string SummarizeHand(
      HandLandmarker task,
      global::Mediapipe.Image image,
      ImageProcessingOptions imageProcessingOptions,
      VisionRunningMode runningMode,
      long timestamp)
    {
      var result = HandLandmarkerResult.Alloc(2);
      var detected = runningMode == VisionRunningMode.IMAGE
        ? task.TryDetect(image, imageProcessingOptions, ref result)
        : task.TryDetectForVideo(image, timestamp, imageProcessingOptions, ref result);
      var handCount = result.handLandmarks?.Count ?? 0;
      var pointCount = handCount > 0 ? result.handLandmarks[0].landmarks?.Count ?? 0 : 0;
      return $"detected={detected}, 手={handCount}, 点数={pointCount}";
    }

    private static string SummarizeObjects(
      ObjectDetector task,
      global::Mediapipe.Image image,
      ImageProcessingOptions imageProcessingOptions,
      VisionRunningMode runningMode,
      long timestamp)
    {
      var result = DetectionResult.Alloc(8);
      var detected = runningMode == VisionRunningMode.IMAGE
        ? task.TryDetect(image, imageProcessingOptions, ref result)
        : task.TryDetectForVideo(image, timestamp, imageProcessingOptions, ref result);
      var objectCount = result.detections?.Count ?? 0;
      return $"detected={detected}, 物体={objectCount}, {FormatObjectLabels(result)}";
    }

    private static string FormatObjectLabels(DetectionResult result)
    {
      if (result.detections == null || result.detections.Count == 0)
      {
        return "labels=无";
      }

      var builder = new StringBuilder(128);
      builder.Append("labels=");
      var max = Mathf.Min(3, result.detections.Count);
      for (var i = 0; i < max; i++)
      {
        if (i > 0)
        {
          builder.Append(", ");
        }

        var detection = result.detections[i];
        if (detection.categories == null || detection.categories.Count == 0)
        {
          builder.Append("unknown");
          continue;
        }

        var category = detection.categories[0];
        var label = string.IsNullOrEmpty(category.displayName) ? category.categoryName : category.displayName;
        builder.Append(string.IsNullOrEmpty(label) ? "unknown" : label);
        builder.Append("(");
        builder.Append(category.score.ToString("0.00"));
        builder.Append(")");
      }

      return builder.ToString();
    }

    private ImageProcessingOptions CreateImageProcessingOptions(out bool flipHorizontally, out bool flipVertically)
    {
      return CreateImageProcessingOptions(false, out flipHorizontally, out flipVertically, out _);
    }

    private ImageProcessingOptions CreateImageProcessingOptions(
      bool mirrorHorizontally,
      out bool flipHorizontally,
      out bool flipVertically,
      out int rotationDegrees)
    {
      var rotation = (global::Mediapipe.Unity.RotationAngle)NormalizeRotationDegrees(_webCamTexture.videoRotationAngle);
      var transformationOptions = ImageTransformationOptions.Build(
        mirrorHorizontally,
        _webCamTexture.videoVerticallyMirrored,
        rotation);

      flipHorizontally = transformationOptions.flipHorizontally;
      flipVertically = transformationOptions.flipVertically;
      rotationDegrees = (int)transformationOptions.rotationAngle;
      return new ImageProcessingOptions(rotationDegrees: rotationDegrees);
    }

    private static int NormalizeRotationDegrees(int degrees)
    {
      var normalized = degrees % 360;
      if (normalized < 0)
      {
        normalized += 360;
      }

      return normalized switch
      {
        >= 315 or < 45 => 0,
        >= 45 and < 135 => 90,
        >= 135 and < 225 => 180,
        _ => 270
      };
    }

    private WebCamDevice GetCurrentCameraDevice()
    {
      var devices = WebCamTexture.devices;
      if (devices.Length == 0)
      {
        return default;
      }

      _selectedCameraIndex = Mathf.Clamp(_selectedCameraIndex, 0, devices.Length - 1);
      return devices[_selectedCameraIndex];
    }

    private string GetCameraSummary()
    {
      var devices = WebCamTexture.devices;
      if (devices.Length == 0)
      {
        return "相机：未找到设备";
      }

      var device = GetCurrentCameraDevice();
      if (_webCamTexture == null)
      {
        return $"相机：#{_selectedCameraIndex} {device.name}，前置={device.isFrontFacing}，未启动，设备数={devices.Length}";
      }

      return $"相机：#{_selectedCameraIndex} {device.name}，前置={device.isFrontFacing}，{_webCamTexture.width}x{_webCamTexture.height}，旋转={_webCamTexture.videoRotationAngle}，竖向镜像={_webCamTexture.videoVerticallyMirrored}，设备数={devices.Length}";
    }

    private static long GetTimestampMillis()
      => (long)(Time.realtimeSinceStartup * 1000);

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
      builder.AppendLine(GetCameraSummary());
      builder.AppendLine($"Pose 模型绑定：{FormatModelBinding(poseModel)}");
      builder.AppendLine($"Face 模型绑定：{(faceModel == null ? "否" : $"{faceModel.name} ({faceModel.bytes.Length / 1024 / 1024} MB)")}");
      builder.AppendLine($"Hand 模型绑定：{FormatModelBinding(handModel)}");
      builder.AppendLine($"Object 模型绑定：{FormatModelBinding(objectModel)}");
      builder.AppendLine($"上次图像推理路径：{_lastInferencePath}");
      builder.AppendLine();
      builder.AppendLine("【上次危险步骤记录】");
      builder.AppendLine(GetCrashMarkerSummary());
      builder.AppendLine();
      builder.AppendLine("【本次操作】");
      builder.AppendLine($"操作：{_lastAction}");
      builder.AppendLine($"结果：{_lastResult}");
      builder.AppendLine();
      builder.AppendLine("【测试顺序建议】");
      builder.AppendLine("1. 先看图形后端。Vulkan 下初始化/任务创建可能成功，但 GPU 纹理输入仍需单独验证。");
      builder.AppendLine("2. 点“相机预览”，确认相机权限和画面本身正常。");
      builder.AppendLine("3. 点“CPU任务对照”，确认模型和 MediaPipe CPU 正常。");
      builder.AppendLine("4. 只在需要定位崩溃时点“危险: GPU初始化”。如果闪退，回来后看“上次危险步骤记录”。");
      builder.AppendLine("5. GPU 初始化成功后，再点“危险: GPU任务”。");
      builder.AppendLine("6. 最关键：点“CPU单帧推理”和“危险: GPU单帧”，看是否能真实跑过相机图像。");
      builder.AppendLine("7. 切到前置相机后，依次点“CPU完整诊断”“GPU完整诊断”“GPU视频诊断”。如果 CPU 有人/脸而 GPU 没有，就是 GPU 链路问题；如果两者都没有，优先看相机旋转/镜像。");
      builder.AppendLine("8. 如果前置相机无检测，点“GPU变换扫描”，看哪组 rotation/flip 能检测到。");

      _reportText.text = builder.ToString();
    }

    private static string FormatModelBinding(TextAsset model)
      => model == null ? "否" : $"{model.name} ({model.bytes.Length / 1024 / 1024} MB)";

    private static string GetImmediateConclusion()
    {
      if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan)
      {
        return "当前是 Vulkan。若 GPU 初始化/任务创建成功但相机图像推理失败，优先怀疑图形后端共享纹理链路；建议切 OpenGLES3 后再测 GPU纹理输入。";
      }

      if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
      {
        return "当前是 OpenGLES3。若 TestGPU 的 Face/Pose 能检出，说明手机 GPU 不是本质不支持；下一步看前置相机变换、VIDEO模式、或主场景生命周期。";
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

    private static void EnsureEventSystem()
    {
      if (FindObjectOfType<EventSystem>() != null)
      {
        return;
      }

      new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void ApplyResponsiveLayout(bool force)
    {
      if (_rootRect == null || _buttonGrid == null || _reportPanelRect == null || _previewPanelRect == null)
      {
        return;
      }

      var devicePortrait = Input.deviceOrientation == DeviceOrientation.Portrait ||
                           Input.deviceOrientation == DeviceOrientation.PortraitUpsideDown;
      var isPortrait = devicePortrait ||
                       UnityEngine.Screen.height > UnityEngine.Screen.width ||
                       _rootRect.rect.height > _rootRect.rect.width;
      if (!force && isPortrait == _lastPortraitLayout)
      {
        return;
      }

      _lastPortraitLayout = isPortrait;

      if (isPortrait)
      {
        Stretch(_titleRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -62f), new Vector2(-20f, -10f));
        Stretch(_buttonBarRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -356f), new Vector2(-20f, -70f));
        Stretch(_reportPanelRect, new Vector2(0f, 0.28f), new Vector2(1f, 1f), new Vector2(20f, 10f), new Vector2(-20f, -366f));
        Stretch(_previewPanelRect, Vector2.zero, new Vector2(1f, 0.28f), new Vector2(20f, 18f), new Vector2(-20f, -10f));

        var width = Mathf.Max(320f, _rootRect.rect.width);
        var cellWidth = Mathf.Max(128f, (width - 56f) * 0.5f);
        _buttonGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _buttonGrid.constraintCount = 2;
        _buttonGrid.cellSize = new Vector2(cellWidth, 38f);
      }
      else
      {
        Stretch(_titleRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -58f), new Vector2(-24f, -12f));
        Stretch(_buttonBarRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -154f), new Vector2(-20f, -64f));
        Stretch(_reportPanelRect, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(20f, 20f), new Vector2(-10f, -166f));
        Stretch(_previewPanelRect, new Vector2(0.62f, 0f), Vector2.one, new Vector2(10f, 20f), new Vector2(-20f, -166f));

        var width = Mathf.Max(760f, _rootRect.rect.width);
        var cellWidth = Mathf.Max(112f, (width - 96f) / 6f);
        _buttonGrid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        _buttonGrid.constraintCount = 2;
        _buttonGrid.cellSize = new Vector2(cellWidth, 38f);
      }
    }

    private void UpdateCameraPreviewTransform()
    {
      if (_cameraPreview == null || _webCamTexture == null)
      {
        return;
      }

      var rect = _cameraPreview.rectTransform;
      var rotation = _webCamTexture.videoRotationAngle;
      rect.localEulerAngles = new Vector3(0f, 0f, -rotation);
      _cameraPreview.uvRect = _webCamTexture.videoVerticallyMirrored
        ? new UnityEngine.Rect(0f, 1f, 1f, -1f)
        : new UnityEngine.Rect(0f, 0f, 1f, 1f);

      if (_previewAspect != null && _webCamTexture.width > 16 && _webCamTexture.height > 16)
      {
        var rotated = rotation == 90 || rotation == 270;
        _previewAspect.aspectRatio = rotated
          ? (float)_webCamTexture.height / _webCamTexture.width
          : (float)_webCamTexture.width / _webCamTexture.height;
      }
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
