using System;
using Glassfall.Gameplay.Builds;
using Glassfall.Gameplay.Combat;
using Glassfall.Gameplay.Encounter;
using Glassfall.Gameplay.Droplet;
using Glassfall.Gameplay.Rewards;
using Glassfall.Gameplay.Wetness;
using UnityEngine;

namespace Glassfall.Gameplay.Run
{
    [DisallowMultipleComponent]
    public sealed class MicroRunController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MicroBuildChoiceController buildController;
        [SerializeField] private ResonanceNode resonanceNode;
        [SerializeField] private ArcHunter arcHunter;
        [SerializeField] private SplitLinkCombat combat;
        [SerializeField] private NeutralDropletSpawner neutralSpawner;
        [SerializeField] private MidRunRewardController rewardController;
        [SerializeField] private DropletSplitSystem splitSystem;
        [SerializeField] private ContaminationSystem contaminationSystem;
        [SerializeField] private WetnessField wetnessField;

        [Header("Encounter Route")]
        [SerializeField] private Vector2[] encounterSpawnPositions =
        {
            new Vector2(-4.0f, 2.5f),
            new Vector2(4.0f, 2.5f),
            new Vector2(0.0f, -3.0f)
        };
        [SerializeField] private ArcHunterMovementMode[] encounterMovementModes =
        {
            ArcHunterMovementMode.Chase,
            ArcHunterMovementMode.Strafe,
            ArcHunterMovementMode.WetRoutePressure
        };

        [Header("M28A Seeded Encounter Variety")]
        [SerializeField] private SeededEncounterCandidatePool[] encounterCandidatePools =
            Array.Empty<SeededEncounterCandidatePool>();
        [SerializeField] private int developmentFixedSeedOverride;

        [Header("World Segments")]
        [SerializeField] private MicroRunWorldSegment[] worldSegments =
            new MicroRunWorldSegment[0];

        [Header("Legacy World Progress Fallback")]
        [Min(1)]
        [SerializeField] private int currentWorldStartEncounter = 1;
        [Min(0)]
        [SerializeField] private int currentWorldEncounterCount;

        [Header("Micro Run V2 Pacing")]
        [Min(0f)]
        [SerializeField] private float preparationMassGainTarget = 2.0f;

        [Min(0f)]
        [SerializeField] private float preparationMaxSeconds = 4.0f;

        [Min(0f)]
        [SerializeField] private float recoveryMassGainTarget = 3.5f;

        [Min(0f)]
        [SerializeField] private float recoveryMaxSeconds = 5.0f;

        [Min(0f)]
        [SerializeField] private float intermissionSeconds = 1.0f;

        [Header("Encounter Presentation")]
        [Min(0f)]
        [SerializeField] private float encounterCompletionBeatSeconds = 0.80f;

        [Header("World Navigation")]
        [Min(0.1f)]
        [SerializeField] private float encounterActivationRadius = 3.75f;

        [Header("Contamination Failure")]
        [Min(0f)]
        [SerializeField] private float failureBeatSeconds = 0.65f;

        [Header("Runtime Mirrors")]
        [SerializeField] private MicroRunPhase debugPhase =
            MicroRunPhase.ChooseBuild;
        [SerializeField] private int debugCurrentEncounter;
        [SerializeField] private float debugElapsedSeconds;
        [SerializeField] private float debugIntermissionRemaining;
        [SerializeField] private float debugPreparationRemaining;
        [SerializeField] private float debugRecoveryRemaining;
        [SerializeField] private float debugPhaseStartMass;
        [SerializeField] private float debugPhaseTargetMass;
        [SerializeField] private float debugPhaseBestMassGain;
        [SerializeField] private float debugContamination01;
        [SerializeField] private ContaminationState debugContaminationState;

        private MicroRunProgress _progress;
        private float _runStartedAt = -1f;
        private float _completedElapsedSeconds;
        private float _intermissionRemaining;
        private float _preparationRemaining;
        private float _recoveryRemaining;
        private float _phaseStartMass;
        private float _phaseTargetMass;
        private float _phaseBestMassGain;
        private float _encounterCompletionBeatRemaining;
        private int _pendingRewardEncounter;
        private float _failureBeatRemaining;
        private MicroRunPhase _failedFromPhase = MicroRunPhase.ChooseBuild;
        private bool _awaitingEncounterArrival;
        private int _activeWorldSegmentIndex = -1;
        private int _worldHandoffCount;
        private MicroRunWorldSegment[] _authoredWorldSegments =
            Array.Empty<MicroRunWorldSegment>();
        private Vector2[] _authoredEncounterSpawnPositions =
            Array.Empty<Vector2>();
        private ArcHunterMovementMode[] _authoredMovementModes =
            Array.Empty<ArcHunterMovementMode>();
        private SeededEncounterCandidatePool[] _authoredCandidatePools =
            Array.Empty<SeededEncounterCandidatePool>();
        private SeededEncounterPlan _encounterPlan;
        private int _runSeed;
        private bool _encounterPlanGenerated;

