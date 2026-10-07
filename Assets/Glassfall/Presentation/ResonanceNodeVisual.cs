using Glassfall.Gameplay.Encounter;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    [RequireComponent(typeof(ResonanceNode))]
    public sealed class ResonanceNodeVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer authoredSprite;

        [Range(12, 96)]
        [SerializeField] private int segments = 40;

        [Min(0.01f)]
        [SerializeField] private float ringWidth = 0.08f;

        [SerializeField] private Color idleColor =
            new Color(0.25f, 0.70f, 0.95f, 0.70f);

        [SerializeField] private Color chargingColor =
            new Color(0.45f, 1f, 0.88f, 0.95f);

        [SerializeField] private Color completedColor =
            new Color(0.95f, 1f, 1f, 0.35f);

        [Header("World Object Readability")]
        [Range(0f, 1f)]
        [SerializeField] private float ringAlphaMultiplier = 0.62f;

        [SerializeField] private int ringSortingOrder = 3;
        [SerializeField] private int spriteSortingOrder = 4;

        private ResonanceNode _node;
        private LineRenderer _line;
        private Material _runtimeMaterial;

        public void ConfigureWorldObjectReadability(
            SpriteRenderer sprite,
            float newRingWidth = 0.055f,
            float newRingAlphaMultiplier = 0.62f,
            int newRingSortingOrder = 3,
            int newSpriteSortingOrder = 4)
        {
            authoredSprite = sprite;
            ringWidth = Mathf.Max(0.01f, newRingWidth);
            ringAlphaMultiplier = Mathf.Clamp01(newRingAlphaMultiplier);
            ringSortingOrder = newRingSortingOrder;
            spriteSortingOrder = newSpriteSortingOrder;
            ApplyLayering();
        }

        private void Awake()
        {
            _node = GetComponent<ResonanceNode>();
            _line = GetComponent<LineRenderer>();
            CacheAuthoredSprite();
            ConfigureRenderer();
            RebuildCircle();
        }

        private void Update()
        {
            if (_node == null || _line == null)
                return;

            float pulse = 1f;
            Color color = idleColor;

            if (_node.IsCompleted)
            {
                color = completedColor;
                pulse = 1.10f;
            }
            else if (_node.IsLinkAligned)
            {
                color = Color.Lerp(
                    chargingColor,
                    Color.white,
                    _node.Charge01 * 0.35f);
                pulse = 1f + Mathf.Sin(Time.time * 14f) * 0.05f;
            }

            color.a *= ringAlphaMultiplier;
            _line.startColor = color;
            _line.endColor = color;
            transform.localScale = Vector3.one * pulse;
        }

        private void ConfigureRenderer()
        {
            _line.useWorldSpace = false;
            _line.loop = true;
            _line.startWidth = ringWidth;
            _line.endWidth = ringWidth;
            _line.numCornerVertices = 2;
            _line.numCapVertices = 2;
            _line.sortingOrder = ringSortingOrder;

            Shader shader = Shader.Find(
                "Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                _runtimeMaterial = new Material(shader)
                {
                    name = "ResonanceNodeVisual_Runtime"
                };
                _line.material = _runtimeMaterial;
            }

            ApplyLayering();
        }

        private void CacheAuthoredSprite()
        {
            if (authoredSprite != null)
                return;

            Transform child = transform.Find("PixelSprite");
            if (child != null)
                authoredSprite = child.GetComponent<SpriteRenderer>();
        }

        private void ApplyLayering()
        {
            if (_line == null)
                _line = GetComponent<LineRenderer>();
            if (_line != null)
                _line.sortingOrder = ringSortingOrder;

            CacheAuthoredSprite();
            if (authoredSprite != null)
                authoredSprite.sortingOrder = spriteSortingOrder;
        }

        private void RebuildCircle()
        {
            int count = Mathf.Max(12, segments);
            _line.positionCount = count;
            float radius = _node != null ? _node.InteractionRadius : 0.65f;

            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                _line.SetPosition(
                    i,
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        0f));
            }
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
        }

        private void OnValidate()
        {
            segments = Mathf.Clamp(segments, 12, 96);
            ringWidth = Mathf.Max(0.01f, ringWidth);
            ringAlphaMultiplier = Mathf.Clamp01(ringAlphaMultiplier);
            ApplyLayering();
        }
    }
}
