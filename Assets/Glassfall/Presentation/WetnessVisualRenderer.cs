using Glassfall.Gameplay.Wetness;
using Unity.Profiling;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WetnessVisualRenderer : MonoBehaviour
    {
        private const string DefaultShaderName =
            "Glassfall/WetnessTrail";

        private static readonly ProfilerMarker TextureUpdateMarker =
            new ProfilerMarker("Glassfall.WetnessVisual.TextureUpdate");

        private static readonly int WetTintId =
            Shader.PropertyToID("_WetTint");
        private static readonly int HighlightColorId =
            Shader.PropertyToID("_HighlightColor");
        private static readonly int OpacityId =
            Shader.PropertyToID("_Opacity");
        private static readonly int HighlightStrengthId =
            Shader.PropertyToID("_HighlightStrength");
        private static readonly int ResponseExponentId =
            Shader.PropertyToID("_ResponseExponent");
        private static readonly int FadedThresholdId =
            Shader.PropertyToID("_FadedThreshold");
        private static readonly int FreshThresholdId =
            Shader.PropertyToID("_FreshThreshold");
        private static readonly int FadedOpacityMultiplierId =
            Shader.PropertyToID("_FadedOpacityMultiplier");
        private static readonly int AgingOpacityMultiplierId =
            Shader.PropertyToID("_AgingOpacityMultiplier");
        private static readonly int FreshHighlightMultiplierId =
            Shader.PropertyToID("_FreshHighlightMultiplier");
        private static readonly int NeighborBlendId =
            Shader.PropertyToID("_NeighborBlend");
        private static readonly int EdgeSoftnessId =
            Shader.PropertyToID("_EdgeSoftness");

        [Header("Sources")]
        [SerializeField] private WetnessField wetnessField;
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Material materialTemplate;
        [SerializeField] private Shader wetnessShader;

        [Header("Presentation")]
        [SerializeField] private Color wetTint =
            new Color(0.16f, 0.42f, 0.62f, 1f);

        [SerializeField] private Color highlightColor =
            new Color(0.72f, 0.90f, 0.98f, 1f);

        [Range(0f, 1f)]
        [SerializeField] private float opacity = 0.38f;

        [Range(0f, 2f)]
        [SerializeField] private float highlightStrength = 0.18f;

        [Min(0.01f)]
        [SerializeField] private float responseExponent = 0.92f;

        [Header("Lifecycle Presentation")]
        [Range(0f, 1f)]
        [SerializeField] private float fadedThreshold = 0.30f;

        [Range(0f, 1f)]
        [SerializeField] private float freshThreshold = 0.65f;

        [Range(0f, 1f)]
        [SerializeField] private float fadedOpacityMultiplier = 0.24f;

        [Range(0f, 1f)]
        [SerializeField] private float agingOpacityMultiplier = 0.42f;

        [Range(0f, 2f)]
        [SerializeField] private float freshHighlightMultiplier = 1.10f;

        [Range(0f, 1f)]
        [SerializeField] private float neighborBlend = 0.46f;

        [Range(0.5f, 4f)]
        [SerializeField] private float edgeSoftness = 1.55f;

        [Min(0.01f)]
        [SerializeField] private float visualRefreshInterval = 0.05f;

        [SerializeField] private int sortingOrder = -2;

        private Texture2D _texture;
        private Sprite _sprite;
        private Color32[] _colors;
        private Material _runtimeMaterial;
        private float _nextUpdateTime;
        private Sprite _previousSprite;
        private Material _previousMaterial;
        private int _previousSortingOrder;
        private bool _rendererStateCaptured;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<SpriteRenderer>();

            CaptureRendererState();

            if (targetRenderer != null)
                targetRenderer.sortingOrder = sortingOrder;
        }

        private void Start()
        {
            if (!TryInitialize())
                return;

            UpdateTexture();
        }

        private void Update()
        {
            if (_texture == null || wetnessField == null ||
                Time.unscaledTime < _nextUpdateTime)
            {
                return;
            }

            _nextUpdateTime = Time.unscaledTime +
                Mathf.Max(visualRefreshInterval, 0.01f);
            UpdateTexture();
        }

        private void OnValidate()
        {
            opacity = Mathf.Clamp01(opacity);
            highlightStrength = Mathf.Max(0f, highlightStrength);
            responseExponent = Mathf.Max(0.01f, responseExponent);
            fadedThreshold = Mathf.Clamp01(fadedThreshold);
            freshThreshold = Mathf.Clamp(freshThreshold, fadedThreshold, 1f);
            fadedOpacityMultiplier = Mathf.Clamp01(fadedOpacityMultiplier);
            agingOpacityMultiplier = Mathf.Clamp01(agingOpacityMultiplier);
            freshHighlightMultiplier = Mathf.Max(0f, freshHighlightMultiplier);
            neighborBlend = Mathf.Clamp01(neighborBlend);
            edgeSoftness = Mathf.Clamp(edgeSoftness, 0.5f, 4f);
            visualRefreshInterval = Mathf.Max(0.01f, visualRefreshInterval);

            if (targetRenderer != null)
                targetRenderer.sortingOrder = sortingOrder;

            ApplyMaterialSettings();
        }

        public void ConfigureLifecycle(
            float newFadedThreshold,
            float newFreshThreshold,
            float newFadedOpacityMultiplier,
            float newFreshHighlightMultiplier,
            float newAgingOpacityMultiplier = 0.58f)
        {
            fadedThreshold = Mathf.Clamp01(newFadedThreshold);
            freshThreshold = Mathf.Clamp(
                newFreshThreshold,
                fadedThreshold,
                1f);
            fadedOpacityMultiplier =
                Mathf.Clamp01(newFadedOpacityMultiplier);
            agingOpacityMultiplier =
                Mathf.Clamp01(newAgingOpacityMultiplier);
            freshHighlightMultiplier =
                Mathf.Max(0f, newFreshHighlightMultiplier);
            ApplyMaterialSettings();
        }


        public void ConfigureReadabilityProfile(
            Color newWetTint,
            Color newHighlightColor,
            float newOpacity,
            float newHighlightStrength,
            float newResponseExponent,
            float newNeighborBlend,
            float newEdgeSoftness,
            float newVisualRefreshInterval,
            int newSortingOrder = -2)
        {
            wetTint = newWetTint;
            highlightColor = newHighlightColor;
            opacity = Mathf.Clamp01(newOpacity);
            highlightStrength = Mathf.Max(0f, newHighlightStrength);
            responseExponent = Mathf.Max(0.01f, newResponseExponent);
            neighborBlend = Mathf.Clamp01(newNeighborBlend);
            edgeSoftness = Mathf.Clamp(newEdgeSoftness, 0.5f, 4f);
            visualRefreshInterval = Mathf.Max(0.01f, newVisualRefreshInterval);
            sortingOrder = newSortingOrder;

            if (targetRenderer != null)
                targetRenderer.sortingOrder = sortingOrder;

            ApplyMaterialSettings();
        }

        private bool TryInitialize()
        {
            if (wetnessField == null || targetRenderer == null)
            {
                Debug.LogWarning(
                    $"{name}: WetnessVisualRenderer needs a " +
                    "WetnessField and SpriteRenderer.",
                    this);
                return false;
            }

            int width = wetnessField.GridWidth;
            int height = wetnessField.GridHeight;
            _texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false);
            _texture.name = $"{name}_WetnessTexture";
            _texture.filterMode = FilterMode.Bilinear;
            _texture.wrapMode = TextureWrapMode.Clamp;
            _texture.anisoLevel = 0;

            _colors = new Color32[width * height];
            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                1f);
            _sprite.name = $"{name}_WetnessSprite";

            targetRenderer.sprite = _sprite;
            targetRenderer.color = Color.white;
            transform.position = wetnessField.WorldCenter;
            ApplyWorldScale(width, height);

            _runtimeMaterial = CreateRuntimeMaterial();
            if (_runtimeMaterial == null)
            {
                ReleaseGeneratedResources();
                return false;
            }

            ApplyMaterialSettings();
            targetRenderer.sharedMaterial = _runtimeMaterial;
            return true;
        }

        private void ApplyWorldScale(int width, int height)
        {
            Vector2 cellSize = new Vector2(
                wetnessField.WorldSize.x / Mathf.Max(width, 1),
                wetnessField.WorldSize.y / Mathf.Max(height, 1));
            Vector3 parentScale = transform.parent != null
                ? transform.parent.lossyScale
                : Vector3.one;
            float parentScaleX = Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f);
            float parentScaleY = Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f);

            transform.localScale = new Vector3(
                cellSize.x / parentScaleX,
                cellSize.y / parentScaleY,
                1f);
        }

        private Material CreateRuntimeMaterial()
        {
            if (materialTemplate != null)
            {
                Material instance = new Material(materialTemplate)
                {
                    name = $"{name}_WetnessMaterial"
                };
                return instance;
            }

            Shader shader = wetnessShader != null
                ? wetnessShader
                : Shader.Find(DefaultShaderName);
            if (shader == null)
            {
                Debug.LogWarning(
                    $"{name}: Could not find {DefaultShaderName}.",
                    this);
                return null;
            }

            return new Material(shader)
            {
                name = $"{name}_WetnessMaterial"
            };
        }

        private void ApplyMaterialSettings()
        {
            if (_runtimeMaterial == null)
                return;

            _runtimeMaterial.SetColor(WetTintId, wetTint);
            _runtimeMaterial.SetColor(
                HighlightColorId,
                highlightColor);
            _runtimeMaterial.SetFloat(OpacityId, opacity);
            _runtimeMaterial.SetFloat(
                HighlightStrengthId,
                highlightStrength);
            _runtimeMaterial.SetFloat(
                ResponseExponentId,
                responseExponent);
            _runtimeMaterial.SetFloat(
                FadedThresholdId,
                fadedThreshold);
            _runtimeMaterial.SetFloat(
                FreshThresholdId,
                freshThreshold);
            _runtimeMaterial.SetFloat(
                FadedOpacityMultiplierId,
                fadedOpacityMultiplier);
            _runtimeMaterial.SetFloat(
                AgingOpacityMultiplierId,
                agingOpacityMultiplier);
            _runtimeMaterial.SetFloat(
                FreshHighlightMultiplierId,
                freshHighlightMultiplier);
            _runtimeMaterial.SetFloat(
                NeighborBlendId,
                neighborBlend);
            _runtimeMaterial.SetFloat(
                EdgeSoftnessId,
                edgeSoftness);
        }

        private void UpdateTexture()
        {
            using (TextureUpdateMarker.Auto())
            {
                wetnessField.CopyTo(_colors, Color.white);
                _texture.SetPixels32(_colors);
                _texture.Apply(false, false);
            }
        }

        private void CaptureRendererState()
        {
            if (_rendererStateCaptured || targetRenderer == null)
                return;

            _previousSprite = targetRenderer.sprite;
            _previousMaterial = targetRenderer.sharedMaterial;
            _previousSortingOrder = targetRenderer.sortingOrder;
            _rendererStateCaptured = true;
        }

        private void OnDestroy()
        {
            if (targetRenderer != null && _rendererStateCaptured)
            {
                if (targetRenderer.sprite == _sprite)
                    targetRenderer.sprite = _previousSprite;
                if (targetRenderer.sharedMaterial == _runtimeMaterial)
                {
                    targetRenderer.sharedMaterial = _previousMaterial;
                }

                targetRenderer.sortingOrder = _previousSortingOrder;
            }

            ReleaseGeneratedResources();
        }

        private void ReleaseGeneratedResources()
        {
            DestroyGeneratedObject(_sprite);
            DestroyGeneratedObject(_texture);
            DestroyGeneratedObject(_runtimeMaterial);
            _sprite = null;
            _texture = null;
            _runtimeMaterial = null;
            _colors = null;
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