        public MicroRunPhase Phase =>
            _progress != null ? _progress.Phase : MicroRunPhase.ChooseBuild;
        public int CurrentEncounter =>
            _progress != null ? _progress.CurrentEncounter : 0;
        public int EncounterCount =>
            _progress != null ? _progress.EncounterCount : GetEncounterCount();
        public int WorldSegmentCount =>
            Mathf.Max(1, GetConfiguredWorldSegmentCount());
        public int CurrentWorldSegmentIndex => ResolveCurrentWorldSegmentIndex();
        public bool HasNextWorldSegment =>
            GetConfiguredWorldSegmentCount() > 0 &&
            CurrentWorldSegmentIndex + 1 < WorldSegmentCount;
        public int NextWorldSegmentIndex => HasNextWorldSegment
            ? CurrentWorldSegmentIndex + 1
            : -1;
        public string NextWorldId
        {
            get
            {
                return TryGetWorldSegmentCopy(
                    NextWorldSegmentIndex,
                    out MicroRunWorldSegment segment)
                    ? segment.WorldId
                    : string.Empty;
            }
        }
        public string CurrentWorldId
        {
            get
            {
                if (TryResolveWorldSegment(
                        Mathf.Max(1, CurrentEncounter),
                        out MicroRunWorldSegment segment,
                        out _,
                        out _,
                        out _))
                {
                    return segment.WorldId;
                }

                return "WORLD_1";
            }
        }
        public int CurrentWorldStartEncounter
        {
            get
            {
                if (TryResolveWorldSegment(
                        Mathf.Max(1, CurrentEncounter),
                        out _,
                        out _,
                        out int startEncounter,
                        out _))
                {
                    return startEncounter;
                }

                return Mathf.Clamp(
                    currentWorldStartEncounter,
                    1,
                    Mathf.Max(1, EncounterCount));
            }
        }
        public int CurrentWorldEncounterCount
        {
            get
            {
                if (TryResolveWorldSegment(
                        Mathf.Max(1, CurrentEncounter),
                        out MicroRunWorldSegment segment,
                        out _,
                        out _,
                        out _))
                {
                    return Mathf.Max(1, segment.EncounterCount);
                }

                int remaining = Mathf.Max(
                    1,
                    EncounterCount - CurrentWorldStartEncounter + 1);
                return currentWorldEncounterCount <= 0
                    ? remaining
                    : Mathf.Clamp(currentWorldEncounterCount, 1, remaining);
            }
        }
        public int CurrentWorldEncounter => Mathf.Clamp(
            CurrentEncounter - CurrentWorldStartEncounter + 1,
            0,
            CurrentWorldEncounterCount);
        public int CompletedCurrentWorldEncounterCount =>
            GetCompletedCurrentWorldEncounterCount();
        public bool IsCurrentWorldFinalEncounter =>
            Phase == MicroRunPhase.Encounter &&
            CurrentWorldEncounter > 0 &&
            CurrentWorldEncounter == CurrentWorldEncounterCount;
        public bool IsRunFinalEncounter =>
            Phase == MicroRunPhase.Encounter &&
            CurrentEncounter > 0 &&
            CurrentEncounter == EncounterCount;
        public bool IsCurrentWorldComplete =>
            CurrentWorldEncounterCount > 0 &&
            CompletedCurrentWorldEncounterCount >= CurrentWorldEncounterCount;
        public bool IsWorldCompletionBeat =>
            IsEncounterCompletionBeat &&
            IsWorldFinalEncounter(CurrentEncounter);
        public int WorldHandoffCount => Mathf.Max(0, _worldHandoffCount);
        public float EncounterCompletionBeatDuration =>
            Mathf.Max(0f, encounterCompletionBeatSeconds);
        public float IntermissionRemaining =>
            Mathf.Max(0f, _intermissionRemaining);
        public float PreparationRemaining =>
            Mathf.Max(0f, _preparationRemaining);
        public float RecoveryRemaining =>
            Mathf.Max(0f, _recoveryRemaining);
        public float EncounterCompletionBeatRemaining =>
            Mathf.Max(0f, _encounterCompletionBeatRemaining);
        public bool IsEncounterCompletionBeat =>
            _encounterCompletionBeatRemaining > 0f;
        public float Contamination01 => contaminationSystem != null
            ? contaminationSystem.Value01
            : 0f;
        public ContaminationState CurrentContaminationState => contaminationSystem != null
            ? contaminationSystem.State
            : ContaminationState.Clean;
        public bool IsContaminationFailed => contaminationSystem != null &&
            contaminationSystem.HasFailed;
        public float FailureBeatRemaining =>
            Mathf.Max(0f, _failureBeatRemaining);
        public float FailureBeatDuration =>
            Mathf.Max(0f, failureBeatSeconds);
        public bool IsFailureBeat => _failureBeatRemaining > 0f;
        public bool IsFailed => Phase == MicroRunPhase.Failed;
        public MicroRunPhase FailedFromPhase => _failedFromPhase;
        public float PhaseStartMass => Mathf.Max(0f, _phaseStartMass);
        public float PhaseTargetMass => Mathf.Max(0f, _phaseTargetMass);
        public float CurrentCombinedMass => GetCombinedMass();
        public float PhaseMassGain => Mathf.Max(0f, _phaseBestMassGain);
        public bool IsComplete => Phase == MicroRunPhase.Complete;
        public bool IsAwaitingEncounterArrival =>
            _awaitingEncounterArrival && Phase == MicroRunPhase.Encounter;
        public float EncounterActivationRadius =>
            Mathf.Max(0.1f, encounterActivationRadius);
        public Vector2 CurrentEncounterTargetPosition
        {
            get
            {
                return TryGetEncounterTargetPosition(
                    Mathf.Max(1, CurrentEncounter),
                    out Vector2 position)
                    ? position
                    : Vector2.zero;
            }
        }
        public int RunSeed => _runSeed;
        public string EncounterPlanSignature => _encounterPlan != null
            ? _encounterPlan.Signature
            : string.Empty;
        public int EncounterPlanCount => _encounterPlan != null
            ? _encounterPlan.Count
            : 0;
        public ArcHunterMovementMode CurrentMovementMode => arcHunter != null
            ? arcHunter.MovementMode
            : ArcHunterMovementMode.Chase;
        public float ElapsedSeconds
        {
            get
            {
                if (_runStartedAt < 0f)
                    return 0f;

                return IsComplete || IsFailed
                    ? _completedElapsedSeconds
                    : Mathf.Max(0f, Time.time - _runStartedAt);
            }
        }

        public void Configure(
            MicroBuildChoiceController builds,
            ResonanceNode node,
            ArcHunter hunter,
            SplitLinkCombat linkCombat)
        {
            buildController = builds;
            resonanceNode = node;
            arcHunter = hunter;
            combat = linkCombat;
        }

        public void ConfigureMassEconomy(NeutralDropletSpawner spawner)
        {
            neutralSpawner = spawner;
        }

        public void ConfigureRewards(MidRunRewardController rewards)
        {
            rewardController = rewards;
        }

