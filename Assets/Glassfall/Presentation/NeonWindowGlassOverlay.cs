using UnityEngine;

namespace Glassfall.Presentation
{
    /// <summary>
    /// A small, point-filtered pixel texture that sits above the Neon video
    /// and below gameplay. It deliberately leaves the center mostly clear;
    /// this is a wet/frosted window cue, not a post-processing blur.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class NeonWindowGlassOverlay : MonoBehaviour
    {
        private const int TextureWidth = 112;
        private const int TextureHeight = 64;

        [Header("Pixel Glass")]
        [Range(0f, 1f)]
        [SerializeField] private float glassOpacity = 0.28f;

        [Range(0f, 1f)]
        [SerializeField] private float condensationOpacity = 0.38f;

        [Range(0f, 1f)]
        [SerializeField] private float rainOpacity = 0.42f;

        [Range(0f, 1f)]
        [SerializeField] private float dropletOpacity = 0.28f;

        [SerializeField] private Color glassTint =
            new Color(0.64f, 0.82f, 0.88f, 1f);

        [SerializeField] private int sortingOrder = -1090;
        [SerializeField] private float worldZ = 5.95f;

        private Collider2D _worldBounds;
        private Camera _targetCamera;
        private Texture2D _texture;
        private Sprite _sprite;
        private SpriteRenderer _renderer;
        private bool _configured;

        public bool IsVisible => _renderer != null && _renderer.enabled;

        public void Configure(
            Collider2D bounds,
            int overlaySortingOrder = -1090,
            float overlayWorldZ = 5.95f)
        {
            Configure(
                bounds,
                null,
                overlaySortingOrder,
                overlayWorldZ);
        }

        public void Configure(
            Collider2D bounds,
            Camera camera,
            int overlaySortingOrder = -1090,
            float overlayWorldZ = 5.95f)
        {
            _worldBounds = bounds;
            _targetCamera = camera;
            sortingOrder = overlaySortingOrder;
            worldZ = overlayWorldZ;
            _configured = true;
            EnsureRuntimeSurface();
            FitToBounds();
        }

        public void SetVisible(bool visible)
        {
            EnsureRuntimeSurface();
            if (_renderer != null)
                _renderer.enabled = visible && _configured;
        }

        public static bool ShouldShowForWorld(
            string worldId,
            bool presentationActive)
        {
            return presentationActive &&
                string.Equals(
                    worldId,
                    "NEON_WINDOW",
                    System.StringComparison.Ordinal);
        }

        private void Awake()
        {
            EnsureRuntimeSurface();
        }

        private void OnEnable()
        {
            if (_configured)
                FitToBounds();
        }

        private void LateUpdate()
        {
            if (_configured)
                FitToBounds();
        }

        private void OnDisable()
        {
            if (_renderer != null)
                _renderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_sprite != null)
                DestroyRuntimeObject(_sprite);
            if (_texture != null)
                DestroyRuntimeObject(_texture);
        }

