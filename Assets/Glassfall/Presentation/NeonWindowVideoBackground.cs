using Glassfall.Gameplay.Run;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VideoPlayer))]
    public sealed class NeonWindowVideoBackground : MonoBehaviour
    {
        private const string NeonWorldId = "NEON_WINDOW";
        private const string VideoSurfaceName = "NeonWindowVideoSurface";
        private const string VideoTextureProperty = "_MainTex";
        private const float SourceAspect = 16f / 9f;
        private const string FoggedGlassShaderName =
            "Glassfall/VideoBackgroundBlur";

        [Header("References")]
        [SerializeField] private VideoClip backgroundClip;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Collider2D worldBounds;
        [SerializeField] private MicroRunController runController;
        [SerializeField] private MeshRenderer videoRenderer;
        [SerializeField] private MeshFilter videoMeshFilter;
        [SerializeField] private Shader videoShader;

        [Header("Presentation")]
        [Range(0f, 1f)]
        [SerializeField] private float opacity = 0.70f;
        [Range(0.5f, 1.1f)]
        [SerializeField] private float brightness = 0.86f;
        [Range(0f, 1.2f)]
        [SerializeField] private float saturation = 0.90f;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private int sortingOrder = -1091;
        [SerializeField] private float worldZ = 6.0f;
        [Range(0.90f, 1f)]
        [SerializeField] private float boundsCoverage = 0.995f;

        [Header("Shared Fogged Glass Treatment")]
        [SerializeField] private bool useFoggedGlassTreatment = true;
        [Range(0f, 12f)]
        [SerializeField] private float initialBlurRadiusPixels = 8f;
        [Range(0f, 1f)]
        [SerializeField] private float initialBlurMix = 0.98f;
        [Range(0f, 12f)]
        [SerializeField] private float middleBlurRadiusPixels = 4f;
        [Range(0f, 1f)]
        [SerializeField] private float middleBlurMix = 0.78f;
        [Range(0f, 12f)]
        [SerializeField] private float lateBlurRadiusPixels = 1.2f;
        [Range(0f, 1f)]
        [SerializeField] private float lateBlurMix = 0.30f;
        [Min(0.01f)]
        [SerializeField] private float blurTransitionSpeed = 2.6f;
        [Min(0)]
        [SerializeField] private int progressWorldSegmentIndex = 1;

        [Header("Readiness")]
        [Min(0.5f)]
        [SerializeField] private float prepareTimeoutSeconds = 5f;

        private VideoPlayer _videoPlayer;
        private NeonWindowWorldPresentation _proceduralPresentation;
        private Material _runtimeMaterial;
        private Mesh _runtimeMesh;
        private bool _eventsSubscribed;
        private bool _playbackRequested;
        private bool _surfaceReady;
        private bool _failureFallbackActive;
        private bool _warnedFailure;
        private float _prepareStartedAt = -1f;
        private bool _hasBlurState;
        private float _currentBlurRadius;
        private float _currentBlurMix;
        private int _highestCompletedEncounterCount;
        private int _lastWorldStartEncounter = -1;
        private int _lastWorldEncounterCount = -1;

        public bool IsPlaybackRequested => _playbackRequested;
        public bool IsVideoSurfaceReady => _surfaceReady;
        public bool IsVideoFailureFallbackActive => _failureFallbackActive;
        // Kept for compatibility with older diagnostics. The former custom
        // NeonWindowGlassOverlay is no longer instantiated or rendered.
        public bool IsGlassOverlayVisible => false;
        public bool HasProceduralFallback =>
            GetComponent<NeonWindowWorldPresentation>() != null;

        public static bool ShouldPlayForWorld(
            string worldId,
            bool presentationActive,
            bool hasClip)
        {
            return presentationActive &&
                   hasClip &&
                   string.Equals(
                       worldId,
                       NeonWorldId,
                       System.StringComparison.Ordinal);
        }

        public static bool ShouldRevealFirstFrame(
            bool playbackRequested,
            bool hasTexture,
            long frameIndex,
            bool hasFailure)
        {
            return playbackRequested &&
                   hasTexture &&
                   frameIndex >= 0 &&
                   !hasFailure;
        }

        public void ConfigureFoggedGlassTreatment(
            bool enabled = true,
            int worldSegmentIndex = 1)
        {
            useFoggedGlassTreatment = enabled;
            progressWorldSegmentIndex = Mathf.Max(0, worldSegmentIndex);
            ResetProgressiveGlassState();
            ApplyMaterialProperties();
        }

        public void Configure(
            VideoClip clip,
            Camera camera,
            Collider2D bounds,
            MicroRunController run,
            MeshRenderer renderer,
            MeshFilter meshFilter,
            Shader shader,
            float videoOpacity = 0.70f,
            float videoBrightness = 0.86f,
            float videoSaturation = 0.90f,
            Color? videoTint = null,
            int videoSortingOrder = -1091,
            float videoWorldZ = 6.0f,
            float coverage = 0.995f)
        {
            backgroundClip = clip;
            targetCamera = camera;
            worldBounds = bounds;
            runController = run;
            videoRenderer = renderer;
            videoMeshFilter = meshFilter;
            videoShader = shader;
            opacity = Mathf.Clamp01(videoOpacity);
            brightness = Mathf.Clamp(videoBrightness, 0.5f, 1.1f);
            saturation = Mathf.Clamp(videoSaturation, 0f, 1.2f);
            tint = NormalizeTint(videoTint ?? Color.white);
            sortingOrder = videoSortingOrder;
            worldZ = videoWorldZ;
            boundsCoverage = Mathf.Clamp(coverage, 0.90f, 1f);

            CacheDependencies();
            ResetProgressiveGlassState();
            ApplyMaterialProperties();
            FitSurfacePreservingAspect();

            if (!Application.isPlaying && videoRenderer != null)
                videoRenderer.enabled = false;

            if (Application.isPlaying && isActiveAndEnabled)
                RefreshPlaybackForCurrentWorld();
        }

        private void Awake()
        {
            ResetProgressiveGlassState();
            CacheDependencies();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            CacheDependencies();
            RefreshPlaybackForCurrentWorld();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            bool shouldPlay = ShouldPlayForWorld(
                runController != null ? runController.CurrentWorldId : null,
                isActiveAndEnabled,
                backgroundClip != null);
            UpdateProgressiveGlass(Time.unscaledDeltaTime);
            ApplyMaterialProperties();
            if (shouldPlay && !_playbackRequested)
                BeginPreparedPlayback();
            else if (!shouldPlay && _playbackRequested)
                StopPlayback();

            CheckPrepareTimeout();
            TryRevealPreparedSurface();
        }

        private void OnDisable()
        {
            StopPlayback();
            ReleaseRuntimeResources();
        }

        private void OnDestroy()
        {
            StopPlayback();
            ReleaseRuntimeResources();
        }

        private void CacheDependencies()
        {
            if (_videoPlayer == null)
                _videoPlayer = GetComponent<VideoPlayer>();
            if (_proceduralPresentation == null)
            {
                _proceduralPresentation =
                    GetComponent<NeonWindowWorldPresentation>();
            }
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (runController == null)
                runController = FindAnyObjectByType<MicroRunController>();

            if (videoRenderer == null || videoMeshFilter == null)
            {
                Transform surface = transform.Find(VideoSurfaceName);
                if (surface != null)
                {
                    if (videoRenderer == null)
                        videoRenderer = surface.GetComponent<MeshRenderer>();
                    if (videoMeshFilter == null)
                        videoMeshFilter = surface.GetComponent<MeshFilter>();
                }
            }
        }

        private void RefreshPlaybackForCurrentWorld()
        {
            string worldId = runController != null
                ? runController.CurrentWorldId
                : string.Empty;
            bool isNeonPresentation = string.Equals(
                worldId,
                NeonWorldId,
                System.StringComparison.Ordinal);

            if (!isNeonPresentation)
            {
                StopPlayback();
                return;
            }

            if (backgroundClip == null)
            {
                ActivateFailureFallback("Neon background video clip is missing.");
                return;
            }

            BeginPreparedPlayback();
        }

        private void BeginPreparedPlayback()
        {
            if (_playbackRequested || backgroundClip == null)
                return;

            EnsureRuntimeSurface();
            if (_videoPlayer == null ||
                videoRenderer == null ||
                _runtimeMaterial == null)
            {
                ActivateFailureFallback(
                    "Neon video surface could not be initialized.");
                return;
            }

            _playbackRequested = true;
            _surfaceReady = false;
            _failureFallbackActive = false;
            _prepareStartedAt = Time.unscaledTime;
            SetProceduralFallbackVisible(true);
            videoRenderer.enabled = false;

            _videoPlayer.enabled = true;
            _videoPlayer.source = VideoSource.VideoClip;
            _videoPlayer.clip = backgroundClip;
            _videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            _videoPlayer.targetMaterialRenderer = videoRenderer;
            _videoPlayer.targetMaterialProperty = VideoTextureProperty;
            _videoPlayer.targetCamera = null;
            _videoPlayer.aspectRatio = useFoggedGlassTreatment
                ? VideoAspectRatio.FitOutside
                : VideoAspectRatio.FitInside;
            _videoPlayer.isLooping = true;
            _videoPlayer.playOnAwake = false;
            _videoPlayer.waitForFirstFrame = true;
            _videoPlayer.skipOnDrop = true;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            _videoPlayer.sendFrameReadyEvents = true;
            SubscribeEvents();

            if (_videoPlayer.isPrepared)
            {
                _videoPlayer.Play();
                return;
            }

            try
            {
                _videoPlayer.Prepare();
            }
            catch (System.Exception exception)
            {
                ActivateFailureFallback(
                    "Neon video prepare failed: " + exception.Message);
            }
        }

        private void StopPlayback()
        {
            UnsubscribeEvents();
            _playbackRequested = false;
            _surfaceReady = false;
            _prepareStartedAt = -1f;
            ResetProgressiveGlassState();
            SetProceduralFallbackVisible(true);

            if (videoRenderer != null)
                videoRenderer.enabled = false;
            if (_videoPlayer == null)
                return;

            if (_videoPlayer.isPlaying)
                _videoPlayer.Stop();
            else if (_videoPlayer.isPrepared)
                _videoPlayer.Stop();
            _videoPlayer.enabled = false;
        }

        private void SubscribeEvents()
        {
            if (_videoPlayer == null || _eventsSubscribed)
                return;

            _videoPlayer.prepareCompleted += HandlePrepared;
            _videoPlayer.frameReady += HandleFrameReady;
            _videoPlayer.errorReceived += HandleError;
            _eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (_videoPlayer == null || !_eventsSubscribed)
                return;

            _videoPlayer.prepareCompleted -= HandlePrepared;
            _videoPlayer.frameReady -= HandleFrameReady;
            _videoPlayer.errorReceived -= HandleError;
            _videoPlayer.sendFrameReadyEvents = false;
            _eventsSubscribed = false;
        }

        private void HandlePrepared(VideoPlayer source)
        {
            if (source == null ||
                source != _videoPlayer ||
                !_playbackRequested ||
                !isActiveAndEnabled)
            {
                return;
            }

            if (!source.isPlaying)
                source.Play();
        }

        private void HandleFrameReady(VideoPlayer source, long frameIndex)
        {
            if (source == null || source != _videoPlayer)
                return;

            if (ShouldRevealFirstFrame(
                    _playbackRequested,
                    source.texture != null,
                    frameIndex,
                    _failureFallbackActive))
            {
                RevealSurface();
            }
        }

        private void HandleError(VideoPlayer source, string message)
        {
            if (source == null || source != _videoPlayer)
                return;

            ActivateFailureFallback(
                string.IsNullOrWhiteSpace(message)
                    ? "VideoPlayer reported an unknown error."
                    : message);
        }

        private void TryRevealPreparedSurface()
        {
            if (_surfaceReady ||
                _videoPlayer == null ||
                !_videoPlayer.isPlaying)
            {
                return;
            }

            if (ShouldRevealFirstFrame(
                    _playbackRequested,
                    _videoPlayer.texture != null,
                    _videoPlayer.frame,
                    _failureFallbackActive))
            {
                RevealSurface();
            }
        }

        private void RevealSurface()
        {
            _surfaceReady = true;
            _prepareStartedAt = -1f;
            if (videoRenderer != null && _runtimeMaterial != null)
                videoRenderer.enabled = true;
            SetProceduralFallbackVisible(false);
            if (_videoPlayer != null)
                _videoPlayer.sendFrameReadyEvents = false;
        }

        private void CheckPrepareTimeout()
        {
            if (!_playbackRequested ||
                _surfaceReady ||
                _failureFallbackActive ||
                _prepareStartedAt < 0f)
            {
                return;
            }

            float elapsed = Time.unscaledTime - _prepareStartedAt;
            if (elapsed >= Mathf.Max(0.5f, prepareTimeoutSeconds))
            {
                ActivateFailureFallback(
                    $"Neon video preparation timed out after {elapsed:0.0}s.");
            }
        }

        private void ActivateFailureFallback(string reason)
        {
            _failureFallbackActive = true;
            _surfaceReady = false;
            _playbackRequested = false;
            _prepareStartedAt = -1f;
            SetProceduralFallbackVisible(true);
            UnsubscribeEvents();

            if (videoRenderer != null)
                videoRenderer.enabled = false;
            if (_videoPlayer != null)
            {
                if (_videoPlayer.isPlaying)
                    _videoPlayer.Stop();
                _videoPlayer.enabled = false;
            }

            if (!_warnedFailure)
            {
                Debug.LogWarning(
                    "[Glassfall] Neon-window video unavailable; " +
                    "keeping the procedural Neon presentation. " + reason,
                    this);
                _warnedFailure = true;
            }
        }

        private void EnsureRuntimeSurface()
        {
            CacheDependencies();
            if (videoRenderer == null || videoMeshFilter == null)
                return;

            if (videoMeshFilter.sharedMesh == null)
            {
                _runtimeMesh = CreateQuadMesh();
                videoMeshFilter.sharedMesh = _runtimeMesh;
            }

            if (_runtimeMaterial == null)
            {
                Shader shader = useFoggedGlassTreatment
                    ? Shader.Find(FoggedGlassShaderName)
                    : null;
                if (shader == null)
                {
                    shader = videoShader != null
                        ? videoShader
                        : Shader.Find("Glassfall/NeonVideoBackground");
                }
                if (shader == null)
                    return;

                _runtimeMaterial = new Material(shader)
                {
                    name = "NeonWindowVideoRuntimeMaterial"
                };
            }

            videoRenderer.sharedMaterial = _runtimeMaterial;
            videoRenderer.sortingOrder = sortingOrder;
            videoRenderer.shadowCastingMode = ShadowCastingMode.Off;
            videoRenderer.receiveShadows = false;
            videoRenderer.lightProbeUsage = LightProbeUsage.Off;
            videoRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            videoRenderer.enabled = false;
            ApplyMaterialProperties();
            FitSurfacePreservingAspect();
        }

        private void ApplyMaterialProperties()
        {
            if (_runtimeMaterial == null)
                return;

            _runtimeMaterial.SetFloat("_Opacity", opacity);
            _runtimeMaterial.SetFloat("_Brightness", brightness);
            _runtimeMaterial.SetFloat("_Saturation", saturation);
            _runtimeMaterial.SetColor("_Tint", NormalizeTint(tint));
            if (useFoggedGlassTreatment &&
                _runtimeMaterial.shader != null &&
                _runtimeMaterial.shader.name == FoggedGlassShaderName)
            {
                _runtimeMaterial.SetFloat("_BlurRadius", _currentBlurRadius);
                _runtimeMaterial.SetFloat("_BlurMix", _currentBlurMix);
                _runtimeMaterial.SetFloat(
                    "_TargetAspect",
                    targetCamera != null
                        ? Mathf.Max(0.01f, targetCamera.aspect)
                        : SourceAspect);
            }
        }

        private void UpdateProgressiveGlass(float deltaTime)
        {
            int phase = ResolveGlassPhase();
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

            float blend = 1f - Mathf.Exp(
                -blurTransitionSpeed * Mathf.Max(0f, deltaTime));
            _currentBlurRadius = Mathf.Lerp(
                _currentBlurRadius,
                targetRadius,
                blend);
            _currentBlurMix = Mathf.Lerp(
                _currentBlurMix,
                targetMix,
                blend);
        }

        private int ResolveGlassPhase()
        {
            if (!useFoggedGlassTreatment || runController == null)
                return 0;

            int activeWorldSegmentIndex =
                runController.CurrentWorldSegmentIndex;
            if (activeWorldSegmentIndex < progressWorldSegmentIndex)
                return 0;
            if (activeWorldSegmentIndex > progressWorldSegmentIndex)
                return 2;

            int encounterCount = Mathf.Max(
                1,
                runController.CurrentWorldEncounterCount);
            int worldStartEncounter = runController.CurrentWorldStartEncounter;
            if (_lastWorldStartEncounter != worldStartEncounter ||
                _lastWorldEncounterCount != encounterCount)
            {
                _lastWorldStartEncounter = worldStartEncounter;
                _lastWorldEncounterCount = encounterCount;
                _highestCompletedEncounterCount = 0;
            }

            _highestCompletedEncounterCount = Mathf.Max(
                _highestCompletedEncounterCount,
                Mathf.Clamp(
                    runController.CompletedCurrentWorldEncounterCount,
                    0,
                    encounterCount));
            float completion01 = Mathf.Clamp01(
                _highestCompletedEncounterCount / (float)encounterCount);
            if (completion01 >= (2f / 3f))
                return 2;
            if (completion01 >= (1f / 3f))
                return 1;
            return 0;
        }

        private void ResetProgressiveGlassState()
        {
            _hasBlurState = false;
            _currentBlurRadius = initialBlurRadiusPixels;
            _currentBlurMix = initialBlurMix;
            _highestCompletedEncounterCount = 0;
            _lastWorldStartEncounter = -1;
            _lastWorldEncounterCount = -1;
        }

        private void SetProceduralFallbackVisible(bool visible)
        {
            if (_proceduralPresentation == null)
            {
                _proceduralPresentation =
                    GetComponent<NeonWindowWorldPresentation>();
            }

            if (_proceduralPresentation != null)
            {
                _proceduralPresentation.SetVideoBackgroundActive(!visible);
            }
        }

        private void FitSurfacePreservingAspect()
        {
            if (videoRenderer == null)
                return;

            Bounds bounds = worldBounds != null
                ? worldBounds.bounds
                : new Bounds(Vector3.zero, new Vector3(42f, 24f, 0f));
            float availableWidth = Mathf.Max(0.01f, bounds.size.x * boundsCoverage);
            float availableHeight = Mathf.Max(0.01f, bounds.size.y * boundsCoverage);
            float width = availableWidth;
            float height = width / SourceAspect;
            if (height > availableHeight)
            {
                height = availableHeight;
                width = height * SourceAspect;
            }

            Transform surface = videoRenderer.transform;
            surface.position = new Vector3(
                bounds.center.x,
                bounds.center.y,
                worldZ);
            surface.rotation = Quaternion.identity;
            surface.localScale = new Vector3(width, height, 1f);
        }

        private void ReleaseRuntimeResources()
        {
            if (videoRenderer != null)
                videoRenderer.enabled = false;

            if (_runtimeMaterial != null)
            {
                DestroyRuntimeObject(_runtimeMaterial);
                _runtimeMaterial = null;
            }

            if (_runtimeMesh != null)
            {
                if (videoMeshFilter != null &&
                    videoMeshFilter.sharedMesh == _runtimeMesh)
                {
                    videoMeshFilter.sharedMesh = null;
                }

                DestroyRuntimeObject(_runtimeMesh);
                _runtimeMesh = null;
            }
        }

        private static Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "NeonWindowVideoQuad"
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

        private static Color NormalizeTint(Color value)
        {
            return new Color(
                Mathf.Clamp01(value.r),
                Mathf.Clamp01(value.g),
                Mathf.Clamp01(value.b),
                Mathf.Clamp01(value.a));
        }

        private static void DestroyRuntimeObject(Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }

        private void OnValidate()
        {
            opacity = Mathf.Clamp01(opacity);
            brightness = Mathf.Clamp(brightness, 0.5f, 1.1f);
            saturation = Mathf.Clamp(saturation, 0f, 1.2f);
            tint = NormalizeTint(tint);
            boundsCoverage = Mathf.Clamp(boundsCoverage, 0.90f, 1f);
            prepareTimeoutSeconds = Mathf.Max(0.5f, prepareTimeoutSeconds);
            initialBlurRadiusPixels = Mathf.Clamp(initialBlurRadiusPixels, 0f, 12f);
            initialBlurMix = Mathf.Clamp01(initialBlurMix);
            middleBlurRadiusPixels = Mathf.Clamp(middleBlurRadiusPixels, 0f, 12f);
            middleBlurMix = Mathf.Clamp01(middleBlurMix);
            lateBlurRadiusPixels = Mathf.Clamp(lateBlurRadiusPixels, 0f, 12f);
            lateBlurMix = Mathf.Clamp01(lateBlurMix);
            blurTransitionSpeed = Mathf.Max(0.01f, blurTransitionSpeed);
            progressWorldSegmentIndex = Mathf.Max(0, progressWorldSegmentIndex);
        }
    }
}