        public void ConfigureMicroRunV2(
            DropletSplitSystem playerSplitSystem,
            float preparationGain = 2.0f,
            float preparationTimeout = 4.0f,
            float recoveryGain = 3.5f,
            float recoveryTimeout = 5.0f,
            float intermissionDuration = 1.0f)
        {
            splitSystem = playerSplitSystem;
            preparationMassGainTarget = Mathf.Max(0f, preparationGain);
            preparationMaxSeconds = Mathf.Max(0f, preparationTimeout);
            recoveryMassGainTarget = Mathf.Max(0f, recoveryGain);
            recoveryMaxSeconds = Mathf.Max(0f, recoveryTimeout);
            intermissionSeconds = Mathf.Max(0f, intermissionDuration);
        }

        public void ConfigureRecoveryPolish(
            float recoveryGain = 3.5f,
            float recoveryTimeout = 5.0f)
        {
            recoveryMassGainTarget = Mathf.Max(0f, recoveryGain);
            recoveryMaxSeconds = Mathf.Max(0f, recoveryTimeout);
        }

        public void ConfigureEncounterPresentation(
            float completionBeatSeconds = 0.80f)
        {
            encounterCompletionBeatSeconds = Mathf.Max(
                0f,
                completionBeatSeconds);
        }

        public void ConfigureContaminationFailure(
            ContaminationSystem source,
            float collapseBeatSeconds = 0.65f)
        {
            contaminationSystem = source;
            failureBeatSeconds = Mathf.Max(0f, collapseBeatSeconds);
        }

        public void ConfigureWorldHandoff(WetnessField field)
        {
            wetnessField = field;
        }

        /// <summary>
        /// Development-only fixed seed hook. Set a non-zero value before a
        /// run starts to reproduce the same six-slot encounter plan across
        /// play-session restarts. Zero restores normal time-based seeding.
        /// </summary>
        public void ConfigureDevelopmentFixedSeed(int seed)
        {
            developmentFixedSeedOverride = seed;
        }

        public void RegenerateEncounterPlanForDevelopment()
        {
            _encounterPlanGenerated = false;
            CaptureAuthoredEncounterConfiguration();
            GenerateEncounterPlanForNewRun();
        }

        public bool TryGetEncounterPlanEntry(
            int encounterIndex,
            out SeededEncounterPlanEntry entry)
        {
            if (_encounterPlan != null &&
                _encounterPlan.TryGetEntry(encounterIndex, out entry))
            {
                return true;
            }

            entry = default;
            return false;
        }

        public void ConfigureEncounterCandidatePools(
            SeededEncounterCandidatePool[] pools)
        {
            if (pools == null || pools.Length == 0)
                return;

            encounterCandidatePools = new SeededEncounterCandidatePool[
                pools.Length];
            for (int i = 0; i < pools.Length; i++)
            {
                SeededEncounterCandidatePool source = pools[i];
                encounterCandidatePools[i] = source == null
                    ? new SeededEncounterCandidatePool()
                    : new SeededEncounterCandidatePool(
                        source.CopyPositions());
            }

            _encounterPlanGenerated = false;
            CaptureAuthoredEncounterConfiguration();
        }

        public void ConfigureEncounterVariation(Vector2[] spawnPositions)
        {
            if (spawnPositions == null || spawnPositions.Length == 0)
                return;

            encounterSpawnPositions = (Vector2[])spawnPositions.Clone();
            _encounterPlanGenerated = false;

            if (GetConfiguredWorldSegmentCount() == 1 &&
                worldSegments[0] != null)
            {
                worldSegments[0].Configure(
                    worldSegments[0].WorldId,
                    encounterSpawnPositions,
                    worldSegments[0].CopyMovementModes());
            }

            CaptureAuthoredEncounterConfiguration();
        }

        public void ConfigureEncounterMovementModes(
            ArcHunterMovementMode[] movementModes)
        {
            if (movementModes == null || movementModes.Length == 0)
                return;

            encounterMovementModes =
                (ArcHunterMovementMode[])movementModes.Clone();
            _encounterPlanGenerated = false;

            if (GetConfiguredWorldSegmentCount() == 1 &&
                worldSegments[0] != null)
            {
                worldSegments[0].Configure(
                    worldSegments[0].WorldId,
                    worldSegments[0].CopyEncounterPositions(),
                    encounterMovementModes);
            }

            CaptureAuthoredEncounterConfiguration();
        }

        public void ConfigureSingleWorldSegment(string worldId)
        {
            if (encounterSpawnPositions == null ||
                encounterSpawnPositions.Length == 0)
            {
                return;
            }

            ConfigureWorldSegments(new[]
            {
                new MicroRunWorldSegment(
                    worldId,
                    encounterSpawnPositions,
                    encounterMovementModes)
            });
        }

