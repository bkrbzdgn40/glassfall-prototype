using System;
using UnityEngine;

namespace Glassfall.Gameplay.Droplet
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class NeutralDroplet : MonoBehaviour
    {
        private const float MinimumValue = 0.01f;

        [Header("Gameplay")]
        [Min(MinimumValue)]
        [SerializeField] private float mass = 1f;

        [Header("Visual Size Hint")]
        [Min(MinimumValue)]
        [SerializeField] private float baseRadius = 0.45f;

        [Min(MinimumValue)]
        [SerializeField] private float referenceMass = 10f;

        [SerializeField, HideInInspector]
        private Vector3 authoredLocalScale;

        [SerializeField, HideInInspector]
        private float authoredVisualRadius;

        [SerializeField, HideInInspector]
        private float lastAppliedVisualRadius;

        [SerializeField, HideInInspector]
        private bool hasAuthoredSize;

        private CircleCollider2D _trigger;
        private bool _consumed;

        public event Action<NeutralDropletConsumed> Consumed;

        public float Mass => Mathf.Max(mass, MinimumValue);

        public float VisualRadius => GetVisualRadiusForMass(Mass);

        public bool IsConsumed => _consumed;

        public float GetVisualRadiusForMass(float value)
        {
            return Mathf.Max(baseRadius, MinimumValue) *
                Mathf.Sqrt(
                    Mathf.Max(value, MinimumValue) /
                    Mathf.Max(referenceMass, MinimumValue));
        }

        private void Awake()
        {
            ClampSerializedValues();
            ConfigureTrigger();
            RefreshAuthoredSizeIfTransformChanged();
            ApplySize();
        }

        private void Reset()
        {
            ClampSerializedValues();
            ConfigureTrigger();
            CaptureAuthoredSize(GetCurrentWorldColliderRadius());
            ApplySize();
        }

        private void OnValidate()
        {
            ClampSerializedValues();
            ConfigureTrigger();
            RefreshAuthoredSizeIfTransformChanged();
            ApplySize();
        }

        public void Initialize(float initialMass)
        {
            mass = Mathf.Max(initialMass, MinimumValue);
            _consumed = false;
            ConfigureTrigger();
            RefreshAuthoredSizeIfTransformChanged();
            ApplySize();

            if (_trigger != null)
                _trigger.enabled = true;
        }

        public bool TryConsume(out float consumedMass)
        {
            if (_consumed)
            {
                consumedMass = 0f;
                return false;
            }

            _consumed = true;
            consumedMass = Mass;

            if (_trigger == null)
                _trigger = GetComponent<CircleCollider2D>();

            if (_trigger != null)
                _trigger.enabled = false;

            Consumed?.Invoke(
                new NeutralDropletConsumed(
                    consumedMass,
                    transform.position));

            return true;
        }

        private void ConfigureTrigger()
        {
            if (_trigger == null)
                _trigger = GetComponent<CircleCollider2D>();

            if (_trigger != null)
                _trigger.isTrigger = true;
        }

        private void ClampSerializedValues()
        {
            mass = Mathf.Max(mass, MinimumValue);
            baseRadius = Mathf.Max(baseRadius, MinimumValue);
            referenceMass = Mathf.Max(referenceMass, MinimumValue);
        }

        private void RefreshAuthoredSizeIfTransformChanged()
        {
            if (!HasValidAuthoredSize())
            {
                CaptureAuthoredSize(GetCurrentWorldColliderRadius());
                return;
            }

            Vector3 expectedScale = GetLocalScale(lastAppliedVisualRadius);
            if (Approximately(transform.localScale, expectedScale))
                return;

            float radiusAtCurrentScale = Mathf.Max(
                lastAppliedVisualRadius,
                MinimumValue);
            CaptureAuthoredSize(radiusAtCurrentScale);
        }

        private void CaptureAuthoredSize(float radius)
        {
            authoredLocalScale = transform.localScale;
            authoredVisualRadius = Mathf.Max(radius, MinimumValue);
            lastAppliedVisualRadius = authoredVisualRadius;
            hasAuthoredSize = true;
        }

        private void ApplySize()
        {
            if (!HasValidAuthoredSize())
                CaptureAuthoredSize(GetCurrentWorldColliderRadius());

            float targetRadius = VisualRadius;
            transform.localScale = GetLocalScale(targetRadius);
            ApplyColliderSize(targetRadius);
            lastAppliedVisualRadius = targetRadius;
        }

        private void ApplyColliderSize(float targetWorldRadius)
        {
            if (_trigger == null)
                return;

            _trigger.radius = targetWorldRadius / GetMaxWorldScale();
        }

        private float GetCurrentWorldColliderRadius()
        {
            if (_trigger == null)
                return VisualRadius;

            return Mathf.Max(
                _trigger.radius * GetMaxWorldScale(),
                MinimumValue);
        }

        private float GetMaxWorldScale()
        {
            Vector3 worldScale = transform.lossyScale;
            return Mathf.Max(
                Mathf.Abs(worldScale.x),
                Mathf.Abs(worldScale.y),
                MinimumValue);
        }

        private Vector3 GetLocalScale(float radius)
        {
            float scaleFactor =
                Mathf.Max(radius, MinimumValue) /
                Mathf.Max(authoredVisualRadius, MinimumValue);

            return new Vector3(
                authoredLocalScale.x * scaleFactor,
                authoredLocalScale.y * scaleFactor,
                authoredLocalScale.z);
        }

        private bool HasValidAuthoredSize()
        {
            return hasAuthoredSize &&
                authoredVisualRadius >= MinimumValue &&
                lastAppliedVisualRadius >= MinimumValue;
        }

        private static bool Approximately(Vector3 first, Vector3 second)
        {
            return Mathf.Approximately(first.x, second.x) &&
                Mathf.Approximately(first.y, second.y) &&
                Mathf.Approximately(first.z, second.z);
        }
    }

    public readonly struct NeutralDropletConsumed
    {
        public NeutralDropletConsumed(float mass, Vector2 position)
        {
            Mass = mass;
            Position = position;
        }

        public float Mass { get; }
        public Vector2 Position { get; }
    }
}
