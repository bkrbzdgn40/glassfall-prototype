using System;
using Glassfall.Gameplay.Droplet;
using Glassfall.Gameplay.Link;
using Glassfall.Gameplay.Wetness;
using UnityEngine;

namespace Glassfall.Gameplay.Combat
{
    public enum ArcHunterMovementMode
    {
        Chase = 0,
        Strafe = 1,
        WetRoutePressure = 2
    }

    public enum ArcHunterCombatState
    {
        Hunt = 0,
        TargetLock = 1,
        Telegraph = 2,
        CommitAttack = 3,
        Recover = 4,
        Reposition = 5
    }

    public static class ArcHunterMovement
    {
        public static Vector2 CalculateStrafeDirection(
            Vector2 current,
            Vector2 target,
            int direction = 1,
            float preferredRadius = 2.25f)
        {
            Vector2 radial = current - target;
            float distance = radial.magnitude;
            if (distance <= 0.0001f)
                return direction >= 0 ? Vector2.right : Vector2.left;

            Vector2 radialDirection = radial / distance;
            Vector2 tangent = new Vector2(
                -radialDirection.y,
                radialDirection.x) * (direction >= 0 ? 1f : -1f);
            float radius = Mathf.Max(0.1f, preferredRadius);
            float correction = Mathf.Clamp(
                (distance - radius) / radius,
                -1f,
                1f);

            return (tangent - radialDirection * correction * 0.85f)
                .normalized;
        }

        public static Vector2 CalculateWetRouteDirection(
            Vector2 current,
            Vector2 target,
            Func<Vector2, float> sampleWetness,
            int direction = 1,
            float probeDistance = 1.25f,
            float routeBias = 0.7f)
        {
            Vector2 toTarget = target - current;
            Vector2 direct = toTarget.sqrMagnitude > 0.0001f
                ? toTarget.normalized
                : Vector2.right;

            if (sampleWetness == null)
                return direct;

            float clampedBias = Mathf.Clamp01(routeBias);
            Vector2 bestDirection = direct;
            float bestScore = ScoreWetRouteCandidate(
                current,
                direct,
                direct,
                sampleWetness,
                probeDistance,
                clampedBias);

            for (int index = 0; index < 8; index++)
            {
                float angle = (360f / 8f) * index;
                Vector2 candidate = Quaternion.Euler(
                    0f,
                    0f,
                    angle * (direction >= 0 ? 1f : -1f)) * direct;
                float score = ScoreWetRouteCandidate(
                    current,
                    candidate,
                    direct,
                    sampleWetness,
                    probeDistance,
                    clampedBias);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDirection = candidate;
                }
            }

            return bestDirection.normalized;
        }