        public void ConfigureWorldSegments(MicroRunWorldSegment[] segments)
        {
            if (segments == null || segments.Length == 0)
                return;

            int validCount = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] != null && segments[i].EncounterCount > 0)
                    validCount++;
            }

            if (validCount == 0)
                return;

            worldSegments = new MicroRunWorldSegment[validCount];
            int writeIndex = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                MicroRunWorldSegment segment = segments[i];
                if (segment == null || segment.EncounterCount <= 0)
                    continue;

                worldSegments[writeIndex++] = segment.Clone();
            }

            SyncLegacyEncounterArraysFromWorldSegments();
            currentWorldStartEncounter = 1;
            currentWorldEncounterCount = worldSegments[0].EncounterCount;
            _activeWorldSegmentIndex = 0;
            _worldHandoffCount = 0;
            _encounterPlanGenerated = false;
            CaptureAuthoredEncounterConfiguration();
        }

        public bool TryGetWorldSegmentCopy(
            int segmentIndex,
            out MicroRunWorldSegment segment)
        {
            segment = null;
            if (worldSegments == null ||
                segmentIndex < 0 ||
                segmentIndex >= worldSegments.Length ||
                worldSegments[segmentIndex] == null ||
                worldSegments[segmentIndex].EncounterCount <= 0)
            {
                return false;
            }

            segment = worldSegments[segmentIndex].Clone();
            return true;
        }

        public bool TryGetEncounterTargetPosition(
            int encounterIndex,
            out Vector2 position)
        {
            position = Vector2.zero;
            if (_encounterPlan != null &&
                _encounterPlan.TryGetEntry(
                    encounterIndex,
                    out SeededEncounterPlanEntry plannedEntry))
            {
                position = plannedEntry.TargetPosition;
                return true;
            }

            int clampedEncounter = Mathf.Clamp(
                encounterIndex,
                1,
                Mathf.Max(1, GetEncounterCount()));

            if (TryResolveWorldSegment(
                    clampedEncounter,
                    out MicroRunWorldSegment segment,
                    out _,
                    out _,
                    out int localEncounterIndex))
            {
                position = segment.GetEncounterPosition(localEncounterIndex);
                return true;
            }

            if (encounterSpawnPositions == null ||
                encounterSpawnPositions.Length == 0)
            {
                return false;
            }

            int index = Mathf.Clamp(
                clampedEncounter - 1,
                0,
                encounterSpawnPositions.Length - 1);
            position = encounterSpawnPositions[index];
            return true;
        }

        public bool TryGetNextCurrentWorldEncounterTargetPosition(
            out Vector2 position)
        {
            position = Vector2.zero;
            if (CurrentWorldEncounter <= 0 ||
                CurrentWorldEncounter >= CurrentWorldEncounterCount)
            {
                return false;
            }

            return TryGetEncounterTargetPosition(
                CurrentEncounter + 1,
                out position);
        }

        public bool TryGetNextEncounterTargetPosition(
            out Vector2 position)
        {
            position = Vector2.zero;
            if (CurrentEncounter <= 0 ||
                CurrentEncounter >= EncounterCount)
            {
                return false;
            }

            return TryGetEncounterTargetPosition(
                CurrentEncounter + 1,
                out position);
        }

        public void ConfigureWorldProgressScope(
            int firstEncounterIndex,
            int encounterCount)
        {
            currentWorldStartEncounter = Mathf.Max(1, firstEncounterIndex);
            currentWorldEncounterCount = Mathf.Max(0, encounterCount);
        }

        public void ConfigureWorldNavigation(
            float activationRadius = 3.75f)
        {
            encounterActivationRadius = Mathf.Max(
                0.1f,
                activationRadius);
        }

        public static float CalculatePhaseMassTarget(
            float startingMass,
            float requiredGain)
        {
            return Mathf.Max(0f, startingMass) + Mathf.Max(0f, requiredGain);
        }

        public static bool HasReachedPhaseMassTarget(
            float currentMass,
            float targetMass)
        {
            return Mathf.Max(0f, currentMass) + 0.0001f >=
                Mathf.Max(0f, targetMass);
        }

        public static float CalculateBestPhaseMassGain(
            float startingMass,
            float previousBestGain,
            float currentMass)
        {
            float currentGain = Mathf.Max(
                0f,
                Mathf.Max(0f, currentMass) - Mathf.Max(0f, startingMass));
            return Mathf.Max(Mathf.Max(0f, previousBestGain), currentGain);
        }

        public static bool HasReachedPhaseMassGainTarget(
            float bestGain,
            float requiredGain)
        {
            return Mathf.Max(0f, bestGain) + 0.0001f >=
                Mathf.Max(0f, requiredGain);
        }

        public static ArcHunterMovementMode GetMovementModeForEncounter(
            int encounterIndex)
        {
            switch (encounterIndex)
            {
                case 2:
                    return ArcHunterMovementMode.Strafe;
                case 3:
                    return ArcHunterMovementMode.WetRoutePressure;
                default:
                    return ArcHunterMovementMode.Chase;
            }
        }

        public static bool ShouldNeutralPopulationRefill(
            MicroRunPhase phase)
        {
            switch (phase)
            {
                case MicroRunPhase.Preparation:
                case MicroRunPhase.ArmResonance:
                case MicroRunPhase.Encounter:
                case MicroRunPhase.Recovery:
                case MicroRunPhase.Intermission:
                    return true;
                case MicroRunPhase.RewardChoice:
                    return false;
                default:
                    return false;
            }
        }

        private void Awake()
        {
            if (buildController == null)
                buildController = FindAnyObjectByType<MicroBuildChoiceController>();
            if (resonanceNode == null)
                resonanceNode = FindAnyObjectByType<ResonanceNode>();
            if (arcHunter == null)
                arcHunter = FindAnyObjectByType<ArcHunter>();
            if (combat == null)
                combat = FindAnyObjectByType<SplitLinkCombat>();
            if (neutralSpawner == null)
                neutralSpawner = FindAnyObjectByType<NeutralDropletSpawner>();
            if (rewardController == null)
                rewardController =
                    FindAnyObjectByType<MidRunRewardController>();
            if (splitSystem == null)
                splitSystem = FindAnyObjectByType<DropletSplitSystem>();
            if (contaminationSystem == null)
                contaminationSystem = FindAnyObjectByType<ContaminationSystem>();
            if (wetnessField == null)
                wetnessField = FindAnyObjectByType<WetnessField>();

            // Migrate the original 0.28s serialized value without requiring
            // users to recreate or manually edit the existing scene setup.
            if (encounterCompletionBeatSeconds < 0.50f)
                encounterCompletionBeatSeconds = 0.80f;

            CaptureAuthoredEncounterConfiguration();
            GenerateEncounterPlanForNewRun();
            _progress = new MicroRunProgress(GetEncounterCount());
            _encounterCompletionBeatRemaining = 0f;
            _pendingRewardEncounter = 0;
            _failureBeatRemaining = 0f;
            _failedFromPhase = MicroRunPhase.ChooseBuild;
            _activeWorldSegmentIndex = ResolveCurrentWorldSegmentIndex();
            _worldHandoffCount = 0;
            PrepareChooseBuildState();
            UpdateDiagnostics();
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlaySessionTimeScale()
        {
            // Enter Play Mode Options can keep static engine state when domain
            // reload is disabled. A reward decision may leave timeScale at
            // zero if Play Mode is stopped while gameplay is frozen, which
            // prevents physics and WASD movement on the next session.
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (_progress == null)
                return;

            _encounterCompletionBeatRemaining = Mathf.Max(
                0f,
                _encounterCompletionBeatRemaining -
                Time.unscaledDeltaTime);
            _failureBeatRemaining = Mathf.Max(
                0f,
                _failureBeatRemaining - Time.unscaledDeltaTime);

            if (Phase == MicroRunPhase.Failed)
            {
                UpdateDiagnostics();
                return;
            }

            if (EvaluateContaminationFailure())
            {
                UpdateDiagnostics();
                return;
            }

            switch (_progress.Phase)
            {
                case MicroRunPhase.ChooseBuild:
                    if (buildController != null &&
                        buildController.HasChoice &&
                        _progress.SelectBuild())
                    {
                        _runStartedAt = Time.time;
                        BeginPreparationPhase();
                    }
                    break;

                case MicroRunPhase.Preparation:
                    _preparationRemaining = Mathf.Max(
                        0f,
                        _preparationRemaining - Time.deltaTime);
                    TrackPhaseMassGain();

                    if ((HasReachedPhaseMassGainTarget(
                             _phaseBestMassGain,
                             _phaseTargetMass - _phaseStartMass) ||
                         _preparationRemaining <= 0f) &&
                        _progress.CompletePreparation())
                    {
                        BeginResonanceObjective();
                    }
                    break;

                case MicroRunPhase.ArmResonance:
                    if (resonanceNode != null &&
                        resonanceNode.IsCompleted &&
                        _progress.CompleteResonance())
                    {
                        BeginCurrentEncounter();
                    }
                    break;

                case MicroRunPhase.Encounter:
                    if (_awaitingEncounterArrival)
                    {
                        if (IsPlayerNearEncounterTarget())
                            ActivateCurrentEncounter();
                        break;
                    }

                    if (arcHunter != null &&
                        arcHunter.IsDefeated &&
                        _progress.DefeatEncounter())
                    {
                        combat?.SetEncounterActive(false);
                        _encounterCompletionBeatRemaining =
                            Mathf.Max(0f, encounterCompletionBeatSeconds);

                        if (_progress.Phase == MicroRunPhase.Complete)
                        {
                            ApplyNeutralPopulationPolicy();
                            _completedElapsedSeconds =
                                Mathf.Max(0f, Time.time - _runStartedAt);
                        }
                        else if (_progress.Phase ==
                                 MicroRunPhase.RewardChoice)
                        {
                            ApplyNeutralPopulationPolicy(true);
                            _pendingRewardEncounter = CurrentEncounter;
                        }
                    }
                    break;

                case MicroRunPhase.RewardChoice:
                    if (IsEncounterCompletionBeat)
                        break;

                    if (_pendingRewardEncounter > 0)
                    {
                        int rewardStep = _pendingRewardEncounter;
                        _pendingRewardEncounter = 0;

                        if (rewardController == null ||
                            !rewardController.BeginRewardChoice(rewardStep))
                        {
                            BeginRecoveryAfterReward();
                        }

                        break;
                    }

                    if (rewardController == null ||
                        rewardController.HasCompletedStep(CurrentEncounter))
                    {
                        BeginRecoveryAfterReward();
                    }
                    break;

                case MicroRunPhase.Recovery:
                    _recoveryRemaining = Mathf.Max(
                        0f,
                        _recoveryRemaining - Time.deltaTime);
                    TrackPhaseMassGain();

                    bool reachedRecoveryTarget = HasReachedPhaseMassGainTarget(
                        _phaseBestMassGain,
                        _phaseTargetMass - _phaseStartMass);
                    if ((reachedRecoveryTarget ||
                         _recoveryRemaining <= 0f) &&
                        _progress.CompleteRecovery())
                    {
                        if (reachedRecoveryTarget)
                            contaminationSystem?.ApplyRecoveryCleanse();
                        BeginIntermission();
                    }
                    break;

                case MicroRunPhase.Intermission:
                    _intermissionRemaining = Mathf.Max(
                        0f,
                        _intermissionRemaining - Time.deltaTime);

                    if (_intermissionRemaining <= 0f &&
                        _progress.FinishIntermission())
                    {
                        BeginCurrentEncounter();
                    }
                    break;
            }

            UpdateDiagnostics();
        }

        private void PrepareChooseBuildState()
        {
            combat?.SetEncounterActive(false);
            rewardController?.ResetRewardsForSession();
            ApplyNeutralPopulationPolicy();

            if (resonanceNode != null)
                resonanceNode.gameObject.SetActive(false);

            if (arcHunter != null)
                arcHunter.gameObject.SetActive(false);

            _intermissionRemaining = 0f;
            _preparationRemaining = 0f;
            _recoveryRemaining = 0f;
            _phaseStartMass = 0f;
            _phaseTargetMass = 0f;
            _completedElapsedSeconds = 0f;
            _encounterCompletionBeatRemaining = 0f;
            _pendingRewardEncounter = 0;
            _failureBeatRemaining = 0f;
            _failedFromPhase = MicroRunPhase.ChooseBuild;
            _awaitingEncounterArrival = false;
            _activeWorldSegmentIndex = 0;
            _worldHandoffCount = 0;
            contaminationSystem?.ResetForNewRun();
            GenerateEncounterPlanForNewRun();
        }

        private void BeginPreparationPhase()
        {
            combat?.SetEncounterActive(false);
            neutralSpawner?.BeginRun();
            ApplyNeutralPopulationPolicy(true);

            if (resonanceNode != null)
                resonanceNode.gameObject.SetActive(false);

            if (arcHunter != null)
                arcHunter.gameObject.SetActive(false);

            StartMassPhase(
                preparationMassGainTarget,
                preparationMaxSeconds,
                isPreparation: true);
        }

        private void BeginResonanceObjective()
        {
            _awaitingEncounterArrival = false;
            combat?.SetEncounterActive(false);
            ApplyNeutralPopulationPolicy();

            if (arcHunter != null)
                arcHunter.gameObject.SetActive(false);

            if (resonanceNode != null)
            {
                resonanceNode.gameObject.SetActive(true);
                resonanceNode.ResetNode();
            }

            _preparationRemaining = 0f;
            ClearMassPhaseTarget();
        }

        private void BeginRecoveryAfterReward()
        {
            if (_progress == null ||
                !_progress.CompleteRewardChoice())
            {
                return;
            }

            combat?.SetEncounterActive(false);
            _awaitingEncounterArrival = false;

            if (resonanceNode != null)
                resonanceNode.gameObject.SetActive(false);

            if (arcHunter != null)
                arcHunter.gameObject.SetActive(false);

            ApplyNeutralPopulationPolicy(true);
            StartMassPhase(
                recoveryMassGainTarget,
                recoveryMaxSeconds,
                isPreparation: false);
        }

        private void BeginIntermission()
        {
            _awaitingEncounterArrival = false;
            ClearMassPhaseTarget();
            ApplyNeutralPopulationPolicy(true);
            _intermissionRemaining = intermissionSeconds;
        }

        private void BeginCurrentEncounter()
        {
            _awaitingEncounterArrival = false;
            ApplyWorldHandoffIfNeeded();
            ClearMassPhaseTarget();
            ApplyNeutralPopulationPolicy();

            if (resonanceNode != null)
                resonanceNode.gameObject.SetActive(false);

            if (arcHunter == null)
                return;

            if (!TryGetEncounterTargetPosition(
                    CurrentEncounter,
                    out Vector2 encounterPosition))
            {
                return;
            }

            arcHunter.gameObject.SetActive(false);
            arcHunter.transform.position = encounterPosition;
            arcHunter.ResetHunter();
            arcHunter.ConfigureMovementMode(
                ResolveMovementModeForEncounter(CurrentEncounter));

            _awaitingEncounterArrival = true;
            if (IsPlayerNearEncounterTarget())
                ActivateCurrentEncounter();
            else
                combat?.SetEncounterActive(false);
            _intermissionRemaining = 0f;
        }

        private void ApplyWorldHandoffIfNeeded()
        {
            int segmentIndex = ResolveCurrentWorldSegmentIndex();
            if (_activeWorldSegmentIndex < 0)
            {
                _activeWorldSegmentIndex = segmentIndex;
                return;
            }

            if (segmentIndex == _activeWorldSegmentIndex)
                return;

            wetnessField?.Clear();
            _activeWorldSegmentIndex = segmentIndex;
            _worldHandoffCount++;
        }

        private void ActivateCurrentEncounter()
        {
            if (arcHunter == null || Phase != MicroRunPhase.Encounter)
                return;

            _awaitingEncounterArrival = false;
            arcHunter.gameObject.SetActive(true);
            combat?.SetEncounterActive(true);
        }

        private bool IsPlayerNearEncounterTarget()
        {
            if (splitSystem == null ||
                splitSystem.PrimaryTransform == null)
                return false;

            Vector2 focus = splitSystem.PrimaryTransform.position;
            if (splitSystem.IsSplit && splitSystem.MirrorTransform != null)
            {
                focus = Vector2.Lerp(
                    focus,
                    splitSystem.MirrorTransform.position,
                    0.5f);
            }

            return Vector2.Distance(
                       focus,
                       CurrentEncounterTargetPosition) <=
                EncounterActivationRadius;
        }

        private void StartMassPhase(
            float requiredGain,
            float maxSeconds,
            bool isPreparation)
        {
            _phaseStartMass = GetCombinedMass();
            _phaseTargetMass = CalculatePhaseMassTarget(
                _phaseStartMass,
                requiredGain);
            _phaseBestMassGain = 0f;

            if (isPreparation)
            {
                _preparationRemaining = Mathf.Max(0f, maxSeconds);
                _recoveryRemaining = 0f;
            }
            else
            {
                _recoveryRemaining = Mathf.Max(0f, maxSeconds);
                _preparationRemaining = 0f;
            }
        }

        private void ClearMassPhaseTarget()
        {
            _phaseStartMass = 0f;
            _phaseTargetMass = 0f;
            _phaseBestMassGain = 0f;
            _preparationRemaining = 0f;
            _recoveryRemaining = 0f;
        }

        private void TrackPhaseMassGain()
        {
            _phaseBestMassGain = CalculateBestPhaseMassGain(
                _phaseStartMass,
                _phaseBestMassGain,
                GetCombinedMass());
        }

        private float GetCombinedMass()
        {
            if (splitSystem == null || splitSystem.PrimaryController == null)
                return 0f;

            float mass = splitSystem.PrimaryController.CurrentMass;
            if (splitSystem.IsSplit && splitSystem.MirrorController != null)
                mass += splitSystem.MirrorController.CurrentMass;

            return Mathf.Max(0f, mass);
        }

        private bool EvaluateContaminationFailure()
        {
            if (!CanEvaluateContamination() ||
                contaminationSystem == null ||
                !contaminationSystem.HasFailed)
                return false;

            return EnterFailedState();
        }

        private bool CanEvaluateContamination()
        {
            switch (Phase)
            {
                case MicroRunPhase.Preparation:
                case MicroRunPhase.ArmResonance:
                case MicroRunPhase.Encounter:
                case MicroRunPhase.Recovery:
                case MicroRunPhase.Intermission:
                    return true;
                default:
                    return false;
            }
        }

        private bool EnterFailedState()
        {
            MicroRunPhase phaseBeforeFailure = Phase;
            if (_progress == null ||
                !_progress.FailRun())
            {
                return false;
            }

            _failedFromPhase = phaseBeforeFailure;
            _completedElapsedSeconds = _runStartedAt >= 0f
                ? Mathf.Max(0f, Time.time - _runStartedAt)
                : 0f;
            _encounterCompletionBeatRemaining = 0f;
            _pendingRewardEncounter = 0;
            _failureBeatRemaining = Mathf.Max(0f, failureBeatSeconds);
            _awaitingEncounterArrival = false;

            combat?.SetEncounterActive(false);
            neutralSpawner?.SetPopulationRefillActive(false);

            if (resonanceNode != null)
                resonanceNode.gameObject.SetActive(false);

            if (arcHunter != null)
                arcHunter.gameObject.SetActive(false);

            return true;
        }

        private ArcHunterMovementMode ResolveMovementModeForEncounter(
            int encounterIndex)
        {
            if (_encounterPlan != null &&
                _encounterPlan.TryGetEntry(
                    encounterIndex,
                    out SeededEncounterPlanEntry plannedEntry))
            {
                return plannedEntry.MovementMode;
            }

            if (TryResolveWorldSegment(
                    encounterIndex,
                    out MicroRunWorldSegment segment,
                    out _,
                    out _,
                    out int localEncounterIndex))
            {
                return segment.GetMovementMode(localEncounterIndex);
            }

            int index = encounterIndex - 1;
            if (encounterMovementModes != null &&
                index >= 0 &&
                index < encounterMovementModes.Length)
            {
                return encounterMovementModes[index];
            }

            return GetMovementModeForEncounter(encounterIndex);
        }

        private int GetCompletedCurrentWorldEncounterCount()
        {
            int total = CurrentWorldEncounterCount;
            int start = CurrentWorldStartEncounter;
            int end = start + total - 1;
            int current = CurrentEncounter;

            if (current < start)
                return 0;
            if (current > end || IsComplete)
                return total;

            int completedGlobalEncounter;
            MicroRunPhase effectivePhase = Phase == MicroRunPhase.Failed
                ? _failedFromPhase
                : Phase;
            switch (effectivePhase)
            {
                case MicroRunPhase.RewardChoice:
                case MicroRunPhase.Recovery:
                case MicroRunPhase.Intermission:
                    completedGlobalEncounter = current;
                    break;
                case MicroRunPhase.Encounter:
                    completedGlobalEncounter = current - 1;
                    break;
                default:
                    completedGlobalEncounter = start - 1;
                    break;
            }

            return Mathf.Clamp(
                completedGlobalEncounter - start + 1,
                0,
                total);
        }

        private bool IsWorldFinalEncounter(int encounterIndex)
        {
            if (TryResolveWorldSegment(
                    encounterIndex,
                    out MicroRunWorldSegment segment,
                    out _,
                    out _,
                    out int localEncounterIndex))
            {
                return localEncounterIndex == segment.EncounterCount - 1;
            }

            return encounterIndex > 0 && encounterIndex == EncounterCount;
        }

        private void ApplyNeutralPopulationPolicy(
            bool allowImmediateAttempt = false)
        {
            if (neutralSpawner == null)
                return;

            neutralSpawner.SetPopulationRefillActive(
                ShouldNeutralPopulationRefill(Phase),
                allowImmediateAttempt);
        }

        private void CaptureAuthoredEncounterConfiguration()
        {
            _authoredWorldSegments = CloneWorldSegments(worldSegments);
            _authoredEncounterSpawnPositions = encounterSpawnPositions != null
                ? (Vector2[])encounterSpawnPositions.Clone()
                : Array.Empty<Vector2>();
            _authoredMovementModes = encounterMovementModes != null
                ? (ArcHunterMovementMode[])encounterMovementModes.Clone()
                : Array.Empty<ArcHunterMovementMode>();
            _authoredCandidatePools = CloneCandidatePools(
                encounterCandidatePools);
        }

        private void GenerateEncounterPlanForNewRun()
        {
            if (_encounterPlanGenerated)
                return;

            if (_authoredWorldSegments == null)
                CaptureAuthoredEncounterConfiguration();

            _runSeed = ResolveRunSeed();
            SeededEncounterCandidatePool[] pools = _authoredCandidatePools;
            if (pools == null || pools.Length == 0)
                pools = BuildDefaultCandidatePools();

            Collider2D playableBounds = arcHunter != null
                ? arcHunter.PlayableBounds
                : null;
            _encounterPlan = SeededEncounterPlanBuilder.Build(
                _runSeed,
                _authoredWorldSegments,
                _authoredEncounterSpawnPositions,
                _authoredMovementModes,
                pools,
                playableBounds);
            _encounterPlanGenerated = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                "[Glassfall M28A] Run seed=" +
                _runSeed +
                " plan=" +
                _encounterPlan.Signature);
            SeededEncounterPlanEntry[] entries =
                _encounterPlan.CopyEntries();
            for (int i = 0; i < entries.Length; i++)
            {
                Debug.Log(
                    "[Glassfall M28A] E" +
                    entries[i].EncounterIndex +
                    " " +
                    entries[i].WorldId +
                    " mode=" +
                    entries[i].MovementMode +
                    " target=" +
                    entries[i].TargetPosition +
                    (entries[i].UsedFallback ? " fallback" : string.Empty));
                if (entries[i].UsedFallback)
                {
                    Debug.LogWarning(
                        "[Glassfall M28A] Spawn fallback for E" +
                        entries[i].EncounterIndex +
                        " at " +
                        entries[i].TargetPosition +
                        ". Authored candidate pool was invalid or empty.");
                }
            }
#endif
        }

        private int ResolveRunSeed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (developmentFixedSeedOverride != 0)
                return developmentFixedSeedOverride;
