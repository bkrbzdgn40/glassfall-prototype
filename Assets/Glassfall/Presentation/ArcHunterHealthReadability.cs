using Glassfall.Gameplay.Combat;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ArcHunter))]
    public sealed class ArcHunterHealthReadability : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField] private ArcHunter hunter;

        [Header("Subtle Health Arc")]
        [SerializeField] private bool showHealthArc = true;
        [Range(12, 64)]
        [SerializeField] private int segments = 30;
        [Min(0.01f)]
        [SerializeField] private float radiusPadding = 0.09f;
        [Min(0.005f)]
        [SerializeField] private float arcWidth = 0.025f;
        [Range(90f, 330f)]
        [SerializeField] private float fullArcDegrees = 270f;
        [SerializeField] private float startAngleDegrees = 135f;
        [SerializeField] private Color healthyColor =
            new Color(0.78f, 1f, 1f, 0.62f);
        [SerializeField] private Color depletedColor =
            new Color(0.42f, 0.72f, 0.78f, 0.76f);
        [Range(0f, 1f)]
        [SerializeField] private float hitAlphaBoost = 0.18f;
        [SerializeField] private int sortingOrder = 6;

        private const string RuntimeArcName = "HealthReadabilityArc_Runtime";

        private LineRenderer _healthArc;
        private Material _runtimeMaterial;
        private float _lastHealth01 = -1f;
        private bool _lastTakingLinkDamage;

        public void Configure(
            ArcHunter source,
            float padding = 0.09f,
            float width = 0.025f,
            float arcDegrees = 270f,
            int newSortingOrder = 6)
        {
            hunter = source != null ? source : GetComponent<ArcHunter>();
            radiusPadding = Mathf.Max(0.01f, padding);
            arcWidth = Mathf.Max(0.005f, width);
            fullArcDegrees = Mathf.Clamp(arcDegrees, 90f, 330f);
            sortingOrder = newSortingOrder;

            if (_healthArc != null)
                ConfigureRenderer();

            _lastHealth01 = -1f;
        }

        private void Awake()
        {
            if (hunter == null)
                hunter = GetComponent<ArcHunter>();

            EnsureRuntimeArc();
            Refresh(force: true);
        }

        private void OnEnable()
        {
            if (hunter == null)
                hunter = GetComponent<ArcHunter>();

            EnsureRuntimeArc();
            _lastHealth01 = -1f;
        }

        private void Update()
        {
            Refresh(force: false);
        }

        private void Refresh(bool force)
        {
            if (_healthArc == null)
                EnsureRuntimeArc();

            if (_healthArc == null)
                return;

            if (!showHealthArc || hunter == null || hunter.IsDefeated)
            {
                _healthArc.enabled = false;
                return;
            }

            float health01 = Mathf.Clamp01(hunter.Health01);
            bool takingLinkDamage = hunter.IsTakingLinkDamage;

            // Full health needs no extra UI. The primary combat-state ring remains
            // the dominant visual until the player has actually dealt damage.
            if (health01 >= 0.999f)
            {
                _healthArc.enabled = false;
                _lastHealth01 = health01;
                _lastTakingLinkDamage = takingLinkDamage;
                return;
            }

            _healthArc.enabled = true;
            if (!force &&
                Mathf.Abs(health01 - _lastHealth01) <= 0.0001f &&
                takingLinkDamage == _lastTakingLinkDamage)
            {
                return;
            }

            RebuildArc(health01, takingLinkDamage);
            _lastHealth01 = health01;
            _lastTakingLinkDamage = takingLinkDamage;
        }

        private void EnsureRuntimeArc()
        {
            if (_healthArc != null)
                return;

            Transform existing = transform.Find(RuntimeArcName);
            GameObject arcObject;
            if (existing != null)
            {
                arcObject = existing.gameObject;
            }
            else
            {
                arcObject = new GameObject(RuntimeArcName);
                arcObject.transform.SetParent(transform, false);
            }

            _healthArc = arcObject.GetComponent<LineRenderer>();
            if (_healthArc == null)
                _healthArc = arcObject.AddComponent<LineRenderer>();

            ConfigureRenderer();
        }

        private void ConfigureRenderer()
        {
            if (_healthArc == null)
                return;

            _healthArc.useWorldSpace = false;
            _healthArc.loop = false;
            _healthArc.startWidth = arcWidth;
            _healthArc.endWidth = arcWidth;
            _healthArc.numCornerVertices = 2;
            _healthArc.numCapVertices = 2;
            _healthArc.sortingOrder = sortingOrder;

            if (_runtimeMaterial == null)
            {
                Shader shader = Shader.Find(
                    "Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");

                if (shader != null)
                {
                    _runtimeMaterial = new Material(shader)
                    {
                        name = "ArcHunterHealthReadability_Runtime"
                    };
                }
            }

            if (_runtimeMaterial != null)
                _healthArc.material = _runtimeMaterial;
        }

        private void RebuildArc(float health01, bool takingLinkDamage)
        {
            int safeSegments = Mathf.Clamp(segments, 12, 64);
            int visibleSegments = Mathf.Clamp(
                Mathf.CeilToInt(safeSegments * health01),
                1,
                safeSegments);
            int pointCount = visibleSegments + 1;
            _healthArc.positionCount = pointCount;

            float radius = Mathf.Max(
                0.05f,
                hunter.InteractionRadius + radiusPadding);
            float visibleArcDegrees = fullArcDegrees * health01;

            for (int index = 0; index < pointCount; index++)
            {
                float t = pointCount <= 1
                    ? 0f
                    : index / (float)(pointCount - 1);
                float angleDegrees = startAngleDegrees + visibleArcDegrees * t;
                float angleRadians = angleDegrees * Mathf.Deg2Rad;
                _healthArc.SetPosition(
                    index,
                    new Vector3(
                        Mathf.Cos(angleRadians) * radius,
                        Mathf.Sin(angleRadians) * radius,
                        0f));
            }

            Color color = Color.Lerp(
                depletedColor,
                healthyColor,
                health01);
            if (takingLinkDamage)
                color.a = Mathf.Clamp01(color.a + hitAlphaBoost);

            _healthArc.startColor = color;
            _healthArc.endColor = color;
            _healthArc.startWidth = arcWidth;
            _healthArc.endWidth = arcWidth;
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
        }

        private void OnValidate()
        {
            segments = Mathf.Clamp(segments, 12, 64);
            radiusPadding = Mathf.Max(0.01f, radiusPadding);
            arcWidth = Mathf.Max(0.005f, arcWidth);
            fullArcDegrees = Mathf.Clamp(fullArcDegrees, 90f, 330f);
            hitAlphaBoost = Mathf.Clamp01(hitAlphaBoost);
        }
    }
}
