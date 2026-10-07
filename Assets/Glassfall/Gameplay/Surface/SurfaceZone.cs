using UnityEngine;

namespace Glassfall.Gameplay.Surface
{
    public enum SurfaceType
    {
        NormalGlass,
        DirtyGlass
    }

    public readonly struct SurfaceModifiers
    {
        public SurfaceModifiers(
            float acceleration,
            float speed,
            float drag,
            float response)
        {
            Acceleration = Mathf.Max(0f, acceleration);
            Speed = Mathf.Max(0f, speed);
            Drag = Mathf.Max(0f, drag);
            Response = Mathf.Max(0f, response);
        }

        public float Acceleration { get; }
        public float Speed { get; }
        public float Drag { get; }
        public float Response { get; }

        public static SurfaceModifiers Normal =>
            new SurfaceModifiers(1f, 1f, 1f, 1f);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class SurfaceZone : MonoBehaviour
    {
        [SerializeField] private SurfaceType surfaceType = SurfaceType.DirtyGlass;

        [Header("Dirty Glass Modifiers")]
        [Min(0f)] [SerializeField] private float accelerationMultiplier = 0.8f;
        [Min(0f)] [SerializeField] private float speedMultiplier = 0.75f;
        [Min(0f)] [SerializeField] private float dragMultiplier = 1.35f;
        [Min(0f)] [SerializeField] private float responseMultiplier = 0.8f;

        private BoxCollider2D _trigger;

        public SurfaceType SurfaceType => surfaceType;

        public SurfaceModifiers Modifiers =>
            surfaceType == SurfaceType.NormalGlass
                ? SurfaceModifiers.Normal
                : new SurfaceModifiers(
                    accelerationMultiplier,
                    speedMultiplier,
                    dragMultiplier,
                    responseMultiplier);

        private void Awake()
        {
            ConfigureTrigger();
        }

        private void Reset()
        {
            ConfigureTrigger();
        }

        private void OnValidate()
        {
            accelerationMultiplier = Mathf.Max(0f, accelerationMultiplier);
            speedMultiplier = Mathf.Max(0f, speedMultiplier);
            dragMultiplier = Mathf.Max(0f, dragMultiplier);
            responseMultiplier = Mathf.Max(0f, responseMultiplier);
            ConfigureTrigger();
        }

        private void ConfigureTrigger()
        {
            if (_trigger == null)
                _trigger = GetComponent<BoxCollider2D>();

            if (_trigger != null)
                _trigger.isTrigger = true;
        }
    }
}
