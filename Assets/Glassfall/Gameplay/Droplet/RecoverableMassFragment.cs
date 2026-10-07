using UnityEngine;

namespace Glassfall.Gameplay.Droplet
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NeutralDroplet))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class RecoverableMassFragment : MonoBehaviour
    {
        [Header("Recovery")]
        [Min(0f)]
        [SerializeField] private float pickupLockSeconds = 0.55f;

        [Header("Runtime Mirrors")]
        [SerializeField] private float debugUnlockRemaining;

        private CircleCollider2D _trigger;
        private float _unlockAt;
        private bool _pickupLocked;

        public bool PickupLocked => _pickupLocked;

        private void Awake()
        {
            _trigger = GetComponent<CircleCollider2D>();
        }

        private void Update()
        {
            if (!_pickupLocked)
                return;

            debugUnlockRemaining = Mathf.Max(0f, _unlockAt - Time.time);
            if (Time.time < _unlockAt)
                return;

            UnlockPickup();
        }

        public void Configure(float lockSeconds = 0.55f)
        {
            pickupLockSeconds = Mathf.Max(0f, lockSeconds);
            if (_trigger == null)
                _trigger = GetComponent<CircleCollider2D>();

            if (pickupLockSeconds <= 0f)
            {
                UnlockPickup();
                return;
            }

            _pickupLocked = true;
            _unlockAt = Time.time + pickupLockSeconds;
            debugUnlockRemaining = pickupLockSeconds;

            if (_trigger != null)
                _trigger.enabled = false;
        }

        private void UnlockPickup()
        {
            _pickupLocked = false;
            debugUnlockRemaining = 0f;

            if (_trigger == null)
                _trigger = GetComponent<CircleCollider2D>();
            if (_trigger != null)
                _trigger.enabled = true;
        }

        private void OnValidate()
        {
            pickupLockSeconds = Mathf.Max(0f, pickupLockSeconds);
        }
    }
}
