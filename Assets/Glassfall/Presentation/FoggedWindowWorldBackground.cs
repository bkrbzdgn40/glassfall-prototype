using Glassfall.Gameplay.Run;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

namespace Glassfall.Presentation
{
    public enum BackgroundSurfaceFitMode
    {
        CameraViewport = 0,
        WorldBounds = 1
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(VideoPlayer))]
    public sealed class FoggedWindowWorldBackground : MonoBehaviour
    {
        private const string RuntimeVideoSurfaceName = "FoggedWindowVideoSurface";
        private const string VideoTextureProperty = "_MainTex";

        private static readonly ProfilerMarker BackgroundCpuMarker =
            new ProfilerMarker("Glassfall.FoggedBackground.CPU");

        [Header("Background Video")]
        [SerializeField] private VideoClip backgroundClip;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MicroRunController progressSource;
        [Min(0)]
        [SerializeField] private int progressWorldSegmentIndex;
        [Range(0f, 1f)]
        [SerializeField] private float backgroundVideoOpacity = 1f;

        [Header("Runtime Video Surface")]
        [SerializeField] private MeshRenderer videoBackgroundRenderer;
        [SerializeField] private MeshFilter videoBackgroundMeshFilter;
        [SerializeField] private Shader videoBackgroundShader;
        [Range(0.5f, 1.1f)]
        [SerializeField] private float videoBrightness = 1f;
        [Range(0f, 1.2f)]
        [SerializeField] private float videoSaturation = 1f;
        [SerializeField] private Color videoTint = Color.white;
        [SerializeField] private int videoSortingOrder = -1100;
        [Min(1f)]
        [SerializeField] private float videoCoverage = 1.035f;
        [SerializeField] private float videoWorldZ = 6.2f;
        [SerializeField] private BackgroundSurfaceFitMode surfaceFitMode =
            BackgroundSurfaceFitMode.CameraViewport;
        [SerializeField] private Collider2D worldBounds;

        [Header("World Progress Blur - 3 Phases")]
        [Tooltip("Start of the world. Intentionally strong blur.")]
        [Range(0f, 12f)]
        [SerializeField] private float initialBlurRadiusPixels = 8f;
        [Range(0f, 1f)]
        [SerializeField] private float initialBlurMix = 0.98f;

        [Tooltip("After roughly the first third of encounter progress.")]
        [Range(0f, 12f)]
        [SerializeField] private float middleBlurRadiusPixels = 4f;
        [Range(0f, 1f)]
        [SerializeField] private float middleBlurMix = 0.78f;

        [Tooltip("Final third of world progress. The wet-glass video remains, but blur is light.")]
        [Range(0f, 12f)]
        [SerializeField] private float lateBlurRadiusPixels = 1.2f;
        [Range(0f, 1f)]
        [SerializeField] private float lateBlurMix = 0.30f;

        [Min(0.01f)]
        [SerializeField] private float blurTransitionSpeed = 2.6f;

        [Header("Performance")]
        [SerializeField] private bool enableVideoPlayback = true;
        [Min(0.016f)]
        [SerializeField] private float visualRefreshInterval = 0.033f;

        [Header("Video Failure Fallback")]
        [Min(0.5f)]
        [SerializeField] private float videoPrepareTimeoutSeconds = 5f;
        [SerializeField] private Color videoFailureFallbackColor =
            new Color(0.10f, 0.18f, 0.20f, 1f);

        private VideoPlayer _videoPlayer;
        private Material _runtimeVideoMaterial;
        private Mesh _runtimeQuadMesh;
        private Vector3 _lastCameraPosition;
        private float _lastOrthographicSize;
        private float _lastAspect;
        private float _nextVisualRefreshTime;
        private bool _hasCameraState;
        private bool _warnedMissingVideoShader;
        private bool _hasBlurState;
        private float _currentBlurRadius;
        private float _currentBlurMix;
        private int _highestCompletedEncounterCount;
        private int _lastWorldStartEncounter = -1;
        private int _lastWorldEncounterCount = -1;
        private bool _videoSurfaceReady;
        private bool _videoEventsSubscribed;
        private bool _videoFailureFallbackActive;
        private bool _warnedVideoFailure;
        private float _videoPrepareStartedAt = -1f;
        private Texture2D _runtimeFallbackTexture;

        public int CurrentBlurPhase { get; private set; }
        public float CurrentBlurRadiusPixels => _currentBlurRadius;
        public float CurrentBlurMix => _currentBlurMix;
        public bool IsVideoFailureFallbackActive =>
            _videoFailureFallbackActive;

        public static bool ShouldActivateVideoPrepareTimeoutFallback(
            bool isPrepared,
            bool surfaceReady,
            float elapsedSeconds,
            float timeoutSeconds)
        {
            return (!isPrepared || !surfaceReady) &&
                   !surfaceReady &&
                   elapsedSeconds >= Mathf.Max(0.5f, timeoutSeconds);
        }

        public void Configure(
            VideoClip clip,
            Camera camera,
            MicroRunController runProgress,
            float videoOpacity = 1f,
            bool videoPlaybackEnabled = true,
            float refreshInterval = 0.033f)
        {
            backgroundClip = clip;
            targetCamera = camera;
            progressSource = runProgress;
            backgroundVideoOpacity = Mathf.Clamp01(videoOpacity);
            enableVideoPlayback = videoPlaybackEnabled;
            visualRefreshInterval = Mathf.Max(0.016f, refreshInterval);

            CacheDependencies();
            ApplyConfiguration(playIfPossible: Application.isPlaying);
        }

        public void ConfigureWorldProgressBinding(int worldSegmentIndex)
        {
            progressWorldSegmentIndex = Mathf.Max(0, worldSegmentIndex);
            _highestCompletedEncounterCount = 0;
            _lastWorldStartEncounter = -1;
            _lastWorldEncounterCount = -1;
            _hasBlurState = false;
        }

        public void ConfigureVideoSurface(
            MeshRenderer renderer,
            MeshFilter meshFilter,
            Shader blurShader,
            float brightness = 1f,
            float saturation = 1f,
            Color? tint = null,
            int sortingOrder = -1100,
            float coverage = 1.035f,
            float worldZ = 6.2f)
        {
            videoBackgroundRenderer = renderer;
            videoBackgroundMeshFilter = meshFilter;
            videoBackgroundShader = blurShader;
            videoBrightness = Mathf.Clamp(brightness, 0.5f, 1.1f);
            videoSaturation = Mathf.Clamp(saturation, 0f, 1.2f);
            videoTint = NormalizeTint(tint ?? Color.white);
            videoSortingOrder = sortingOrder;
            videoCoverage = Mathf.Max(1f, coverage);
            videoWorldZ = worldZ;

            if (!Application.isPlaying && videoBackgroundRenderer != null)
                videoBackgroundRenderer.enabled = false;

            ApplyConfiguration(playIfPossible: Application.isPlaying);
        }

        public void ConfigureProgressiveBlur(
            float startRadiusPixels = 8f,
            float startMix = 0.98f,
            float midRadiusPixels = 4f,
            float midMix = 0.78f,
            float endRadiusPixels = 1.2f,
            float endMix = 0.30f,
            float transitionSpeed = 2.6f)
        {
            initialBlurRadiusPixels = Mathf.Clamp(startRadiusPixels, 0f, 12f);
            initialBlurMix = Mathf.Clamp01(startMix);
            middleBlurRadiusPixels = Mathf.Clamp(midRadiusPixels, 0f, 12f);
            middleBlurMix = Mathf.Clamp01(midMix);
            lateBlurRadiusPixels = Mathf.Clamp(endRadiusPixels, 0f, 12f);
            lateBlurMix = Mathf.Clamp01(endMix);
            blurTransitionSpeed = Mathf.Max(0.01f, transitionSpeed);

            _hasBlurState = false;
            UpdateProgressiveBlur(0f);
            UpdateVideoMaterialProperties();
        }

        public void ConfigureWorldSurface(
            Collider2D bounds,
            BackgroundSurfaceFitMode fitMode = BackgroundSurfaceFitMode.WorldBounds,
            float coverage = 1.02f)
        {
            worldBounds = bounds;
            surfaceFitMode = fitMode;
            videoCoverage = Mathf.Max(1f, coverage);
            FitVideoSurface();
        }

        private void Awake()
        {
            CacheDependencies();
            DisableLegacyOverlayChildren();
            ApplyConfiguration(playIfPossible: false);
        }

        private void OnEnable()
        {
            CacheDependencies();
            DisableLegacyOverlayChildren();
            ApplyConfiguration(playIfPossible: Application.isPlaying);
        }

        private void OnDisable()
        {
            UnsubscribeVideoEvents();
            _videoSurfaceReady = false;
            _videoFailureFallbackActive = false;
            _videoPrepareStartedAt = -1f;

            if (_videoPlayer != null && _videoPlayer.isPlaying)
                _videoPlayer.Stop();

            ReleaseRuntimeVideoResources();
        }

        private void LateUpdate()
        {
            using (BackgroundCpuMarker.Auto())
            {
                if (targetCamera == null || progressSource == null)
                    CacheDependencies();

                UpdateProgressiveBlur(Time.unscaledDeltaTime);
                UpdateVideoMaterialProperties();
                CheckVideoPreparationTimeout();
                TryRevealPreparedVideoSurface();

                if (targetCamera == null)
                    return;

                bool cameraChanged = HasCameraStateChanged();
                if (!cameraChanged && Time.unscaledTime < _nextVisualRefreshTime)
                    return;

                FitVideoSurface();
                CaptureCameraState();
                _nextVisualRefreshTime = Time.unscaledTime +
                    Mathf.Max(visualRefreshInterval, 0.016f);
            }
        }

        private void CacheDependencies()
        {
            if (_videoPlayer == null)
                _videoPlayer = GetComponent<VideoPlayer>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            if (progressSource == null)
                progressSource = FindAnyObjectByType<MicroRunController>();

            if (videoBackgroundRenderer == null || videoBackgroundMeshFilter == null)
            {
                Transform surface = transform.Find(RuntimeVideoSurfaceName);
                if (surface != null)
                {
                    if (videoBackgroundRenderer == null)
                        videoBackgroundRenderer = surface.GetComponent<MeshRenderer>();
                    if (videoBackgroundMeshFilter == null)
                        videoBackgroundMeshFilter = surface.GetComponent<MeshFilter>();
                }
            }
        }

        private void ApplyConfiguration(bool playIfPossible)
        {
            bool shouldPlayVideo = enableVideoPlayback && backgroundClip != null;

            DisableLegacyOverlayChildren();
            UpdateProgressiveBlur(0f);

            if (Application.isPlaying && enableVideoPlayback)
                EnsureRuntimeVideoSurface();

            if (_videoPlayer != null)
            {
                _videoPlayer.source = VideoSource.VideoClip;
                _videoPlayer.clip = backgroundClip;
                _videoPlayer.aspectRatio = VideoAspectRatio.FitOutside;
                _videoPlayer.isLooping = true;
                // Playback is owned explicitly so the material surface stays
                // hidden until the first decoded frame is actually available.
                _videoPlayer.playOnAwake = false;
                _videoPlayer.waitForFirstFrame = true;
                _videoPlayer.skipOnDrop = true;
                _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

                if (Application.isPlaying &&
                    shouldPlayVideo &&
                    videoBackgroundRenderer != null &&
                    _runtimeVideoMaterial != null)
                {
                    _videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
                    _videoPlayer.targetMaterialRenderer = videoBackgroundRenderer;
                    _videoPlayer.targetMaterialProperty = VideoTextureProperty;
                    _videoPlayer.targetCamera = null;
                    _videoPlayer.targetCameraAlpha = 1f;
                    videoBackgroundRenderer.enabled = _videoSurfaceReady;
                }
                else
                {
                    // Editor preview/fallback only. Runtime uses the material surface above.
                    _videoPlayer.renderMode = VideoRenderMode.CameraFarPlane;
                    _videoPlayer.targetCamera = targetCamera;
                    _videoPlayer.targetCameraAlpha = backgroundVideoOpacity;
                    if (videoBackgroundRenderer != null)
                        videoBackgroundRenderer.enabled = false;
                }

                if (!shouldPlayVideo)
                {
                    UnsubscribeVideoEvents();
                    _videoSurfaceReady = false;
                    if (_videoPlayer.isPlaying)
                        _videoPlayer.Stop();
                    _videoPlayer.enabled = false;

                    if (Application.isPlaying &&
                        enableVideoPlayback &&
                        backgroundClip == null)
                    {
                        ActivateVideoFailureFallback(
                            "Background video clip is missing.");
                    }
                    else if (videoBackgroundRenderer != null)
                    {
                        videoBackgroundRenderer.enabled = false;
                    }
                }
                else
                {
                    _videoPlayer.enabled = true;
                    if (playIfPossible)
                        BeginPreparedVideoPlayback();
                }
            }

            FitVideoSurface();
            UpdateVideoMaterialProperties();
            CaptureCameraState();
        }

        private void EnsureRuntimeVideoSurface()
        {
            if (!Application.isPlaying)
                return;

            if (videoBackgroundRenderer == null || videoBackgroundMeshFilter == null)
            {
                Transform existing = transform.Find(RuntimeVideoSurfaceName);
                GameObject surfaceObject;
                if (existing != null)
                {
                    surfaceObject = existing.gameObject;
                }
                else
                {
                    surfaceObject = new GameObject(RuntimeVideoSurfaceName);
                    surfaceObject.transform.SetParent(transform, false);
                }

                if (videoBackgroundMeshFilter == null)
                {
                    videoBackgroundMeshFilter = surfaceObject.GetComponent<MeshFilter>();
                    if (videoBackgroundMeshFilter == null)
                        videoBackgroundMeshFilter = surfaceObject.AddComponent<MeshFilter>();
                }

                if (videoBackgroundRenderer == null)
                {
                    videoBackgroundRenderer = surfaceObject.GetComponent<MeshRenderer>();
                    if (videoBackgroundRenderer == null)
                        videoBackgroundRenderer = surfaceObject.AddComponent<MeshRenderer>();
                }
            }

            if (videoBackgroundMeshFilter.sharedMesh == null)
            {
                _runtimeQuadMesh = CreateRuntimeQuadMesh();
                videoBackgroundMeshFilter.sharedMesh = _runtimeQuadMesh;
            }

            if (_runtimeVideoMaterial == null)
            {
                Shader shader = videoBackgroundShader != null
                    ? videoBackgroundShader
                    : Shader.Find("Glassfall/VideoBackgroundBlur");

                if (shader == null)
                {
                    if (!_warnedMissingVideoShader)
                    {
                        Debug.LogWarning(
                            "[Glassfall] VideoBackgroundBlur shader bulunamadı. " +
                            "Video CameraFarPlane fallback ile oynatılacak.",
                            this);
                        _warnedMissingVideoShader = true;
                    }
                    return;
                }

                _runtimeVideoMaterial = new Material(shader)
                {
                    name = "FoggedWindowVideoRuntimeMaterial"
                };
            }

            videoBackgroundRenderer.sharedMaterial = _runtimeVideoMaterial;
            videoBackgroundRenderer.sortingOrder = videoSortingOrder;
            videoBackgroundRenderer.shadowCastingMode = ShadowCastingMode.Off;
            videoBackgroundRenderer.receiveShadows = false;
            videoBackgroundRenderer.lightProbeUsage = LightProbeUsage.Off;
            videoBackgroundRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            videoBackgroundRenderer.enabled = _videoSurfaceReady;

            UpdateVideoMaterialProperties();
            FitVideoSurface();
        }

        private void BeginPreparedVideoPlayback()
        {
            if (_videoPlayer == null ||
                !enableVideoPlayback ||
                backgroundClip == null)
            {
                return;
            }

            _videoSurfaceReady = false;
            _videoFailureFallbackActive = false;
            _videoPrepareStartedAt = Time.unscaledTime;
            if (videoBackgroundRenderer != null)
                videoBackgroundRenderer.enabled = false;

            SubscribeVideoEvents();
            _videoPlayer.sendFrameReadyEvents = true;

            if (_videoPlayer.isPrepared)
            {
                if (!_videoPlayer.isPlaying)
                    _videoPlayer.Play();
                return;
            }

            try
            {
                _videoPlayer.Prepare();
            }
            catch (System.Exception exception)
            {
                ActivateVideoFailureFallback(
                    "Video prepare threw an exception: " + exception.Message);
            }
        }

        private void SubscribeVideoEvents()
        {
            if (_videoPlayer == null || _videoEventsSubscribed)
                return;

            _videoPlayer.prepareCompleted += HandleVideoPrepared;
            _videoPlayer.frameReady += HandleVideoFrameReady;
            _videoPlayer.errorReceived += HandleVideoError;
            _videoEventsSubscribed = true;
        }

        private void UnsubscribeVideoEvents()
        {
            if (_videoPlayer == null || !_videoEventsSubscribed)
                return;

            _videoPlayer.prepareCompleted -= HandleVideoPrepared;
            _videoPlayer.frameReady -= HandleVideoFrameReady;
            _videoPlayer.errorReceived -= HandleVideoError;
            _videoPlayer.sendFrameReadyEvents = false;
            _videoEventsSubscribed = false;
        }

        private void HandleVideoPrepared(VideoPlayer source)
        {
            if (source == null ||
                source != _videoPlayer ||
                !isActiveAndEnabled ||
                !enableVideoPlayback ||
                backgroundClip == null)
            {
                return;
            }

            if (!source.isPlaying)
                source.Play();
        }

        private void HandleVideoFrameReady(VideoPlayer source, long frameIndex)
        {
            if (source == null || source != _videoPlayer || frameIndex < 0)
                return;

            RevealVideoSurface();
        }

        private void HandleVideoError(VideoPlayer source, string message)
        {
            if (source == null || source != _videoPlayer)
                return;

            ActivateVideoFailureFallback(
                string.IsNullOrWhiteSpace(message)
                    ? "VideoPlayer reported an unknown error."
                    : message);
        }

        private void CheckVideoPreparationTimeout()
        {
            if (_videoFailureFallbackActive ||
                _videoPlayer == null ||
                _videoPrepareStartedAt < 0f ||
                !enableVideoPlayback ||
                backgroundClip == null)
            {
                return;
            }

            float elapsed = Time.unscaledTime - _videoPrepareStartedAt;
            if (ShouldActivateVideoPrepareTimeoutFallback(
                    _videoPlayer.isPrepared,
                    _videoSurfaceReady,
                    elapsed,
                    videoPrepareTimeoutSeconds))
            {
                ActivateVideoFailureFallback(
                    $"Video preparation timed out after {elapsed:0.0}s.");
            }
        }

        private void ActivateVideoFailureFallback(string reason)
        {
            if (_videoFailureFallbackActive)
                return;

            _videoFailureFallbackActive = true;
            _videoSurfaceReady = true;
            _videoPrepareStartedAt = -1f;

            UnsubscribeVideoEvents();
            if (_videoPlayer != null)
            {
                if (_videoPlayer.isPlaying)
                    _videoPlayer.Stop();
                _videoPlayer.enabled = false;
            }

            if (_runtimeVideoMaterial != null)
            {
                EnsureRuntimeFallbackTexture();
                _runtimeVideoMaterial.SetTexture(
                    VideoTextureProperty,
                    _runtimeFallbackTexture);
            }

            if (videoBackgroundRenderer != null &&
                _runtimeVideoMaterial != null)
            {
                videoBackgroundRenderer.enabled = true;
            }

            if (!_warnedVideoFailure)
            {
                Debug.LogWarning(
                    "[Glassfall] Fogged-window video unavailable; " +
                    "using the static fog background fallback. " + reason,
                    this);
                _warnedVideoFailure = true;
            }
        }

        private void EnsureRuntimeFallbackTexture()
        {
            if (_runtimeFallbackTexture != null)
                return;

            Color baseColor = NormalizeTint(videoFailureFallbackColor);
            Color lighter = Color.Lerp(baseColor, Color.white, 0.08f);
            _runtimeFallbackTexture = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false)
            {
                name = "FoggedWindowStaticFallback",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            _runtimeFallbackTexture.SetPixels(new[]
            {
                baseColor,
                lighter,
                lighter,
                baseColor
            });
            _runtimeFallbackTexture.Apply(false, true);
        }

        private void TryRevealPreparedVideoSurface()
        {
            if (_videoSurfaceReady ||
                _videoPlayer == null ||
                !_videoPlayer.isPlaying ||
                _videoPlayer.texture == null ||
                _videoPlayer.frame < 0)
            {
                return;
            }

            RevealVideoSurface();
        }

        private void RevealVideoSurface()
        {
            _videoFailureFallbackActive = false;
            _videoPrepareStartedAt = -1f;
            _videoSurfaceReady = true;
            if (videoBackgroundRenderer != null &&
                _runtimeVideoMaterial != null)
            {
                videoBackgroundRenderer.enabled = true;
            }

            if (_videoPlayer != null)
                _videoPlayer.sendFrameReadyEvents = false;
        }

        private void UpdateProgressiveBlur(float deltaTime)
        {
            int phase = ResolveBlurPhase();
            CurrentBlurPhase = phase;

            float targetRadius;
            float targetMix;
            switch (phase)
            {
                case 1:
                    targetRadius = middleBlurRadiusPixels;
                    targetMix = middleBlurMix;
                    break;
                case 2:
                    targetRadius = lateBlurRadiusPixels;
                    targetMix = lateBlurMix;
                    break;
                default:
                    targetRadius = initialBlurRadiusPixels;
                    targetMix = initialBlurMix;
                    break;
            }

            if (!_hasBlurState || deltaTime <= 0f)
            {
                _currentBlurRadius = targetRadius;
                _currentBlurMix = targetMix;
                _hasBlurState = true;
                return;
            }

            float blend = 1f - Mathf.Exp(-blurTransitionSpeed * Mathf.Max(0f, deltaTime));
            _currentBlurRadius = Mathf.Lerp(_currentBlurRadius, targetRadius, blend);
            _currentBlurMix = Mathf.Lerp(_currentBlurMix, targetMix, blend);
        }

        private int ResolveBlurPhase()
        {
            if (progressSource == null)
                return 0;

            int activeWorldSegmentIndex =
                progressSource.CurrentWorldSegmentIndex;
            if (activeWorldSegmentIndex < progressWorldSegmentIndex)
                return 0;
            if (activeWorldSegmentIndex > progressWorldSegmentIndex)
                return 2;

            int encounterCount = Mathf.Max(
                1,
                progressSource.CurrentWorldEncounterCount);
            int worldStartEncounter = progressSource.CurrentWorldStartEncounter;
            if (_lastWorldStartEncounter != worldStartEncounter ||
                _lastWorldEncounterCount != encounterCount)
            {
                _lastWorldStartEncounter = worldStartEncounter;
                _lastWorldEncounterCount = encounterCount;
                _highestCompletedEncounterCount = 0;
            }

            int completedEncounterCount = Mathf.Clamp(
                progressSource.CompletedCurrentWorldEncounterCount,
                0,
                encounterCount);
            _highestCompletedEncounterCount = Mathf.Max(
                _highestCompletedEncounterCount,
                completedEncounterCount);

            float completion01 = Mathf.Clamp01(
                _highestCompletedEncounterCount / (float)encounterCount);

            if (completion01 >= (2f / 3f))
                return 2;
            if (completion01 >= (1f / 3f))
                return 1;
            return 0;
        }

        private static Mesh CreateRuntimeQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "FoggedWindowRuntimeQuad"
            };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void UpdateVideoMaterialProperties()
        {
            if (_runtimeVideoMaterial == null)
                return;

            _runtimeVideoMaterial.SetFloat("_BlurRadius", _currentBlurRadius);
            _runtimeVideoMaterial.SetFloat("_BlurMix", _currentBlurMix);
            _runtimeVideoMaterial.SetFloat("_Brightness", videoBrightness);
            _runtimeVideoMaterial.SetFloat("_Saturation", videoSaturation);
            _runtimeVideoMaterial.SetFloat("_Opacity", backgroundVideoOpacity);
            _runtimeVideoMaterial.SetColor("_Tint", NormalizeTint(videoTint));
            _runtimeVideoMaterial.SetFloat(
                "_TargetAspect",
                targetCamera != null ? Mathf.Max(0.01f, targetCamera.aspect) : (16f / 9f));
        }

        private void ReleaseRuntimeVideoResources()
        {
            if (videoBackgroundRenderer != null)
                videoBackgroundRenderer.enabled = false;

            if (_runtimeVideoMaterial != null)
            {
                Destroy(_runtimeVideoMaterial);
                _runtimeVideoMaterial = null;
            }

            if (_runtimeQuadMesh != null)
            {
                if (videoBackgroundMeshFilter != null &&
                    videoBackgroundMeshFilter.sharedMesh == _runtimeQuadMesh)
                {
                    videoBackgroundMeshFilter.sharedMesh = null;
                }

                Destroy(_runtimeQuadMesh);
                _runtimeQuadMesh = null;
            }

            if (_runtimeFallbackTexture != null)
            {
                Destroy(_runtimeFallbackTexture);
                _runtimeFallbackTexture = null;
            }
        }

        private void DisableLegacyOverlayChildren()
        {
            DisableLegacyChild("FoggedWindowFarBlur");
            DisableLegacyChild("FoggedWindowMidBlur");
            DisableLegacyChild("FoggedWindowNearBlur");
            DisableLegacyChild("FoggedGlassOverlay");
            DisableLegacyChild("FoggedGlassStreakOverlay");
        }

        private void DisableLegacyChild(string childName)
        {
            Transform child = transform.Find(childName);
            if (child != null && child.gameObject.activeSelf)
                child.gameObject.SetActive(false);
        }

        private static Color NormalizeTint(Color tint)
        {
            return new Color(
                Mathf.Clamp01(tint.r),
                Mathf.Clamp01(tint.g),
                Mathf.Clamp01(tint.b),
                Mathf.Clamp01(tint.a));
        }

        private bool HasCameraStateChanged()
        {
            if (!_hasCameraState || targetCamera == null)
                return true;

            Vector3 position = targetCamera.transform.position;
            return (position - _lastCameraPosition).sqrMagnitude > 0.000001f ||
                   !Mathf.Approximately(targetCamera.orthographicSize, _lastOrthographicSize) ||
                   !Mathf.Approximately(targetCamera.aspect, _lastAspect);
        }

        private void CaptureCameraState()
        {
            if (targetCamera == null)
                return;

            _lastCameraPosition = targetCamera.transform.position;
            _lastOrthographicSize = targetCamera.orthographicSize;
            _lastAspect = targetCamera.aspect;
            _hasCameraState = true;
        }

        private void FitVideoSurface()
        {
            if (videoBackgroundRenderer == null || targetCamera == null || !targetCamera.orthographic)
                return;

            switch (surfaceFitMode)
            {
                case BackgroundSurfaceFitMode.WorldBounds:
                    if (TryFitVideoSurfaceToWorldBounds())
                        return;
                    break;
            }

            FitVideoSurfaceToCameraViewport();
        }

        private void FitVideoSurfaceToCameraViewport()
        {
            Transform surfaceTransform = videoBackgroundRenderer.transform;
            Vector3 cameraPosition = targetCamera.transform.position;
            surfaceTransform.position = new Vector3(
                cameraPosition.x,
                cameraPosition.y,
                videoWorldZ);
            surfaceTransform.rotation = Quaternion.identity;

            float targetHeight = targetCamera.orthographicSize * 2f * videoCoverage;
            float targetWidth = targetHeight * targetCamera.aspect;
            surfaceTransform.localScale = new Vector3(targetWidth, targetHeight, 1f);
        }

        private bool TryFitVideoSurfaceToWorldBounds()
        {
            if (worldBounds == null)
                return false;

            Bounds bounds = worldBounds.bounds;
            if (bounds.size.x <= 0.001f || bounds.size.y <= 0.001f)
                return false;

            Transform surfaceTransform = videoBackgroundRenderer.transform;
            surfaceTransform.position = new Vector3(
                bounds.center.x,
                bounds.center.y,
                videoWorldZ);
            surfaceTransform.rotation = Quaternion.identity;
            surfaceTransform.localScale = new Vector3(
                bounds.size.x * videoCoverage,
                bounds.size.y * videoCoverage,
                1f);
            return true;
        }

        private void OnValidate()
        {
            progressWorldSegmentIndex = Mathf.Max(0, progressWorldSegmentIndex);
            backgroundVideoOpacity = Mathf.Clamp01(backgroundVideoOpacity);
            videoCoverage = Mathf.Max(1f, videoCoverage);
            if (worldBounds == null)
            {
                GameObject area = GameObject.Find("NeurtalSpawnArea");
                if (area == null)
                    area = GameObject.Find("NeutralSpawnArea");
                if (area != null)
                    worldBounds = area.GetComponent<Collider2D>();
            }
            initialBlurRadiusPixels = Mathf.Clamp(initialBlurRadiusPixels, 0f, 12f);
            initialBlurMix = Mathf.Clamp01(initialBlurMix);
            middleBlurRadiusPixels = Mathf.Clamp(middleBlurRadiusPixels, 0f, 12f);
            middleBlurMix = Mathf.Clamp01(middleBlurMix);
            lateBlurRadiusPixels = Mathf.Clamp(lateBlurRadiusPixels, 0f, 12f);
            lateBlurMix = Mathf.Clamp01(lateBlurMix);
            blurTransitionSpeed = Mathf.Max(0.01f, blurTransitionSpeed);
            visualRefreshInterval = Mathf.Max(visualRefreshInterval, 0.016f);
            videoPrepareTimeoutSeconds = Mathf.Max(
                0.5f,
                videoPrepareTimeoutSeconds);
            videoFailureFallbackColor = NormalizeTint(
                videoFailureFallbackColor);
        }
    }
}
