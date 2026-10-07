using System;
using Glassfall.Gameplay.Surface;
using Glassfall.Gameplay.Wetness;
using Glassfall.Input;
using Glassfall.Presentation;
using UnityEngine;

namespace Glassfall.Gameplay.Droplet
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DropletController))]
    public sealed class DropletSplitSystem : MonoBehaviour
    {
        [Header("Split Tuning")]
        [Range(0f, 0.99f)]
        [SerializeField] private float splitCostRatio = 0.10f;

        [Min(0.01f)]
        [SerializeField] private float minimumChildMass = 4f;

        [Min(0f)]
        [SerializeField] private float splitGap = 0.15f;

        [Min(0f)]
        [SerializeField] private float remergePadding = 0.05f;

        [Min(0f)]
        [SerializeField] private float remergeLockSeconds = 0.20f;

        [SerializeField] private LayerMask blockingLayers = ~0;

        [Header("Runtime Mirrors (observation only)")]
        [SerializeField] private bool debugIsSplit;
        [SerializeField] private float debugPrimaryMass;
        [SerializeField] private float debugMirrorMass;
        [SerializeField] private float debugCombinedMass;
        [SerializeField] private float debugLastSplitCost;
        [SerializeField] private float debugCurrentSeparation;
        [SerializeField] private float debugRemergeThreshold;
        [SerializeField] private float debugRemergeLockRemaining;
        [SerializeField] private float debugPrimaryInputPolarity = 1f;
        [SerializeField] private float debugMirrorInputPolarity;

        private DropletController _primaryController;
        private DropletVisual _primaryVisual;
        private Rigidbody2D _primaryBody;
        private CircleCollider2D _primaryCollider;
        private DropletTrailDepositor _primaryDepositor;
        private float _remergeUnlockTime;
        private bool _splitActive;
        private float _runtimeSplitCostRatio;
        private bool _runtimeSplitCostInitialized;

        private GameObject _mirrorObject;
        private DropletController _mirrorController;
        private Rigidbody2D _mirrorBody;
        private CircleCollider2D _mirrorCollider;

        public event Action<SplitResult> SplitPerformed;
        public event Action<ReMergeResult> ReMergePerformed;

        public bool IsSplit =>
            _splitActive &&
            _mirrorObject != null &&
            _mirrorObject.activeInHierarchy &&
            _mirrorController != null;

        public DropletController PrimaryController
        {
            get
            {
                if (_primaryController == null)
                    _primaryController = GetComponent<DropletController>();
                return _primaryController;
            }
        }
        public DropletController MirrorController =>
            IsSplit ? _mirrorController : null;
        public Transform PrimaryTransform =>
            PrimaryController != null ? PrimaryController.transform : null;
        public Transform MirrorTransform =>
            IsSplit ? _mirrorController.transform : null;
        public float SplitCostRatio => GetRuntimeSplitCostRatio();

        private void Awake()
        {
            _primaryController = GetComponent<DropletController>();
            _primaryVisual = GetComponent<DropletVisual>();
            _primaryBody = GetComponent<Rigidbody2D>();
            _primaryCollider = GetComponent<CircleCollider2D>();
            _primaryDepositor = GetComponent<DropletTrailDepositor>();
            ResetRuntimeRewardTuning();
        }

        private void OnValidate()
        {
            splitCostRatio = Mathf.Clamp(splitCostRatio, 0f, 0.99f);
            minimumChildMass = Mathf.Max(0.01f, minimumChildMass);
            splitGap = Mathf.Max(0f, splitGap);
            remergePadding = Mathf.Max(0f, remergePadding);
            remergeLockSeconds = Mathf.Max(0f, remergeLockSeconds);
        }

        public void ResetRuntimeRewardTuning()
        {
            _runtimeSplitCostRatio =
                Mathf.Clamp(splitCostRatio, 0f, 0.99f);
            _runtimeSplitCostInitialized = true;
        }

        public void ApplyEfficientDivisionTuning(
            float costRatio = 0.07f)
        {
            EnsureRuntimeSplitCostInitialized();
            _runtimeSplitCostRatio = Mathf.Min(
                _runtimeSplitCostRatio,
                Mathf.Clamp(costRatio, 0f, 0.99f));
        }

        private void FixedUpdate()
        {
            if (_splitActive && !IsSplit)
            {
                RecoverFromMissingMirror();
                return;
            }

            if (!IsSplit)
                return;

            if (Time.fixedTime < _remergeUnlockTime)
                return;

            float separation = Vector2.Distance(
                _primaryBody.position,
                _mirrorBody.position);
            float threshold = _primaryController.CurrentRadius +
                _mirrorController.CurrentRadius +
                remergePadding;

            if (separation <= threshold)
                ReMergeInternal();
        }

        private void Update()
        {
            UpdateDiagnostics();
        }

        public bool ProcessCommand(PlayerCommand command)
        {
            if (command.ResetPressed)
            {
                if (IsSplit)
                    ReMergeInternal();
                else if (_splitActive)
                    RecoverFromMissingMirror();

                _primaryController.ResetToSpawn();
                _primaryVisual?.ReinitializeFromCurrentState();
                _primaryDepositor?.ResetDepositAnchor(
                    _primaryBody.position);
                return true;
            }

            if (command.SplitPressed && !IsSplit)
                TrySplit(command.MoveDirection);

            return false;
        }

        public static bool TryCalculateChildMass(
            float originalMass,
            float costRatio,
            float minimumMass,
            out float splitCost,
            out float childMass)
        {
            splitCost = 0f;
            childMass = 0f;

            if (originalMass <= 0f ||
                costRatio < 0f ||
                costRatio >= 1f ||
                minimumMass <= 0f)
            {
                return false;
            }

            splitCost = originalMass * costRatio;
            float remainingMass = originalMass - splitCost;
            childMass = remainingMass * 0.5f;
            return childMass >= minimumMass;
        }

        private bool TrySplit(Vector2 requestedAxis)
        {
            if (IsSplit || _splitActive)
                return false;

            if (!HasRequiredComponents() ||
                !TryCalculateChildMass(
                    _primaryController.CurrentMass,
                    GetRuntimeSplitCostRatio(),
                    minimumChildMass,
                    out float splitCost,
                    out float childMass))
            {
                return false;
            }

            Vector2 axis = GetSplitAxis(requestedAxis);
            float childRadius =
                _primaryController.GetRadiusForMass(childMass);

            if (!TryFindSafeSpawnPositions(
                    axis,
                    childRadius,
                    out Vector2 primaryPosition,
                    out Vector2 mirrorPosition))
            {
                return false;
            }

            Vector2 parentVelocity = _primaryController.CurrentVelocity;
            Vector2 mirrorVelocity = -parentVelocity;
            Quaternion inheritedRotation = _primaryController.transform.rotation;
            _primaryVisual.ResetToCanonicalState();
            GameObject mirrorObject = Instantiate(
                gameObject,
                mirrorPosition,
                inheritedRotation,
                transform.parent);
            mirrorObject.name = "Droplet_Mirror";

            if (!TryGetMirrorComponents(
                    mirrorObject,
                    out DropletController mirrorController,
                    out DropletVisual mirrorVisual,
                    out Rigidbody2D mirrorBody,
                    out CircleCollider2D mirrorCollider,
                    out UnityInputAdapter mirrorInput,
                    out DropletSurfaceSensor mirrorSurface,
                    out DropletTrailDepositor mirrorDepositor))
            {
                _primaryVisual.ReinitializeFromCurrentState();
                mirrorObject.SetActive(false);
                Destroy(mirrorObject);
                return false;
            }

            DropletSplitSystem mirrorSplit =
                mirrorObject.GetComponent<DropletSplitSystem>();
            if (mirrorSplit == null)
            {
                _primaryVisual.ReinitializeFromCurrentState();
                mirrorObject.SetActive(false);
                Destroy(mirrorObject);
                return false;
            }

            mirrorSplit.enabled = false;
            mirrorInput.ClearQueuedCommands();
            mirrorSurface.ResetState();
            DropletWetnessSensor mirrorWetness =
                mirrorObject.GetComponent<DropletWetnessSensor>();
            mirrorWetness?.Refresh(mirrorPosition);
            mirrorController.ConfigureSplitRole(-1f, false, false, true);

            _primaryController.ConfigureSplitRole(1f, true, false, true);
            _primaryController.SetMass(childMass);
            mirrorController.SetMass(childMass);

            _primaryBody.position = primaryPosition;
            mirrorBody.position = mirrorPosition;
            _primaryBody.linearVelocity = parentVelocity;
            mirrorBody.linearVelocity = mirrorVelocity;
            _primaryVisual.ReinitializeFromCurrentState();
            mirrorVisual.ReinitializeFromCurrentState();
            _primaryBody.WakeUp();
            mirrorBody.WakeUp();

            Physics2D.IgnoreCollision(
                _primaryCollider,
                mirrorCollider,
                true);

            _primaryDepositor?.ResetDepositAnchor(primaryPosition);
            mirrorDepositor.ResetDepositAnchor(mirrorPosition);

            _mirrorObject = mirrorObject;
            _mirrorController = mirrorController;
            _mirrorBody = mirrorBody;
            _mirrorCollider = mirrorCollider;
            _remergeUnlockTime = Time.fixedTime + remergeLockSeconds;
            _splitActive = true;
            debugLastSplitCost = splitCost;

            SplitPerformed?.Invoke(
                new SplitResult(
                    _primaryController.CurrentMass,
                    childMass,
                    splitCost));
            return true;
        }

        private float GetRuntimeSplitCostRatio()
        {
            EnsureRuntimeSplitCostInitialized();
            return _runtimeSplitCostRatio;
        }

        private void EnsureRuntimeSplitCostInitialized()
        {
            if (!_runtimeSplitCostInitialized)
                ResetRuntimeRewardTuning();
        }

        private bool HasRequiredComponents()
        {
            return _primaryController != null &&
                _primaryVisual != null &&
                _primaryBody != null &&
                _primaryCollider != null &&
                _primaryDepositor != null &&
                GetComponent<UnityInputAdapter>() != null &&
                GetComponent<DropletMergeSystem>() != null &&
                GetComponent<DropletWetnessSensor>() != null &&
                GetComponent<DropletSurfaceSensor>() != null &&
                _primaryDepositor.Field != null &&
                GetComponent<DropletWetnessSensor>().Field != null;
        }

        private bool TryGetMirrorComponents(
            GameObject mirrorObject,
            out DropletController controller,
            out DropletVisual visual,
            out Rigidbody2D body,
            out CircleCollider2D collider,
            out UnityInputAdapter input,
            out DropletSurfaceSensor surface,
            out DropletTrailDepositor depositor)
        {
            controller = mirrorObject.GetComponent<DropletController>();
            visual = mirrorObject.GetComponent<DropletVisual>();
            body = mirrorObject.GetComponent<Rigidbody2D>();
            collider = mirrorObject.GetComponent<CircleCollider2D>();
            input = mirrorObject.GetComponent<UnityInputAdapter>();
            surface = mirrorObject.GetComponent<DropletSurfaceSensor>();
            depositor = mirrorObject.GetComponent<DropletTrailDepositor>();

            return controller != null &&
                visual != null &&
                body != null &&
                collider != null &&
                input != null &&
                surface != null &&
                depositor != null &&
                mirrorObject.GetComponent<DropletMergeSystem>() != null &&
                mirrorObject.GetComponent<DropletWetnessSensor>() != null;
        }

        private Vector2 GetSplitAxis(Vector2 requestedAxis)
        {
            if (requestedAxis.sqrMagnitude > 0.0001f)
                return requestedAxis.normalized;

            Vector2 velocity = _primaryController.CurrentVelocity;
            if (velocity.sqrMagnitude > 0.0001f)
                return velocity.normalized;

            return Vector2.right;
        }

        private bool TryFindSafeSpawnPositions(
            Vector2 axis,
            float childRadius,
            out Vector2 primaryPosition,
            out Vector2 mirrorPosition)
        {
            float separation = childRadius * 2f + splitGap;
            float halfSeparation = separation * 0.5f;
            Vector2 center = _primaryBody.position;

            if (TryGetSafePair(
                    center,
                    axis,
                    halfSeparation,
                    childRadius,
                    out primaryPosition,
                    out mirrorPosition))
            {
                return true;
            }

            Vector2 perpendicular = new Vector2(-axis.y, axis.x);
            return TryGetSafePair(
                center,
                perpendicular,
                halfSeparation,
                childRadius,
                out primaryPosition,
                out mirrorPosition);
        }

        private bool TryGetSafePair(
            Vector2 center,
            Vector2 axis,
            float halfSeparation,
            float radius,
            out Vector2 primaryPosition,
            out Vector2 mirrorPosition)
        {
            primaryPosition = center + axis * halfSeparation;
            mirrorPosition = center - axis * halfSeparation;
            return IsPositionClear(primaryPosition, radius) &&
                IsPositionClear(mirrorPosition, radius);
        }

        private bool IsPositionClear(Vector2 position, float radius)
        {
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(
                position,
                radius,
                blockingLayers);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider2D overlap = overlaps[i];
                if (overlap == null || overlap.isTrigger)
                    continue;

                if (overlap.transform == transform ||
                    overlap.transform.IsChildOf(transform) ||
                    overlap.attachedRigidbody == _primaryBody)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void ReMergeInternal()
        {
            if (!IsSplit)
            {
                if (_mirrorController != null)
                    RecoverFromMissingMirror();
                return;
            }

            float primaryMass = _primaryController.CurrentMass;
            float mirrorMass = _mirrorController.CurrentMass;
            float combinedMass = primaryMass + mirrorMass;
            if (combinedMass <= 0f)
                return;

            Vector2 combinedPosition = (
                primaryMass * _primaryBody.position +
                mirrorMass * _mirrorBody.position) /
                combinedMass;
            Vector2 combinedVelocity = (
                primaryMass * _primaryBody.linearVelocity +
                mirrorMass * _mirrorBody.linearVelocity) /
                combinedMass;

            Physics2D.IgnoreCollision(
                _primaryCollider,
                _mirrorCollider,
                false);

            _mirrorObject.SetActive(false);
            Destroy(_mirrorObject);
            _splitActive = false;
            _mirrorObject = null;
            _mirrorController = null;
            _mirrorBody = null;
            _mirrorCollider = null;

            _primaryController.SetMass(combinedMass);
            _primaryBody.position = combinedPosition;
            _primaryBody.linearVelocity = combinedVelocity;
            _primaryVisual?.ReinitializeFromCurrentState();
            _primaryBody.WakeUp();
            _primaryController.ConfigureSplitRole(1f, true, true, false);
            _primaryDepositor?.ResetDepositAnchor(combinedPosition);

            ReMergePerformed?.Invoke(
                new ReMergeResult(
                    primaryMass,
                    mirrorMass,
                    combinedMass));
        }

        private void RecoverFromMissingMirror()
        {
            _splitActive = false;
            _mirrorObject = null;
            _mirrorController = null;
            _mirrorBody = null;
            _mirrorCollider = null;
            _primaryController.ConfigureSplitRole(1f, true, true, false);
            _primaryVisual?.ReinitializeFromCurrentState();
            _primaryDepositor?.ResetDepositAnchor(_primaryBody.position);
        }

        private void UpdateDiagnostics()
        {
            debugIsSplit = IsSplit;
            debugPrimaryMass = _primaryController != null
                ? _primaryController.CurrentMass
                : 0f;
            debugMirrorMass = IsSplit
                ? _mirrorController.CurrentMass
                : 0f;
            debugCombinedMass = debugPrimaryMass + debugMirrorMass;
            debugCurrentSeparation = IsSplit
                ? Vector2.Distance(
                    _primaryBody.position,
                    _mirrorBody.position)
                : 0f;
            debugRemergeThreshold = IsSplit
                ? _primaryController.CurrentRadius +
                  _mirrorController.CurrentRadius +
                  remergePadding
                : 0f;
            debugRemergeLockRemaining = IsSplit
                ? Mathf.Max(0f, _remergeUnlockTime - Time.fixedTime)
                : 0f;
            debugPrimaryInputPolarity = _primaryController != null
                ? _primaryController.MovementInputPolarity
                : 1f;
            debugMirrorInputPolarity = IsSplit
                ? _mirrorController.MovementInputPolarity
                : 0f;
        }
    }

    public readonly struct SplitResult
    {
        public SplitResult(
            float primaryMass,
            float mirrorMass,
            float splitCost)
        {
            PrimaryMass = primaryMass;
            MirrorMass = mirrorMass;
            SplitCost = splitCost;
        }

        public float PrimaryMass { get; }
        public float MirrorMass { get; }
        public float SplitCost { get; }
    }

    public readonly struct ReMergeResult
    {
        public ReMergeResult(
            float primaryMass,
            float mirrorMass,
            float combinedMass)
        {
            PrimaryMass = primaryMass;
            MirrorMass = mirrorMass;
            CombinedMass = combinedMass;
        }

        public float PrimaryMass { get; }
        public float MirrorMass { get; }
        public float CombinedMass { get; }
    }
}
