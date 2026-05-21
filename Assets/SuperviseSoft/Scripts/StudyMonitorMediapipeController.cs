using System;
using System.Collections;
using System.Collections.Generic;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.ObjectDetector;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity.Experimental;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using ObjectDetectionResult = Mediapipe.Tasks.Components.Containers.DetectionResult;
using TaskNormalizedLandmark = Mediapipe.Tasks.Components.Containers.NormalizedLandmark;
using UiImage = UnityEngine.UI.Image;

namespace SuperviseSoft.Mediapipe
{
  public class StudyMonitorMediapipeController : MonoBehaviour
  {
    private const string FaceProfilesPrefsKey = "SuperviseSoft.StudyMonitor.FaceProfiles";

    private static readonly int[] FaceSignatureLandmarkIndices =
    {
      1, 10, 33, 61, 152, 199, 234, 263, 291, 454
    };

    private static readonly int[] PosePresenceLandmarkIndices =
    {
      0, 11, 12, 23, 24, 25, 26
    };

    private static readonly List<string> PostureObjectAllowList = new()
    {
      "chair", "couch", "bench", "bed", "dining table", "table", "desk",
      "book", "laptop", "cell phone", "keyboard"
    };

    private enum BodyPosture
    {
      Unknown,
      Sitting,
      Standing,
      Squatting,
    }

    private sealed class GpuImageBuildResult
    {
      public global::Mediapipe.Image image;
      public string error;

      public void Reset()
      {
        image = null;
        error = null;
      }
    }

    [Serializable]
    public class FaceProfile
    {
      public string personName;
      public List<float> signature = new();
    }

    [Serializable]
    private class FaceProfileStore
    {
      public List<FaceProfile> profiles = new();
    }

    [Header("MediaPipe Models")]
    public TextAsset poseModel;
    public TextAsset faceModel;
    public TextAsset handModel;
    public TextAsset objectModel;

    [Header("Camera")]
    public RawImage cameraView;
    public UiImage hiddenCameraImage;
    public Toggle showCameraToggle;
    public Dropdown cameraDropdown;
    public int requestedWidth = 640;
    public int requestedHeight = 480;
    public int requestedFps = 30;
    public bool mirrorCameraPreview = true;

    [Header("Debug View")]
    public bool showDebugLandmarks = true;
    public int maxHands = 2;

    [Header("UI")]
    public Text bottomStatusText;
    public Text reportText;
    public Text identityText;
    public InputField enrollNameInput;
    public Button enrollFaceButton;

    [Header("Rules")]
    public float detectionIntervalSeconds = 0f;
    public float debugIntervalSeconds = 5f;
    public float absenceGraceSeconds = 1.5f;
    public float landmarkVisibilityThreshold = 0.45f;
    public float landmarkPresenceThreshold = 0.2f;
    public float blinkThreshold = 0.22f;
    public float blinkOpenThreshold = 0.2f;
    public float blinkMinimumClosedSeconds = 0.0f;
    public float blinkMaximumClosedSeconds = 0.6f;
    public float blinkCooldownSeconds = 0.18f;
    public float identityDistanceThreshold = 0.16f;
    public float postureStableSeconds = 0.8f;
    public float postureUnknownResetSeconds = 1.0f;
    public float headDownPitchThreshold = 8f;
    public float headPitchNeutralDegrees = 0f;
    public float headPitchBaselineSeconds = 1.0f;
    public float headPitchNeutralDeadZone = 3f;
    public bool invertHeadPitch = false;
    public bool invertHeadDownScoreOnAndroid = true;
    public bool useUpperBodyPostureHeuristic = false;
    public float upperBodyBaselineSeconds = 1.2f;
    public float upperBodyStandingYOffset = 0.12f;
    public float upperBodyStandingScaleBoost = 0.18f;
    public bool preferGpuDelegateOnAndroid = true;
    public bool useGpuTextureInputOnAndroid = true;
    public float cameraWarmupBeforeGpuSeconds = 0.35f;
    public bool enableDeskAwarePosture = true;
    public bool deskObjectsOverrideFullBodyPosture = true;
    public bool enableObjectDetectionPostureEvidence = true;
    public bool useOcclusionPostureEvidence = true;
    public float objectDetectionIntervalSeconds = 1.0f;
    public float objectDetectionScoreThreshold = 0.25f;
    public float handDeskEvidenceMinY = 0.52f;

    [Header("Profiles")]
    public List<FaceProfile> registeredFaces = new();

    private WebCamTexture _webCamTexture;
    private TextureFramePool _textureFramePool;
    private PoseLandmarker _poseLandmarker;
    private FaceLandmarker _faceLandmarker;
    private HandLandmarker _handLandmarker;
    private ObjectDetector _objectDetector;
    private PoseLandmarkerResult _poseResult;
    private FaceLandmarkerResult _faceResult;
    private HandLandmarkerResult _handResult;
    private ObjectDetectionResult _objectResult;
    private Coroutine _runCoroutine;
    private Coroutine _cameraRestartCoroutine;
    private bool _isMediapipeInitialized;
    private int _currentCameraIndex;
    private bool _cameraSwitchPending;
    private int _pendingCameraIndex;
    private Button _cameraPickerButton;
    private Text _cameraPickerLabel;
    private RectTransform _cameraPickerPanel;
    private RectTransform _cameraPickerContent;
    private StudyMonitorLandmarkOverlay _landmarkOverlay;
    private readonly List<IReadOnlyList<TaskNormalizedLandmark>> _handLandmarkLists = new();
    private int _lastLayoutScreenWidth = -1;
    private int _lastLayoutScreenHeight = -1;
    private Rect _lastLayoutSafeArea;
    private bool _lastLayoutPortrait;

    private bool _personInFrame;
    private bool _faceInFrame;
    private bool _poseInFrame;
    private bool _headDown;
    private bool _likelyReading;
    private bool _eyesClosed;
    private bool _eyesOpenObserved;
    private int _blinkCount;
    private int _standUpCount;
    private float _lastSeenTime = -999f;
    private float _lastBlinkTime = -999f;
    private float _eyesClosedSince = -1f;
    private float _lastBlinkScore = -1f;
    private float _headPitchDegrees;
    private float _headDownPitchScore;
    private float _headPitchBaselineDegrees;
    private float _lastDebugTime = -999f;
    private bool _hasHeadPitch;
    private bool _headPitchBaselineReady;
    private float _headPitchBaselineStartTime = -1f;
    private float _headPitchBaselineSum;
    private int _headPitchBaselineSampleCount;
    private string _postureBasis = "无";
    private string _sceneObjectContext = "无";
    private bool _deskLikeObjectInFrame;
    private bool _seatLikeObjectInFrame;
    private bool _readingObjectInFrame;
    private float _lastObjectDetectionTime = -999f;
    private BaseOptions.Delegate _activeDelegate = BaseOptions.Delegate.CPU;
    private string _inferenceStatusDetail = "未初始化";
    private bool _upperBodyBaselineReady;
    private float _upperBodyBaselineStartTime = -1f;
    private float _upperBodyBaselineYSum;
    private float _upperBodyBaselineScaleSum;
    private int _upperBodyBaselineSampleCount;
    private float _upperBodyBaselineY;
    private float _upperBodyBaselineScale;
    private float _upperBodyMovedUp;
    private float _upperBodyScaleBoost;
    private BodyPosture _stablePosture = BodyPosture.Unknown;
    private BodyPosture _candidatePosture = BodyPosture.Unknown;
    private float _candidatePostureSince;
    private float _unknownPostureSince = -1f;
    private string _currentIdentity = "未知";
    private string _lastError;
    private string _lastInputTransformDetail = "无";
    private long _processedFrameCount;
    private int _textureFrameMissCount;
    private bool _lastPoseDetected;
    private bool _lastFaceDetected;
    private bool _lastHandDetected;
    private int _lastPoseLandmarkCount;
    private int _lastFaceLandmarkCount;
    private int _lastHandCount;

    private void Awake()
    {
      LoadFaceProfiles();
      ApplyCameraVisibility();

      BuildCameraPickerUi();
      RefreshCameraPickerList(false);
      ApplyResponsiveLayout(true);

      if (showCameraToggle != null)
      {
        showCameraToggle.onValueChanged.AddListener(_ => ApplyCameraVisibility());
      }

      if (enrollFaceButton != null)
      {
        enrollFaceButton.onClick.AddListener(EnrollCurrentFace);
      }

      EnsureLandmarkOverlay();
    }

    private void OnEnable()
    {
      _runCoroutine = StartCoroutine(Run());
    }

    private void OnDisable()
    {
      if (_cameraRestartCoroutine != null)
      {
        StopCoroutine(_cameraRestartCoroutine);
        _cameraRestartCoroutine = null;
      }

      if (_runCoroutine != null)
      {
        StopCoroutine(_runCoroutine);
        _runCoroutine = null;
      }

      DisposeMediapipe();
      StopCamera();
    }

    private void Update()
    {
      ApplyResponsiveLayout(false);
      UpdateCameraPreviewTransform();
    }