        private void EnsureRuntimeSurface()
        {
            if (_renderer != null)
                return;

            _texture = CreateGlassTexture();
            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, TextureWidth, TextureHeight),
                new Vector2(0.5f, 0.5f),
                1f);
            _sprite.name = "NeonWindowPixelGlassOverlay";

            GameObject surface = new GameObject(
                "NeonWindowPixelGlassOverlaySurface");
            surface.hideFlags = HideFlags.HideAndDontSave;
            surface.transform.SetParent(transform, false);
            _renderer = surface.AddComponent<SpriteRenderer>();
            _renderer.sprite = _sprite;
            _renderer.sortingOrder = sortingOrder;
            _renderer.color = Color.white;
            _renderer.enabled = false;
            FitToBounds();
        }

        private void FitToBounds()
        {
            if (_renderer == null)
                return;

            Bounds bounds;
            if (_targetCamera != null && _targetCamera.orthographic)
            {
                float height = Mathf.Max(
                    0.01f,
                    _targetCamera.orthographicSize * 2f);
                float aspect = Mathf.Max(0.01f, _targetCamera.aspect);
                bounds = new Bounds(
                    _targetCamera.transform.position,
                    new Vector3(
                        height * aspect * 1.02f,
                        height * 1.02f,
                        0f));
            }
            else
            {
                bounds = _worldBounds != null
                    ? _worldBounds.bounds
                    : new Bounds(
                        Vector3.zero,
                        new Vector3(42f, 24f, 0f));
            }
            Transform surface = _renderer.transform;
            surface.position = new Vector3(
                bounds.center.x,
                bounds.center.y,
                worldZ);
            surface.localRotation = Quaternion.identity;
            surface.localScale = new Vector3(
                bounds.size.x / TextureWidth,
                bounds.size.y / TextureHeight,
                1f);
            _renderer.sortingOrder = sortingOrder;
        }

        private Texture2D CreateGlassTexture()
        {
            Texture2D texture = new Texture2D(
                TextureWidth,
                TextureHeight,
                TextureFormat.RGBA32,
                false)
            {
                name = "NeonWindowPixelGlassOverlayTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[TextureWidth * TextureHeight];
            float[] rainColumns = { 0.12f, 0.29f, 0.48f, 0.69f, 0.87f };
            Vector2[] dropletCenters =
            {
                new Vector2(0.09f, 0.20f),
                new Vector2(0.88f, 0.31f),
                new Vector2(0.17f, 0.78f),
                new Vector2(0.82f, 0.82f)
            };

            for (int y = 0; y < TextureHeight; y++)
            {
                float v = (y + 0.5f) / TextureHeight;
                for (int x = 0; x < TextureWidth; x++)
                {
                    float u = (x + 0.5f) / TextureWidth;
                    float edgeDistance = Mathf.Min(
                        Mathf.Min(u, 1f - u),
                        Mathf.Min(v, 1f - v));
                    float edge = 1f - Mathf.SmoothStep(
                        0.08f,
                        0.38f,
                        edgeDistance);

                    float cloudA = 0.5f + 0.5f * Mathf.Sin(
                        u * 37f + v * 13f);
                    float cloudB = 0.5f + 0.5f * Mathf.Sin(
                        u * 17f - v * 31f + 1.4f);
                    float softCluster = Mathf.SmoothStep(
                        0.35f,
                        0.78f,
                        cloudA * cloudB);

                    float alpha = glassOpacity * (
                        edge * 0.90f +
                        softCluster * condensationOpacity *
                        (0.30f + edge * 0.70f));

                    for (int i = 0; i < rainColumns.Length; i++)
                    {
                        float columnDistance = Mathf.Abs(
                            u - rainColumns[i]);
                        float segment = Mathf.SmoothStep(
                            0.30f,
                            0.015f,
                            Mathf.Abs(v - (0.16f + i * 0.11f)));
                        float streak = Mathf.Exp(
                            -columnDistance * columnDistance * 900f) *
                            segment * rainOpacity;
                        alpha += glassOpacity * streak;
                    }

                    for (int i = 0; i < dropletCenters.Length; i++)
                    {
                        float distance = Vector2.Distance(
                            new Vector2(u, v),
                            dropletCenters[i]);
                        float ring = Mathf.SmoothStep(
                            0.040f,
                            0.014f,
                            Mathf.Abs(distance - 0.032f));
                        alpha += glassOpacity * ring * dropletOpacity;
                    }

                    Color color = glassTint;
                    color.a = Mathf.Clamp01(alpha);
                    pixels[y * TextureWidth + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
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
            glassOpacity = Mathf.Clamp01(glassOpacity);
            condensationOpacity = Mathf.Clamp01(condensationOpacity);
            rainOpacity = Mathf.Clamp01(rainOpacity);
            dropletOpacity = Mathf.Clamp01(dropletOpacity);
            glassTint.a = 1f;
        }
    }
}
