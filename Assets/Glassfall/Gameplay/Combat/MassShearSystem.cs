using System;
using Glassfall.Gameplay.Droplet;
using Glassfall.Gameplay.Run;
using UnityEngine;

namespace Glassfall.Gameplay.Combat
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ArcHunter))]
    public sealed class MassShearSystem : MonoBehaviour
    {
        private const float MinimumPositiveMass = 0.01f;

        [Header("References")]
        [SerializeField] private DropletSplitSystem playerSplitSystem;
        [SerializeField] private NeutralDroplet massFragmentPrefab;
        [SerializeField] private Transform fragmentParent;
        [SerializeField] private ContaminationSystem contaminationSystem;

        [Header("Contact Threat")]
        [SerializeField] private bool contactThreatEnabled;

        [Min(0f)]
        [SerializeField] private float contactPadding = 0.16f;

        [Min(0.05f)]
        [SerializeField] private float hitCooldownSeconds = 0.85f;

        [Header("Mass Shear")]
        [Min(0f)]
        [SerializeField] private float mergedShearMass = 0.5f;

        [Min(0f)]
        [SerializeField] private float splitShearMass = 1f;

        [Min(MinimumPositiveMass)]
        [SerializeField] private float minimumRetainedMass = 1f;

        [Header("Recovery Fragment")]
        [Min(0f)]
        [SerializeField] private float fragmentEjectPadding = 0.28f;

        [Min(0f)]
        [SerializeField] private float fragmentPickupLockSeconds = 0.55f;

        [Header("Knockback")]
        [Min(0f)]
        [SerializeField] private float knockbackSpeed = 2.25f;

        [Header("Runtime Mirrors")]
        [SerializeField] private bool debugContactThreatActive;
        [SerializeField] private bool debugLastHitWasSplit;
        [SerializeField] private float debugLastShearedMass;
        [SerializeField] private float debugCooldownRemaining;

        private ArcHunter _hunter;
        private float _nextAllowedHitTime;
        private float _runtimeMergedShearMultiplier = 1f;

        public event Action<MassShearResult> MassSheared;

        public float MergedShearMass =>
            mergedShearMass * _runtimeMergedShearMultiplier;
        public float SplitShearMass => splitShearMass;
        public float HitCooldownSeconds => hitCooldownSeconds;
        public bool ContactThreatEnabled => contactThreatEnabled;

        private void Awake()
        {
            _hunter = GetComponent<ArcHunter>();
            if (playerSplitSystem == null)
                playerSplitSystem = FindAnyObjectByType<DropletSplitSystem>();
            if (contaminationSystem == null)
                contaminationSystem = FindAnyObjectByType<ContaminationSystem>();

            ResetRuntimeRewardTuning();
            UpdateDiagnostics(false, false, 0f);
        }

        private void OnEnable()
        {
            _nextAllowedHitTime = 0f;
        }

        private void FixedUpdate()
        {
            if (_hunter == null)
                _hunter = GetComponent<ArcHunter>();
            if (playerSplitSystem == null)
                playerSplitSystem = FindAnyObjectByType<DropletSplitSystem>();
            if (contaminationSystem == null)
                contaminationSystem = FindAnyObjectByType<ContaminationSystem>();

            float now = Time.fixedTime;
            debugCooldownRemaining = Mathf.Max(
                0f,
                _nextAllowedHitTime - now);

            if (!contactThreatEnabled ||
                _hunter == null ||
                _hunter.IsDefeated ||
                playerSplitSystem == null ||
                massFragmentPrefab == null ||
                now < _nextAllowedHitTime)
            {
                debugContactThreatActive = false;
                return;
            }

            TryApplyContactShear(now, true, null, false);
        }

        public void SetContactThreatEnabled(bool enabled)
        {
            contactThreatEnabled = enabled;
            if (!enabled)
                debugContactThreatActive = false;
        }

        public bool TryApplyAttackImpact(
            DropletController preferredTarget = null)
        {
            if (_hunter == null)
                _hunter = GetComponent<ArcHunter>();
            if (playerSplitSystem == null)
                playerSplitSystem = FindAnyObjectByType<DropletSplitSystem>();
            if (contaminationSystem == null)
                contaminationSystem = FindAnyObjectByType<ContaminationSystem>();

            float now = Time.fixedTime;
            if (_hunter == null ||
                _hunter.IsDefeated ||
                playerSplitSystem == null ||
                massFragmentPrefab == null ||
                now < _nextAllowedHitTime)
            {
                return false;
            }

            return TryApplyContactShear(
                now,
                false,
                preferredTarget,
                true);
        }

        public void ConfigureContamination(ContaminationSystem source)
        {
            contaminationSystem = source;
        }

        public void Configure(
            DropletSplitSystem player,
            NeutralDroplet fragmentPrefab,
            Transform recoveryFragmentParent = null,
            float mergedLoss = 0.5f,
            float splitLoss = 1f,
            float cooldownSeconds = 0.85f,
            float contactExtraPadding = 0.16f,
            float minimumMassAfterHit = 1f,
            float playerKnockbackSpeed = 2.25f,
            float pickupLockSeconds = 0.55f)
        {
            playerSplitSystem = player;
            massFragmentPrefab = fragmentPrefab;
            fragmentParent = recoveryFragmentParent;
            mergedShearMass = Mathf.Max(0f, mergedLoss);
            splitShearMass = Mathf.Max(0f, splitLoss);
            hitCooldownSeconds = Mathf.Max(0.05f, cooldownSeconds);
            contactPadding = Mathf.Max(0f, contactExtraPadding);
            minimumRetainedMass = Mathf.Max(
                MinimumPositiveMass,
                minimumMassAfterHit);
            knockbackSpeed = Mathf.Max(0f, playerKnockbackSpeed);
            fragmentPickupLockSeconds = Mathf.Max(0f, pickupLockSeconds);
            ResetRuntimeRewardTuning();
        }

        public void ResetRuntimeRewardTuning()
        {
            _runtimeMergedShearMultiplier = 1f;
        }

        public void ApplyHighCohesionReward(float mergedShearMultiplier = 0.75f)
        {
            _runtimeMergedShearMultiplier =
                Mathf.Clamp01(mergedShearMultiplier);
        }

        public static float GetRequestedShearMass(
            bool isSplitForm,
            float mergedLoss,
            float splitLoss)
        {
            return Mathf.Max(
                0f,
                isSplitForm ? splitLoss : mergedLoss);
        }

        public static float CalculateShearedMass(
            float currentMass,
            float requestedShear,
            float minimumMassAfterHit)
        {
            float safeCurrentMass = Mathf.Max(0f, currentMass);
            float safeMinimum = Mathf.Max(
                MinimumPositiveMass,
                minimumMassAfterHit);
            float availableMass = Mathf.Max(
                0f,
                safeCurrentMass - safeMinimum);

            return Mathf.Min(
                Mathf.Max(0f, requestedShear),
                availableMass);
        }

        public static bool IsWithinContactRange(
            float centerDistance,
            float dropletRadius,
            float hunterRadius,
            float extraPadding)
        {
            float contactDistance =
                Mathf.Max(0f, dropletRadius) +
                Mathf.Max(0f, hunterRadius) +
                Mathf.Max(0f, extraPadding);

            return Mathf.Max(0f, centerDistance) <= contactDistance;
        }

        private DropletController FindContactTarget(
            DropletController preferredTarget = null)
        {
            if (preferredTarget != null)
            {
                if (!preferredTarget.gameObject.activeInHierarchy)
                    return null;

                float preferredDistance = Vector2.Distance(
                    transform.position,
                    preferredTarget.transform.position);
                return IsWithinContactRange(
                        preferredDistance,
                        preferredTarget.CurrentRadius,
                        _hunter != null ? _hunter.InteractionRadius : 0f,
                        contactPadding)
                    ? preferredTarget
                    : null;
            }

            DropletController primary = playerSplitSystem.PrimaryController;
            DropletController mirror = playerSplitSystem.MirrorController;

            DropletController best = null;
            float bestDistance = float.PositiveInfinity;

            ConsiderContactCandidate(primary, ref best, ref bestDistance);
            ConsiderContactCandidate(mirror, ref best, ref bestDistance);
            return best;
        }

        private bool TryApplyContactShear(
            float now,
            bool contactSensor,
            DropletController preferredTarget,
            bool applyContamination)
        {
            DropletController target = FindContactTarget(preferredTarget);
            if (target == null)
            {
                if (contactSensor)
                    debugContactThreatActive = false;
                return false;
            }

            bool isSplitHit = target.IsSplitForm;
            float requestedShear = GetRequestedShearMass(
                isSplitHit,
                MergedShearMass,
                SplitShearMass);
            float shearedMass = CalculateShearedMass(
                target.CurrentMass,
                requestedShear,
                minimumRetainedMass);

            if (shearedMass <= 0f)
            {
                if (contactSensor)
                    debugContactThreatActive = true;

                if (!applyContamination)
                    return false;

                contaminationSystem?.ApplyArcHunterHit(isSplitHit);
                _nextAllowedHitTime = now + hitCooldownSeconds;
                UpdateDiagnostics(false, isSplitHit, 0f);
                return true;
            }

            ApplyShear(target, shearedMass, isSplitHit);
            if (applyContamination)
                contaminationSystem?.ApplyArcHunterHit(isSplitHit);
            _nextAllowedHitTime = now + hitCooldownSeconds;
            UpdateDiagnostics(contactSensor, isSplitHit, shearedMass);
            return true;
        }

        private void ConsiderContactCandidate(
            DropletController candidate,
            ref DropletController best,
            ref float bestDistance)
        {
            if (candidate == null)
                return;

            float distance = Vector2.Distance(
                transform.position,
                candidate.transform.position);

            if (!IsWithinContactRange(
                    distance,
                    candidate.CurrentRadius,
                    _hunter != null ? _hunter.InteractionRadius : 0f,
                    contactPadding) ||
                distance >= bestDistance)
            {
                return;
            }

            best = candidate;
            bestDistance = distance;
        }

        private void ApplyShear(
            DropletController target,
            float shearedMass,
            bool isSplitHit)
        {
            Vector2 hunterPosition = transform.position;
            Vector2 targetPosition = target.transform.position;
            Vector2 awayFromHunter = targetPosition - hunterPosition;
            if (awayFromHunter.sqrMagnitude <= 0.0001f)
            {
                awayFromHunter = target.CurrentVelocity.sqrMagnitude > 0.0001f
                    ? target.CurrentVelocity
                    : Vector2.right;
            }
            awayFromHunter.Normalize();

            float radiusBeforeHit = target.CurrentRadius;
            target.SetMass(target.CurrentMass - shearedMass);

            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            if (body != null && knockbackSpeed > 0f)
            {
                body.linearVelocity += awayFromHunter * knockbackSpeed;
                body.WakeUp();
            }

            SpawnRecoveryFragment(
                targetPosition,
                radiusBeforeHit,
                shearedMass,
                awayFromHunter);

            MassSheared?.Invoke(
                new MassShearResult(
                    target,
                    shearedMass,
                    isSplitHit,
                    target.CurrentMass));
        }

        private void SpawnRecoveryFragment(
            Vector2 playerPosition,
            float playerRadiusBeforeHit,
            float shearedMass,
            Vector2 ejectDirection)
        {
            float fragmentRadius = massFragmentPrefab.GetVisualRadiusForMass(
                shearedMass);
            float ejectDistance =
                Mathf.Max(0f, playerRadiusBeforeHit) +
                fragmentRadius +
                fragmentEjectPadding;
            Vector2 spawnPosition =
                playerPosition + ejectDirection * ejectDistance;

            NeutralDroplet fragment = Instantiate(
                massFragmentPrefab,
                spawnPosition,
                Quaternion.identity,
                fragmentParent);
            fragment.name = "RecoverableMassFragment";
            fragment.Initialize(shearedMass);

            RecoverableMassFragment recovery =
                fragment.GetComponent<RecoverableMassFragment>();
            if (recovery == null)
                recovery = fragment.gameObject.AddComponent<RecoverableMassFragment>();

            recovery.Configure(fragmentPickupLockSeconds);
        }

        private void UpdateDiagnostics(
            bool contactThreatActive,
            bool lastHitWasSplit,
            float lastShearedMass)
        {
            debugContactThreatActive = contactThreatActive;
            debugLastHitWasSplit = lastHitWasSplit;
            debugLastShearedMass = lastShearedMass;
            debugCooldownRemaining = Mathf.Max(
                0f,
                _nextAllowedHitTime - Time.fixedTime);
        }

        private void OnValidate()
        {
            contactPadding = Mathf.Max(0f, contactPadding);
            hitCooldownSeconds = Mathf.Max(0.05f, hitCooldownSeconds);
            mergedShearMass = Mathf.Max(0f, mergedShearMass);
            splitShearMass = Mathf.Max(0f, splitShearMass);
            minimumRetainedMass = Mathf.Max(
                MinimumPositiveMass,
                minimumRetainedMass);
            fragmentEjectPadding = Mathf.Max(0f, fragmentEjectPadding);
            fragmentPickupLockSeconds = Mathf.Max(
                0f,
                fragmentPickupLockSeconds);
            knockbackSpeed = Mathf.Max(0f, knockbackSpeed);
        }
    }

    public readonly struct MassShearResult
    {
        public MassShearResult(
            DropletController target,
            float shearedMass,
            bool wasSplitHit,
            float remainingMass)
        {
            Target = target;
            ShearedMass = shearedMass;
            WasSplitHit = wasSplitHit;
            RemainingMass = remainingMass;
        }

        public DropletController Target { get; }
        public float ShearedMass { get; }
        public bool WasSplitHit { get; }
        public float RemainingMass { get; }
    }
}