    private IEnumerator Run()
    {
      SetStatus("初始化相机...");

      _inferenceStatusDetail = $"图形后端：{SystemInfo.graphicsDeviceType}";

      yield return StartCamera();
      if (_webCamTexture == null || !_webCamTexture.isPlaying)
      {
        SetError("没有可用摄像头，无法开始检测。");
        yield break;
      }

      if (cameraView != null)
      {
        cameraView.texture = _webCamTexture;
      }
      UpdateCameraPreviewTransform();
      ApplyCameraVisibility();

      if (cameraWarmupBeforeGpuSeconds > 0f)
      {
        yield return new WaitForSeconds(cameraWarmupBeforeGpuSeconds);
      }

      if (ShouldPreferGpuDelegate())
      {
        SetStatus("相机已启动，正在初始化 GPU...");
        yield return global::Mediapipe.Unity.GpuManager.Initialize();
        _inferenceStatusDetail = global::Mediapipe.Unity.GpuManager.IsInitialized
          ? $"GPU 初始化成功，图形后端：{SystemInfo.graphicsDeviceType}"
          : $"GPU 初始化失败，图形后端：{SystemInfo.graphicsDeviceType}";
      }
      else if (preferGpuDelegateOnAndroid)
      {
        _inferenceStatusDetail = $"GPU 未启用：当前图形后端为 {SystemInfo.graphicsDeviceType}";
      }

      if (!InitializeMediapipeTasks())
      {
        yield break;
      }

      _textureFramePool = new TextureFramePool(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGBA32, 8);

      _poseResult = PoseLandmarkerResult.Alloc(1, false);
      _faceResult = FaceLandmarkerResult.Alloc(1, true, true);
      _handResult = HandLandmarkerResult.Alloc(Mathf.Max(1, maxHands));
      _objectResult = ObjectDetectionResult.Alloc(8);
      ResetUpperBodyBaseline();
      ResetHeadPitchBaseline();

      SetStatus("检测已启动，等待人物进入画面...");

      var waitForEndOfFrame = new WaitForEndOfFrame();
      var useGpuImageInput = CanUseGpuTextureInput();
      var glContext = useGpuImageInput ? global::Mediapipe.Unity.GpuManager.GetGlContext() : null;

      try
      {
        while (enabled)
        {
          if (_cameraSwitchPending)
          {
            yield return SwitchCamera(_pendingCameraIndex);
            if (!RecreateActiveMediapipeTasks())
            {
              yield break;
            }

            if (useGpuImageInput)
            {
              glContext?.Dispose();
              glContext = global::Mediapipe.Unity.GpuManager.GetGlContext();
              _inferenceStatusDetail = $"GPU 任务运行中，图像输入：GPU纹理输入，相机切换后已重建任务";
            }

            ResetUpperBodyBaseline();
            ResetHeadPitchBaseline();
            UpdateStatusUi();
          }

          if (_webCamTexture.width <= 16)
          {
            yield return null;
            continue;
          }

          var imageProcessingOptions = CreateImageProcessingOptions(out var flipHorizontally, out var flipVertically);
          if (useGpuImageInput)
          {
            var timestampMillisGpu = GetTimestampMillis();
            var gpuImageBuild = new GpuImageBuildResult();
            var poseDetectedGpu = false;
            var faceDetectedGpu = false;
            var handDetectedGpu = false;
            var gpuFrameIssue = string.Empty;

            yield return BuildGpuInputImage(glContext, flipHorizontally, flipVertically, waitForEndOfFrame, gpuImageBuild);
            if (gpuImageBuild.image == null)
            {
              gpuFrameIssue = $"Pose输入失败：{gpuImageBuild.error}";
            }
            else
            {
              try
              {
                poseDetectedGpu = _poseLandmarker.TryDetectForVideo(
                  gpuImageBuild.image,
                  timestampMillisGpu++,
                  imageProcessingOptions,
                  ref _poseResult);
              }
              catch (Exception exception)
              {
                Debug.LogWarning($"[StudyMonitor] GPU pose inference failed and will retry on GPU: {exception.Message}");
                gpuFrameIssue = $"Pose推理异常：{exception.Message}";
              }
              finally
              {
                gpuImageBuild.image?.Dispose();
              }
            }

            yield return BuildGpuInputImage(glContext, flipHorizontally, flipVertically, waitForEndOfFrame, gpuImageBuild);
            if (gpuImageBuild.image == null)
            {
              gpuFrameIssue = AppendFrameIssue(gpuFrameIssue, $"Face输入失败：{gpuImageBuild.error}");
            }
            else
            {
              try
              {
                faceDetectedGpu = _faceLandmarker.TryDetectForVideo(
                  gpuImageBuild.image,
                  timestampMillisGpu++,
                  imageProcessingOptions,
                  ref _faceResult);
              }
              catch (Exception exception)
              {
                Debug.LogWarning($"[StudyMonitor] GPU face inference failed and will retry on GPU: {exception.Message}");
                gpuFrameIssue = AppendFrameIssue(gpuFrameIssue, $"Face推理异常：{exception.Message}");
              }
              finally
              {
                gpuImageBuild.image?.Dispose();
              }
            }

            if (_handLandmarker != null)
            {
              yield return BuildGpuInputImage(glContext, flipHorizontally, flipVertically, waitForEndOfFrame, gpuImageBuild);
              if (gpuImageBuild.image == null)
              {
                gpuFrameIssue = AppendFrameIssue(gpuFrameIssue, $"Hand输入失败：{gpuImageBuild.error}");
              }
              else
              {
                try
                {
                  handDetectedGpu = _handLandmarker.TryDetectForVideo(
                    gpuImageBuild.image,
                    timestampMillisGpu++,
                    imageProcessingOptions,
                    ref _handResult);
                }
                catch (Exception exception)
                {
                  Debug.LogWarning($"[StudyMonitor] GPU hand inference failed and will retry on GPU: {exception.Message}");
                  gpuFrameIssue = AppendFrameIssue(gpuFrameIssue, $"Hand推理异常：{exception.Message}");
                }
                finally
                {
                  gpuImageBuild.image?.Dispose();
                }
              }
            }

            if (ShouldRunObjectDetection())
            {
              yield return BuildGpuInputImage(glContext, flipHorizontally, flipVertically, waitForEndOfFrame, gpuImageBuild);
              if (gpuImageBuild.image == null)
              {
                gpuFrameIssue = AppendFrameIssue(gpuFrameIssue, $"Object输入失败：{gpuImageBuild.error}");
              }
              else
              {
                try
                {
                  var objectDetected = _objectDetector.TryDetectForVideo(
                    gpuImageBuild.image,
                    timestampMillisGpu++,
                    imageProcessingOptions,
                    ref _objectResult);
                  UpdateSceneObjectContext(objectDetected ? _objectResult : default);
                }
                catch (Exception exception)
                {
                  Debug.LogWarning($"[StudyMonitor] Object detection failed and was disabled: {exception.Message}");
                  gpuFrameIssue = AppendFrameIssue(gpuFrameIssue, $"Object推理异常，已关闭物体检测：{exception.Message}");
                  _objectDetector?.Close();
                  _objectDetector = null;
                  ResetSceneObjectContext();
                }
                finally
                {
                  gpuImageBuild.image?.Dispose();
                }
              }
            }

            AnalyzeResults(poseDetectedGpu, faceDetectedGpu, handDetectedGpu);
            _inferenceStatusDetail = string.IsNullOrEmpty(gpuFrameIssue)
              ? "GPU 任务运行中，图像输入：逐任务GPU纹理输入"
              : $"GPU 任务运行中，{gpuFrameIssue}";
            UpdateStatusUi();
            DebugPoseEveryInterval();

            yield return WaitForNextDetection();
            continue;
          }

          if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
          {
            _textureFrameMissCount++;
            _inferenceStatusDetail = $"{_activeDelegate} 任务运行中，帧池暂时无空闲：{_textureFrameMissCount}";
            UpdateStatusUi();
            yield return waitForEndOfFrame;
            continue;
          }

          var request = textureFrame.ReadTextureAsync(_webCamTexture, flipHorizontally, flipVertically);
          yield return new WaitUntil(() => request.done);

          if (request.hasError)
          {
            textureFrame.Release();
            Debug.LogWarning("[StudyMonitor] 摄像头画面读取失败，跳过本帧。");
            yield return WaitForNextDetection();
            continue;
          }

          var timestampMillis = GetTimestampMillis();
          var poseImage = textureFrame.BuildCPUImage();
          var poseDetected = _poseLandmarker.TryDetectForVideo(poseImage, timestampMillis, imageProcessingOptions, ref _poseResult);

          var faceImage = textureFrame.BuildCPUImage();
          var faceDetected = _faceLandmarker.TryDetectForVideo(faceImage, timestampMillis, imageProcessingOptions, ref _faceResult);

          var handDetected = false;
          if (_handLandmarker != null)
          {
            var handImage = textureFrame.BuildCPUImage();
            handDetected = _handLandmarker.TryDetectForVideo(handImage, timestampMillis, imageProcessingOptions, ref _handResult);
          }

          if (ShouldRunObjectDetection())
          {
            try
            {
              var objectImage = textureFrame.BuildCPUImage();
              var objectDetected = _objectDetector.TryDetectForVideo(
                objectImage,
                timestampMillis,
                imageProcessingOptions,
                ref _objectResult);
              UpdateSceneObjectContext(objectDetected ? _objectResult : default);
            }
            catch (Exception exception)
            {
              Debug.LogWarning($"[StudyMonitor] Object detection failed and was disabled: {exception.Message}");
              _objectDetector?.Close();
              _objectDetector = null;
              ResetSceneObjectContext();
            }
          }

          textureFrame.Release();

          AnalyzeResults(poseDetected, faceDetected, handDetected);
          UpdateStatusUi();
          DebugPoseEveryInterval();

          yield return WaitForNextDetection();
        }
      }
      finally
      {
        glContext?.Dispose();
      }
    }

    private IEnumerator WaitForNextDetection()
    {
      if (detectionIntervalSeconds > 0f)
      {
        yield return new WaitForSeconds(detectionIntervalSeconds);
      }
      else
      {
        yield return null;
      }
    }

    private IEnumerator BuildGpuInputImage(
      global::Mediapipe.GlContext glContext,
      bool flipHorizontally,
      bool flipVertically,
      WaitForEndOfFrame waitForEndOfFrame,
      GpuImageBuildResult result)
    {
      result.Reset();

      if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
      {
        _textureFrameMissCount++;
        result.error = $"TextureFramePool 暂时没有空闲帧：{_textureFrameMissCount}";
        yield return waitForEndOfFrame;
        yield break;
      }

      try
      {
        textureFrame.ReadTextureOnGPU(_webCamTexture, flipHorizontally, flipVertically);
        result.image = textureFrame.BuildGPUImage(glContext);
      }
      catch (Exception exception)
      {
        textureFrame.Release();
        result.error = $"{exception.GetType().Name}: {exception.Message}";
        Debug.LogWarning($"[StudyMonitor] GPU image build failed and will retry on GPU: {result.error}");
        yield break;
      }

      yield return waitForEndOfFrame;
    }

    private static string AppendFrameIssue(string current, string next)
    {
      return string.IsNullOrEmpty(current) ? next : $"{current}；{next}";
    }

    private bool InitializeMediapipeTasks()
    {
      if (poseModel == null)
      {
        SetError("Pose 模型没有绑定，请检查场景中的 StudyMonitorMediapipeController。");
        return false;
      }

      if (faceModel == null)
      {
        SetError("Face 模型没有绑定，请检查场景中的 StudyMonitorMediapipeController。");
        return false;
      }

      if (handModel == null)
      {
        Debug.LogWarning("[StudyMonitor] Hand model is not assigned; hand/finger landmarks will be disabled.");
      }

      if (enableObjectDetectionPostureEvidence && objectModel == null)
      {
        Debug.LogWarning("[StudyMonitor] Object model is not assigned; desk-aware posture evidence will be disabled.");
      }

      try
      {
        if (!_isMediapipeInitialized)
        {
          global::Mediapipe.Protobuf.SetLogHandler(global::Mediapipe.Protobuf.DefaultLogHandler);
          global::Mediapipe.Glog.Initialize("SuperviseSoftStudyMonitor");
          _isMediapipeInitialized = true;
        }

        var preferredDelegate = ShouldPreferGpuDelegate() &&
                                global::Mediapipe.Unity.GpuManager.IsInitialized
          ? BaseOptions.Delegate.GPU
          : BaseOptions.Delegate.CPU;

        if (TryCreateMediapipeTasks(preferredDelegate, out var creationError))
        {
          _activeDelegate = preferredDelegate;
          if (preferredDelegate == BaseOptions.Delegate.GPU)
          {
            var inputMode = CanUseGpuTextureInput() ? "GPU纹理输入" : "CPU相机帧上传";
            _inferenceStatusDetail = $"GPU 任务创建成功，图形后端：{SystemInfo.graphicsDeviceType}，图像输入：{inputMode}";
          }
          return true;
        }

        if (preferredDelegate != BaseOptions.Delegate.CPU)
        {
          Debug.LogWarning($"[StudyMonitor] GPU delegate failed, falling back to CPU: {creationError}");
          _inferenceStatusDetail = $"GPU 任务创建失败，已回退 CPU：{creationError}";
          DisposeTaskApis();
          if (TryCreateMediapipeTasks(BaseOptions.Delegate.CPU, out creationError))
          {
            _activeDelegate = BaseOptions.Delegate.CPU;
            return true;
          }
        }

        SetError($"MediaPipe 初始化失败：{creationError}");
        return false;
      }
      catch (Exception exception)
      {
        SetError($"MediaPipe 初始化失败：{exception.Message}");
        Debug.LogException(exception);
        return false;
      }
    }

    private bool TryCreateMediapipeTasks(BaseOptions.Delegate delegateCase, out string error)
    {
      error = null;
      var runningMode = RunningMode.VIDEO;
      var gpuResources = delegateCase == BaseOptions.Delegate.GPU
        ? global::Mediapipe.Unity.GpuManager.GpuResources
        : null;

      try
      {
        var poseOptions = new PoseLandmarkerOptions(
          new BaseOptions(delegateCase, modelAssetBuffer: poseModel.bytes),
          runningMode: runningMode,
          numPoses: 1,
          minPoseDetectionConfidence: 0.5f,
          minPosePresenceConfidence: 0.5f,
          minTrackingConfidence: 0.5f,
          outputSegmentationMasks: false);

        var faceOptions = new FaceLandmarkerOptions(
          new BaseOptions(delegateCase, modelAssetBuffer: faceModel.bytes),
          runningMode: runningMode,
          numFaces: 1,
          minFaceDetectionConfidence: 0.5f,
          minFacePresenceConfidence: 0.5f,
          minTrackingConfidence: 0.5f,
          outputFaceBlendshapes: true,
          outputFaceTransformationMatrixes: true);

        HandLandmarkerOptions handOptions = null;
        if (handModel != null)
        {
          handOptions = new HandLandmarkerOptions(
            new BaseOptions(delegateCase, modelAssetBuffer: handModel.bytes),
            runningMode: runningMode,
            numHands: Mathf.Max(1, maxHands),
            minHandDetectionConfidence: 0.5f,
            minHandPresenceConfidence: 0.5f,
            minTrackingConfidence: 0.5f);
        }

        ObjectDetectorOptions objectOptions = null;
        if (enableObjectDetectionPostureEvidence && objectModel != null)
        {
          objectOptions = new ObjectDetectorOptions(
            new BaseOptions(delegateCase, modelAssetBuffer: objectModel.bytes),
            runningMode: runningMode,
            maxResults: 8,
            scoreThreshold: objectDetectionScoreThreshold,
            categoryAllowList: PostureObjectAllowList);
        }

        _poseLandmarker = PoseLandmarker.CreateFromOptions(poseOptions, gpuResources);
        _faceLandmarker = FaceLandmarker.CreateFromOptions(faceOptions, gpuResources);
        _handLandmarker = handOptions == null ? null : HandLandmarker.CreateFromOptions(handOptions, gpuResources);
        _objectDetector = null;
        if (objectOptions != null)
        {
          try
          {
            _objectDetector = ObjectDetector.CreateFromOptions(objectOptions, gpuResources);
          }
          catch (Exception exception)
          {
            Debug.LogWarning($"[StudyMonitor] Object detector initialization failed; posture will use landmark/occlusion evidence only. {exception.Message}");
            ResetSceneObjectContext();
          }
        }

        return true;
      }
      catch (Exception exception)
      {
        error = exception.Message;
        Debug.LogException(exception);
        DisposeTaskApis();
        return false;
      }
    }

