using Glassfall.Gameplay.Wetness;
using UnityEngine;

namespace Glassfall.Presentation
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WetnessDebugRenderer : MonoBehaviour
    {
        [SerializeField] private WetnessField wetnessField;
        [SerializeField] private SpriteRenderer targetRenderer;

        [Header("Debug View")]
        [Min(0.01f)] [SerializeField] private float debugUpdateInterval = 0.1f;
        [SerializeField] private Color wetColor = Color.white;
        [SerializeField] private int sortingOrder = -10;

        private Texture2D _texture;
        private Sprite _sprite;
        private Color32[] _colors;
        private float _nextUpdateTime;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<SpriteRenderer>();

            if (targetRenderer != null)
                targetRenderer.sortingOrder = sortingOrder;
        }

        private void Start()
        {
            if (wetnessField == null || targetRenderer == null)
                return;

            int width = wetnessField.GridWidth;
            int height = wetnessField.GridHeight;
            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _texture.filterMode = FilterMode.Point;
            _texture.wrapMode = TextureWrapMode.Clamp;
            _colors = new Color32[width * height];
            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                1f);
            targetRenderer.sprite = _sprite;
            transform.position = wetnessField.WorldCenter;
            transform.localScale = new Vector3(
                wetnessField.WorldSize.x / width,
                wetnessField.WorldSize.y / height,
                1f);
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
                Mathf.Max(debugUpdateInterval, 0.01f);
            UpdateTexture();
        }

        private void UpdateTexture()
        {
            wetnessField.CopyTo(_colors, wetColor);
            _texture.SetPixels32(_colors);
            _texture.Apply(false, false);
        }

        private void OnDestroy()
        {
            if (_sprite != null)
                Destroy(_sprite);
            if (_texture != null)
                Destroy(_texture);
        }
    }
}
