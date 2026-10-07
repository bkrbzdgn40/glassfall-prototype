using System;
using Glassfall.Gameplay.Run;
using UnityEngine;

namespace Glassfall.Gameplay.Droplet
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DropletController))]
    public sealed class DropletMergeSystem : MonoBehaviour
    {
        [Header("Neutral Collection")]
        [Min(0f)]
        [SerializeField] private float mergedCollectionMultiplier =
            NeutralCollectionRules.DefaultMergedMultiplier;

        [Min(0f)]
        [SerializeField] private float splitCollectionMultiplier =
            NeutralCollectionRules.DefaultSplitMultiplier;

        [Header("Contamination")]
        [SerializeField] private ContaminationSystem contaminationSystem;

        public event Action<DropletMergeResult> DropletMerged;

        public int MergeCount { get; private set; }

        private DropletController _controller;
        private float _runtimeMergedCollectionMultiplier;
        private float _runtimeSplitCollectionMultiplier;
        private bool _runtimeCollectionInitialized;

        public float MergedCollectionMultiplier
        {
            get
            {
                EnsureRuntimeCollectionInitialized();
                return _runtimeMergedCollectionMultiplier;
            }
        }

        public float SplitCollectionMultiplier
        {
            get
            {
                EnsureRuntimeCollectionInitialized();
                return _runtimeSplitCollectionMultiplier;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<DropletController>();
            if (contaminationSystem == null)
                contaminationSystem = FindAnyObjectByType<ContaminationSystem>();
            ResetRuntimeRewardTuning();
        }

        public void ConfigureContamination(ContaminationSystem source)
        {
            contaminationSystem = source;
        }

        public void ConfigureNeutralCollection(
            float mergedMultiplier = NeutralCollectionRules.DefaultMergedMultiplier,
            float splitMultiplier = NeutralCollectionRules.DefaultSplitMultiplier)
        {
            mergedCollectionMultiplier = Mathf.Max(0f, mergedMultiplier);
            splitCollectionMultiplier = Mathf.Max(0f, splitMultiplier);
            ResetRuntimeRewardTuning();
        }

        public void ResetRuntimeRewardTuning()
        {
            _runtimeMergedCollectionMultiplier =
                Mathf.Max(0f, mergedCollectionMultiplier);
            _runtimeSplitCollectionMultiplier =
                Mathf.Max(0f, splitCollectionMultiplier);
            _runtimeCollectionInitialized = true;
        }

        public void ApplyAbsorbentCoreTuning(
            float mergedMultiplier = 1.25f)
        {
            EnsureRuntimeCollectionInitialized();
            _runtimeMergedCollectionMultiplier = Mathf.Max(
                _runtimeMergedCollectionMultiplier,
                Mathf.Max(0f, mergedMultiplier));
        }

        public static float CalculateCollectionMassGain(
            float sourceMass,
            bool isSplitCollection,
            bool isRecoveryFragment,
            float mergedMultiplier = NeutralCollectionRules.DefaultMergedMultiplier,
            float splitMultiplier = NeutralCollectionRules.DefaultSplitMultiplier)
        {
            if (isRecoveryFragment)
                return Mathf.Max(0f, sourceMass);

            return NeutralCollectionRules.CalculateMassGain(
                sourceMass,
                isSplitCollection,
                mergedMultiplier,
                splitMultiplier);
        }

        private void EnsureRuntimeCollectionInitialized()
        {
            if (!_runtimeCollectionInitialized)
                ResetRuntimeRewardTuning();
        }

        private void OnValidate()
        {
            mergedCollectionMultiplier = Mathf.Max(
                0f,
                mergedCollectionMultiplier);
            splitCollectionMultiplier = Mathf.Max(
                0f,
                splitCollectionMultiplier);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            NeutralDroplet neutralDroplet =
                other.GetComponentInParent<NeutralDroplet>();
            RecoverableMassFragment recoveryFragment =
                other.GetComponentInParent<RecoverableMassFragment>();

            if (neutralDroplet == null ||
                !neutralDroplet.TryConsume(out float sourceMass))
            {
                return;
            }

            bool isSplitCollection =
                _controller != null && _controller.IsSplitForm;
            bool isRecoveryFragment = recoveryFragment != null;
            float collectionMultiplier = isRecoveryFragment
                ? 1f
                : isSplitCollection
                    ? SplitCollectionMultiplier
                    : MergedCollectionMultiplier;
            float gainedMass = CalculateCollectionMassGain(
                sourceMass,
                isSplitCollection,
                isRecoveryFragment,
                MergedCollectionMultiplier,
                SplitCollectionMultiplier);

            float previousMass = _controller.CurrentMass;
            float newMass = previousMass + gainedMass;

            _controller.SetMass(newMass);
            MergeCount++;

            if (!isRecoveryFragment)
                contaminationSystem?.ApplyNeutralCleanse();

            Destroy(neutralDroplet.gameObject);

            DropletMerged?.Invoke(
                new DropletMergeResult(
                    previousMass,
                    gainedMass,
                    newMass,
                    sourceMass,
                    collectionMultiplier,
                    isSplitCollection));
        }
    }

    public readonly struct DropletMergeResult
    {
        public DropletMergeResult(
            float previousMass,
            float consumedMass,
            float newMass,
            float sourceMass,
            float collectionMultiplier,
            bool isSplitCollection)
        {
            PreviousMass = previousMass;
            ConsumedMass = consumedMass;
            NewMass = newMass;
            SourceMass = sourceMass;
            CollectionMultiplier = collectionMultiplier;
            IsSplitCollection = isSplitCollection;
        }

        public float PreviousMass { get; }
        public float ConsumedMass { get; }
        public float NewMass { get; }
        public float SourceMass { get; }
        public float CollectionMultiplier { get; }
        public bool IsSplitCollection { get; }
    }
}