        private static float ScoreWetRouteCandidate(
            Vector2 current,
            Vector2 candidate,
            Vector2 direct,
            Func<Vector2, float> sampleWetness,
            float probeDistance,
            float routeBias)
        {
            float wetness = Mathf.Clamp01(sampleWetness(
                current + candidate * Mathf.Max(0f, probeDistance)));
            float targetAlignment = Mathf.Clamp01(
                (Vector2.Dot(candidate, direct) + 1f) * 0.5f);
            return wetness * routeBias +
                targetAlignment * (1f - routeBias);
        }
    }

    [DisallowMultipleComponent]
    public sealed class ArcHunter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SplitLinkSystem linkSystem;
        [SerializeField] private WetnessField wetnessField;
        [SerializeField] private Collider2D playableBounds;
        [SerializeField] private MassShearSystem massShearSystem;

        [Header("Movement")]
        [Min(0f)]
        [SerializeField] private float moveSpeed = 2f;

        [SerializeField] private ArcHunterMovementMode movementMode =
            ArcHunterMovementMode.Chase;

        [Min(0.1f)]
        [SerializeField] private float strafeRadius = 2.25f;

        [Min(0.1f)]
        [SerializeField] private float wetRouteProbeDistance = 1.25f;

        [Range(0f, 1f)]
        [SerializeField] private float wetRouteBias = 0.7f;

        [Header("Arena Containment")]
        [Min(0f)]
        [SerializeField] private float containmentPadding = 0.18f;

        [Header("Telegraphed Combat Cycle")]
        [Min(0.1f)]
        [SerializeField] private float preferredAttackRange = 2.4f;

        [Min(0.1f)]
        [SerializeField] private float telegraphDurationSeconds = 0.55f;

        [Min(0.1f)]
        [SerializeField] private float dashSpeed = 6f;

        [Min(0.1f)]
        [SerializeField] private float maxDashDistance = 3.2f;

        [Min(0.1f)]
        [SerializeField] private float recoverDurationSeconds = 0.6f;

        [Min(0.1f)]
        [SerializeField] private float repositionDurationSeconds = 0.7f;

        [Min(0f)]
        [SerializeField] private float attackCooldownSeconds = 1f;

        [Min(0f)]
        [SerializeField] private float linkAvoidanceRadius = 0.55f;

        [Header("Combat")]
        [Min(0.1f)]
        [SerializeField] private float maxHealth = 10f;

        [Min(0.05f)]
        [SerializeField] private float interactionRadius = 0.32f;

        [Header("Reward")]
        [Min(0f)]
        [SerializeField] private float combinedMassReward = 1.5f;

        [Header("Runtime Mirrors")]
        [SerializeField] private float debugHealth;
        [SerializeField] private bool debugTakingLinkDamage;
        [SerializeField] private float debugConductivity01;
        [SerializeField] private bool debugDefeated;
        [SerializeField] private ArcHunterCombatState debugCombatState;
        [SerializeField] private bool debugTargetLocked;
        [SerializeField] private float debugTelegraph01;

        private float _health;
        private bool _defeated;
        private bool _takingLinkDamage;
        private float _conductivity01;
        private int _strafeDirection = 1;
        private ArcHunterCombatState _combatState = ArcHunterCombatState.Hunt;
        private float _stateRemaining;
        private float _nextAttackAllowedTime;
        private float _dashRemainingDistance;
        private Vector2 _committedDirection;
        private DropletController _lockedTarget;
        private bool _attackImpactApplied;

        public float InteractionRadius => interactionRadius;
        public float Health01 => maxHealth <= 0f
            ? 0f
            : Mathf.Clamp01(_health / maxHealth);
        public bool IsTakingLinkDamage => _takingLinkDamage;
        public float Conductivity01 => _conductivity01;
        public bool IsConductive => _conductivity01 > 0.001f;
        public bool IsDefeated => _defeated;
        public ArcHunterMovementMode MovementMode => movementMode;
        public float EngagementDistance => preferredAttackRange;
        public Collider2D PlayableBounds => playableBounds;
        public ArcHunterCombatState CombatState => _combatState;
        public bool IsTelegraphing => _combatState == ArcHunterCombatState.Telegraph;
        public bool IsCommittingAttack =>
            _combatState == ArcHunterCombatState.CommitAttack;
        public bool IsRecovering => _combatState == ArcHunterCombatState.Recover;
        public float Telegraph01 => IsTelegraphing
            ? 1f - Mathf.Clamp01(
                _stateRemaining / Mathf.Max(telegraphDurationSeconds, 0.01f))
            : 0f;
        public Vector2 CommittedAttackDirection => _committedDirection;
        public DropletController LockedTarget => _lockedTarget;

        public void Configure(SplitLinkSystem source)
        {
            linkSystem = source;
        }

        public void ConfigureArenaContainment(
            Collider2D bounds,
            float padding = 0.18f)
        {
            playableBounds = bounds;
            containmentPadding = Mathf.Max(0f, padding);
        }

        public void ConfigureCombatCycle(
            float preferredDistance = 2.4f,
            float telegraphDuration = 0.55f,
            float attackDashSpeed = 6f,
            float attackMaxDistance = 3.2f,
            float recoverDuration = 0.6f,
            float repositionDuration = 0.7f,
            float attackCooldown = 1f,
            float avoidanceRadius = 0.55f)
        {
            preferredAttackRange = Mathf.Max(0.1f, preferredDistance);
            telegraphDurationSeconds = Mathf.Max(0.1f, telegraphDuration);
            dashSpeed = Mathf.Max(0.1f, attackDashSpeed);
            maxDashDistance = Mathf.Max(0.1f, attackMaxDistance);
            recoverDurationSeconds = Mathf.Max(0.1f, recoverDuration);
            repositionDurationSeconds = Mathf.Max(0.1f, repositionDuration);
            attackCooldownSeconds = Mathf.Max(0f, attackCooldown);
            linkAvoidanceRadius = Mathf.Max(0f, avoidanceRadius);
        }

        public void ConfigureMassShear(MassShearSystem source)
        {
            massShearSystem = source;
            massShearSystem?.SetContactThreatEnabled(false);
        }

        public void ConfigureWetness(WetnessField source)
        {
            wetnessField = source;
        }

        public void ConfigureMovementMode(ArcHunterMovementMode mode)
        {
            movementMode = mode;
            _strafeDirection = 1;
        }

        private void Awake()
        {
            if (linkSystem == null)
                linkSystem = FindAnyObjectByType<SplitLinkSystem>();
            if (wetnessField == null)
                wetnessField = FindAnyObjectByType<WetnessField>();
            if (massShearSystem == null)
                massShearSystem = GetComponent<MassShearSystem>();
            if (playableBounds == null)
            {
                GameObject area = GameObject.Find("NeurtalSpawnArea");
                if (area == null)
                    area = GameObject.Find("NeutralSpawnArea");
                if (area != null)
                    playableBounds = area.GetComponent<Collider2D>();
            }

            massShearSystem?.SetContactThreatEnabled(false);
            _health = maxHealth;
            ResetCombatState();
            transform.position = ConstrainToPlayableBounds(transform.position);
            UpdateDiagnostics();
        }

        private void Update()
        {
            if (_defeated || linkSystem == null)
                return;

            Vector2 currentPosition = ConstrainToPlayableBounds(
                transform.position);
            transform.position = currentPosition;

            switch (_combatState)
            {
                case ArcHunterCombatState.Hunt:
                    UpdateHunt(currentPosition, Time.deltaTime);
                    break;
                case ArcHunterCombatState.TargetLock:
                    UpdateTargetLock(currentPosition, Time.deltaTime);
                    break;
                case ArcHunterCombatState.Telegraph:
                    UpdateTelegraph(currentPosition, Time.deltaTime);
                    break;
                case ArcHunterCombatState.CommitAttack:
                    UpdateCommitAttack(currentPosition, Time.deltaTime);
                    break;
                case ArcHunterCombatState.Recover:
                    UpdateRecover(Time.deltaTime);
                    break;
                case ArcHunterCombatState.Reposition:
                    UpdateReposition(currentPosition, Time.deltaTime);
                    break;
            }

            UpdateDiagnostics();
        }

        public void SetTakingLinkDamage(bool value)
        {
            _takingLinkDamage = !_defeated && value;
            UpdateDiagnostics();
        }

        public void SetConductivity(float value)
        {
            _conductivity01 = _defeated
                ? 0f
                : Mathf.Clamp01(value);
            UpdateDiagnostics();
        }

        public void ApplyBodyDisplacement(Vector2 displacement)
        {
            if (_defeated || displacement.sqrMagnitude <= 0f)
                return;

            transform.position = ConstrainToPlayableBounds(
                (Vector2)transform.position + displacement);
        }

        public void ApplyLinkDamage(float amount)
        {
            if (_defeated || amount <= 0f)
                return;

            _health = Mathf.Max(0f, _health - amount);
            if (_health <= 0f)
                Defeat();

            UpdateDiagnostics();
        }

        public void ResetHunter()
        {
            _health = maxHealth;
            _defeated = false;
            _takingLinkDamage = false;
            _conductivity01 = 0f;
            movementMode = ArcHunterMovementMode.Chase;
            _strafeDirection = 1;
            massShearSystem?.SetContactThreatEnabled(false);
            ResetCombatState();
            transform.position = ConstrainToPlayableBounds(transform.position);
            UpdateDiagnostics();
        }

        private void UpdateHunt(Vector2 currentPosition, float deltaTime)
        {
            DropletController target = GetCandidateTarget(currentPosition);
            if (target == null)
                return;

            Vector2 targetPosition = ConstrainToPlayableBounds(
                target.transform.position);
            float distance = Vector2.Distance(currentPosition, targetPosition);
            if (Time.time >= _nextAttackAllowedTime &&
                distance <= preferredAttackRange)
            {
                _lockedTarget = target;
                _stateRemaining = 0.1f;
                _combatState = ArcHunterCombatState.TargetLock;
                return;
            }

            Vector2 direction = GetHuntDirection(
                currentPosition,
                targetPosition);
            direction = ConstrainDirectionAtBounds(
                currentPosition,
                direction,
                targetPosition);
            MoveByDirection(direction, moveSpeed, deltaTime);
        }

        private void UpdateTargetLock(Vector2 currentPosition, float deltaTime)
        {
            if (!IsLockedTargetValid())
            {
                AbortAttackToHunt();
                return;
            }

            _stateRemaining = Mathf.Max(0f, _stateRemaining - deltaTime);
            if (_stateRemaining > 0f)
                return;

            _stateRemaining = telegraphDurationSeconds;
            _attackImpactApplied = false;
            _combatState = ArcHunterCombatState.Telegraph;
        }

        private void UpdateTelegraph(Vector2 currentPosition, float deltaTime)
        {
            if (!IsLockedTargetValid())
            {
                AbortAttackToHunt();
                return;
            }

            // A tell is intentionally readable: only a tiny approach is allowed.
            DropletController target = _lockedTarget;
            Vector2 targetPosition = ConstrainToPlayableBounds(
                target.transform.position);
            Vector2 direction = targetPosition - currentPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                MoveByDirection(
                    ConstrainDirectionAtBounds(
                        currentPosition,
                        direction.normalized,
                        targetPosition),
                    moveSpeed * 0.12f,
                    deltaTime);
            }

            _stateRemaining = Mathf.Max(0f, _stateRemaining - deltaTime);
            if (_stateRemaining <= 0f)
                BeginCommittedAttack();
        }

        private void BeginCommittedAttack()
        {
            if (!IsLockedTargetValid())
            {
                AbortAttackToHunt();
                return;
            }

            Vector2 targetPosition = ConstrainToPlayableBounds(
                _lockedTarget.transform.position);
            Vector2 offset = targetPosition - (Vector2)transform.position;
            float distance = offset.magnitude;
            if (distance <= 0.0001f)
                offset = Vector2.right;

            _committedDirection = offset.normalized;
            _dashRemainingDistance = Mathf.Min(
                maxDashDistance,
                Mathf.Max(interactionRadius, distance));
            _combatState = ArcHunterCombatState.CommitAttack;
            _stateRemaining = _dashRemainingDistance /
                Mathf.Max(dashSpeed, 0.1f);
        }

        private void UpdateCommitAttack(Vector2 currentPosition, float deltaTime)
        {
            if (_dashRemainingDistance <= 0.0001f)
            {
                FinishCommittedAttack();
                return;
            }

            float distanceThisFrame = Mathf.Min(
                _dashRemainingDistance,
                dashSpeed * Mathf.Max(0f, deltaTime));
            Vector2 nextPosition = ConstrainToPlayableBounds(
                currentPosition + _committedDirection * distanceThisFrame);
            float travelled = Vector2.Distance(currentPosition, nextPosition);
            transform.position = nextPosition;
            _dashRemainingDistance = Mathf.Max(
                0f,
                _dashRemainingDistance - travelled);
            _stateRemaining = _dashRemainingDistance /
                Mathf.Max(dashSpeed, 0.1f);

            if (!_attackImpactApplied && massShearSystem != null)
            {
                _attackImpactApplied = massShearSystem.TryApplyAttackImpact(
                    _lockedTarget);
            }

            if (travelled <= 0.0001f || _dashRemainingDistance <= 0.0001f)
                FinishCommittedAttack();
        }

        private void FinishCommittedAttack()
        {
            _combatState = ArcHunterCombatState.Recover;
            _stateRemaining = recoverDurationSeconds;
            _nextAttackAllowedTime = Time.time + attackCooldownSeconds;
            _lockedTarget = null;
            _dashRemainingDistance = 0f;
        }

        private void UpdateRecover(float deltaTime)
        {
            _stateRemaining = Mathf.Max(0f, _stateRemaining - deltaTime);
            if (_stateRemaining <= 0f)
            {
                _combatState = ArcHunterCombatState.Reposition;
                _stateRemaining = repositionDurationSeconds;
                _strafeDirection *= -1;
            }
        }

        private void UpdateReposition(Vector2 currentPosition, float deltaTime)
        {
            DropletController target = GetCandidateTarget(currentPosition);
            if (target != null)
            {
                Vector2 targetPosition = ConstrainToPlayableBounds(
                    target.transform.position);
                Vector2 repositionDirection =
                    ArcHunterMovement.CalculateStrafeDirection(
                        currentPosition,
                        targetPosition,
                        _strafeDirection,
                        preferredAttackRange);
                if (IsDirectPathOnActiveLink(
                        currentPosition,
                        repositionDirection))
                {
                    repositionDirection = RotateDirection(
                        repositionDirection,
                        _strafeDirection >= 0 ? 35f : -35f);
                }

                repositionDirection = ConstrainDirectionAtBounds(
                    currentPosition,
                    repositionDirection,
                    targetPosition);
                MoveByDirection(
                    repositionDirection,
                    moveSpeed,
                    deltaTime);
            }

            _stateRemaining = Mathf.Max(0f, _stateRemaining - deltaTime);
            if (_stateRemaining <= 0f)
                _combatState = ArcHunterCombatState.Hunt;
        }

        private DropletController GetCandidateTarget(Vector2 currentPosition)
        {
            if (linkSystem == null || linkSystem.SplitSystem == null)
                return null;

            DropletController primary =
                linkSystem.SplitSystem.PrimaryController;
            DropletController mirror =
                linkSystem.SplitSystem.MirrorController;

            if (!linkSystem.IsSplit)
                return IsUsableTarget(primary) ? primary : null;
            if (!IsUsableTarget(mirror))
                return IsUsableTarget(primary) ? primary : null;
            if (!IsUsableTarget(primary))
                return mirror;

            float primaryDistance = Vector2.Distance(
                currentPosition,
                primary.transform.position);
            float mirrorDistance = Vector2.Distance(
                currentPosition,
                mirror.transform.position);
            return primaryDistance <= mirrorDistance ? primary : mirror;
        }

        private bool IsLockedTargetValid()
        {
            if (!IsUsableTarget(_lockedTarget) || linkSystem == null ||
                linkSystem.SplitSystem == null)
            {
                return false;
            }

            DropletController primary =
                linkSystem.SplitSystem.PrimaryController;
            DropletController mirror =
                linkSystem.SplitSystem.MirrorController;
            return _lockedTarget == primary || _lockedTarget == mirror;
        }

        private static bool IsUsableTarget(DropletController target)
        {
            return target != null && target.gameObject.activeInHierarchy;
        }

        private Vector2 GetHuntDirection(
            Vector2 currentPosition,
            Vector2 targetPosition)
        {
            Vector2 direct = targetPosition - currentPosition;
            if (direct.sqrMagnitude <= 0.0001f)
                return Vector2.right;
            direct.Normalize();

            if (IsDirectPathOnActiveLink(currentPosition, direct))
            {
                Vector2 lateral = new Vector2(
                    -direct.y,
                    direct.x) * (_strafeDirection >= 0 ? 1f : -1f);
                return lateral.normalized;
            }

            if (movementMode == ArcHunterMovementMode.Strafe)
            {
                return ArcHunterMovement.CalculateStrafeDirection(
                    currentPosition,
                    targetPosition,
                    _strafeDirection,
                    strafeRadius);
            }

            if (movementMode == ArcHunterMovementMode.WetRoutePressure)
            {
                return ArcHunterMovement.CalculateWetRouteDirection(
                    currentPosition,
                    targetPosition,
                    wetnessField != null
                        ? wetnessField.SampleWorld
                        : null,
                    _strafeDirection,
                    wetRouteProbeDistance,
                    wetRouteBias);
            }

            return direct;
        }

        private bool IsDirectPathOnActiveLink(
            Vector2 currentPosition,
            Vector2 directDirection)
        {
            if (linkSystem == null || !linkSystem.IsSplit)
                return false;

            if (!linkSystem.TryGetTrimmedSegment(
                    0.32f,
                    out Vector2 linkStart,
                    out Vector2 linkEnd))
            {
                return false;
            }

            float probeDistance = Mathf.Max(0.45f, moveSpeed * 0.35f);
            Vector2 probePosition = currentPosition +
                directDirection.normalized * probeDistance;
            float dangerRadius = interactionRadius + linkAvoidanceRadius;

            return SplitLinkGeometry.DistancePointToSegment(
                       probePosition,
                       linkStart,
                       linkEnd) <= dangerRadius;
        }

        private static Vector2 RotateDirection(Vector2 direction, float degrees)
        {
            return (Quaternion.Euler(0f, 0f, degrees) *
                (direction.sqrMagnitude > 0.0001f
                    ? direction.normalized
                    : Vector2.right)).normalized;
        }

        private void MoveByDirection(
            Vector2 direction,
            float speed,
            float deltaTime)
        {
            if (direction.sqrMagnitude <= 0.0001f || speed <= 0f)
                return;

            Vector2 current = ConstrainToPlayableBounds(transform.position);
            Vector2 constrained = ConstrainDirectionAtBounds(
                current,
                direction.normalized,
                current + direction);
            transform.position = ConstrainToPlayableBounds(
                current + constrained * speed * Mathf.Max(0f, deltaTime));
        }

        private void AbortAttackToHunt()
        {
            _lockedTarget = null;
            _attackImpactApplied = false;
            _dashRemainingDistance = 0f;
            _combatState = ArcHunterCombatState.Hunt;
            _stateRemaining = 0f;
        }

        private void ResetCombatState()
        {
            _combatState = ArcHunterCombatState.Hunt;
            _stateRemaining = 0f;
            _nextAttackAllowedTime = 0f;
            _dashRemainingDistance = 0f;
            _committedDirection = Vector2.zero;
            _lockedTarget = null;
            _attackImpactApplied = false;
        }

        private Vector2 ConstrainToPlayableBounds(Vector2 position)
        {
            if (playableBounds == null)
                return position;

            Bounds bounds = playableBounds.bounds;
            float margin = Mathf.Max(
                0.05f,
                interactionRadius + containmentPadding);
            if (bounds.size.x <= margin * 2f ||
                bounds.size.y <= margin * 2f)
            {
                return bounds.center;
            }

            return new Vector2(
                Mathf.Clamp(
                    position.x,
                    bounds.min.x + margin,
                    bounds.max.x - margin),
                Mathf.Clamp(
                    position.y,
                    bounds.min.y + margin,
                    bounds.max.y - margin));
        }

        private Vector2 ConstrainDirectionAtBounds(
            Vector2 position,
            Vector2 direction,
            Vector2 targetPosition)
        {
            if (playableBounds == null)
                return direction.sqrMagnitude > 0.0001f
                    ? direction.normalized
                    : Vector2.zero;

            Bounds bounds = playableBounds.bounds;
            float margin = Mathf.Max(
                0.05f,
                interactionRadius + containmentPadding);
            const float edgeEpsilon = 0.01f;

            if (position.x <= bounds.min.x + margin + edgeEpsilon &&
                direction.x < 0f)
            {
                direction.x = 0f;
            }
            else if (position.x >= bounds.max.x - margin - edgeEpsilon &&
                     direction.x > 0f)
            {
                direction.x = 0f;
            }

            if (position.y <= bounds.min.y + margin + edgeEpsilon &&
                direction.y < 0f)
            {
                direction.y = 0f;
            }
            else if (position.y >= bounds.max.y - margin - edgeEpsilon &&
                     direction.y > 0f)
            {
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                Vector2 inward = targetPosition - position;
                if (inward.sqrMagnitude > 0.0001f)
                    return inward.normalized;
            }

            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.zero;
        }

        private void Defeat()
        {
            if (_defeated)
                return;

            _defeated = true;
            _takingLinkDamage = false;
            _conductivity01 = 0f;
            _lockedTarget = null;
            massShearSystem?.SetContactThreatEnabled(false);
            linkSystem?.TryAddCombinedMassReward(combinedMassReward);
        }

        private void UpdateDiagnostics()
        {
            debugHealth = _health;
            debugTakingLinkDamage = _takingLinkDamage;
            debugConductivity01 = _conductivity01;
            debugDefeated = _defeated;
            debugCombatState = _combatState;
            debugTargetLocked = _lockedTarget != null;
            debugTelegraph01 = Telegraph01;
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            strafeRadius = Mathf.Max(0.1f, strafeRadius);
            wetRouteProbeDistance = Mathf.Max(0.1f, wetRouteProbeDistance);
            wetRouteBias = Mathf.Clamp01(wetRouteBias);
            containmentPadding = Mathf.Max(0f, containmentPadding);
            preferredAttackRange = Mathf.Max(0.1f, preferredAttackRange);
            telegraphDurationSeconds = Mathf.Max(
                0.1f,
                telegraphDurationSeconds);
            dashSpeed = Mathf.Max(0.1f, dashSpeed);
            maxDashDistance = Mathf.Max(0.1f, maxDashDistance);
            recoverDurationSeconds = Mathf.Max(
                0.1f,
                recoverDurationSeconds);
            repositionDurationSeconds = Mathf.Max(
                0.1f,
                repositionDurationSeconds);
            attackCooldownSeconds = Mathf.Max(0f, attackCooldownSeconds);
            linkAvoidanceRadius = Mathf.Max(0f, linkAvoidanceRadius);
            maxHealth = Mathf.Max(0.1f, maxHealth);
            interactionRadius = Mathf.Max(0.05f, interactionRadius);
            combinedMassReward = Mathf.Max(0f, combinedMassReward);
        }
    }
}
