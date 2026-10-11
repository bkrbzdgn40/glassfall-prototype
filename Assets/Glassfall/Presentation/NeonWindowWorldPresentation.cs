using System.Collections.Generic;
using Glassfall.Gameplay.Run;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    public sealed class NeonWindowWorldPresentation : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private const string RuntimeRootName = "RuntimeNeonVisuals";

        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Collider2D worldBounds;
        [SerializeField] private MicroRunController runController;
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioSource transitionSource;

        [Header("Neon Palette")]
        [SerializeField] private Color clearColor =
            new Color(0.008f, 0.018f, 0.055f, 1f);
        [SerializeField] private Color baseColor =
            new Color(0.012f, 0.035f, 0.085f, 1f);
        [SerializeField] private Color cyan =
            new Color(0.12f, 0.95f, 1f, 0.72f);
        [SerializeField] private Color magenta =
            new Color(1f, 0.12f, 0.78f, 0.68f);
        [SerializeField] private Color violet =
            new Color(0.34f, 0.12f, 0.70f, 0.42f);

        [Header("Motion")]
        [Min(0f)]
        [SerializeField] private float reflectionPulseSpeed = 1.15f;

        private readonly List<SpriteRenderer> _animatedRenderers =
            new List<SpriteRenderer>();
        private readonly List<float> _baseAlphas = new List<float>();
        private Transform _runtimeRoot;
        private Texture2D _pixelTexture;
        private Sprite _pixelSprite;
        private AudioClip _ambienceClip;
        private AudioClip _transitionClip;
        private Color _previousCameraColor;
        private bool _cameraColorCaptured;
        private bool _videoBackgroundActive;

        public bool IsRuntimePresentationReady => _runtimeRoot != null;
        public bool AreProceduralVisualsVisible =>
            _runtimeRoot != null && _runtimeRoot.gameObject.activeSelf;

        public void SetVideoBackgroundActive(bool active)
        {
            _videoBackgroundActive = active;
            if (_runtimeRoot != null &&
                _runtimeRoot.gameObject.activeSelf == active)
            {
                _runtimeRoot.gameObject.SetActive(!active);
            }
        }

        public void Configure(
            Camera camera,
            Collider2D bounds,
            MicroRunController run,
            AudioSource ambience,
            AudioSource transition)
        {
            targetCamera = camera;
            worldBounds = bounds;
            runController = run;
            ambienceSource = ambience;
            transitionSource = transition;

            if (Application.isPlaying && isActiveAndEnabled)
                ActivatePresentation();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            ResolveReferences();
            ActivatePresentation();
        }

        private void Update()
        {
            if (!Application.isPlaying ||
                _videoBackgroundActive ||
                _animatedRenderers.Count == 0)
                return;

            float time = Time.unscaledTime * reflectionPulseSpeed;
            for (int i = 0; i < _animatedRenderers.Count; i++)
            {
                SpriteRenderer renderer = _animatedRenderers[i];
                if (renderer == null)
                    continue;

                Color color = renderer.color;
                float wave = 0.78f + 0.22f * Mathf.Sin(
                    time + i * 1.37f);
                color.a = _baseAlphas[i] * wave;
                renderer.color = color;
            }
        }

        private void OnDisable()
        {
            SetVideoBackgroundActive(false);

            if (ambienceSource != null && ambienceSource.isPlaying)
                ambienceSource.Stop();

            if (_cameraColorCaptured && targetCamera != null)
            {
                targetCamera.backgroundColor = _previousCameraColor;
                _cameraColorCaptured = false;
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(_pixelSprite);
            DestroyRuntimeObject(_pixelTexture);
            DestroyRuntimeObject(_ambienceClip);
            DestroyRuntimeObject(_transitionClip);
        }

        private void ResolveReferences()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (runController == null)
                runController = FindAnyObjectByType<MicroRunController>();
        }

        private void ActivatePresentation()
        {
            EnsureRuntimeVisuals();
            EnsureAudioClips();

            if (targetCamera != null && !_cameraColorCaptured)
            {
                _previousCameraColor = targetCamera.backgroundColor;
                _cameraColorCaptured = true;
                targetCamera.backgroundColor = clearColor;
            }

            if (ambienceSource != null && _ambienceClip != null)
            {
                ambienceSource.clip = _ambienceClip;
                ambienceSource.loop = true;
                ambienceSource.playOnAwake = false;
                ambienceSource.spatialBlend = 0f;
                ambienceSource.volume = 0.075f;
                if (!ambienceSource.isPlaying)
                    ambienceSource.Play();
            }

            if (transitionSource != null && _transitionClip != null)
            {
                transitionSource.loop = false;
                transitionSource.playOnAwake = false;
                transitionSource.spatialBlend = 0f;
                transitionSource.PlayOneShot(_transitionClip, 0.24f);
            }
        }

        private void EnsureRuntimeVisuals()
        {
            if (_runtimeRoot != null)
                return;

            GameObject runtimeObject = new GameObject(RuntimeRootName);
            runtimeObject.transform.SetParent(transform, false);
            _runtimeRoot = runtimeObject.transform;
            _runtimeRoot.gameObject.SetActive(!_videoBackgroundActive);

            _pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "NeonWindowRuntimePixel",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _pixelTexture.SetPixel(0, 0, Color.white);
            _pixelTexture.Apply(false, true);
            _pixelSprite = Sprite.Create(
                _pixelTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            _pixelSprite.name = "NeonWindowRuntimeSprite";

            Bounds bounds = worldBounds != null
                ? worldBounds.bounds
                : new Bounds(Vector3.zero, new Vector3(42f, 24f, 0f));
            Vector2 center = bounds.center;
            Vector2 size = bounds.size;

            CreatePanel(
                "NeonBase",
                center,
                size * 1.04f,
                baseColor,
                -1095,
                false);

            float bottom = center.y - size.y * 0.5f;
            float[] cityX = { -0.46f, -0.34f, -0.21f, -0.08f, 0.07f,
                0.19f, 0.31f, 0.44f };
            float[] cityHeight = { 0.34f, 0.55f, 0.41f, 0.69f, 0.47f,
                0.61f, 0.38f, 0.52f };
            for (int i = 0; i < cityX.Length; i++)
            {
                float height = size.y * cityHeight[i];
                Color silhouette = i % 2 == 0
                    ? new Color(0.015f, 0.045f, 0.11f, 0.98f)
                    : new Color(0.025f, 0.025f, 0.09f, 0.98f);
                CreatePanel(
                    "CityMass_" + i,
                    new Vector2(
                        center.x + size.x * cityX[i],
                        bottom + height * 0.5f),
                    new Vector2(size.x * 0.105f, height),
                    silhouette,
                    -1092,
                    false);
            }

            float[] stripX = { -0.42f, -0.29f, -0.13f, 0.02f, 0.16f,
                0.28f, 0.39f };
            for (int i = 0; i < stripX.Length; i++)
            {
                Color color = i % 2 == 0 ? cyan : magenta;
                float height = size.y * (0.30f + (i % 3) * 0.11f);
                CreatePanel(
                    "NeonStrip_" + i,
                    new Vector2(
                        center.x + size.x * stripX[i],
                        bottom + height * 0.72f),
                    new Vector2(size.x * 0.008f, height),
                    color,
                    -1088,
                    true);
            }

            for (int i = 0; i < 6; i++)
            {
                Color reflection = i % 2 == 0 ? cyan : magenta;
                reflection.a *= 0.34f;
                CreatePanel(
                    "WetReflection_" + i,
                    new Vector2(
                        center.x + size.x * (-0.40f + i * 0.16f),
                        center.y - size.y * 0.18f),
                    new Vector2(size.x * 0.018f, size.y * 0.56f),
                    reflection,
                    -1087,
                    true);
            }

            for (int i = 0; i < 4; i++)
            {
                Color haze = violet;
                haze.a *= 0.55f;
                CreatePanel(
                    "VioletGlassBand_" + i,
                    new Vector2(
                        center.x,
                        center.y + size.y * (-0.34f + i * 0.22f)),
                    new Vector2(size.x * 0.92f, size.y * 0.018f),
                    haze,
                    -1089,
                    true);
            }
        }

        private void CreatePanel(
            string objectName,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder,
            bool animate)
        {
            GameObject panel = new GameObject(objectName);
            panel.transform.SetParent(_runtimeRoot, false);
            panel.transform.position = new Vector3(position.x, position.y, 5.8f);
            panel.transform.localScale = new Vector3(
                Mathf.Max(0.01f, size.x),
                Mathf.Max(0.01f, size.y),
                1f);

            SpriteRenderer renderer = panel.AddComponent<SpriteRenderer>();
            renderer.sprite = _pixelSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (animate)
            {
                _animatedRenderers.Add(renderer);
                _baseAlphas.Add(color.a);
            }
        }

        private void EnsureAudioClips()
        {
            if (_ambienceClip == null)
                _ambienceClip = CreateAmbienceClip();
            if (_transitionClip == null)
                _transitionClip = CreateTransitionClip();
        }

        private static AudioClip CreateAmbienceClip()
        {
            const float duration = 4f;
            int sampleCount = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float slowPulse = 0.62f + 0.38f * Mathf.Sin(
                    time * Mathf.PI * 0.5f);
                samples[i] =
                    Mathf.Sin(time * Mathf.PI * 2f * 55f) * 0.075f +
                    Mathf.Sin(time * Mathf.PI * 2f * 110f) * 0.022f *
                    slowPulse;
            }

            AudioClip clip = AudioClip.Create(
                "NeonWindowAmbience_Runtime",
                sampleCount,
                1,
                SampleRate,
                false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateTransitionClip()
        {
            const float duration = 0.72f;
            int sampleCount = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float time = i / (float)SampleRate;
                float frequency = Mathf.Lerp(210f, 620f, t * t);
                float envelope = Mathf.Sin(Mathf.PI * t) * (1f - t * 0.35f);
                samples[i] = Mathf.Sin(
                    time * Mathf.PI * 2f * frequency) *
                    envelope * 0.20f;
            }

            AudioClip clip = AudioClip.Create(
                "NeonWindowTransition_Runtime",
                sampleCount,
                1,
                SampleRate,
                false);
            clip.SetData(samples, 0);
            return clip;
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
            reflectionPulseSpeed = Mathf.Max(0f, reflectionPulseSpeed);
        }
    }
}
