using System;
using System.Collections.Generic;
using Glassfall.Gameplay.Combat;
using Glassfall.Gameplay.Droplet;
using Glassfall.Gameplay.Run;
using Glassfall.Gameplay.Wetness;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Glassfall.Gameplay.Rewards
{
    [DisallowMultipleComponent]
    public sealed class MidRunRewardController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DropletSplitSystem splitSystem;
        [SerializeField] private DropletMergeSystem mergeSystem;
        [SerializeField] private SplitLinkCombat combat;
        [SerializeField] private MergedFormIdentity mergedFormIdentity;
        [SerializeField] private MassShearSystem massShearSystem;
        [SerializeField] private WetnessField wetnessField;
        [SerializeField] private ContaminationSystem contaminationSystem;

        [Header("Decision Freeze")]
        [SerializeField] private bool freezeGameplayDuringChoice = true;

        [Header("Reward Pool V1")]
        [Min(1)]
        [SerializeField] private int recentHistoryLength = 3;
        [SerializeField] private bool avoidRepeatedChoiceSets = true;

        [Header("Runtime Mirrors")]
        [SerializeField] private int debugActiveRewardStep;
        [SerializeField] private bool debugGameplayFrozen;
        [SerializeField] private MidRunReward debugFirstReward =
            MidRunReward.None;
        [SerializeField] private MidRunReward debugSecondReward =
            MidRunReward.None;
        [SerializeField] private int debugCompletedRewardSteps;
        [SerializeField] private int debugOneTimeRewardsSelected;

        private int _activeRewardStep;
        private MidRunReward _firstReward = MidRunReward.None;
        private MidRunReward _secondReward = MidRunReward.None;
        private float _selectedAtUnscaledTime = -1f;
        private bool _ownsGameplayFreeze;
        private float _timeScaleBeforeFreeze = 1f;
        private System.Random _choiceRandom;
        private static int _sessionSeedSerial;

        private readonly List<MidRunReward> _activeChoices =
            new List<MidRunReward>(3);
        private readonly List<MidRunReward> _selectedHistory =
            new List<MidRunReward>(3);
        private readonly List<MidRunReward> _selectedOneTimeRewards =
            new List<MidRunReward>(MidRunRewardRules.PoolSize);
        private readonly Dictionary<int, MidRunReward> _completedRewardsByStep =
            new Dictionary<int, MidRunReward>();
        private readonly List<string> _offeredChoiceHistory =
            new List<string>(3);
        private readonly List<MidRunReward> _supportedRewards =
            new List<MidRunReward>(MidRunRewardRules.PoolSize);

        public int ActiveRewardStep => _activeRewardStep;
        public bool IsChoosing => _activeRewardStep > 0;
        public MidRunReward FirstReward => _firstReward;
        public MidRunReward SecondReward => _secondReward;
        public float SelectedAtUnscaledTime => _selectedAtUnscaledTime;
        public bool FreezeGameplayDuringChoice => freezeGameplayDuringChoice;
        public bool IsGameplayFrozen => _ownsGameplayFreeze;
        public int ActiveChoiceCount => _activeChoices.Count;
        public int SupportedRewardCount => _supportedRewards.Count;
        public int RecentHistoryLength => recentHistoryLength;
        public int SelectedOneTimeRewardCount => _selectedOneTimeRewards.Count;
        public int CompletedRewardStepCount => _completedRewardsByStep.Count;

        public MidRunReward GetActiveChoice(int index)
        {
            return index >= 0 && index < _activeChoices.Count
                ? _activeChoices[index]
                : MidRunReward.None;
        }

        public MidRunReward GetRewardForStep(int rewardStep)
        {
            return rewardStep > 0 &&
                   _completedRewardsByStep.TryGetValue(
                       rewardStep,
                       out MidRunReward reward)
                ? reward
                : MidRunReward.None;
        }

        public void Configure(
            DropletSplitSystem playerSplitSystem,
            DropletMergeSystem playerMergeSystem,
            SplitLinkCombat linkCombat,
            MergedFormIdentity identity = null,
            MassShearSystem shearSystem = null,
            WetnessField field = null,
            ContaminationSystem contamination = null)
        {
            splitSystem = playerSplitSystem;
            mergeSystem = playerMergeSystem;
            combat = linkCombat;
            mergedFormIdentity = identity;
            massShearSystem = shearSystem;
            wetnessField = field;
            contaminationSystem = contamination;

            EnsureReferences();
        }

        public void ConfigureRewardPool(
            int historyLength = 3,
            bool avoidChoiceSetRepeats = true)
        {
            recentHistoryLength = Mathf.Max(1, historyLength);
            avoidRepeatedChoiceSets = avoidChoiceSetRepeats;
            TrimHistory(_selectedHistory);
            TrimHistory(_offeredChoiceHistory);
        }

        public void ConfigureDecisionFreeze(bool freezeGameplay = true)
        {
            freezeGameplayDuringChoice = freezeGameplay;

            if (!freezeGameplayDuringChoice)
                ReleaseGameplayFreeze();

            UpdateDiagnostics();
        }

        private void Awake()
        {
            EnsureReferences();
            ReseedChoiceRandom();
            ResetRewardsForSession();
        }

        private void Update()
        {
            if (!IsChoosing || Keyboard.current == null)
                return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame ||
                Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                TryChoose(GetActiveChoice(0));
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame ||
                     Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                TryChoose(GetActiveChoice(1));
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame ||
                     Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                TryChoose(GetActiveChoice(2));
            }
        }

        public bool BeginRewardChoice(int rewardStep)
        {
            if (IsChoosing ||
                rewardStep < 1 ||
                HasCompletedStep(rewardStep))
            {
                return false;
            }

            EnsureReferences();
            if (splitSystem == null || mergeSystem == null || combat == null)
                return false;

            BuildSupportedRewardPool();
            if (_supportedRewards.Count == 0)
                return false;

            EnsureChoiceRandom();
            // Runtime offers always come from the eligible M25 pool. The
            // legacy GetFirst/Second/ThirdOption helpers are not consulted.
            int rotation = _choiceRandom.Next(int.MaxValue);
            MidRunReward[] choices = MidRunRewardRules.BuildChoices(
                rewardStep,
                _supportedRewards,
                _selectedOneTimeRewards,
                rotation);
            string signature = CreateChoiceSignature(choices);

            if (avoidRepeatedChoiceSets)
            {
                int attempts = 0;
                while (_offeredChoiceHistory.Contains(signature) &&
                       attempts < MidRunRewardRules.PoolSize)
                {
                    attempts++;
                    rotation = _choiceRandom.Next(int.MaxValue);
                    choices = MidRunRewardRules.BuildChoices(
                        rewardStep,
                        _supportedRewards,
                        _selectedOneTimeRewards,
                        rotation + attempts);
                    signature = CreateChoiceSignature(choices);
                }
            }

            if (choices.Length == 0)
                return false;

            _activeRewardStep = rewardStep;
            _activeChoices.Clear();
            _activeChoices.AddRange(choices);
            _offeredChoiceHistory.Add(signature);
            TrimHistory(_offeredChoiceHistory);
            AcquireGameplayFreeze();
            UpdateDiagnostics();
            return true;
        }

        public bool TryChoose(MidRunReward reward)
        {
            if (!IsChoosing || !_activeChoices.Contains(reward))
                return false;

            EnsureReferences();
            if (!ApplyReward(reward))
                return false;

            int completedStep = _activeRewardStep;
            _completedRewardsByStep[completedStep] = reward;

            if (completedStep == 1)
                _firstReward = reward;
            else if (completedStep == 2)
                _secondReward = reward;

            if (MidRunRewardRules.GetRepeatPolicy(reward) ==
                    MidRunRewardRepeatPolicy.OneTime &&
                !_selectedOneTimeRewards.Contains(reward))
            {
                _selectedOneTimeRewards.Add(reward);
            }

            _selectedHistory.Add(reward);
            TrimHistory(_selectedHistory);
            _activeChoices.Clear();
            _activeRewardStep = 0;
            _selectedAtUnscaledTime = Time.unscaledTime;
            ReleaseGameplayFreeze();
            UpdateDiagnostics();
            return true;
        }

        public bool HasCompletedStep(int rewardStep)
        {
            return rewardStep > 0 &&
                _completedRewardsByStep.ContainsKey(rewardStep);
        }

        public void ResetRewardsForSession()
        {
            EnsureReferences();
            ReseedChoiceRandom();
            ReleaseGameplayFreeze();

            _activeRewardStep = 0;
            _firstReward = MidRunReward.None;
            _secondReward = MidRunReward.None;
            _selectedAtUnscaledTime = -1f;
            _activeChoices.Clear();
            _selectedHistory.Clear();
            _selectedOneTimeRewards.Clear();
            _completedRewardsByStep.Clear();
            _offeredChoiceHistory.Clear();

            splitSystem?.ResetRuntimeRewardTuning();
            mergeSystem?.ResetRuntimeRewardTuning();
            combat?.ResetRuntimeRewardTuning();
            mergedFormIdentity?.ResetRuntimeRewardTuning();
            massShearSystem?.ResetRuntimeRewardTuning();
            wetnessField?.ResetRuntimeRewardTuning();
            contaminationSystem?.ResetRuntimeRewardTuning();

            UpdateDiagnostics();
        }

        private void OnDisable()
        {
            ReleaseGameplayFreeze();
        }

        private void AcquireGameplayFreeze()
        {
            if (!freezeGameplayDuringChoice || _ownsGameplayFreeze)
                return;

            _timeScaleBeforeFreeze = Time.timeScale;
            Time.timeScale = 0f;
            _ownsGameplayFreeze = true;
        }

        private void ReleaseGameplayFreeze()
        {
            if (!_ownsGameplayFreeze)
                return;

            Time.timeScale = _timeScaleBeforeFreeze;
            _ownsGameplayFreeze = false;
        }

        private bool ApplyReward(MidRunReward reward)
        {
            switch (reward)
            {
                case MidRunReward.EfficientDivision:
                    if (splitSystem == null)
                        return false;
                    splitSystem.ApplyEfficientDivisionTuning(
                        MidRunRewardRules.EfficientDivisionSplitCostRatio);
                    return true;

                case MidRunReward.AbsorbentCore:
                    if (mergeSystem == null)
                        return false;
                    mergeSystem.ApplyAbsorbentCoreTuning(
                        MidRunRewardRules
                            .AbsorbentCoreMergedCollectionMultiplier);
                    return true;

                case MidRunReward.StableLink:
                    if (combat == null)
                        return false;
                    combat.ApplyStableLinkReward(
                        MidRunRewardRules.StableLinkEndpointInset);
                    return true;

                case MidRunReward.ConductiveResidue:
                    if (combat == null)
                        return false;
                    combat.ApplyConductiveResidueReward(
                        MidRunRewardRules
                            .ConductiveResidueActivationThreshold);
                    return true;

                case MidRunReward.GravitationalDrop:
                    if (mergedFormIdentity == null)
                        return false;
                    mergedFormIdentity.ApplyGravitationalDropReward(
                        MidRunRewardRules
                            .GravitationalDropAttractionRadiusMultiplier);
                    return true;

                case MidRunReward.DenseCore:
                    if (mergedFormIdentity == null)
                        return false;
                    mergedFormIdentity.ApplyDenseCoreReward(
                        MidRunRewardRules.DenseCoreShoveMultiplier);
                    return true;

                case MidRunReward.HighCohesion:
                    if (massShearSystem == null)
                        return false;
                    massShearSystem.ApplyHighCohesionReward(
                        MidRunRewardRules.HighCohesionMergedShearMultiplier);
                    return true;

                case MidRunReward.ElasticMembrane:
                    if (combat == null)
                        return false;
                    combat.ApplyElasticMembraneReward(
                        MidRunRewardRules
                            .ElasticMembraneStableDistanceMultiplier);
                    return true;

                case MidRunReward.SlowEvaporation:
                    if (wetnessField == null)
                        return false;
                    wetnessField.ApplySlowEvaporationReward(
                        MidRunRewardRules.SlowEvaporationDecayMultiplier);
                    return true;

                case MidRunReward.PurifyingResonance:
                    if (contaminationSystem == null)
                        return false;
                    contaminationSystem.ApplyPurifyingResonanceReward(
                        MidRunRewardRules
                            .PurifyingResonanceRecoveryCleanseMultiplier);
                    return true;

                default:
                    return false;
            }
        }

        private void EnsureReferences()
        {
            if (splitSystem == null)
                splitSystem = FindSceneObject<DropletSplitSystem>();

            if (mergeSystem == null && splitSystem != null)
                mergeSystem = splitSystem.GetComponent<DropletMergeSystem>();
            if (mergeSystem == null)
                mergeSystem = FindSceneObject<DropletMergeSystem>();

            if (combat == null)
                combat = FindSceneObject<SplitLinkCombat>();

            if (mergedFormIdentity == null && splitSystem != null)
            {
                mergedFormIdentity =
                    splitSystem.GetComponent<MergedFormIdentity>();
            }
            if (mergedFormIdentity == null)
                mergedFormIdentity =
                    FindSceneObject<MergedFormIdentity>();

            if (massShearSystem == null)
                massShearSystem = FindSceneObject<MassShearSystem>();

            if (wetnessField == null && combat != null)
                wetnessField = combat.WetnessField;
            if (wetnessField == null)
                wetnessField = FindSceneObject<WetnessField>();

            if (contaminationSystem == null)
                contaminationSystem = FindSceneObject<ContaminationSystem>();
        }

        private void EnsureChoiceRandom()
        {
            if (_choiceRandom == null)
                ReseedChoiceRandom();
        }

        private void ReseedChoiceRandom()
        {
            unchecked
            {
                _sessionSeedSerial++;
                int seed = Environment.TickCount ^
                    (int)DateTime.UtcNow.Ticks ^
                    GetEntityId().GetHashCode() ^
                    (_sessionSeedSerial * 486187739);
                _choiceRandom = new System.Random(seed);
            }
        }

        private static T FindSceneObject<T>() where T : UnityEngine.Object
        {
            return FindAnyObjectByType<T>(FindObjectsInactive.Include);
        }

        private void BuildSupportedRewardPool()
        {
            _supportedRewards.Clear();
            MidRunReward[] allRewards = MidRunRewardRules.GetAllRewards();
            for (int i = 0; i < allRewards.Length; i++)
            {
                if (IsRewardSupported(allRewards[i]))
                    _supportedRewards.Add(allRewards[i]);
            }
        }

        private bool IsRewardSupported(MidRunReward reward)
        {
            switch (reward)
            {
                case MidRunReward.EfficientDivision:
                    return splitSystem != null;
                case MidRunReward.AbsorbentCore:
                    return mergeSystem != null;
                case MidRunReward.StableLink:
                case MidRunReward.ConductiveResidue:
                case MidRunReward.ElasticMembrane:
                    return combat != null;
                case MidRunReward.GravitationalDrop:
                case MidRunReward.DenseCore:
                    return mergedFormIdentity != null;
                case MidRunReward.HighCohesion:
                    return massShearSystem != null;
                case MidRunReward.SlowEvaporation:
                    return wetnessField != null;
                case MidRunReward.PurifyingResonance:
                    return contaminationSystem != null;
                default:
                    return false;
            }
        }

        private void TrimHistory<T>(List<T> history)
        {
            int maxCount = Mathf.Max(1, recentHistoryLength);
            while (history.Count > maxCount)
                history.RemoveAt(0);
        }

        private static string CreateChoiceSignature(
            IReadOnlyList<MidRunReward> choices)
        {
            if (choices == null || choices.Count == 0)
                return string.Empty;

            // Treat a choice as a set for repetition checks. Rotation may
            // change card order, but should not make the same three rewards
            // look new to the short history.
            int[] values = new int[choices.Count];
            for (int i = 0; i < choices.Count; i++)
                values[i] = (int)choices[i];
            System.Array.Sort(values);

            string signature = string.Empty;
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    signature += ",";
                signature += values[i].ToString();
            }

            return signature;
        }

        private void UpdateDiagnostics()
        {
            debugActiveRewardStep = _activeRewardStep;
            debugGameplayFrozen = _ownsGameplayFreeze;
            debugFirstReward = _firstReward;
            debugSecondReward = _secondReward;
            debugCompletedRewardSteps = _completedRewardsByStep.Count;
            debugOneTimeRewardsSelected = _selectedOneTimeRewards.Count;
        }

        private void OnValidate()
        {
            recentHistoryLength = Mathf.Max(1, recentHistoryLength);
        }
    }
}