    private bool RecreateActiveMediapipeTasks()
    {
      var activeDelegate = _activeDelegate;
      DisposeTaskApis();

      if (!TryCreateMediapipeTasks(activeDelegate, out var error))
      {
        SetError($"相机切换后重建 MediaPipe 任务失败：{error}");
        return false;
      }

      _activeDelegate = activeDelegate;
      if (_activeDelegate == BaseOptions.Delegate.GPU)
      {
        _inferenceStatusDetail = $"GPU 任务已随相机切换重建，图形后端：{SystemInfo.graphicsDeviceType}，图像输入：GPU纹理输入";
      }
      else
      {
        _inferenceStatusDetail = $"CPU 任务已随相机切换重建";
      }

      _poseResult = PoseLandmarkerResult.Alloc(1, false);
      _faceResult = FaceLandmarkerResult.Alloc(1, true, true);
      _handResult = HandLandmarkerResult.Alloc(Mathf.Max(1, maxHands));
      _objectResult = ObjectDetectionResult.Alloc(8);
      return true;
    }

    private bool ShouldPreferGpuDelegate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      return preferGpuDelegateOnAndroid;
#else
      return false;
#endif
    }

    private bool CanUseGpuTextureInput()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      return useGpuTextureInputOnAndroid &&
             _activeDelegate == BaseOptions.Delegate.GPU &&
             SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 &&
             global::Mediapipe.Unity.GpuManager.GpuResources != null;
#else
      return false;
#endif
    }

    private IEnumerator StartCamera()
    {
      var devices = WebCamTexture.devices;
      if (devices.Length == 0)
      {
        RefreshCameraPickerList(false);
        yield break;
      }

      _currentCameraIndex = Mathf.Clamp(_currentCameraIndex, 0, devices.Length - 1);

      var device = devices[_currentCameraIndex];
      GetRequestedCameraSize(out var cameraWidth, out var cameraHeight);
      _webCamTexture = new WebCamTexture(device.name, cameraWidth, cameraHeight, requestedFps);
      _webCamTexture.Play();

      yield return new WaitUntil(() => _webCamTexture.width > 16);

      mirrorCameraPreview = device.isFrontFacing;
      UpdateCameraPickerLabel(devices);
      RefreshCameraPickerList(false);

      if (cameraView != null)
      {
        cameraView.texture = _webCamTexture;
      }
    }

    private void BuildCameraPickerUi()
    {
      if (_cameraPickerButton != null)
      {
        return;
      }

      RectTransform sourceRect = null;
      Transform parent = null;
      if (cameraDropdown != null)
      {
        sourceRect = cameraDropdown.GetComponent<RectTransform>();
        parent = cameraDropdown.transform.parent;
        cameraDropdown.gameObject.SetActive(false);
      }
      else if (showCameraToggle != null)
      {
        parent = showCameraToggle.transform.parent;
      }

      if (parent == null)
      {
        return;
      }

      parent.SetAsLastSibling();

      var font = ResolveUiFont();
      var root = new GameObject("Runtime Camera Picker", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiImage), typeof(Button));
      root.transform.SetParent(parent, false);
      root.transform.SetAsLastSibling();

      var rootRect = root.GetComponent<RectTransform>();
      if (sourceRect != null)
      {
        CopyRectTransform(sourceRect, rootRect);
      }
      else
      {
        Stretch(rootRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(208f, 6f), new Vector2(440f, -6f));
      }

      var image = root.GetComponent<UiImage>();
      image.color = new Color(0.18f, 0.22f, 0.26f, 1f);

      _cameraPickerButton = root.GetComponent<Button>();
      _cameraPickerButton.targetGraphic = image;
      _cameraPickerButton.onClick.AddListener(ToggleCameraPickerPanel);

      _cameraPickerLabel = CreateRuntimeText("Label", root.transform, font, "刷新相机列表", 17, TextAnchor.MiddleLeft, Color.white);
      Stretch(_cameraPickerLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-30f, -2f));

      var arrow = CreateRuntimeText("Arrow", root.transform, font, "▼", 12, TextAnchor.MiddleCenter, Color.white);
      Stretch(arrow.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-24f, 2f), new Vector2(-6f, -2f));

      _cameraPickerPanel = CreateCameraPickerPanel(root.transform);
      _cameraPickerPanel.gameObject.SetActive(false);
    }

    private RectTransform CreateCameraPickerPanel(Transform parent)
    {
      var panel = new GameObject("Camera List Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiImage), typeof(ScrollRect));
      panel.transform.SetParent(parent, false);

      var panelRect = panel.GetComponent<RectTransform>();
      panelRect.anchorMin = new Vector2(0f, 0f);
      panelRect.anchorMax = new Vector2(1f, 0f);
      panelRect.pivot = new Vector2(0.5f, 1f);
      panelRect.anchoredPosition = new Vector2(0f, -4f);
      panelRect.sizeDelta = new Vector2(0f, 300f);

      panel.GetComponent<UiImage>().color = new Color(0.08f, 0.1f, 0.12f, 0.98f);

      var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiImage), typeof(Mask));
      viewport.transform.SetParent(panel.transform, false);
      var viewportRect = viewport.GetComponent<RectTransform>();
      Stretch(viewportRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      viewport.GetComponent<UiImage>().color = Color.white;
      viewport.GetComponent<Mask>().showMaskGraphic = false;

      var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
      content.transform.SetParent(viewport.transform, false);
      _cameraPickerContent = content.GetComponent<RectTransform>();
      _cameraPickerContent.anchorMin = new Vector2(0f, 1f);
      _cameraPickerContent.anchorMax = new Vector2(1f, 1f);
      _cameraPickerContent.pivot = new Vector2(0.5f, 1f);
      _cameraPickerContent.sizeDelta = Vector2.zero;

      var layout = content.GetComponent<VerticalLayoutGroup>();
      layout.childControlWidth = true;
      layout.childControlHeight = true;
      layout.childForceExpandWidth = true;
      layout.childForceExpandHeight = false;
      layout.spacing = 2f;
      layout.padding = new RectOffset(4, 4, 4, 4);

      var fitter = content.GetComponent<ContentSizeFitter>();
      fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

      var scrollRect = panel.GetComponent<ScrollRect>();
      scrollRect.viewport = viewportRect;
      scrollRect.content = _cameraPickerContent;
      scrollRect.horizontal = false;
      scrollRect.vertical = true;

      return panelRect;
    }

    private void ToggleCameraPickerPanel()
    {
      if (_cameraPickerPanel == null)
      {
        return;
      }

      var show = !_cameraPickerPanel.gameObject.activeSelf;
      _cameraPickerButton.transform.parent.SetAsLastSibling();
      _cameraPickerButton.transform.SetAsLastSibling();
      RefreshCameraPickerList(show);
      _cameraPickerPanel.gameObject.SetActive(show);
      _cameraPickerPanel.SetAsLastSibling();
    }

    private void RefreshCameraPickerList(bool keepPanelOpen)
    {
      BuildCameraPickerUi();
      if (_cameraPickerContent == null)
      {
        return;
      }

      for (var i = _cameraPickerContent.childCount - 1; i >= 0; i--)
      {
        Destroy(_cameraPickerContent.GetChild(i).gameObject);
      }

      var devices = WebCamTexture.devices;
      if (devices.Length == 0)
      {
        AddCameraPickerRow("未找到相机，点击此处刷新", -1, false);
        SetCameraPickerText("未找到相机");
        _cameraPickerButton.interactable = true;
      }
      else
      {
        _currentCameraIndex = Mathf.Clamp(_currentCameraIndex, 0, devices.Length - 1);
        for (var i = 0; i < devices.Length; i++)
        {
          AddCameraPickerRow(BuildCameraOptionLabel(devices[i], i, i == _currentCameraIndex), i, true);
        }

        UpdateCameraPickerLabel(devices);
      }

      if (_cameraPickerPanel != null)
      {
        _cameraPickerPanel.gameObject.SetActive(keepPanelOpen);
      }
    }

    private void AddCameraPickerRow(string label, int index, bool selectable)
    {
      var font = ResolveUiFont();
      var row = new GameObject($"Camera Option {index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiImage), typeof(Button), typeof(LayoutElement));
      row.transform.SetParent(_cameraPickerContent, false);

      var layoutElement = row.GetComponent<LayoutElement>();
      layoutElement.preferredHeight = 30f;

      var image = row.GetComponent<UiImage>();
      image.color = index == _currentCameraIndex && selectable
        ? new Color(0.16f, 0.46f, 0.75f, 1f)
        : new Color(0.16f, 0.18f, 0.2f, 1f);

      var button = row.GetComponent<Button>();
      button.targetGraphic = image;
      button.interactable = true;
      button.onClick.AddListener(() =>
      {
        if (selectable)
        {
          SelectCamera(index);
        }
        else
        {
          RefreshCameraPickerList(true);
        }
      });

      var text = CreateRuntimeText("Label", row.transform, font, label, 15, TextAnchor.MiddleLeft, Color.white);
      Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
    }

    private void SelectCamera(int index)
    {
      var devices = WebCamTexture.devices;
      if (index < 0 || index >= devices.Length)
      {
        RefreshCameraPickerList(true);
        return;
      }

      _currentCameraIndex = index;
      _pendingCameraIndex = index;
      _cameraSwitchPending = false;
      SetStatus("正在切换相机并重启 GPU 检测...");
      UpdateCameraPickerLabel(devices);
      RefreshCameraPickerList(false);

      if (_cameraRestartCoroutine != null)
      {
        StopCoroutine(_cameraRestartCoroutine);
      }

      _cameraRestartCoroutine = StartCoroutine(RestartDetectionWithCamera(index));
    }

    private IEnumerator RestartDetectionWithCamera(int index)
    {
      var devices = WebCamTexture.devices;
      if (index < 0 || index >= devices.Length)
      {
        SetStatus("切换相机失败：相机列表已变化，请重新选择。");
        RefreshCameraPickerList(false);
        _cameraRestartCoroutine = null;
        yield break;
      }

      _currentCameraIndex = index;
      UpdateCameraPickerLabel(devices);
      RefreshCameraPickerList(false);

      if (_runCoroutine != null)
      {
        StopCoroutine(_runCoroutine);
        _runCoroutine = null;
      }

      DisposeMediapipe();
      StopCamera();
      ClearRuntimeTrackingState();
      SetStatus($"正在打开相机并重启 GPU：{BuildCameraOptionLabel(devices[index], index, false)}");

      yield return null;
      yield return null;

      _runCoroutine = StartCoroutine(Run());
      _cameraRestartCoroutine = null;
    }

    private IEnumerator SwitchCamera(int index)
    {
      _cameraSwitchPending = false;

      var devices = WebCamTexture.devices;
      if (index < 0 || index >= devices.Length)
      {
        SetStatus("切换相机失败：相机列表已变化，请重新选择。");
        RefreshCameraPickerList(false);
        _cameraSwitchPending = false;
        yield break;
      }

      if (_webCamTexture != null && _webCamTexture.isPlaying)
      {
        _webCamTexture.Stop();
      }

      _webCamTexture = null;
      _textureFramePool?.Dispose();
      _textureFramePool = null;

      var device = devices[index];
      SetStatus($"正在打开相机：{BuildCameraOptionLabel(device, index, false)}");
      GetRequestedCameraSize(out var cameraWidth, out var cameraHeight);
      _webCamTexture = new WebCamTexture(device.name, cameraWidth, cameraHeight, requestedFps);
      _webCamTexture.Play();

      var startTime = Time.realtimeSinceStartup;
      while (_webCamTexture != null &&
             _webCamTexture.width <= 16 &&
             Time.realtimeSinceStartup - startTime < 4f)
      {
        yield return null;
      }

      if (_webCamTexture == null || _webCamTexture.width <= 16)
      {
        SetStatus("切换相机失败，请再次点击刷新相机列表。");
        RefreshCameraPickerList(false);
        yield break;
      }

      mirrorCameraPreview = device.isFrontFacing;
      _currentCameraIndex = index;
      UpdateCameraPickerLabel(devices);
      RefreshCameraPickerList(false);

      _textureFramePool = new TextureFramePool(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGBA32, 8);
      ResetUpperBodyBaseline();
      ResetHeadPitchBaseline();

      if (cameraView != null)
      {
        cameraView.texture = _webCamTexture;
      }

      UpdateCameraPreviewTransform();
      ApplyCameraVisibility();
      SetStatus($"相机切换完成：{BuildCameraOptionLabel(device, index, false)}");
      yield return null;
    }

    private static string BuildCameraOptionLabel(WebCamDevice device, int index, bool selected)
    {
      var name = string.IsNullOrEmpty(device.name) ? $"相机 {index + 1}" : device.name;
      var direction = device.isFrontFacing ? "前置" : "后置/外接";
      return $"{(selected ? "✓ " : string.Empty)}{index + 1}. {direction} - {name}";
    }

    private void GetRequestedCameraSize(out int width, out int height)
    {
      var longSide = Mathf.Max(requestedWidth, requestedHeight);
      var shortSide = Mathf.Min(requestedWidth, requestedHeight);
      var isLandscape = Screen.width >= Screen.height;

      width = isLandscape ? longSide : shortSide;
      height = isLandscape ? shortSide : longSide;
    }

    private void UpdateCameraPickerLabel(WebCamDevice[] devices)
    {
      if (devices == null || devices.Length == 0)
      {
        SetCameraPickerText("未找到相机");
        return;
      }

      _currentCameraIndex = Mathf.Clamp(_currentCameraIndex, 0, devices.Length - 1);
      var device = devices[_currentCameraIndex];
      var name = string.IsNullOrEmpty(device.name) ? $"相机 {_currentCameraIndex + 1}" : device.name;
      SetCameraPickerText($"{_currentCameraIndex + 1}/{devices.Length} {name}");
    }

    private void SetCameraPickerText(string text)
    {
      if (_cameraPickerLabel != null)
      {
        _cameraPickerLabel.text = text;
      }
    }

    private Text CreateRuntimeText(string name, Transform parent, Font font, string text, int fontSize, TextAnchor alignment, Color color)
    {
      var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
      gameObject.transform.SetParent(parent, false);

      var textComponent = gameObject.GetComponent<Text>();
      textComponent.font = font;
      textComponent.text = text;
      textComponent.fontSize = fontSize;
      textComponent.alignment = alignment;
      textComponent.color = color;
      textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
      textComponent.verticalOverflow = VerticalWrapMode.Truncate;
      return textComponent;
    }

    private static void CopyRectTransform(RectTransform source, RectTransform target)
    {
      target.anchorMin = source.anchorMin;
      target.anchorMax = source.anchorMax;
      target.pivot = source.pivot;
      target.anchoredPosition = source.anchoredPosition;
      target.sizeDelta = source.sizeDelta;
      target.offsetMin = source.offsetMin;
      target.offsetMax = source.offsetMax;
      target.localScale = source.localScale;
      target.localRotation = source.localRotation;
    }

    private static void Stretch(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
      if (rectTransform == null)
      {
        return;
      }

      rectTransform.anchorMin = anchorMin;
      rectTransform.anchorMax = anchorMax;
      rectTransform.offsetMin = offsetMin;
      rectTransform.offsetMax = offsetMax;
    }

    private void ApplyResponsiveLayout(bool force)
    {
      var screenWidth = Screen.width;
      var screenHeight = Screen.height;
      var safeArea = Screen.safeArea;
      var isPortrait = screenHeight > screenWidth;

      if (!force &&
          _lastLayoutScreenWidth == screenWidth &&
          _lastLayoutScreenHeight == screenHeight &&
          _lastLayoutSafeArea == safeArea &&
          _lastLayoutPortrait == isPortrait)
      {
        return;
      }

      var canvas = ResolveCanvas();
      if (canvas == null)
      {
        return;
      }

      var canvasRect = canvas.GetComponent<RectTransform>();
      if (canvasRect == null)
      {
        return;
      }

      var scaler = canvas.GetComponent<CanvasScaler>();
      if (scaler != null)
      {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = isPortrait ? 0f : 0.5f;
      }

      Canvas.ForceUpdateCanvases();
      var canvasSize = canvasRect.rect.size;
      if (canvasSize.x <= 1f || canvasSize.y <= 1f || screenWidth <= 0 || screenHeight <= 0)
      {
        return;
      }

      GetSafeAreaInsets(safeArea, canvasSize, screenWidth, screenHeight,
        out var safeLeft, out var safeRight, out var safeBottom, out var safeTop);

      var titleRect = canvas.transform.Find("Title") as RectTransform;
      var toolbarRect = enrollFaceButton != null ? enrollFaceButton.transform.parent as RectTransform : null;
      var previewRect = cameraView != null ? cameraView.rectTransform.parent as RectTransform : null;
      var reportPanelRect = reportText != null ? reportText.transform.parent as RectTransform : null;
      var bottomBarRect = bottomStatusText != null ? bottomStatusText.transform.parent as RectTransform : null;

      if (titleRect != null)
      {
        SetTopBand(titleRect, safeTop + 16f, safeLeft + 24f, safeRight + 24f, isPortrait ? 44f : 52f);
      }

      if (isPortrait)
      {
        ApplyPortraitLayout(canvasSize, safeLeft, safeRight, safeBottom, safeTop, toolbarRect, previewRect, reportPanelRect, bottomBarRect);
      }
      else
      {
        ApplyLandscapeLayout(safeLeft, safeRight, safeBottom, safeTop, toolbarRect, previewRect, reportPanelRect, bottomBarRect);
      }

      _lastLayoutScreenWidth = screenWidth;
      _lastLayoutScreenHeight = screenHeight;
      _lastLayoutSafeArea = safeArea;
      _lastLayoutPortrait = isPortrait;

      UpdateCameraPreviewTransform();
    }

    private Canvas ResolveCanvas()
    {
      if (cameraView != null && cameraView.canvas != null)
      {
        return cameraView.canvas;
      }

      if (bottomStatusText != null && bottomStatusText.canvas != null)
      {
        return bottomStatusText.canvas;
      }

      if (reportText != null && reportText.canvas != null)
      {
        return reportText.canvas;
      }

      return null;
    }

    private static void GetSafeAreaInsets(Rect safeArea, Vector2 canvasSize, int screenWidth, int screenHeight,
      out float left, out float right, out float bottom, out float top)
    {
      left = Mathf.Max(0f, safeArea.xMin / screenWidth * canvasSize.x);
      right = Mathf.Max(0f, (screenWidth - safeArea.xMax) / screenWidth * canvasSize.x);
      bottom = Mathf.Max(0f, safeArea.yMin / screenHeight * canvasSize.y);
      top = Mathf.Max(0f, (screenHeight - safeArea.yMax) / screenHeight * canvasSize.y);
    }

    private void ApplyLandscapeLayout(float safeLeft, float safeRight, float safeBottom, float safeTop,
      RectTransform toolbarRect, RectTransform previewRect, RectTransform reportPanelRect, RectTransform bottomBarRect)
    {
      SetTopBand(toolbarRect, safeTop + 76f, safeLeft + 20f, safeRight + 20f, 48f);
      SetBottomBand(bottomBarRect, safeBottom, safeLeft, safeRight, 70f);

      var contentTop = safeTop + 144f;
      var contentBottom = safeBottom + 84f;
      Stretch(previewRect, new Vector2(0f, 0f), new Vector2(0.72f, 1f),
        new Vector2(safeLeft + 20f, contentBottom), new Vector2(-16f, -contentTop));
      Stretch(reportPanelRect, new Vector2(0.72f, 0f), Vector2.one,
        new Vector2(0f, contentBottom), new Vector2(-(safeRight + 20f), -contentTop));

      LayoutLandscapeToolbar(toolbarRect);
    }

    private void ApplyPortraitLayout(Vector2 canvasSize, float safeLeft, float safeRight, float safeBottom, float safeTop,
      RectTransform toolbarRect, RectTransform previewRect, RectTransform reportPanelRect, RectTransform bottomBarRect)
    {
      const float margin = 12f;
      const float toolbarHeight = 150f;
      const float bottomHeight = 78f;

      SetTopBand(toolbarRect, safeTop + 72f, safeLeft + margin, safeRight + margin, toolbarHeight);
      SetBottomBand(bottomBarRect, safeBottom, safeLeft, safeRight, bottomHeight);
      LayoutPortraitToolbar(toolbarRect);

      var contentTop = safeTop + 72f + toolbarHeight + margin;
      var contentBottom = safeBottom + bottomHeight + margin;
      var availableHeight = Mathf.Max(240f, canvasSize.y - contentTop - contentBottom);
      var previewHeight = Mathf.Clamp(availableHeight * 0.55f, 220f, availableHeight - 110f);

      SetTopBand(previewRect, contentTop, safeLeft + margin, safeRight + margin, previewHeight);
      Stretch(reportPanelRect, Vector2.zero, Vector2.one,
        new Vector2(safeLeft + margin, contentBottom),
        new Vector2(-(safeRight + margin), -(contentTop + previewHeight + margin)));
    }

    private void LayoutLandscapeToolbar(RectTransform toolbarRect)
    {
      if (toolbarRect == null)
      {
        return;
      }

      Canvas.ForceUpdateCanvases();
      var width = toolbarRect.rect.width;
      if (width < 900f)
      {
        LayoutPortraitToolbar(toolbarRect);
        return;
      }

      Stretch(showCameraToggle != null ? showCameraToggle.GetComponent<RectTransform>() : null,
        new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 8f), new Vector2(188f, -8f));
      Stretch(GetCameraPickerRect(),
        new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(208f, 6f), new Vector2(440f, -6f));
      Stretch(enrollNameInput != null ? enrollNameInput.GetComponent<RectTransform>() : null,
        new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(460f, 8f), new Vector2(640f, -8f));
      Stretch(enrollFaceButton != null ? enrollFaceButton.GetComponent<RectTransform>() : null,
        new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(660f, 8f), new Vector2(804f, -8f));
      Stretch(identityText != null ? identityText.rectTransform : null,
        new Vector2(0f, 0f), Vector2.one, new Vector2(824f, 8f), new Vector2(-16f, -8f));
    }

    private void LayoutPortraitToolbar(RectTransform toolbarRect)
    {
      if (toolbarRect == null)
      {
        return;
      }

      Canvas.ForceUpdateCanvases();
      var width = Mathf.Max(320f, toolbarRect.rect.width);
      const float gap = 8f;
      const float left = 12f;
      var contentWidth = Mathf.Max(220f, width - left * 2f);
      var halfWidth = (contentWidth - gap) * 0.5f;
      var buttonWidth = Mathf.Clamp(contentWidth * 0.38f, 128f, 180f);
      var inputWidth = Mathf.Max(120f, contentWidth - buttonWidth - gap);

      SetTopLeftRect(showCameraToggle != null ? showCameraToggle.GetComponent<RectTransform>() : null, left, 10f, halfWidth, 34f);
      SetTopLeftRect(GetCameraPickerRect(), left + halfWidth + gap, 8f, halfWidth, 38f);
      SetTopLeftRect(enrollNameInput != null ? enrollNameInput.GetComponent<RectTransform>() : null, left, 54f, inputWidth, 38f);
      SetTopLeftRect(enrollFaceButton != null ? enrollFaceButton.GetComponent<RectTransform>() : null,
        left + inputWidth + gap, 54f, buttonWidth, 38f);
      SetTopLeftRect(identityText != null ? identityText.rectTransform : null, left, 102f, contentWidth, 38f);
    }

    private RectTransform GetCameraPickerRect()
    {
      if (_cameraPickerButton != null)
      {
        return _cameraPickerButton.GetComponent<RectTransform>();
      }

      return cameraDropdown != null ? cameraDropdown.GetComponent<RectTransform>() : null;
    }

    private static void SetTopBand(RectTransform rectTransform, float top, float left, float right, float height)
    {
      if (rectTransform == null)
      {
        return;
      }

      rectTransform.anchorMin = new Vector2(0f, 1f);
      rectTransform.anchorMax = new Vector2(1f, 1f);
      rectTransform.pivot = new Vector2(0.5f, 1f);
      rectTransform.offsetMin = new Vector2(left, -(top + height));
      rectTransform.offsetMax = new Vector2(-right, -top);
    }

    private static void SetBottomBand(RectTransform rectTransform, float bottom, float left, float right, float height)
    {
      if (rectTransform == null)
      {
        return;
      }

      rectTransform.anchorMin = new Vector2(0f, 0f);
      rectTransform.anchorMax = new Vector2(1f, 0f);
      rectTransform.pivot = new Vector2(0.5f, 0f);
      rectTransform.offsetMin = new Vector2(left, bottom);
      rectTransform.offsetMax = new Vector2(-right, bottom + height);
    }

    private static void SetTopLeftRect(RectTransform rectTransform, float left, float top, float width, float height)
    {
      if (rectTransform == null)
      {
        return;
      }

      rectTransform.anchorMin = new Vector2(0f, 1f);
      rectTransform.anchorMax = new Vector2(0f, 1f);
      rectTransform.pivot = new Vector2(0f, 1f);
      rectTransform.anchoredPosition = new Vector2(left, -top);
      rectTransform.sizeDelta = new Vector2(width, height);
    }

    private void EnsureLandmarkOverlay()
    {
      if (!showDebugLandmarks || cameraView == null)
      {
        return;
      }

      if (_landmarkOverlay != null)
      {
        return;
      }

      var overlayObject = new GameObject("Debug Landmarks Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(StudyMonitorLandmarkOverlay));
      overlayObject.transform.SetParent(cameraView.transform, false);

      var overlayRect = overlayObject.GetComponent<RectTransform>();
      Stretch(overlayRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
      overlayRect.localScale = Vector3.one;
      overlayRect.localRotation = Quaternion.identity;

      _landmarkOverlay = overlayObject.GetComponent<StudyMonitorLandmarkOverlay>();
    }

    private void DrawDebugLandmarks(
      IReadOnlyList<TaskNormalizedLandmark> poseLandmarks,
      IReadOnlyList<TaskNormalizedLandmark> faceLandmarks,
      IReadOnlyList<IReadOnlyList<TaskNormalizedLandmark>> handLandmarks)
    {
      if (!showDebugLandmarks)
      {
        _landmarkOverlay?.Clear();
        return;
      }

      EnsureLandmarkOverlay();
      _landmarkOverlay?.Draw(poseLandmarks, faceLandmarks, handLandmarks);
    }

    private ImageProcessingOptions CreateImageProcessingOptions(out bool flipHorizontally, out bool flipVertically)
    {
      var rotation = (global::Mediapipe.Unity.RotationAngle)NormalizeRotationDegrees(_webCamTexture.videoRotationAngle);
      const bool mirrorModelInput = false;
      var transformationOptions = ImageTransformationOptions.Build(
        mirrorModelInput,
        _webCamTexture.videoVerticallyMirrored,
        rotation);

      flipHorizontally = transformationOptions.flipHorizontally;
      flipVertically = transformationOptions.flipVertically;
      _lastInputTransformDetail =
        $"rotation={(int)transformationOptions.rotationAngle}, flipH={flipHorizontally}, flipV={flipVertically}, previewMirror={mirrorCameraPreview}";
      return new ImageProcessingOptions(rotationDegrees: (int)transformationOptions.rotationAngle);
    }

    private void StopCamera()
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
    }

    private void DisposeMediapipe()
    {
      DisposeTaskApis();
      _textureFramePool?.Dispose();
      _textureFramePool = null;

      if (global::Mediapipe.Unity.GpuManager.IsInitialized)
      {
        global::Mediapipe.Unity.GpuManager.Shutdown();
      }

      if (_isMediapipeInitialized)
      {
        global::Mediapipe.Glog.Shutdown();
        global::Mediapipe.Protobuf.ResetLogHandler();
        _isMediapipeInitialized = false;
      }
    }

    private void DisposeTaskApis()
    {
      _poseLandmarker?.Close();
      _poseLandmarker = null;
      _faceLandmarker?.Close();
      _faceLandmarker = null;
      _handLandmarker?.Close();
      _handLandmarker = null;
      _objectDetector?.Close();
      _objectDetector = null;
    }

    private void ClearRuntimeTrackingState()
    {
      _personInFrame = false;
      _faceInFrame = false;
      _poseInFrame = false;
      _headDown = false;
      _likelyReading = false;
      _stablePosture = BodyPosture.Unknown;
      _lastSeenTime = -999f;
      _currentIdentity = "未知";
      _postureBasis = "无";
      _sceneObjectContext = "无";
      _deskLikeObjectInFrame = false;
      _seatLikeObjectInFrame = false;
      _readingObjectInFrame = false;
      _handLandmarkLists.Clear();
      _landmarkOverlay?.Clear();
      _processedFrameCount = 0;
      _textureFrameMissCount = 0;
      _lastPoseDetected = false;
      _lastFaceDetected = false;
      _lastHandDetected = false;
      _lastPoseLandmarkCount = 0;
      _lastFaceLandmarkCount = 0;
      _lastHandCount = 0;
      ResetUpperBodyBaseline();
      ResetHeadPitchBaseline();
    }

    private void AnalyzeResults(bool poseDetected, bool faceDetected, bool handDetected)
    {
      var poseLandmarks = GetPoseLandmarks(poseDetected);
      var faceLandmarks = GetFaceLandmarks(faceDetected);
      var handLandmarks = GetHandLandmarks(handDetected);

      _processedFrameCount++;
      _lastPoseDetected = poseDetected;
      _lastFaceDetected = faceDetected;
      _lastHandDetected = handDetected;
      _lastPoseLandmarkCount = poseLandmarks?.Count ?? 0;
      _lastFaceLandmarkCount = faceLandmarks?.Count ?? 0;
      _lastHandCount = handLandmarks.Count;

      _poseInFrame = HasTrackedPose(poseLandmarks);
      _faceInFrame = HasTrackedFace(faceLandmarks);

      if (_poseInFrame || _faceInFrame)
      {
        _lastSeenTime = Time.unscaledTime;
      }

      _personInFrame = Time.unscaledTime - _lastSeenTime <= absenceGraceSeconds;
      _headDown = _personInFrame && EstimateHeadDown(faceLandmarks, poseLandmarks);
      _stablePosture = UpdateStablePosture(EstimatePosture(poseLandmarks));
      _likelyReading = _personInFrame &&
        _headDown &&
        (_stablePosture != BodyPosture.Standing ||
         _deskLikeObjectInFrame ||
         _seatLikeObjectInFrame ||
         _readingObjectInFrame);

      UpdateBlinkState();
      _currentIdentity = ResolveIdentity(faceLandmarks);
      DrawDebugLandmarks(poseLandmarks, faceLandmarks, handLandmarks);
    }

    private List<TaskNormalizedLandmark> GetPoseLandmarks(bool poseDetected)
    {
      if (!poseDetected || _poseResult.poseLandmarks == null || _poseResult.poseLandmarks.Count == 0)
      {
        return null;
      }

      return _poseResult.poseLandmarks[0].landmarks;
    }

    private List<TaskNormalizedLandmark> GetFaceLandmarks(bool faceDetected)
    {
      if (!faceDetected || _faceResult.faceLandmarks == null || _faceResult.faceLandmarks.Count == 0)
      {
        return null;
      }

      return _faceResult.faceLandmarks[0].landmarks;
    }

    private IReadOnlyList<IReadOnlyList<TaskNormalizedLandmark>> GetHandLandmarks(bool handDetected)
    {
      _handLandmarkLists.Clear();
      if (!handDetected || _handResult.handLandmarks == null || _handResult.handLandmarks.Count == 0)
      {
        return _handLandmarkLists;
      }

      foreach (var handLandmarks in _handResult.handLandmarks)
      {
        if (handLandmarks.landmarks != null && handLandmarks.landmarks.Count > 0)
        {
          _handLandmarkLists.Add(handLandmarks.landmarks);
        }
      }

      return _handLandmarkLists;
    }

    private bool ShouldRunObjectDetection()
    {
      if (!enableDeskAwarePosture || !enableObjectDetectionPostureEvidence || _objectDetector == null)
      {
        return false;
      }

      var interval = Mathf.Max(0.1f, objectDetectionIntervalSeconds);
      return Time.unscaledTime - _lastObjectDetectionTime >= interval;
    }

    private void UpdateSceneObjectContext(ObjectDetectionResult result)
    {
      _lastObjectDetectionTime = Time.unscaledTime;
      _deskLikeObjectInFrame = false;
      _seatLikeObjectInFrame = false;
      _readingObjectInFrame = false;
      _sceneObjectContext = "无";

      if (result.detections == null || result.detections.Count == 0)
      {
        return;
      }

      var labels = new List<string>(4);
      foreach (var detection in result.detections)
      {
        if (detection.categories == null || detection.categories.Count == 0)
        {
          continue;
        }

        var category = detection.categories[0];
        if (category.score < objectDetectionScoreThreshold)
        {
          continue;
        }

        var label = ResolveCategoryLabel(category);
        if (string.IsNullOrEmpty(label))
        {
          continue;
        }

        labels.Add($"{label}:{category.score:0.00}");
        var normalizedLabel = label.ToLowerInvariant();
        if (normalizedLabel.Contains("table") || normalizedLabel.Contains("desk"))
        {
          _deskLikeObjectInFrame = true;
        }

        if (normalizedLabel.Contains("chair") ||
            normalizedLabel.Contains("bench") ||
            normalizedLabel.Contains("couch") ||
            normalizedLabel.Contains("bed"))
        {
          _seatLikeObjectInFrame = true;
        }

        if (normalizedLabel.Contains("book") ||
            normalizedLabel.Contains("laptop") ||
            normalizedLabel.Contains("cell phone") ||
            normalizedLabel.Contains("keyboard"))
        {
          _readingObjectInFrame = true;
        }
      }

      if (labels.Count > 0)
      {
        _sceneObjectContext = string.Join(", ", labels);
      }
    }

    private void ResetSceneObjectContext()
    {
      _deskLikeObjectInFrame = false;
      _seatLikeObjectInFrame = false;
      _readingObjectInFrame = false;
      _sceneObjectContext = "无";
    }

    private static string ResolveCategoryLabel(global::Mediapipe.Tasks.Components.Containers.Category category)
    {
      return !string.IsNullOrWhiteSpace(category.categoryName)
        ? category.categoryName
        : category.displayName;
    }

    private bool HasTrackedPose(IReadOnlyList<TaskNormalizedLandmark> landmarks)
    {
      if (landmarks == null || landmarks.Count < 29)
      {
        return false;
      }

      var trackedCount = 0;
      foreach (var index in PosePresenceLandmarkIndices)
      {
        if (IsPresent(landmarks[index]))
        {
          trackedCount++;
        }
      }

      return trackedCount >= 2;
    }

    private bool HasTrackedFace(IReadOnlyList<TaskNormalizedLandmark> landmarks)
    {
      if (landmarks == null || landmarks.Count == 0)
      {
        return false;
      }

      var trackedCount = 0;
      foreach (var landmark in landmarks)
      {
        if (IsPresent(landmark))
        {
          trackedCount++;
          if (trackedCount >= 12)
          {
            return true;
          }
        }
      }

      return false;
    }

    private BodyPosture EstimatePosture(IReadOnlyList<TaskNormalizedLandmark> landmarks)
    {
      _postureBasis = "无";
      if (landmarks == null || landmarks.Count < 29)
      {
        return BodyPosture.Unknown;
      }

      var shouldersVisible = IsVisible(landmarks[11]) || IsVisible(landmarks[12]);
      var hipsVisible = IsVisible(landmarks[23]) || IsVisible(landmarks[24]);
      var kneesVisible = IsVisible(landmarks[25]) || IsVisible(landmarks[26]);
      var anklesVisible = IsVisible(landmarks[27]) || IsVisible(landmarks[28]);

      if (!shouldersVisible)
      {
        return BodyPosture.Unknown;
      }

      if (!hipsVisible)
      {
        return EstimatePartialBodyPosture(landmarks, "未看到髋/腿");
      }

      if (enableDeskAwarePosture &&
          deskObjectsOverrideFullBodyPosture &&
          HasScenePostureObjectEvidence())
      {
        _postureBasis = $"检测到桌/椅/阅读物，优先按桌前姿态处理：{_sceneObjectContext}";
        return BodyPosture.Sitting;
      }

      var bodySpan = GetVisibleVerticalSpan(landmarks);
      var shoulderY = AverageVisibleY(landmarks[11], landmarks[12]);
      var hipY = AverageVisibleY(landmarks[23], landmarks[24]);
      var kneeY = AverageVisibleY(landmarks[25], landmarks[26]);
      var ankleY = AverageVisibleY(landmarks[27], landmarks[28]);
      var torsoHeight = Mathf.Abs(hipY - shoulderY);
      var legHeight = anklesVisible ? Mathf.Abs(ankleY - hipY) : 0f;
      var leftKneeAngle = TryGetAngle(landmarks, 23, 25, 27, out var leftAngle) ? leftAngle : -1f;
      var rightKneeAngle = TryGetAngle(landmarks, 24, 26, 28, out var rightAngle) ? rightAngle : -1f;
      var kneeAngle = AveragePositive(leftKneeAngle, rightKneeAngle);

      if (hipsVisible && kneesVisible && anklesVisible)
      {
        _postureBasis = "全身骨架";
        var legsMostlyVertical = kneeY > hipY && ankleY > kneeY;
        if (legsMostlyVertical &&
            kneeAngle > 150f &&
            torsoHeight > 0.03f &&
            legHeight > torsoHeight * 1.15f &&
            bodySpan > 0.62f)
        {
          return BodyPosture.Standing;
        }

        if (kneeAngle > 0f && kneeAngle < 95f && legHeight > torsoHeight * 0.75f)
        {
          return BodyPosture.Squatting;
        }

        if (kneeAngle > 0f && kneeAngle < 150f)
        {
          return BodyPosture.Sitting;
        }
      }

      if (!kneesVisible || !anklesVisible)
      {
        return EstimatePartialBodyPosture(landmarks, "未看到膝/脚踝");
      }

      _postureBasis = "全身骨架";
      return BodyPosture.Sitting;
    }

    private BodyPosture EstimatePartialBodyPosture(IReadOnlyList<TaskNormalizedLandmark> landmarks, string missingReason)
    {
      if (enableDeskAwarePosture && (_deskLikeObjectInFrame || _seatLikeObjectInFrame || _readingObjectInFrame))
      {
        _postureBasis = $"{missingReason}，检测到桌/椅/阅读物：{_sceneObjectContext}";
        return BodyPosture.Sitting;
      }

      if (enableDeskAwarePosture && useOcclusionPostureEvidence && HasDeskLikeOcclusionEvidence(landmarks))
      {
        _postureBasis = $"{missingReason}，前方遮挡/阅读动作证据";
        return BodyPosture.Sitting;
      }

      if (useUpperBodyPostureHeuristic)
      {
        var posture = EstimateUpperBodyPosture(landmarks);
        if (posture != BodyPosture.Unknown)
        {
          _postureBasis = $"{missingReason}，上半身相对高度估算";
          return posture;
        }
      }

      _postureBasis = $"{missingReason}，无桌椅证据，按站姿处理";
      _upperBodyMovedUp = 0f;
      _upperBodyScaleBoost = 0f;
      return BodyPosture.Standing;
    }

    private bool HasScenePostureObjectEvidence()
    {
      return _deskLikeObjectInFrame || _seatLikeObjectInFrame || _readingObjectInFrame;
    }

    private bool HasDeskLikeOcclusionEvidence(IReadOnlyList<TaskNormalizedLandmark> landmarks)
    {
      if (!_personInFrame || landmarks == null || landmarks.Count < 25)
      {
        return false;
      }

      var shouldersVisible = IsVisible(landmarks[11]) || IsVisible(landmarks[12]);
      var hipsVisible = IsVisible(landmarks[23]) || IsVisible(landmarks[24]);
      if (!shouldersVisible || hipsVisible)
      {
        return false;
      }

      if (_headDown)
      {
        return true;
      }

      var shoulderY = AverageVisibleY(landmarks[11], landmarks[12]);
      foreach (var handLandmarks in _handLandmarkLists)
      {
        if (handLandmarks == null)
        {
          continue;
        }

        for (var i = 0; i < handLandmarks.Count; i++)
        {
          var hand = handLandmarks[i];
          if (IsPresent(hand) && (hand.y >= handDeskEvidenceMinY || hand.y >= shoulderY + 0.12f))
          {
            return true;
          }
        }
      }

      return false;
    }

    private BodyPosture EstimateUpperBodyPosture(IReadOnlyList<TaskNormalizedLandmark> landmarks)
    {
      if (!useUpperBodyPostureHeuristic ||
          !TryGetUpperBodyMetrics(landmarks, out var centerY, out var scale))
      {
        return BodyPosture.Unknown;
      }

      UpdateUpperBodyBaseline(centerY, scale);
      if (!_upperBodyBaselineReady)
      {
        return BodyPosture.Unknown;
      }

      _upperBodyMovedUp = _upperBodyBaselineY - centerY;
      _upperBodyScaleBoost = _upperBodyBaselineScale <= 0.001f
        ? 0f
        : (scale - _upperBodyBaselineScale) / _upperBodyBaselineScale;

      if (_upperBodyMovedUp >= upperBodyStandingYOffset ||
          (_upperBodyMovedUp >= upperBodyStandingYOffset * 0.55f && _upperBodyScaleBoost >= upperBodyStandingScaleBoost))
      {
        return BodyPosture.Standing;
      }

      if (_upperBodyMovedUp <= upperBodyStandingYOffset * 0.35f)
      {
        return BodyPosture.Sitting;
      }

      return BodyPosture.Unknown;
    }

    private void UpdateUpperBodyBaseline(float centerY, float scale)
    {
      if (_upperBodyBaselineReady)
      {
        if (_stablePosture == BodyPosture.Sitting)
        {
          _upperBodyBaselineY = Mathf.Lerp(_upperBodyBaselineY, centerY, 0.01f);
          _upperBodyBaselineScale = Mathf.Lerp(_upperBodyBaselineScale, scale, 0.01f);
        }

        return;
      }

      if (_upperBodyBaselineStartTime < 0f)
      {
        _upperBodyBaselineStartTime = Time.unscaledTime;
      }

      _upperBodyBaselineYSum += centerY;
      _upperBodyBaselineScaleSum += scale;
      _upperBodyBaselineSampleCount++;

      if (Time.unscaledTime - _upperBodyBaselineStartTime < upperBodyBaselineSeconds ||
          _upperBodyBaselineSampleCount <= 0)
      {
        return;
      }

      _upperBodyBaselineY = _upperBodyBaselineYSum / _upperBodyBaselineSampleCount;
      _upperBodyBaselineScale = Mathf.Max(0.001f, _upperBodyBaselineScaleSum / _upperBodyBaselineSampleCount);
      _upperBodyBaselineReady = true;
    }

    private void ResetUpperBodyBaseline()
    {
      _upperBodyBaselineReady = false;
      _upperBodyBaselineStartTime = -1f;
      _upperBodyBaselineYSum = 0f;
      _upperBodyBaselineScaleSum = 0f;
      _upperBodyBaselineSampleCount = 0;
      _upperBodyBaselineY = 0f;
      _upperBodyBaselineScale = 0f;
      _upperBodyMovedUp = 0f;
      _upperBodyScaleBoost = 0f;
    }

    private bool TryGetUpperBodyMetrics(IReadOnlyList<TaskNormalizedLandmark> landmarks, out float centerY, out float scale)
    {
      centerY = 0f;
      scale = 0f;

      if (landmarks == null || landmarks.Count < 25 ||
          (!IsVisible(landmarks[11]) && !IsVisible(landmarks[12])))
      {
        return false;
      }

      var yTotal = 0f;
      var yCount = 0;
      AddVisibleY(landmarks[0], ref yTotal, ref yCount);
      AddVisibleY(landmarks[11], ref yTotal, ref yCount);
      AddVisibleY(landmarks[12], ref yTotal, ref yCount);
      AddVisibleY(landmarks[23], ref yTotal, ref yCount);
      AddVisibleY(landmarks[24], ref yTotal, ref yCount);

      if (yCount < 2)
      {
        return false;
      }

      centerY = yTotal / yCount;
      var shoulderWidth = TryGetDistance(landmarks, 11, 12, out var width) ? width : 0f;
      var shoulderY = AverageVisibleY(landmarks[11], landmarks[12]);
      var hipY = AverageVisibleY(landmarks[23], landmarks[24]);
      var torsoHeight = Mathf.Abs(hipY - shoulderY);
      var headToShoulder = IsVisible(landmarks[0]) ? Mathf.Abs(landmarks[0].y - shoulderY) : 0f;

      scale = Mathf.Max(shoulderWidth, torsoHeight, headToShoulder * 1.8f, 0.001f);
      return true;
    }

    private BodyPosture UpdateStablePosture(BodyPosture current)
    {
      if (current == BodyPosture.Unknown)
      {
        if (_unknownPostureSince < 0f)
        {
          _unknownPostureSince = Time.unscaledTime;
        }

        if (Time.unscaledTime - _unknownPostureSince >= postureUnknownResetSeconds)
        {
          _stablePosture = BodyPosture.Unknown;
        }

        return _stablePosture;
      }

      _unknownPostureSince = -1f;
      if (_candidatePosture != current)
      {
        _candidatePosture = current;
        _candidatePostureSince = Time.unscaledTime;
        return _stablePosture;
      }

      if (Time.unscaledTime - _candidatePostureSince < postureStableSeconds)
      {
        return _stablePosture;
      }

      if (_stablePosture != current)
      {
        if (_stablePosture != BodyPosture.Standing && current == BodyPosture.Standing)
        {
          _standUpCount++;
        }

        _stablePosture = current;
      }

      return _stablePosture;
    }

    private bool EstimateHeadDown(IReadOnlyList<TaskNormalizedLandmark> faceLandmarks, IReadOnlyList<TaskNormalizedLandmark> poseLandmarks)
    {
      _hasHeadPitch = TryGetFacePitchDegrees(out _headPitchDegrees);
      _headDownPitchScore = 0f;
      _headPitchBaselineDegrees = headPitchNeutralDegrees;
      _headPitchBaselineReady = true;

      var eyeLookDown = -1f;
      var eyeLookUp = -1f;
      if (_faceInFrame && faceLandmarks != null)
      {
        eyeLookDown = AveragePositive(
          GetBlendshapeScore("eyeLookDownLeft"),
          GetBlendshapeScore("eyeLookDownRight"));
        eyeLookUp = AveragePositive(
          GetBlendshapeScore("eyeLookUpLeft"),
          GetBlendshapeScore("eyeLookUpRight"));
      }

      var eyesSupportDown = eyeLookDown >= 0.5f && (eyeLookUp < 0f || eyeLookDown >= eyeLookUp + 0.05f);
      var hasLandmarkScore = TryGetLandmarkHeadDownScore(faceLandmarks, out var landmarkScore);
      if (hasLandmarkScore)
      {
        _headDownPitchScore = landmarkScore;
        if (!_hasHeadPitch)
        {
          _hasHeadPitch = true;
          _headPitchDegrees = landmarkScore;
        }

        if (landmarkScore <= -headDownPitchThreshold * 0.55f)
        {
          return false;
        }

        if (landmarkScore >= headDownPitchThreshold)
        {
          return true;
        }
      }

      if (_hasHeadPitch)
      {
        var matrixScore =
          (_headPitchDegrees - _headPitchBaselineDegrees) *
          (invertHeadPitch ? -1f : 1f) *
          GetPlatformHeadDownScoreSign();
        if (!hasLandmarkScore)
        {
          _headDownPitchScore = matrixScore;
        }

        if (matrixScore <= -headDownPitchThreshold * 0.65f)
        {
          return false;
        }

        if (matrixScore >= headDownPitchThreshold)
        {
          return !hasLandmarkScore ||
            landmarkScore >= headPitchNeutralDeadZone ||
            eyesSupportDown;
        }

        if (eyesSupportDown &&
            (hasLandmarkScore
              ? landmarkScore >= headDownPitchThreshold * 0.35f
              : Mathf.Abs(_headPitchDegrees - _headPitchBaselineDegrees) >= headDownPitchThreshold * 0.5f))
        {
          return true;
        }
      }

      if (hasLandmarkScore)
      {
        if (eyesSupportDown && landmarkScore >= headDownPitchThreshold * 0.45f)
        {
          return true;
        }
      }

      if (eyesSupportDown && (!hasLandmarkScore || landmarkScore >= 0f))
      {
        return true;
      }
      return false;
    }

    private bool TryGetLandmarkHeadDownScore(IReadOnlyList<TaskNormalizedLandmark> faceLandmarks, out float score)
    {
      score = 0f;
      if (!_faceInFrame || faceLandmarks == null || faceLandmarks.Count <= 263)
      {
        return false;
      }

      var forehead = faceLandmarks[10];
      var chin = faceLandmarks[152];
      var noseTip = faceLandmarks[1];
      var leftEyeOuter = faceLandmarks[33];
      var rightEyeOuter = faceLandmarks[263];
      if (!IsPresent(forehead) ||
          !IsPresent(chin) ||
          !IsPresent(noseTip) ||
          !IsPresent(leftEyeOuter) ||
          !IsPresent(rightEyeOuter))
      {
        return false;
      }

      var signedFaceHeight = chin.y - forehead.y;
      var faceHeight = Mathf.Abs(signedFaceHeight);
      if (faceHeight <= 0.001f)
      {
        return false;
      }

      var eyeCenterY = (leftEyeOuter.y + rightEyeOuter.y) * 0.5f;
      var faceDownSign = signedFaceHeight >= 0f ? 1f : -1f;
      var noseFromEyeRatio = ((noseTip.y - eyeCenterY) * faceDownSign) / faceHeight;

      const float neutralNoseFromEyeRatio = 0.24f;
      score = (noseFromEyeRatio - neutralNoseFromEyeRatio) * 120f;
      score *= GetPlatformHeadDownScoreSign();
      return IsFinite(score);
    }

    private float GetPlatformHeadDownScoreSign()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      return invertHeadDownScoreOnAndroid ? -1f : 1f;
#else
      return 1f;
#endif
    }

    private bool UpdateHeadPitchBaseline(float pitchDegrees)
    {
      if (_headPitchBaselineReady)
      {
        return true;
      }

      if (_headPitchBaselineStartTime < 0f)
      {
        _headPitchBaselineStartTime = Time.unscaledTime;
      }

      _headPitchBaselineSum += pitchDegrees;
      _headPitchBaselineSampleCount++;

      if (Time.unscaledTime - _headPitchBaselineStartTime < headPitchBaselineSeconds ||
          _headPitchBaselineSampleCount <= 0)
      {
        return false;
      }

      _headPitchBaselineDegrees = _headPitchBaselineSum / _headPitchBaselineSampleCount;
      _headPitchBaselineReady = true;
      return true;
    }

    private void ResetHeadPitchBaseline()
    {
      _headPitchBaselineReady = false;
      _headPitchBaselineStartTime = -1f;
      _headPitchBaselineSum = 0f;
      _headPitchBaselineSampleCount = 0;
      _headPitchBaselineDegrees = 0f;
      _headDownPitchScore = 0f;
    }

    private bool TryGetFacePitchDegrees(out float pitchDegrees)
    {
      pitchDegrees = 0f;
      if (!_faceInFrame ||
          _faceResult.facialTransformationMatrixes == null ||
          _faceResult.facialTransformationMatrixes.Count == 0)
      {
        return false;
      }

      var rotation = _faceResult.facialTransformationMatrixes[0].rotation;
      pitchDegrees = NormalizeSignedAngle(rotation.eulerAngles.x);
      return IsFinite(pitchDegrees);
    }

    private void UpdateBlinkState()
    {
      if (!_faceInFrame || !TryGetBlinkScores(out var leftBlink, out var rightBlink, out var blinkScore))
      {
        ResetBlinkTracking();
        return;
      }

      var now = Time.unscaledTime;
      var previousBlinkScore = _lastBlinkScore;
      _lastBlinkScore = blinkScore;
      var minBlink = Mathf.Min(leftBlink, rightBlink);
      var maxBlink = Mathf.Max(leftBlink, rightBlink);
      var closedNow =
        (blinkScore >= blinkThreshold && minBlink >= blinkThreshold * 0.25f) ||
        (maxBlink >= blinkThreshold * 1.6f && minBlink >= 0.05f);
      var openNow = blinkScore <= blinkOpenThreshold ||
        (!closedNow && previousBlinkScore >= blinkThreshold && previousBlinkScore - blinkScore >= 0.08f);

      if (!_eyesOpenObserved)
      {
        _eyesOpenObserved = !closedNow || openNow;
        _eyesClosed = false;
        _eyesClosedSince = -1f;
        return;
      }

      if (closedNow)
      {
        if (!_eyesClosed)
        {
          _eyesClosed = true;
          _eyesClosedSince = now;
          if (now - _lastBlinkTime >= blinkCooldownSeconds)
          {
            _blinkCount++;
            _lastBlinkTime = now;
          }
        }

        return;
      }

      if (openNow || blinkScore <= blinkThreshold * 0.75f)
      {
        _eyesClosed = false;
        _eyesClosedSince = -1f;
        _eyesOpenObserved = true;
        return;
      }
    }

    private bool TryGetBlinkScores(out float leftBlink, out float rightBlink, out float blinkScore)
    {
      leftBlink = -1f;
      rightBlink = -1f;
      blinkScore = -1f;

      leftBlink = GetBlendshapeScore("eyeBlinkLeft");
      rightBlink = GetBlendshapeScore("eyeBlinkRight");
      var hasBlendshape = leftBlink >= 0f && rightBlink >= 0f;
      var hasLandmarks = TryGetLandmarkBlinkScores(out var landmarkLeftBlink, out var landmarkRightBlink);

      if (!hasBlendshape && !hasLandmarks)
      {
        return false;
      }

      if (hasLandmarks)
      {
        if (hasBlendshape)
        {
          leftBlink = landmarkLeftBlink >= 0.65f ? Mathf.Max(leftBlink, landmarkLeftBlink) : leftBlink;
          rightBlink = landmarkRightBlink >= 0.65f ? Mathf.Max(rightBlink, landmarkRightBlink) : rightBlink;
        }
        else
        {
          leftBlink = landmarkLeftBlink;
          rightBlink = landmarkRightBlink;
        }
      }

      blinkScore = (leftBlink + rightBlink) * 0.5f;
      return true;
    }

    private bool TryGetLandmarkBlinkScores(out float leftBlink, out float rightBlink)
    {
      leftBlink = -1f;
      rightBlink = -1f;
      if (!_faceInFrame ||
          _faceResult.faceLandmarks == null ||
          _faceResult.faceLandmarks.Count == 0 ||
          _faceResult.faceLandmarks[0].landmarks == null)
      {
        return false;
      }

      var landmarks = _faceResult.faceLandmarks[0].landmarks;
      if (landmarks.Count <= 386)
      {
        return false;
      }

      var hasLeft = TryGetEyeClosureScore(landmarks, 159, 145, 33, 133, out leftBlink);
      var hasRight = TryGetEyeClosureScore(landmarks, 386, 374, 362, 263, out rightBlink);
      return hasLeft && hasRight;
    }

    private bool TryGetEyeClosureScore(
      IReadOnlyList<TaskNormalizedLandmark> landmarks,
      int upperIndex,
      int lowerIndex,
      int outerIndex,
      int innerIndex,
      out float closureScore)
    {
      closureScore = -1f;
      var upper = landmarks[upperIndex];
      var lower = landmarks[lowerIndex];
      var outer = landmarks[outerIndex];
      var inner = landmarks[innerIndex];
      if (!IsPresent(upper) || !IsPresent(lower) || !IsPresent(outer) || !IsPresent(inner))
      {
        return false;
      }

      var vertical = Vector2.Distance(new Vector2(upper.x, upper.y), new Vector2(lower.x, lower.y));
      var horizontal = Vector2.Distance(new Vector2(outer.x, outer.y), new Vector2(inner.x, inner.y));
      if (horizontal <= 0.001f)
      {
        return false;
      }

      var eyeOpenRatio = vertical / horizontal;
      closureScore = Mathf.Clamp01((0.19f - eyeOpenRatio) / 0.13f);
      return IsFinite(closureScore);
    }

    private void ResetBlinkTracking()
    {
      _eyesClosed = false;
      _eyesOpenObserved = false;
      _eyesClosedSince = -1f;
      _lastBlinkScore = -1f;
    }

    private string ResolveIdentity(IReadOnlyList<TaskNormalizedLandmark> faceLandmarks)
    {
      if (faceLandmarks == null || faceLandmarks.Count == 0)
      {
        return "未知";
      }

      if (!TryBuildFaceSignature(faceLandmarks, out var signature) || registeredFaces.Count == 0)
      {
        return "未知";
      }

      var bestName = "未知";
      var bestDistance = float.MaxValue;
      foreach (var profile in registeredFaces)
      {
        if (profile == null || profile.signature == null || profile.signature.Count != signature.Count)
        {
          continue;
        }

        var distance = EuclideanDistance(signature, profile.signature);
        if (distance < bestDistance)
        {
          bestDistance = distance;
          bestName = string.IsNullOrWhiteSpace(profile.personName) ? "未命名" : profile.personName;
        }
      }

      return bestDistance <= identityDistanceThreshold ? bestName : "未知";
    }

    public void EnrollCurrentFace()
    {
      var faceLandmarks = GetFaceLandmarks(_faceInFrame);
      if (faceLandmarks == null || !TryBuildFaceSignature(faceLandmarks, out var signature))
      {
        Debug.LogWarning("[StudyMonitor] 当前没有可录入的人脸。");
        return;
      }

      var personName = enrollNameInput != null ? enrollNameInput.text.Trim() : string.Empty;
      if (string.IsNullOrWhiteSpace(personName))
      {
        personName = "孩子";
      }

      var profile = registeredFaces.Find(item => item != null && item.personName == personName);
      if (profile == null)
      {
        profile = new FaceProfile { personName = personName };
        registeredFaces.Add(profile);
      }

      profile.signature = signature;
      SaveFaceProfiles();
      _currentIdentity = personName;
      Debug.Log($"[StudyMonitor] 已录入人脸：{personName}");
      UpdateStatusUi();
    }

    private bool TryBuildFaceSignature(IReadOnlyList<TaskNormalizedLandmark> landmarks, out List<float> signature)
    {
      signature = null;
      if (landmarks == null || landmarks.Count <= 454)
      {
        return false;
      }

      var minX = float.MaxValue;
      var maxX = float.MinValue;
      var minY = float.MaxValue;
      var maxY = float.MinValue;

      foreach (var landmark in landmarks)
      {
        minX = Mathf.Min(minX, landmark.x);
        maxX = Mathf.Max(maxX, landmark.x);
        minY = Mathf.Min(minY, landmark.y);
        maxY = Mathf.Max(maxY, landmark.y);
      }

      var centerX = (minX + maxX) * 0.5f;
      var centerY = (minY + maxY) * 0.5f;
      var scale = Mathf.Max(maxX - minX, maxY - minY, 0.0001f);
      signature = new List<float>(FaceSignatureLandmarkIndices.Length * 3);

      foreach (var index in FaceSignatureLandmarkIndices)
      {
        var point = landmarks[index];
        signature.Add((point.x - centerX) / scale);
        signature.Add((point.y - centerY) / scale);
        signature.Add(point.z / scale);
      }

      return true;
    }

    private float GetBlendshapeScore(string categoryName)
    {
      if (_faceResult.faceBlendshapes == null || _faceResult.faceBlendshapes.Count == 0)
      {
        return -1f;
      }

      var categories = _faceResult.faceBlendshapes[0].categories;
      if (categories == null)
      {
        return -1f;
      }

      foreach (var category in categories)
      {
        if (string.Equals(category.categoryName, categoryName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category.displayName, categoryName, StringComparison.OrdinalIgnoreCase))
        {
          return category.score;
        }
      }

      return -1f;
    }

    private void UpdateStatusUi()
    {
      var status = _personInFrame ? "画面中" : "未在画面中";
      var posture = ToChinese(_stablePosture);
      var attention = _likelyReading ? "疑似看书/写字" : (_headDown ? "低头" : "看前方/未知");
      var face = _faceInFrame ? "检测到脸" : "未检测到脸";
      var headPitch = _hasHeadPitch ? $"{_headPitchDegrees:0.0}°" : "无";
      var headDownPitch = _hasHeadPitch ? $"{_headDownPitchScore:0.0}°" : "无";
      var headPitchBaseline = _headPitchBaselineReady ? $"{_headPitchBaselineDegrees:0.0}°" : "校准中";
      var blinkScore = _lastBlinkScore >= 0f ? _lastBlinkScore.ToString("0.00") : "无";
      var cameraCount = WebCamTexture.devices.Length;
      var handCount = _handLandmarkLists.Count;
      var detectionMode = detectionIntervalSeconds <= 0f ? "逐帧实时" : $"{detectionIntervalSeconds:0.00}s/次";
      var inferenceDelegate = $"{_activeDelegate}（{_inferenceStatusDetail}）";
      var latestInference =
        $"帧={_processedFrameCount} 丢帧池={_textureFrameMissCount} " +
        $"Pose={_lastPoseDetected}/{_lastPoseLandmarkCount} " +
        $"Face={_lastFaceDetected}/{_lastFaceLandmarkCount} " +
        $"Hand={_lastHandDetected}/{_lastHandCount}";
      var objectContext = enableDeskAwarePosture && _objectDetector != null ? _sceneObjectContext : "未启用";
      var upperBodyDebug = _upperBodyBaselineReady
        ? $"上移={_upperBodyMovedUp:0.00} 放大={_upperBodyScaleBoost:0.00}"
        : "上半身基线未启用/无数据";

      if (bottomStatusText != null)
      {
        bottomStatusText.text = $"人物：{status}    姿态：{posture}    注意力：{attention}    身份：{_currentIdentity}";
      }

      if (identityText != null)
      {
        identityText.text = $"当前身份：{_currentIdentity}";
      }

      if (reportText != null)
      {
        reportText.text =
          $"人物：{status}\n" +
          $"姿态：{posture}\n" +
          $"姿态依据：{_postureBasis}\n" +
          $"场景物体：{objectContext}\n" +
          $"姿态调试：{upperBodyDebug}\n" +
          $"脸部：{face}\n" +
          $"头部俯仰角：{headPitch}\n" +
          $"头部基准角：{headPitchBaseline}\n" +
          $"低头判断角：{headDownPitch}\n" +
          $"低头：{YesNo(_headDown)}\n" +
          $"疑似看书/写字：{YesNo(_likelyReading)}\n" +
          $"手部数量：{handCount}\n" +
          $"闭眼程度：{blinkScore}\n" +
          $"眨眼次数：{_blinkCount}\n" +
          $"起立次数：{_standUpCount}\n" +
          $"相机数量：{cameraCount}\n" +
          $"推理后端：{inferenceDelegate}\n" +
          $"最近推理：{latestInference}\n" +
          $"输入变换：{_lastInputTransformDetail}\n" +
          $"检测频率：{detectionMode}\n" +
          $"已录入人数：{registeredFaces.Count}";
      }
    }

    private void DebugPoseEveryInterval()
    {
      if (Time.unscaledTime - _lastDebugTime < debugIntervalSeconds)
      {
        return;
      }

      _lastDebugTime = Time.unscaledTime;
      Debug.Log(
        $"[StudyMonitor] 人物={(_personInFrame ? "在画面中" : "离开画面")}，" +
        $"身份={_currentIdentity}，姿态={ToChinese(_stablePosture)}，依据={_postureBasis}，" +
        $"上移={_upperBodyMovedUp:0.00}，放大={_upperBodyScaleBoost:0.00}，头部俯仰角={(_hasHeadPitch ? _headPitchDegrees.ToString("0.0") : "无")}，判断角={(_hasHeadPitch ? _headDownPitchScore.ToString("0.0") : "无")}，低头={YesNo(_headDown)}，疑似看书/写字={YesNo(_likelyReading)}，" +
        $"手部数量={_handLandmarkLists.Count}，眨眼次数={_blinkCount}，起立次数={_standUpCount}");
    }

    private void ApplyCameraVisibility()
    {
      var showCamera = showCameraToggle == null || showCameraToggle.isOn;

      if (cameraView != null)
      {
        cameraView.gameObject.SetActive(showCamera);
      }

      if (hiddenCameraImage != null)
      {
        hiddenCameraImage.gameObject.SetActive(!showCamera);
        hiddenCameraImage.color = Color.green;
      }
    }

    private void UpdateCameraPreviewTransform()
    {
      if (cameraView == null || _webCamTexture == null)
      {
        return;
      }

      var rotation = (global::Mediapipe.Unity.RotationAngle)NormalizeRotationDegrees(_webCamTexture.videoRotationAngle);
      var rect = new Rect(0f, 0f, 1f, 1f);

      if (_webCamTexture.videoVerticallyMirrored)
      {
        rect = FlipUvVertically(rect);
      }

      if (mirrorCameraPreview)
      {
        rect = rotation == global::Mediapipe.Unity.RotationAngle.Rotation0 ||
               rotation == global::Mediapipe.Unity.RotationAngle.Rotation180
          ? FlipUvHorizontally(rect)
          : FlipUvVertically(rect);
      }

      cameraView.uvRect = rect;
      cameraView.rectTransform.localEulerAngles =
        global::Mediapipe.Unity.RotationAngleExtension.GetEulerAngles(
          global::Mediapipe.Unity.RotationAngleExtension.Reverse(rotation));
      FitCameraViewToParent(rotation);

      if (_landmarkOverlay != null)
      {
        _landmarkOverlay.rectTransform.localScale = Vector3.one;
      }
    }

    private void FitCameraViewToParent(global::Mediapipe.Unity.RotationAngle rotation)
    {
      var previewRect = cameraView.rectTransform.parent as RectTransform;
      if (previewRect == null || previewRect.rect.width <= 0f || previewRect.rect.height <= 0f)
      {
        return;
      }

      var displayWidth = _webCamTexture.width;
      var displayHeight = _webCamTexture.height;
      if (rotation == global::Mediapipe.Unity.RotationAngle.Rotation90 ||
          rotation == global::Mediapipe.Unity.RotationAngle.Rotation270)
      {
        (displayWidth, displayHeight) = (displayHeight, displayWidth);
      }

      var imageAspect = Mathf.Max(0.001f, (float)displayWidth / displayHeight);
      var containerWidth = previewRect.rect.width;
      var containerHeight = previewRect.rect.height;
      var containerAspect = containerWidth / containerHeight;

      var targetWidth = containerWidth;
      var targetHeight = containerHeight;
      if (containerAspect > imageAspect)
      {
        targetWidth = targetHeight * imageAspect;
      }
      else
      {
        targetHeight = targetWidth / imageAspect;
      }

      var rectTransform = cameraView.rectTransform;
      rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
      rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
      rectTransform.pivot = new Vector2(0.5f, 0.5f);
      rectTransform.anchoredPosition = Vector2.zero;
      rectTransform.localScale = Vector3.one;
      rectTransform.sizeDelta =
        rotation == global::Mediapipe.Unity.RotationAngle.Rotation90 ||
        rotation == global::Mediapipe.Unity.RotationAngle.Rotation270
          ? new Vector2(targetHeight, targetWidth)
          : new Vector2(targetWidth, targetHeight);
    }

    private static Rect FlipUvHorizontally(Rect rect)
    {
      return new Rect(1f - rect.x, rect.y, -rect.width, rect.height);
    }

    private static Rect FlipUvVertically(Rect rect)
    {
      return new Rect(rect.x, 1f - rect.y, rect.width, -rect.height);
    }

    private void SetStatus(string message)
    {
      _lastError = null;
      if (bottomStatusText != null)
      {
        bottomStatusText.text = message;
      }

      if (reportText != null)
      {
        reportText.text = message;
      }
    }

    private void SetError(string message)
    {
      _lastError = message;
      Debug.LogError($"[StudyMonitor] {message}");

      if (bottomStatusText != null)
      {
        bottomStatusText.text = message;
      }

      if (reportText != null)
      {
        reportText.text = message;
      }
    }

    private void LoadFaceProfiles()
    {
      if (!PlayerPrefs.HasKey(FaceProfilesPrefsKey))
      {
        return;
      }

      var json = PlayerPrefs.GetString(FaceProfilesPrefsKey);
      if (string.IsNullOrWhiteSpace(json))
      {
        return;
      }

      try
      {
        var store = JsonUtility.FromJson<FaceProfileStore>(json);
        registeredFaces = store?.profiles ?? new List<FaceProfile>();
      }
      catch (Exception exception)
      {
        Debug.LogWarning($"[StudyMonitor] 读取人脸录入数据失败：{exception.Message}");
      }
    }

    private void SaveFaceProfiles()
    {
      var store = new FaceProfileStore { profiles = registeredFaces };
      PlayerPrefs.SetString(FaceProfilesPrefsKey, JsonUtility.ToJson(store));
      PlayerPrefs.Save();
    }

    private bool IsVisible(TaskNormalizedLandmark landmark)
    {
      return IsPresent(landmark) &&
        (!landmark.visibility.HasValue || landmark.visibility.Value >= landmarkVisibilityThreshold);
    }

    private bool IsPresent(TaskNormalizedLandmark landmark)
    {
      if (!IsFinite(landmark.x) || !IsFinite(landmark.y) || !IsFinite(landmark.z))
      {
        return false;
      }

      if (landmark.presence.HasValue && landmark.presence.Value < landmarkPresenceThreshold)
      {
        return false;
      }

      return landmark.x >= -0.25f && landmark.x <= 1.25f &&
        landmark.y >= -0.25f && landmark.y <= 1.25f;
    }

    private float GetVisibleVerticalSpan(IReadOnlyList<TaskNormalizedLandmark> landmarks)
    {
      var minY = float.MaxValue;
      var maxY = float.MinValue;

      foreach (var landmark in landmarks)
      {
        if (!IsVisible(landmark))
        {
          continue;
        }

        minY = Mathf.Min(minY, landmark.y);
        maxY = Mathf.Max(maxY, landmark.y);
      }

      return minY == float.MaxValue ? 0f : maxY - minY;
    }

    private bool TryGetAngle(IReadOnlyList<TaskNormalizedLandmark> landmarks, int a, int b, int c, out float angle)
    {
      angle = 0f;
      if (!IsVisible(landmarks[a]) || !IsVisible(landmarks[b]) || !IsVisible(landmarks[c]))
      {
        return false;
      }

      var ba = new Vector2(landmarks[a].x - landmarks[b].x, landmarks[a].y - landmarks[b].y);
      var bc = new Vector2(landmarks[c].x - landmarks[b].x, landmarks[c].y - landmarks[b].y);
      angle = Vector2.Angle(ba, bc);
      return true;
    }

    private bool TryGetDistance(IReadOnlyList<TaskNormalizedLandmark> landmarks, int a, int b, out float distance)
    {
      distance = 0f;
      if (!IsVisible(landmarks[a]) || !IsVisible(landmarks[b]))
      {
        return false;
      }

      distance = Vector2.Distance(
        new Vector2(landmarks[a].x, landmarks[a].y),
        new Vector2(landmarks[b].x, landmarks[b].y));
      return true;
    }

    private void AddVisibleY(TaskNormalizedLandmark landmark, ref float total, ref int count)
    {
      if (!IsVisible(landmark))
      {
        return;
      }

      total += landmark.y;
      count++;
    }

    private float AverageVisibleY(TaskNormalizedLandmark a, TaskNormalizedLandmark b)
    {
      var total = 0f;
      var count = 0;
      if (IsVisible(a))
      {
        total += a.y;
        count++;
      }

      if (IsVisible(b))
      {
        total += b.y;
        count++;
      }

      return count == 0 ? 0f : total / count;
    }

    private static bool IsFinite(float value)
    {
      return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static float AveragePositive(float a, float b)
    {
      if (a < 0f && b < 0f)
      {
        return -1f;
      }

      if (a < 0f)
      {
        return b;
      }

      if (b < 0f)
      {
        return a;
      }

      return (a + b) * 0.5f;
    }

    private static float EuclideanDistance(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
      if (a == null || b == null || a.Count != b.Count)
      {
        return float.MaxValue;
      }

      var sum = 0f;
      for (var i = 0; i < a.Count; i++)
      {
        var delta = a[i] - b[i];
        sum += delta * delta;
      }

      return Mathf.Sqrt(sum / a.Count);
    }

    private static long GetTimestampMillis()
    {
      return (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
    }

    private static int NormalizeRotationDegrees(int degrees)
    {
      degrees %= 360;
      if (degrees < 0)
      {
        degrees += 360;
      }

      return degrees;
    }

    private static float NormalizeSignedAngle(float degrees)
    {
      degrees %= 360f;
      if (degrees > 180f)
      {
        degrees -= 360f;
      }
      else if (degrees < -180f)
      {
        degrees += 360f;
      }

      return degrees;
    }

    private static Font ResolveUiFont()
    {
      return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
        Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static string ToChinese(BodyPosture posture)
    {
      return posture switch
      {
        BodyPosture.Sitting => "坐姿",
        BodyPosture.Standing => "站姿",
        BodyPosture.Squatting => "蹲姿",
        _ => "未知",
      };
    }

    private static string YesNo(bool value)
    {
      return value ? "是" : "否";
    }
  }
}
