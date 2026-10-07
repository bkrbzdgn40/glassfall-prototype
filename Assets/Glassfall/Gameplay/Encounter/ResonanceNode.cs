using System;
using Glassfall.Gameplay.Link;
using UnityEngine;

namespace Glassfall.Gameplay.Encounter
{
    [DisallowMultipleComponent]
    public sealed class ResonanceNode : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SplitLinkSystem linkSystem;

        [Header("Interaction")]
        [Min(0.05f)]
        [SerializeField] private float interactionRadius = 0.65f;

        [Min(0.05f)]
        [SerializeField] private float chargeDuration = 0.8f;

        [Min(0f)]
        [SerializeField] private float chargeDecayPerSecond = 1.25f;

        [Header("Reward")]
        [Min(0f)]
        [SerializeField] private float combinedMassReward = 1.5f;

        [Header("Runtime Mirrors")]
        [SerializeField] private bool debugLinkAligned;
        [SerializeField] private float debugCharge01;
        [SerializeField] private bool debugCompleted;

        private float _chargeSeconds;
        private bool _completed;

        public event Action Completed;

        public float InteractionRadius => interactionRadius;
        public float Charge01 => chargeDuration <= 0f
            ? 1f
            : Mathf.Clamp01(_chargeSeconds / chargeDuration);
        public bool IsLinkAligned => debugLinkAligned;
        public bool IsCompleted => _completed;

        public void Configure(SplitLinkSystem source)
        {
            linkSystem = source;
        }

        private void Awake()
        {
            if (linkSystem == null)
                linkSystem = FindAnyObjectByType<SplitLinkSystem>();
        }

        private void Update()
        {
            if (_completed)
            {
                UpdateDiagnostics(false);
                return;
            }

            bool aligned = linkSystem != null &&
                linkSystem.IsSplit &&
                linkSystem.IsCircleOnFullLink(
                    transform.position,
                    interactionRadius);

            if (aligned)
                _chargeSeconds += Time.deltaTime;
            else
                _chargeSeconds = Mathf.Max(
                    0f,
                    _chargeSeconds - chargeDecayPerSecond * Time.deltaTime);

            if (_chargeSeconds >= chargeDuration)
                Complete();

            UpdateDiagnostics(aligned);
        }

        public void ResetNode()
        {
            _chargeSeconds = 0f;
            _completed = false;
            UpdateDiagnostics(false);
        }

        private void Complete()
        {
            if (_completed)
                return;

            _completed = true;
            _chargeSeconds = chargeDuration;
            linkSystem?.TryAddCombinedMassReward(combinedMassReward);
            Completed?.Invoke();
        }

        private void UpdateDiagnostics(bool aligned)
        {
            debugLinkAligned = aligned;
            debugCharge01 = Charge01;
            debugCompleted = _completed;
        }

        private void OnValidate()
        {
            interactionRadius = Mathf.Max(0.05f, interactionRadius);
            chargeDuration = Mathf.Max(0.05f, chargeDuration);
            chargeDecayPerSecond = Mathf.Max(0f, chargeDecayPerSecond);
            combinedMassReward = Mathf.Max(0f, combinedMassReward);
        }
    }
}
