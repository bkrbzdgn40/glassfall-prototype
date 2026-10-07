using Glassfall.Gameplay.Surface;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    public sealed class SurfaceZoneVisual : MonoBehaviour
    {
        private const string DefaultShaderName =
            "Glassfall/DirtyGlass";

        private static readonly int DirtyTintId =
            Shader.PropertyToID("_DirtyTint");
        private static readonly int OpacityId =
            Shader.PropertyToID("_Opacity");
        private static readonly int NoiseScaleId =
            Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseStrengthId =
            Shader.PropertyToID("_NoiseStrength");
        private static readonly int EdgeSoftnessId =
            Shader.PropertyToID("_EdgeSoftness");

        [Header("Sources")]
        [SerializeField] private SurfaceZone surfaceZone;
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Material materialTemplate;
        [SerializeField] private Shader dirtyGlassShader;

        [Header("Dirty Glass Presentation")]
        [SerializeField] private Color dirtyTint =
            new Color(0.48f, 0.39f, 0.33f, 1f);

        [Range(0f, 1f)]
        [SerializeField] private float opacity = 0.22f;

        [Min(0.01f)]
        [SerializeField] private float noiseScale = 3.5f;

        [Range(0f, 1f)]
        [SerializeField] private float noiseStrength = 0.12f;

        [Range(0f, 0.5f)]
        [SerializeField] private float edgeSoftness = 0.08f;

        [SerializeField] private int sortingOrder = -3;

        private BoxCollider2D _boxCollider;
        private Material _runtimeMaterial;
        private Material _previousMaterial;
        private Color _previousColor;
        private int _previousSortingOrder;
        private bool _previousEnabled;
        private SpriteDrawMode _previousDrawMode;
        private Vector2 _previousSize;
        private bool _rendererStateCaptured;

        private void Awake()
        {
            ResolveReferences();
            if (!CaptureAndValidateRenderer())
                return;

            if (!CreateRuntimeMaterial())
                return;

            ApplyMaterialSettings();
            RefreshPresentation();
        }

        private void OnEnable()
        {
            if (!_rendererStateCaptured || _runtimeMaterial == null)
                return;

            ApplyRendererAlignment();
            ApplyMaterialSettings();
            RefreshPresentation();
        }

        private void OnDisable()
        {
            RestoreRendererState();
        }

        private void OnValidate()
        {
            opacity = Mathf.Clamp01(opacity);
            noiseScale = Mathf.Max(0.01f, noiseScale);
            noiseStrength = Mathf.Clamp01(noiseStrength);
            edgeSoftness = Mathf.Clamp(edgeSoftness, 0f, 0.5f);

            ResolveReferences();

            if (_runtimeMaterial != null)
            {
                ApplyMaterialSettings();
                RefreshPresentation();
            }
        }

        private void ResolveReferences()
        {
            if (surfaceZone == null)
                surfaceZone = GetComponent<SurfaceZone>();
            if (targetRenderer == null)
                targetRenderer = GetComponent<SpriteRenderer>();
        }

        private bool CaptureAndValidateRenderer()
        {
            if (surfaceZone == null)
            {
                Debug.LogWarning(
                    $"{name}: SurfaceZoneVisual needs a SurfaceZone.",
                    this);
                return false;
            }

            if (targetRenderer == null)
            {
                Debug.LogWarning(
                    $"{name}: SurfaceZoneVisual needs a SpriteRenderer.",
                    this);
                return false;
            }

            Collider2D collider = GetComponent<Collider2D>();
            _boxCollider = collider as BoxCollider2D;
            if (_boxCollider == null)
            {
                string colliderType = collider == null
                    ? "none"
                    : collider.GetType().Name;
                Debug.LogWarning(
                    $"{name}: SurfaceZoneVisual supports " +
                    $"BoxCollider2D only; found {colliderType}.",
                    this);
                targetRenderer.enabled = false;
                return false;
            }

            if (_rendererStateCaptured)
                return true;

            _previousMaterial = targetRenderer.sharedMaterial;
            _previousColor = targetRenderer.color;
            _previousSortingOrder = targetRenderer.sortingOrder;
            _previousEnabled = targetRenderer.enabled;
            _previousDrawMode = targetRenderer.drawMode;
            _previousSize = targetRenderer.size;
            _rendererStateCaptured = true;

            if (_boxCollider.offset != Vector2.zero)
            {
                Debug.LogWarning(
                    $"{name}: BoxCollider2D offset is {_boxCollider.offset}. " +
                    "Keep the offset at zero so the reused SpriteRenderer " +
                    "stays aligned with the gameplay zone.",
                    this);
            }

            ApplyRendererAlignment();
            return true;
        }

        private void ApplyRendererAlignment()
        {
            targetRenderer.drawMode = SpriteDrawMode.Simple;
            ValidateSimpleRendererAlignment();
            targetRenderer.sortingOrder = sortingOrder;
        }

        private void ValidateSimpleRendererAlignment()
        {
            Sprite sprite = targetRenderer.sprite;
            if (sprite == null)
            {
                Debug.LogWarning(
                    $"{name}: SurfaceZoneVisual needs an authored Sprite " +
                    "to align with its BoxCollider2D.",
                    this);
                return;
            }

            Vector3 rendererScale = targetRenderer.transform.lossyScale;
            Vector3 colliderScale = _boxCollider.transform.lossyScale;
            Vector2 visualWorldSize = new Vector2(
                sprite.bounds.size.x * Mathf.Abs(rendererScale.x),
                sprite.bounds.size.y * Mathf.Abs(rendererScale.y));
            Vector2 colliderWorldSize = new Vector2(
                _boxCollider.size.x * Mathf.Abs(colliderScale.x),
                _boxCollider.size.y * Mathf.Abs(colliderScale.y));

            if (Mathf.Approximately(
                    visualWorldSize.x,
                    colliderWorldSize.x) &&
                Mathf.Approximately(
                    visualWorldSize.y,
                    colliderWorldSize.y))
            {
                return;
            }

            Debug.LogWarning(
                $"{name}: authored Sprite bounds produce a world size of " +
                $"{visualWorldSize}, but the BoxCollider2D world size is " +
                $"{colliderWorldSize}. Keep their authored local sizes " +
                "matched; SurfaceZoneVisual will not rescale the shared " +
                "Transform because that would change the gameplay zone.",
                this);
        }

        private bool CreateRuntimeMaterial()
        {
            if (_runtimeMaterial != null)
                return true;

            if (materialTemplate != null)
            {
                _runtimeMaterial = new Material(materialTemplate)
                {
                    name = $"{name}_DirtyGlassMaterial"
                };
                return true;
            }

            Shader shader = dirtyGlassShader != null
                ? dirtyGlassShader
                : Shader.Find(DefaultShaderName);
            if (shader == null)
            {
                Debug.LogWarning(
                    $"{name}: Could not find {DefaultShaderName}.",
                    this);
                targetRenderer.enabled = false;
                return false;
            }

            _runtimeMaterial = new Material(shader)
            {
                name = $"{name}_DirtyGlassMaterial"
            };
            return true;
        }

        private void ApplyMaterialSettings()
        {
            if (_runtimeMaterial == null)
                return;

            if (_runtimeMaterial.HasProperty(DirtyTintId))
                _runtimeMaterial.SetColor(DirtyTintId, dirtyTint);
            if (_runtimeMaterial.HasProperty(OpacityId))
                _runtimeMaterial.SetFloat(OpacityId, opacity);
            if (_runtimeMaterial.HasProperty(NoiseScaleId))
                _runtimeMaterial.SetFloat(NoiseScaleId, noiseScale);
            if (_runtimeMaterial.HasProperty(NoiseStrengthId))
            {
                _runtimeMaterial.SetFloat(
                    NoiseStrengthId,
                    noiseStrength);
            }
            if (_runtimeMaterial.HasProperty(EdgeSoftnessId))
            {
                _runtimeMaterial.SetFloat(
                    EdgeSoftnessId,
                    edgeSoftness);
            }

            targetRenderer.sharedMaterial = _runtimeMaterial;
            targetRenderer.color = Color.white;
            targetRenderer.sortingOrder = sortingOrder;
        }

        private void RefreshPresentation()
        {
            if (targetRenderer == null || surfaceZone == null)
                return;

            targetRenderer.enabled =
                _previousEnabled &&
                surfaceZone.SurfaceType == SurfaceType.DirtyGlass;
        }

        private void OnDestroy()
        {
            RestoreRendererState();

            if (_runtimeMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_runtimeMaterial);
                else
                    DestroyImmediate(_runtimeMaterial);
            }
        }

        private void RestoreRendererState()
        {
            if (targetRenderer == null || !_rendererStateCaptured)
                return;

            if (targetRenderer.sharedMaterial == _runtimeMaterial)
                targetRenderer.sharedMaterial = _previousMaterial;

            targetRenderer.color = _previousColor;
            targetRenderer.sortingOrder = _previousSortingOrder;
            targetRenderer.enabled = _previousEnabled;
            targetRenderer.drawMode = _previousDrawMode;
            targetRenderer.size = _previousSize;
        }
    }
}
