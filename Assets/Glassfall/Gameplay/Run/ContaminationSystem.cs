using UnityEngine;

namespace Glassfall.Gameplay.Run
{
    public enum ContaminationState
    {
        Clean = 0,
        Tainted = 1,
        Corrupted = 2,
        Critical = 3,
        Contaminated = 4
    }

    [DisallowMultipleComponent]
    public sealed class ContaminationSystem : MonoBehaviour
    {
        [Header("Arc Hunter Exposure")]
        [Range(0f, 1f)]
        [SerializeField] private float mergedHitGain = 0.18f;

        [Range(0f, 1f)]
        [SerializeField] private float splitHitGain = 0.23f;

        [Header("Purification")]
        [Range(0f, 1f)]
        [SerializeField] private float neutralCleanseAmount = 0.04f;

        [Range(0f, 1f)]
        [SerializeField] private float recoveryCleanseAmount = 0.24f;

        [Header("Runtime Reward Tuning")]
        [Range(0.5f, 1.5f)]
        [SerializeField] private float recoveryCleanseRewardMultiplier = 1f;

        [Header("Visual State Thresholds")]
        [Range(0f, 1f)]
        [SerializeField] private float taintedThreshold = 0.12f;

        [Range(0f, 1f)]
        [SerializeField] private float corruptedThreshold = 0.38f;

        [Range(0f, 1f)]
        [SerializeField] private float criticalThreshold = 0.72f;

        [Range(0.01f, 1f)]
        [SerializeField] private float failureThreshold = 1f;

        [Header("Presentation Beats")]
        [Min(0.01f)]
        [SerializeField] private float hitFlashSeconds = 0.18f;

        [Min(0.01f)]
        [SerializeField] private float cleansePulseSeconds = 0.38f;

        [Header("Runtime Mirrors")]
        [SerializeField] private float debugContamination01;
        [SerializeField] private ContaminationState debugState;

        private float _contamination01;
        private float _hitFlashRemaining;
        private float _cleansePulseRemaining;

        public float Value01 => Mathf.Clamp01(_contamination01);
        public ContaminationState State => GetStateFor(
            Value01,
            taintedThreshold,
            corruptedThreshold,
            criticalThreshold,
            failureThreshold);
        public bool HasFailed => State == ContaminationState.Contaminated;
        public float HitFlash01 => Mathf.Clamp01(
            _hitFlashRemaining / Mathf.Max(hitFlashSeconds, 0.01f));
        public float CleansePulse01 => Mathf.Clamp01(
            _cleansePulseRemaining /
            Mathf.Max(cleansePulseSeconds, 0.01f));
        public float RecoveryCleanseAmount =>
            recoveryCleanseAmount * recoveryCleanseRewardMultiplier;

        public void Configure(
            float mergedGain = 0.18f,
            float splitGain = 0.23f,
            float neutralCleanse = 0.04f,
            float recoveryCleanse = 0.24f,
            float taintedAt = 0.12f,
            float corruptedAt = 0.38f,
            float criticalAt = 0.72f,
            float failedAt = 1f)
        {
            mergedHitGain = Mathf.Clamp01(mergedGain);
            splitHitGain = Mathf.Clamp01(splitGain);
            neutralCleanseAmount = Mathf.Clamp01(neutralCleanse);
            recoveryCleanseAmount = Mathf.Clamp01(recoveryCleanse);
            SetThresholds(
                taintedAt,
                corruptedAt,
                criticalAt,
                failedAt);
            UpdateDiagnostics();
        }

        public float ApplyArcHunterHit(bool splitEndpointHit)
        {
            float gain = splitEndpointHit
                ? splitHitGain
                : mergedHitGain;
            _contamination01 = CalculateAfterGain(
                _contamination01,
                gain);
            _hitFlashRemaining = hitFlashSeconds;
            _cleansePulseRemaining = 0f;
            UpdateDiagnostics();
            return Value01;
        }

        public float ApplyNeutralCleanse()
        {
            return ApplyCleanse(neutralCleanseAmount);
        }

        public float ApplyRecoveryCleanse()
        {
            return ApplyCleanse(
                RecoveryCleanseAmount,
                guaranteeTierDrop: true);
        }

        public void ResetRuntimeRewardTuning()
        {
            recoveryCleanseRewardMultiplier = 1f;
        }

        public void ApplyPurifyingResonanceReward(
            float cleanseMultiplier = 1.15f)
        {
            recoveryCleanseRewardMultiplier = Mathf.Clamp(
                Mathf.Max(1f, cleanseMultiplier),
                0.5f,
                1.5f);
        }

        public void ConfigurePresentationBeats(
            float hitFlashDuration = 0.18f,
            float cleansePulseDuration = 0.38f)
        {
            hitFlashSeconds = Mathf.Max(0.01f, hitFlashDuration);
            cleansePulseSeconds = Mathf.Max(0.01f, cleansePulseDuration);
        }