#endif

            long ticks = DateTime.UtcNow.Ticks;
            return unchecked((int)(ticks ^ (ticks >> 32) ^ Environment.TickCount));
        }

        private SeededEncounterCandidatePool[] BuildDefaultCandidatePools()
        {
            SeededEncounterCandidatePool[] pools =
                new SeededEncounterCandidatePool[
                    SeededEncounterPlanBuilder.RequiredSlotCount];
            for (int slot = 0; slot < pools.Length; slot++)
            {
                Vector2 authoredPosition =
                    GetAuthoredPositionForSlot(slot);
                pools[slot] =
                    SeededEncounterPlanBuilder.CreateDefaultCandidatePool(
                        slot,
                        authoredPosition);
            }

            return pools;
        }

        private Vector2 GetAuthoredPositionForSlot(int slot)
        {
            int cursor = 0;
            if (_authoredWorldSegments != null)
            {
                for (int i = 0; i < _authoredWorldSegments.Length; i++)
                {
                    MicroRunWorldSegment segment = _authoredWorldSegments[i];
                    if (segment == null || segment.EncounterCount <= 0)
                        continue;

                    if (slot < cursor + segment.EncounterCount)
                    {
                        return segment.GetEncounterPosition(slot - cursor);
                    }

                    cursor += segment.EncounterCount;
                }
            }

            if (_authoredEncounterSpawnPositions != null &&
                _authoredEncounterSpawnPositions.Length > 0)
            {
                return _authoredEncounterSpawnPositions[Mathf.Clamp(
                    slot,
                    0,
                    _authoredEncounterSpawnPositions.Length - 1)];
            }

            return Vector2.zero;
        }

        private static MicroRunWorldSegment[] CloneWorldSegments(
            MicroRunWorldSegment[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<MicroRunWorldSegment>();

            MicroRunWorldSegment[] clone =
                new MicroRunWorldSegment[source.Length];
            for (int i = 0; i < source.Length; i++)
                clone[i] = source[i] != null ? source[i].Clone() : null;
            return clone;
        }

        private static SeededEncounterCandidatePool[] CloneCandidatePools(
            SeededEncounterCandidatePool[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<SeededEncounterCandidatePool>();

            SeededEncounterCandidatePool[] clone =
                new SeededEncounterCandidatePool[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                clone[i] = source[i] == null
                    ? new SeededEncounterCandidatePool()
                    : new SeededEncounterCandidatePool(
                        source[i].CopyPositions());
            }

            return clone;
        }

        private int GetEncounterCount()
        {
            int segmentEncounterCount = 0;
            if (worldSegments != null)
            {
                for (int i = 0; i < worldSegments.Length; i++)
                {
                    if (worldSegments[i] != null)
                        segmentEncounterCount += worldSegments[i].EncounterCount;
                }
            }

            if (segmentEncounterCount > 0)
                return segmentEncounterCount;

            return encounterSpawnPositions == null ||
                   encounterSpawnPositions.Length == 0
                ? 1
                : encounterSpawnPositions.Length;
        }

        private int GetConfiguredWorldSegmentCount()
        {
            if (worldSegments == null || worldSegments.Length == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < worldSegments.Length; i++)
            {
                if (worldSegments[i] != null &&
                    worldSegments[i].EncounterCount > 0)
                {
                    count++;
                }
            }

            return count;
        }

        private int ResolveCurrentWorldSegmentIndex()
        {
            return TryResolveWorldSegment(
                Mathf.Max(1, CurrentEncounter),
                out _,
                out int segmentIndex,
                out _,
                out _)
                ? segmentIndex
                : 0;
        }

        private bool TryResolveWorldSegment(
            int encounterIndex,
            out MicroRunWorldSegment segment,
            out int segmentIndex,
            out int startEncounter,
            out int localEncounterIndex)
        {
            segment = null;
            segmentIndex = -1;
            startEncounter = 1;
            localEncounterIndex = 0;

            if (GetConfiguredWorldSegmentCount() == 0)
                return false;

            int target = Mathf.Clamp(
                encounterIndex,
                1,
                Mathf.Max(1, GetEncounterCount()));
            int cursor = 1;
            for (int i = 0; i < worldSegments.Length; i++)
            {
                MicroRunWorldSegment candidate = worldSegments[i];
                if (candidate == null || candidate.EncounterCount <= 0)
                    continue;

                int end = cursor + candidate.EncounterCount - 1;
                if (target >= cursor && target <= end)
                {
                    segment = candidate;
                    segmentIndex = i;
                    startEncounter = cursor;
                    localEncounterIndex = target - cursor;
                    return true;
                }

                cursor = end + 1;
            }

            return false;
        }

        private void SyncLegacyEncounterArraysFromWorldSegments()
        {
            int total = GetEncounterCount();
            if (total <= 0 || GetConfiguredWorldSegmentCount() == 0)
                return;

            Vector2[] positions = new Vector2[total];
            ArcHunterMovementMode[] modes =
                new ArcHunterMovementMode[total];
            int writeIndex = 0;

            for (int i = 0; i < worldSegments.Length; i++)
            {
                MicroRunWorldSegment segment = worldSegments[i];
                if (segment == null || segment.EncounterCount <= 0)
                    continue;

                Vector2[] segmentPositions = segment.CopyEncounterPositions();
                ArcHunterMovementMode[] segmentModes = segment.CopyMovementModes();
                for (int local = 0; local < segment.EncounterCount; local++)
                {
                    positions[writeIndex] = segmentPositions[local];
                    modes[writeIndex] = local < segmentModes.Length
                        ? segmentModes[local]
                        : ArcHunterMovementMode.Chase;
                    writeIndex++;
                }
            }

            encounterSpawnPositions = positions;
            encounterMovementModes = modes;
        }

        private void UpdateDiagnostics()
        {
            debugPhase = Phase;
            debugCurrentEncounter = CurrentEncounter;
            debugElapsedSeconds = ElapsedSeconds;
            debugIntermissionRemaining = IntermissionRemaining;
            debugPreparationRemaining = PreparationRemaining;
            debugRecoveryRemaining = RecoveryRemaining;
            debugPhaseStartMass = PhaseStartMass;
            debugPhaseTargetMass = PhaseTargetMass;
            debugPhaseBestMassGain = PhaseMassGain;
            debugContamination01 = Contamination01;
            debugContaminationState = CurrentContaminationState;
        }

        private void OnValidate()
        {
            preparationMassGainTarget = Mathf.Max(0f, preparationMassGainTarget);
            preparationMaxSeconds = Mathf.Max(0f, preparationMaxSeconds);
            recoveryMassGainTarget = Mathf.Max(0f, recoveryMassGainTarget);
            recoveryMaxSeconds = Mathf.Max(0f, recoveryMaxSeconds);
            intermissionSeconds = Mathf.Max(0f, intermissionSeconds);
            encounterCompletionBeatSeconds = Mathf.Max(
                0f,
                encounterCompletionBeatSeconds);
            failureBeatSeconds = Mathf.Max(0f, failureBeatSeconds);
            encounterActivationRadius = Mathf.Max(
                0.1f,
                encounterActivationRadius);
            currentWorldStartEncounter = Mathf.Max(
                1,
                currentWorldStartEncounter);
            currentWorldEncounterCount = Mathf.Max(
                0,
                currentWorldEncounterCount);

            if (encounterSpawnPositions == null ||
                encounterSpawnPositions.Length == 0)
            {
                encounterSpawnPositions =
                    new[] { new Vector2(-4.0f, 2.5f) };
            }
        }
    }
}
