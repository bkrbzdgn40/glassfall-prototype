using Glassfall.Gameplay.Builds;
using Glassfall.Gameplay.Link;
using Glassfall.Gameplay.Wetness;
using UnityEngine;

namespace Glassfall.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public sealed class SplitLinkCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SplitLinkSystem linkSystem;
        [SerializeField] private ArcHunter target;
        [SerializeField] private WetnessField wetnessField;

        [Header("Link Damage")]
        [Min(0f)]
        [SerializeField] private float endpointInset = 0.45f;

        [Min(0f)]
        [SerializeField] private float hitPadding = 0.08f;

        [Min(0f)]
        [SerializeField] private float damagePerSecond = 7f;

        [Header("Wetness Conductivity")]
        [Range(0f, 1f)]
        [SerializeField] private float conductivityActivationThreshold = 0.15f;

        [Range(0f, 1f)]
        [SerializeField] private float fullEffectWetness = 0.50f;

        [Min(1f)]
        [SerializeField] private float fullConductivityDamageMultiplier = 2f;

        [Header("Surface Tension")]
        [Min(0f)]
        [SerializeField] private float closeMaxDistance = 1.5f;

        [Min(0.01f)]
        [SerializeField] private float stableMaxDistance = 5.5f;

        [Min(0.02f)]
        [SerializeField] private float fullPenaltyDistance = 9f;

        [Range(0f, 1f)]
        [SerializeField] private float minimumOverstretchedDamageMultiplier = 0.45f;

        [Header("Runtime Mirrors")]
        [SerializeField] private float debugTargetWetness;
        [SerializeField] private float debugConductivity01;
        [SerializeField] private float debugWetnessDamageMultiplier = 1f;
        [SerializeField] private float debugCapacitorCharge01;
        [SerializeField] private float debugCapacitorDamageMultiplier = 1f;
        [SerializeField] private float debugLinkSeparation;
        [SerializeField] private float debugSurfaceTensionOverstretch01;
        [SerializeField] private float debugSurfaceTensionDamageMultiplier = 1f;
        [SerializeField] private SurfaceTensionBand debugSurfaceTensionBand =
            SurfaceTensionBand.Close;
        [SerializeField] private float debugFinalDamageMultiplier = 1f;
        [SerializeField] private float debugAppliedOutputPerSecond;
        [SerializeField] private bool debugLinkHit;

        private float _runtimeHitPadding;
        private float _runtimeDamagePerSecond;
        private float _runtimeConductivityActivationThreshold;
        private float _runtimeFullEffectWetness;
        private float _runtimeFullConductivityDamageMultiplier;
        private bool _runtimeTuningInitialized;

        private bool _encounterActive = true;
        private bool _stableLinkRewardEnabled;
        private float _stableLinkEndpointInset = 0.32f;
        private bool _conductiveResidueRewardEnabled;
        private float _conductiveResidueActivationThreshold = 0.10f;
        private bool _elasticMembraneRewardEnabled;
        private float _elasticMembraneStableDistanceMultiplier = 1.20f;
        private bool _capacitorEnabled;
        private float _capacitorChargeDelay = 1.5f;
        private float _capacitorChargeDuration = 2.5f;
        private float _capacitorMaxMultiplier = 1.6f;
        private float _splitDuration;

        public WetnessField WetnessField => wetnessField;
        public float TargetWetness => debugTargetWetness;
        public float Conductivity01 => debugConductivity01;
        public float CurrentDamageMultiplier => debugFinalDamageMultiplier;
        public float CurrentAppliedOutputPerSecond =>
            debugAppliedOutputPerSecond;
        public bool IsApplyingLinkDamage =>
            debugLinkHit && debugAppliedOutputPerSecond > 0.001f;
        public float EndpointInset => GetRuntimeEndpointInset();
        public float HitPadding => GetRuntimeHitPadding();
        public float DamagePerSecond => GetRuntimeDamagePerSecond();
        public float ConductivityActivationThreshold =>
            GetRuntimeConductivityActivationThreshold();
        public float FullEffectWetness => GetRuntimeFullEffectWetness();
        public float FullConductivityDamageMultiplier =>
            GetRuntimeFullConductivityDamageMultiplier();
        public float CapacitorCharge01 => debugCapacitorCharge01;
        public float CapacitorDamageMultiplier => debugCapacitorDamageMultiplier;
        public float LinkSeparation => debugLinkSeparation;
        public float SurfaceTensionOverstretch01 =>
            debugSurfaceTensionOverstretch01;
        public float SurfaceTensionDamageMultiplier =>
            debugSurfaceTensionDamageMultiplier;
        public SurfaceTensionBand CurrentSurfaceTensionBand =>
            debugSurfaceTensionBand;
        public float StableMaxDistance => GetRuntimeStableMaxDistance();
        public float FullPenaltyDistance => GetRuntimeFullPenaltyDistance();
        public bool CapacitorEnabled => _capacitorEnabled;
        public bool IsEncounterActive => _encounterActive;

        public void Configure(
            SplitLinkSystem source,
            ArcHunter hunter)
        {
            linkSystem = source;
            target = hunter;
        }

        public void ConfigureWetness(WetnessField field)
        {
            wetnessField = field;
        }

        public void ConfigureSurfaceTension(
            float closeDistance = 1.5f,
            float stableDistance = 5.5f,
            float fullPenaltyAtDistance = 9f,
            float minimumDamageMultiplier = 0.45f)
        {
            closeMaxDistance = Mathf.Max(0f, closeDistance);
            stableMaxDistance = Mathf.Max(closeMaxDistance, stableDistance);
            fullPenaltyDistance = Mathf.Max(
                stableMaxDistance + 0.01f,
                fullPenaltyAtDistance);
            minimumOverstretchedDamageMultiplier =
                Mathf.Clamp01(minimumDamageMultiplier);
        }

        private void Awake()
        {
            if (linkSystem == null)
                linkSystem = GetComponent<SplitLinkSystem>();
            if (linkSystem == null)
                linkSystem = FindAnyObjectByType<SplitLinkSystem>();
            if (target == null)
                target = FindAnyObjectByType<ArcHunter>();

            ResetRuntimeBuildTuning();
            ResetRuntimeRewardTuning();
        }

        private void OnDisable()
        {
            ClearTargetRuntimeState();
            _splitDuration = 0f;
            UpdateCapacitorDiagnostics(0f, 1f);
            UpdateSurfaceTensionDiagnostics(false, Vector2.zero, Vector2.zero);
        }

        private void FixedUpdate()
        {
            if (!_encounterActive)
            {
                ClearTargetRuntimeState();
                _splitDuration = 0f;
                UpdateCapacitorDiagnostics(0f, 1f);
                UpdateSurfaceTensionDiagnostics(
                    false,
                    Vector2.zero,
                    Vector2.zero);
                UpdateDiagnostics(0f, 0f, 1f, false);
                return;
            }

            Vector2 fullStart = default;
            Vector2 fullEnd = default;
            bool hasValidSplitPair = linkSystem != null &&
                linkSystem.TryGetFullSegment(
                    out fullStart,
                    out fullEnd);
            UpdateCapacitorState(hasValidSplitPair, Time.fixedDeltaTime);
            UpdateSurfaceTensionDiagnostics(
                hasValidSplitPair,
                fullStart,
                fullEnd);

            if (target == null || target.IsDefeated || linkSystem == null)
            {
                ClearTargetRuntimeState();
                UpdateDiagnostics(0f, 0f, 1f, false);
                return;
            }

            float targetWetness = wetnessField != null
                ? wetnessField.SampleWorld(target.transform.position)
                : 0f;

            float conductivity01 =
                WetLinkConductivity.CalculateConductivity01(
                    targetWetness,
                    GetRuntimeConductivityActivationThreshold(),
                    GetRuntimeFullEffectWetness());

            float wetnessDamageMultiplier =
                WetLinkConductivity.CalculateDamageMultiplier(
                    conductivity01,
                    GetRuntimeFullConductivityDamageMultiplier());
            float finalDamageMultiplier =
                wetnessDamageMultiplier *
                debugCapacitorDamageMultiplier *
                debugSurfaceTensionDamageMultiplier;

            target.SetConductivity(conductivity01);

            if (!linkSystem.TryGetTrimmedSegment(
                    GetRuntimeEndpointInset(),
                    out Vector2 start,
                    out Vector2 end))
            {
                target.SetTakingLinkDamage(false);
                UpdateDiagnostics(
                    targetWetness,
                    conductivity01,
                    finalDamageMultiplier,
                    false);
                return;
            }

            bool hit = SplitLinkGeometry.IsCircleTouchingSegment(
                target.transform.position,
                target.InteractionRadius + GetRuntimeHitPadding(),
                start,
                end);

            target.SetTakingLinkDamage(hit);
            float appliedOutputPerSecond = hit
                ? GetRuntimeDamagePerSecond() * finalDamageMultiplier
                : 0f;
            if (hit)
            {
                target.ApplyLinkDamage(
                    appliedOutputPerSecond * Time.fixedDeltaTime);
            }

            UpdateDiagnostics(
                targetWetness,
                conductivity01,
                finalDamageMultiplier,
                hit,
                appliedOutputPerSecond);
        }

        public void SetEncounterActive(bool active)
        {
            _encounterActive = active;

            if (active)
                return;

            ClearTargetRuntimeState();
            _splitDuration = 0f;
            UpdateCapacitorDiagnostics(0f, 1f);
            UpdateSurfaceTensionDiagnostics(false, Vector2.zero, Vector2.zero);
            UpdateDiagnostics(0f, 0f, 1f, false);
        }

        public void ResetRuntimeBuildTuning()
        {
            _runtimeHitPadding = hitPadding;
            _runtimeDamagePerSecond = damagePerSecond;
            _runtimeConductivityActivationThreshold =
                conductivityActivationThreshold;
            _runtimeFullEffectWetness = fullEffectWetness;
            _runtimeFullConductivityDamageMultiplier =
                fullConductivityDamageMultiplier;
            _runtimeTuningInitialized = true;

            _capacitorEnabled = false;
            _splitDuration = 0f;
            UpdateCapacitorDiagnostics(0f, 1f);
        }

        public void ResetRuntimeRewardTuning()
        {
            _stableLinkRewardEnabled = false;
            _stableLinkEndpointInset = 0.32f;
            _conductiveResidueRewardEnabled = false;
            _conductiveResidueActivationThreshold = 0.10f;
            _elasticMembraneRewardEnabled = false;
            _elasticMembraneStableDistanceMultiplier = 1.20f;
        }

        public void ApplyStableLinkReward(
            float rewardEndpointInset = 0.32f)
        {
            _stableLinkRewardEnabled = true;
            _stableLinkEndpointInset = Mathf.Clamp(
                rewardEndpointInset,
                0.01f,
                Mathf.Max(0.01f, endpointInset));
        }

        public void ApplyConductiveResidueReward(
            float activationThreshold = 0.10f)
        {
            _conductiveResidueRewardEnabled = true;
            _conductiveResidueActivationThreshold =
                Mathf.Clamp01(activationThreshold);
        }

        public void ApplyElasticMembraneReward(
            float stableDistanceMultiplier = 1.20f)
        {
            _elasticMembraneRewardEnabled = true;
            _elasticMembraneStableDistanceMultiplier =
                Mathf.Max(1f, stableDistanceMultiplier);
        }

        public void ApplyConductiveFocusTuning(
            float activationThreshold = 0.05f,
            float conductiveFullEffectWetness = 0.35f,
            float maximumConductivityMultiplier = 2.4f)
        {
            ResetRuntimeBuildTuning();
            _runtimeConductivityActivationThreshold =
                Mathf.Clamp01(activationThreshold);
            _runtimeFullEffectWetness =
                Mathf.Clamp01(conductiveFullEffectWetness);
            _runtimeFullConductivityDamageMultiplier =
                Mathf.Max(1f, maximumConductivityMultiplier);
            EnsureRuntimeWetnessRange();
        }

        public void ApplyBroadArcTuning(
            float broadHitPadding = 0.17f,
            float damageScale = 0.825f)
        {
            ResetRuntimeBuildTuning();
            _runtimeHitPadding = Mathf.Max(hitPadding, broadHitPadding);
            _runtimeDamagePerSecond = damagePerSecond *
                Mathf.Clamp01(damageScale);
        }

        public void ConfigureCapacitor(
            float chargeDelay = 1.5f,
            float chargeDuration = 2.5f,
            float maximumMultiplier = 1.6f)
        {
            ResetRuntimeBuildTuning();
            _capacitorEnabled = true;
            _capacitorChargeDelay = Mathf.Max(0f, chargeDelay);
            _capacitorChargeDuration = Mathf.Max(0f, chargeDuration);
            _capacitorMaxMultiplier = Mathf.Max(1f, maximumMultiplier);
        }

        // Explicit hook used by focused EditMode tests. Runtime uses FixedUpdate.
        public void AdvanceCapacitor(bool validSplitPair, float deltaTime)
        {
            UpdateCapacitorState(validSplitPair, deltaTime);
        }

        private void ClearTargetRuntimeState()
        {
            if (target == null)
                return;

            target.SetTakingLinkDamage(false);
            target.SetConductivity(0f);
        }

        private void UpdateDiagnostics(
            float wetness,
            float conductivity01,
            float finalDamageMultiplier,
            bool linkHit,
            float appliedOutputPerSecond = 0f)
        {
            debugTargetWetness = wetness;
            debugConductivity01 = conductivity01;
            debugWetnessDamageMultiplier =
                WetLinkConductivity.CalculateDamageMultiplier(
                    conductivity01,
                    GetRuntimeFullConductivityDamageMultiplier());
            debugFinalDamageMultiplier = finalDamageMultiplier;
            debugAppliedOutputPerSecond = linkHit
                ? Mathf.Max(0f, appliedOutputPerSecond)
                : 0f;
            debugLinkHit = linkHit;
        }

        private void UpdateSurfaceTensionDiagnostics(
            bool validSplitPair,
            Vector2 start,
            Vector2 end)
        {
            if (!validSplitPair)
            {
                debugLinkSeparation = 0f;
                debugSurfaceTensionOverstretch01 = 0f;
                debugSurfaceTensionDamageMultiplier = 1f;
                debugSurfaceTensionBand = SurfaceTensionBand.Close;
                return;
            }

            float separation = Vector2.Distance(start, end);
            debugLinkSeparation = separation;
            float runtimeStableMaxDistance = GetRuntimeStableMaxDistance();
            float runtimeFullPenaltyDistance = GetRuntimeFullPenaltyDistance();

            debugSurfaceTensionOverstretch01 =
                SurfaceTensionRules.CalculateOverstretch01(
                    separation,
                    runtimeStableMaxDistance,
                    runtimeFullPenaltyDistance);
            debugSurfaceTensionDamageMultiplier =
                SurfaceTensionRules.CalculateDamageMultiplier(
                    separation,
                    runtimeStableMaxDistance,
                    runtimeFullPenaltyDistance,
                    minimumOverstretchedDamageMultiplier);
            debugSurfaceTensionBand = SurfaceTensionRules.CalculateBand(
                separation,
                closeMaxDistance,
                runtimeStableMaxDistance);
        }

        private void UpdateCapacitorState(bool validSplitPair, float deltaTime)
        {
            if (!_capacitorEnabled || !validSplitPair)
            {
                _splitDuration = 0f;
                UpdateCapacitorDiagnostics(0f, 1f);
                return;
            }

            _splitDuration += Mathf.Max(0f, deltaTime);
            float charge01 = MicroBuildCapacitor.CalculateCharge01(
                _splitDuration,
                _capacitorChargeDelay,
                _capacitorChargeDuration);
            float multiplier = MicroBuildCapacitor.CalculateMultiplier(
                charge01,
                _capacitorMaxMultiplier);
            UpdateCapacitorDiagnostics(charge01, multiplier);
        }

        private void UpdateCapacitorDiagnostics(
            float charge01,
            float multiplier)
        {
            debugCapacitorCharge01 = Mathf.Clamp01(charge01);
            debugCapacitorDamageMultiplier = Mathf.Max(1f, multiplier);
        }

        private void EnsureRuntimeInitialized()
        {
            if (!_runtimeTuningInitialized)
                ResetRuntimeBuildTuning();
        }

        private float GetRuntimeStableMaxDistance()
        {
            float value = Mathf.Max(closeMaxDistance, stableMaxDistance);
            if (_elasticMembraneRewardEnabled)
            {
                value *= Mathf.Max(
                    1f,
                    _elasticMembraneStableDistanceMultiplier);
            }

            return value;
        }

        private float GetRuntimeFullPenaltyDistance()
        {
            float baselineStable = Mathf.Max(closeMaxDistance, stableMaxDistance);
            float penaltySpan = Mathf.Max(
                0.01f,
                fullPenaltyDistance - baselineStable);

            return GetRuntimeStableMaxDistance() + penaltySpan;
        }

        private float GetRuntimeEndpointInset()
        {
            float value = Mathf.Max(0f, endpointInset);
            if (_stableLinkRewardEnabled)
                value = Mathf.Min(value, _stableLinkEndpointInset);

            return value;
        }

        private float GetRuntimeHitPadding()
        {
            EnsureRuntimeInitialized();
            return _runtimeHitPadding;
        }

        private float GetRuntimeDamagePerSecond()
        {
            EnsureRuntimeInitialized();
            return _runtimeDamagePerSecond;
        }

        private float GetRuntimeConductivityActivationThreshold()
        {
            EnsureRuntimeInitialized();
            float value = _runtimeConductivityActivationThreshold;

            if (_conductiveResidueRewardEnabled)
            {
                value = Mathf.Min(
                    value,
                    _conductiveResidueActivationThreshold);
            }

            return value;
        }

        private float GetRuntimeFullEffectWetness()
        {
            EnsureRuntimeInitialized();
            return _runtimeFullEffectWetness;
        }

        private float GetRuntimeFullConductivityDamageMultiplier()
        {
            EnsureRuntimeInitialized();
            return _runtimeFullConductivityDamageMultiplier;
        }

        private void EnsureRuntimeWetnessRange()
        {
            if (_runtimeFullEffectWetness <=
                _runtimeConductivityActivationThreshold)
            {
                _runtimeFullEffectWetness = Mathf.Min(
                    1f,
                    _runtimeConductivityActivationThreshold + 0.0001f);
            }
        }

        private void OnValidate()
        {
            endpointInset = Mathf.Max(0f, endpointInset);
            hitPadding = Mathf.Max(0f, hitPadding);
            damagePerSecond = Mathf.Max(0f, damagePerSecond);
            conductivityActivationThreshold =
                Mathf.Clamp01(conductivityActivationThreshold);
            fullEffectWetness = Mathf.Clamp01(fullEffectWetness);
            if (fullEffectWetness <= conductivityActivationThreshold)
            {
                fullEffectWetness = Mathf.Min(
                    1f,
                    conductivityActivationThreshold + 0.05f);
            }

            fullConductivityDamageMultiplier =
                Mathf.Max(1f, fullConductivityDamageMultiplier);

            closeMaxDistance = Mathf.Max(0f, closeMaxDistance);
            stableMaxDistance = Mathf.Max(
                closeMaxDistance,
                stableMaxDistance);
            fullPenaltyDistance = Mathf.Max(
                stableMaxDistance + 0.01f,
                fullPenaltyDistance);
            minimumOverstretchedDamageMultiplier =
                Mathf.Clamp01(minimumOverstretchedDamageMultiplier);
        }
    }
}