        public void ResetForNewRun()
        {
            ResetRuntimeRewardTuning();
            _contamination01 = 0f;
            _hitFlashRemaining = 0f;
            _cleansePulseRemaining = 0f;
            UpdateDiagnostics();
        }

        public static float CalculateAfterGain(
            float current,
            float gain)
        {
            return Mathf.Clamp01(
                Mathf.Clamp01(current) + Mathf.Max(0f, gain));
        }

        public static float CalculateAfterCleanse(
            float current,
            float cleanse)
        {
            return Mathf.Clamp01(
                Mathf.Clamp01(current) - Mathf.Max(0f, cleanse));
        }

        public static ContaminationState GetStateFor(
            float contamination,
            float taintedAt,
            float corruptedAt,
            float criticalAt,
            float failedAt)
        {
            float value = Mathf.Clamp01(contamination);
            float tainted = Mathf.Clamp01(taintedAt);
            float corrupted = Mathf.Clamp(
                corruptedAt,
                tainted,
                1f);
            float critical = Mathf.Clamp(
                criticalAt,
                corrupted,
                1f);
            float failed = Mathf.Clamp(
                failedAt,
                critical,
                1f);

            if (value + 0.0001f >= failed)
                return ContaminationState.Contaminated;
            if (value + 0.0001f >= critical)
                return ContaminationState.Critical;
            if (value + 0.0001f >= corrupted)
                return ContaminationState.Corrupted;
            if (value + 0.0001f >= tainted)
                return ContaminationState.Tainted;
            return ContaminationState.Clean;
        }

        private void Update()
        {
            // These are presentation-only timers. Keep them independent from
            // reward/pause timeScale changes so a hit or cleanse pulse cannot
            // get visually stuck behind a frozen gameplay state.
            float deltaTime = Time.unscaledDeltaTime;
            _hitFlashRemaining = Mathf.Max(
                0f,
                _hitFlashRemaining - deltaTime);
            _cleansePulseRemaining = Mathf.Max(
                0f,
                _cleansePulseRemaining - deltaTime);
            UpdateDiagnostics();
        }

        private float ApplyCleanse(
            float amount,
            bool guaranteeTierDrop = false)
        {
            float previous = _contamination01;
            ContaminationState previousState = State;

            _contamination01 = CalculateAfterCleanse(
                _contamination01,
                amount);

            if (guaranteeTierDrop &&
                previousState != ContaminationState.Clean)
            {
                float tierTarget = GetTierDropTarget(previousState);
                _contamination01 = Mathf.Min(
                    _contamination01,
                    tierTarget);
            }

            if (_contamination01 + 0.0001f < previous)
            {
                _cleansePulseRemaining = cleansePulseSeconds;
                _hitFlashRemaining = 0f;
            }

            UpdateDiagnostics();
            return Value01;
        }

        private float GetTierDropTarget(ContaminationState previousState)
        {
            const float epsilon = 0.01f;

            return previousState switch
            {
                ContaminationState.Contaminated =>
                    Mathf.Max(0f, failureThreshold - epsilon),
                ContaminationState.Critical =>
                    Mathf.Max(0f, criticalThreshold - epsilon),
                ContaminationState.Corrupted =>
                    Mathf.Max(0f, corruptedThreshold - epsilon),
                ContaminationState.Tainted =>
                    Mathf.Max(0f, taintedThreshold - epsilon),
                _ => 0f
            };
        }

        private void SetThresholds(
            float taintedAt,
            float corruptedAt,
            float criticalAt,
            float failedAt)
        {
            taintedThreshold = Mathf.Clamp01(taintedAt);
            corruptedThreshold = Mathf.Clamp(
                corruptedAt,
                taintedThreshold,
                1f);
            criticalThreshold = Mathf.Clamp(
                criticalAt,
                corruptedThreshold,
                1f);
            failureThreshold = Mathf.Clamp(
                failedAt,
                criticalThreshold,
                1f);
        }

        private void UpdateDiagnostics()
        {
            debugContamination01 = Value01;
            debugState = State;
        }

        private void OnValidate()
        {
            mergedHitGain = Mathf.Clamp01(mergedHitGain);
            splitHitGain = Mathf.Clamp01(splitHitGain);
            neutralCleanseAmount = Mathf.Clamp01(neutralCleanseAmount);
            recoveryCleanseAmount = Mathf.Clamp01(recoveryCleanseAmount);
            recoveryCleanseRewardMultiplier = Mathf.Clamp(
                recoveryCleanseRewardMultiplier,
                0.5f,
                1.5f);
            SetThresholds(
                taintedThreshold,
                corruptedThreshold,
                criticalThreshold,
                failureThreshold);
            hitFlashSeconds = Mathf.Max(0.01f, hitFlashSeconds);
            cleansePulseSeconds = Mathf.Max(0.01f, cleansePulseSeconds);
        }
    }
}
