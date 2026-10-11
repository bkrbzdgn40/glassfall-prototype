using Glassfall.Gameplay.Builds;
using Glassfall.Gameplay.Combat;
using Glassfall.Gameplay.Droplet;
using Glassfall.Gameplay.Rewards;
using Glassfall.Gameplay.Run;
using UnityEngine;

namespace Glassfall.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MicroRunOverlay : MonoBehaviour
    {
        [SerializeField] private MicroRunController runController;
        [SerializeField] private MicroBuildChoiceController buildController;
        [SerializeField] private DropletSplitSystem splitSystem;
        [SerializeField] private DropletMergeSystem mergeSystem;
        [SerializeField] private MidRunRewardController rewardController;

        private GUIStyle _objectiveTitle;
        private GUIStyle _objectiveBody;
        private GUIStyle _progressStyle;
        private GUIStyle _completeTitle;
        private GUIStyle _completeBody;
        private GUIStyle _massTitle;
        private GUIStyle _massBody;
        private GUIStyle _beatStyle;
        private GUIStyle _finalIntroStyle;
        private GUIStyle _failureTitle;
        private GUIStyle _failureBody;

        [Header("Encounter Presentation")]
        [Min(0.01f)]
        [SerializeField] private float completionBeatDisplaySeconds = 0.80f;

        [Min(0.01f)]
        [SerializeField] private float finalPressureIntroSeconds = 0.65f;

        [Min(0.01f)]
        [SerializeField] private float completePanelFadeSeconds = 0.18f;

        private MicroRunPhase _lastPhase;
        private bool _phaseInitialized;
        private bool _wasAwaitingEncounterArrival;
        private float _finalPressureIntroRemaining;
        private float _completePanelElapsed;
        private float _lastPresentationUpdateTime = -1f;

        public void Configure(
            MicroRunController run,
            MicroBuildChoiceController builds)
        {
            runController = run;
            buildController = builds;
        }

        public void ConfigureMass(
            DropletSplitSystem playerSplitSystem,
            DropletMergeSystem playerMergeSystem = null)
        {
            splitSystem = playerSplitSystem;
            mergeSystem = playerMergeSystem;
        }

        public void ConfigureRewards(MidRunRewardController rewards)
        {
            rewardController = rewards;
        }

        public void ConfigurePresentation(
            float completionBeatSeconds = 0.80f,
            float finalIntroSeconds = 0.65f,
            float completeFadeSeconds = 0.18f)
        {
            completionBeatDisplaySeconds = Mathf.Max(
                0.01f,
                completionBeatSeconds);
            finalPressureIntroSeconds = Mathf.Max(
                0.01f,
                finalIntroSeconds);
            completePanelFadeSeconds = Mathf.Max(
                0.01f,
                completeFadeSeconds);
        }

        private void Awake()
        {
            if (runController == null)
                runController = GetComponent<MicroRunController>();
            if (buildController == null)
                buildController = FindAnyObjectByType<MicroBuildChoiceController>();
            if (splitSystem == null)
                splitSystem = FindAnyObjectByType<DropletSplitSystem>();
            if (mergeSystem == null && splitSystem != null)
                mergeSystem = splitSystem.GetComponent<DropletMergeSystem>();
            if (rewardController == null)
                rewardController =
                    FindAnyObjectByType<MidRunRewardController>();

            // The run controller owns the completion beat duration. Keep this
            // serialized field only as a fallback for scenes missing the run.
            if (runController != null)
            {
                completionBeatDisplaySeconds = Mathf.Max(
                    0.01f,
                    runController.EncounterCompletionBeatDuration);
            }
            else if (completionBeatDisplaySeconds < 0.50f)
            {
                completionBeatDisplaySeconds = 0.80f;
            }
        }

        private void OnGUI()
        {
            if (runController == null)
                return;

            EnsureStyles();
            UpdatePresentationState();

            if (runController.Phase == MicroRunPhase.ChooseBuild)
                return;

            if (runController.IsFailureBeat)
            {
                DrawCollapseBeat();
                return;
            }

            if (runController.Phase == MicroRunPhase.Failed)
            {
                DrawFailedPanel();
                return;
            }

            if (runController.IsEncounterCompletionBeat)
            {
                DrawEncounterClearedBeat();
                return;
            }

            DrawMassStatus();

            if (runController.Phase == MicroRunPhase.Complete)
            {
                DrawCompletePanel();
                return;
            }

            DrawObjectivePanel();
        }

        private void UpdatePresentationState()
        {
            float now = Time.unscaledTime;
            float delta = _lastPresentationUpdateTime < 0f
                ? 0f
                : Mathf.Max(0f, now - _lastPresentationUpdateTime);
            _lastPresentationUpdateTime = now;

            MicroRunPhase phase = runController.Phase;
            bool awaitingEncounterArrival =
                phase == MicroRunPhase.Encounter &&
                runController.IsAwaitingEncounterArrival;
            if (!_phaseInitialized)
            {
                _lastPhase = phase;
                _wasAwaitingEncounterArrival = awaitingEncounterArrival;
                _phaseInitialized = true;
            }
            else
            {
                bool phaseChanged = phase != _lastPhase;
                bool finalEncounterActivated =
                    phase == MicroRunPhase.Encounter &&
                    runController.IsRunFinalEncounter &&
                    !awaitingEncounterArrival &&
                    (phaseChanged || _wasAwaitingEncounterArrival);
                if (finalEncounterActivated)
                {
                    _finalPressureIntroRemaining =
                        finalPressureIntroSeconds;
                }

                if (phaseChanged && phase == MicroRunPhase.Complete)
                    _completePanelElapsed = 0f;
                if (phaseChanged && phase == MicroRunPhase.Failed)
                    _completePanelElapsed = 0f;

                _lastPhase = phase;
                _wasAwaitingEncounterArrival = awaitingEncounterArrival;
            }

            _finalPressureIntroRemaining = Mathf.Max(
                0f,
                _finalPressureIntroRemaining - delta);
            _completePanelElapsed = Mathf.Min(
                completePanelFadeSeconds,
                _completePanelElapsed + delta);
        }

        private void DrawEncounterClearedBeat()
        {
            float remaining = runController.EncounterCompletionBeatRemaining;
            float fadeOutSeconds = Mathf.Min(
                0.25f,
                GetCompletionBeatDisplaySeconds() * 0.4f);
            float alpha = remaining > fadeOutSeconds
                ? 1f
                : Mathf.SmoothStep(
                    0f,
                    1f,
                    remaining / Mathf.Max(fadeOutSeconds, 0.01f));
            float width = Mathf.Min(540f, Screen.width - 48f);
            const float height = 112f;
            Rect box = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            Color previousColor = GUI.color;
            GUI.color = new Color(0.02f, 0.07f, 0.12f, 0.84f * alpha);
            GUI.Box(box, GUIContent.none);
            GUI.color = new Color(0.78f, 1f, 0.92f, alpha);
            bool worldCompleted = runController.IsWorldCompletionBeat;
            string completedWorld = FormatWorldName(
                runController.CurrentWorldId);
            string headline = worldCompleted
                ? completedWorld + " STABILIZED"
                : "ENCOUNTER CLEARED";
            string followup = runController.IsComplete
                ? "RUN COMPLETE"
                : worldCompleted && runController.HasNextWorldSegment
                    ? FormatWorldName(runController.NextWorldId) +
                      " SIGNAL ACQUIRED"
                    : "REWARD READY";

            GUI.Label(
                new Rect(box.x + 20f, box.y + 18f, box.width - 40f, 44f),
                headline,
                _beatStyle);
            GUI.color = new Color(0.80f, 0.90f, 1f, alpha);
            GUI.Label(
                new Rect(box.x + 20f, box.y + 64f, box.width - 40f, 24f),
                followup,
                _progressStyle);
            GUI.color = previousColor;
        }

        private void DrawCollapseBeat()
        {
            float alpha = Mathf.Clamp01(
                runController.FailureBeatRemaining /
                Mathf.Max(runController.FailureBeatDuration, 0.01f));
            Color previousColor = GUI.color;
            GUI.color = new Color(0.20f, 0.01f, 0.03f, 0.62f * alpha);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);

            float width = Mathf.Min(540f, Screen.width - 48f);
            const float height = 112f;
            Rect box = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            GUI.color = new Color(1f, 0.52f, 0.58f, alpha);
            GUI.Box(box, GUIContent.none);
            GUI.Label(
                new Rect(box.x + 20f, box.y + 18f, box.width - 40f, 44f),
                "COLLAPSED",
                _beatStyle);
            GUI.color = new Color(1f, 0.82f, 0.84f, alpha);
            GUI.Label(
                new Rect(box.x + 20f, box.y + 64f, box.width - 40f, 24f),
                "CONTAMINATED",
                _progressStyle);
            GUI.color = previousColor;
        }

        private void DrawFailedPanel()
        {
            float width = Mathf.Min(620f, Screen.width - 48f);
            const float height = 246f;
            Rect box = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            float panelAlpha = Mathf.Clamp01(
                _completePanelElapsed /
                Mathf.Max(completePanelFadeSeconds, 0.01f));
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, panelAlpha);
            GUI.Box(box, GUIContent.none);

            GUI.color = new Color(1f, 0.42f, 0.50f, panelAlpha);
            GUI.DrawTexture(
                new Rect(box.x, box.y, box.width, 6f),
                Texture2D.whiteTexture);
            GUI.Label(
                new Rect(box.x + 24f, box.y + 24f, box.width - 48f, 54f),
                "RUN FAILED",
                _failureTitle);

            string buildName = buildController != null
                ? GetBuildName(buildController.SelectedBuild)
                : "UNKNOWN";
            GUI.Label(
                new Rect(box.x + 34f, box.y + 96f, box.width - 68f, 100f),
                "CONTAMINATED\n" +
                $"BUILD: {buildName}\n" +
                $"TIME: {FormatTime(runController.ElapsedSeconds)}",
                _failureBody);
            GUI.color = previousColor;
        }


        private void DrawMassStatus()
        {
            if (splitSystem == null || splitSystem.PrimaryController == null)
                return;

            const float width = 310f;
            const float height = 112f;
            const float margin = 16f;
            Rect box = new Rect(margin, 108f, width, height);

            GUI.Box(box, GUIContent.none);

            float totalMass = splitSystem.PrimaryController.CurrentMass;
            if (splitSystem.IsSplit && splitSystem.MirrorController != null)
                totalMass += splitSystem.MirrorController.CurrentMass;

            GUI.Label(
                new Rect(box.x + 16f, box.y + 7f, box.width - 32f, 28f),
                $"MASS  {totalMass:0.0}",
                _massTitle);

            float mergedMultiplier = mergeSystem != null
                ? mergeSystem.MergedCollectionMultiplier
                : NeutralCollectionRules.DefaultMergedMultiplier;
            float splitMultiplier = mergeSystem != null
                ? mergeSystem.SplitCollectionMultiplier
                : NeutralCollectionRules.DefaultSplitMultiplier;

            string state = splitSystem.IsSplit
                ? $"SPLIT · {splitMultiplier * 100f:0}% COLLECTION · LINK ACTIVE"
                : $"MERGED · HARVEST/SHOVE · {mergedMultiplier * 100f:0}%";

            GUI.Label(
                new Rect(box.x + 16f, box.y + 34f, box.width - 32f, 24f),
                state,
                _massBody);

            GUI.Label(
                new Rect(box.x + 16f, box.y + 59f, box.width - 32f, 22f),
                GetContaminationSummary(),
                _massBody);

            GUI.Label(
                new Rect(box.x + 16f, box.y + 80f, box.width - 32f, 22f),
                GetRewardSummary(),
                _massBody);
        }

        private string GetRewardSummary()
        {
            if (rewardController == null)
                return "REWARDS · NONE";

            int completedCount = rewardController.CompletedRewardStepCount;
            if (completedCount <= 0)
                return "REWARDS · NONE";

            string first = GetRewardName(
                rewardController.GetRewardForStep(1));
            string second = GetRewardName(
                rewardController.GetRewardForStep(2));

            if (completedCount == 1 || second == null)
                return first != null ? $"REWARD · {first}" : "REWARDS · 1";
            if (completedCount == 2)
                return $"REWARDS · {first} + {second}";

            return $"REWARDS · {first} + {second} +{completedCount - 2}";
        }

        private string GetContaminationSummary()
        {
            switch (runController.CurrentContaminationState)
            {
                case ContaminationState.Tainted:
                    return "TAINTED · SEEK CLEAN WATER";
                case ContaminationState.Corrupted:
                    return "CORRUPTED · RECOVERY PURIFICATION";
                case ContaminationState.Critical:
                    return "CRITICAL · AVOID THE NEXT HIT";
                case ContaminationState.Contaminated:
                    return "CONTAMINATED · RESONANCE LOST";
                default:
                    return "RESONANCE · CLEAN";
            }
        }

        private void DrawObjectivePanel()
        {
            const float width = 390f;
            const float height = 132f;
            const float margin = 16f;

            Rect box = new Rect(
                Screen.width - width - margin,
                margin,
                width,
                height);

            GUI.Box(box, GUIContent.none);

            string title;
            string body;

            switch (runController.Phase)
            {
                case MicroRunPhase.Preparation:
                    title = "BUILD MASS";
                    body =
                        GetWindowStabilityText() + "\n" +
                        $"Harvest +{Mathf.Max(0f, runController.PhaseTargetMass - runController.PhaseStartMass):0.0} mass before resonance.\n" +
                        $"Gained {runController.PhaseMassGain:0.0} · " +
                        $"{runController.PreparationRemaining:0.0}s fallback";
                    break;

                case MicroRunPhase.ArmResonance:
                    title = "ARM THE GLASS";
                    body =
                        GetWindowStabilityText() + "\n" +
                        "Travel to the first anchor, then split and hold the link through the Resonance Node.";
                    break;

                case MicroRunPhase.Encounter:
                    bool worldFinalEncounter =
                        runController.IsCurrentWorldFinalEncounter;
                    bool runFinalEncounter = runController.IsRunFinalEncounter;
                    bool awaitingArrival =
                        runController.IsAwaitingEncounterArrival;
                    if (awaitingArrival)
                    {
                        title = runFinalEncounter
                            ? "REACH FINAL REGION"
                            : worldFinalEncounter
                                ? "REACH WINDOW CORE"
                            : $"REACH REGION {runController.CurrentWorldEncounter}/" +
                              $"{runController.CurrentWorldEncounterCount}";
                        body =
                            GetWindowStabilityText(
                                runController.CurrentWorldEncounter - 1) + "\n" +
                            "Follow the navigation marker to the next unstable region.";
                    }
                    else
                    {
                        if (runFinalEncounter)
                            DrawFinalPressureAccent();
                        title = runFinalEncounter
                            ? "FINAL PRESSURE"
                            : worldFinalEncounter
                                ? "WINDOW PRESSURE"
                            : $"HUNT {runController.CurrentWorldEncounter}/" +
                              $"{runController.CurrentWorldEncounterCount}";
                        body =
                            GetWindowStabilityText(
                                runController.CurrentWorldEncounter - 1) + "\n" +
                            GetEncounterSubtitle(
                                runController.CurrentMovementMode);
                    }
                    break;

                case MicroRunPhase.Recovery:
                    title = "RECOVER MASS";
                    body =
                        GetWindowStabilityText() + "\n" +
                        $"Rebuild +{Mathf.Max(0f, runController.PhaseTargetMass - runController.PhaseStartMass):0.0} mass and purify while you travel " +
                        (runController.IsCurrentWorldComplete &&
                         runController.HasNextWorldSegment
                            ? $"toward {FormatWorldName(runController.NextWorldId)}.\n"
                            : "to the next anchor.\n") +
                        $"Recovered {runController.PhaseMassGain:0.0} · " +
                        $"{runController.RecoveryRemaining:0.0}s fallback";
                    break;

                case MicroRunPhase.Intermission:
                    bool handingOff = runController.IsCurrentWorldComplete &&
                        runController.HasNextWorldSegment;
                    title = handingOff
                        ? "WINDOW STABILIZED"
                        : "RECOVERY COMPLETE";
                    body = GetWindowStabilityText() + "\n" +
                        (handingOff
                            ? $"Entering {FormatWorldName(runController.NextWorldId)} in " +
                              $"{runController.IntermissionRemaining:0.0}s"
                            : $"Next hunt in {runController.IntermissionRemaining:0.0}s");
                    break;

                default:
                    return;
            }

            GUI.Label(
                new Rect(box.x + 16f, box.y + 10f, box.width - 32f, 34f),
                title,
                _objectiveTitle);

            GUI.Label(
                new Rect(box.x + 16f, box.y + 43f, box.width - 32f, 54f),
                body,
                _objectiveBody);

            GUI.Label(
                new Rect(box.x + 16f, box.y + 104f, box.width - 32f, 20f),
                FormatTime(runController.ElapsedSeconds),
                _progressStyle);
        }

        private string GetWindowStabilityText(int completedOverride = -1)
        {
            int completed = completedOverride >= 0
                ? completedOverride
                : GetCompletedEncounterCount();
            int total = Mathf.Max(
                1,
                runController != null
                    ? runController.CurrentWorldEncounterCount
                    : 1);
            return $"WINDOW STABILITY {Mathf.Clamp(completed, 0, total)}/{total}";
        }

        private int GetCompletedEncounterCount()
        {
            return runController != null
                ? runController.CompletedCurrentWorldEncounterCount
                : 0;
        }

        private float GetCompletionBeatDisplaySeconds()
        {
            if (runController != null)
                return Mathf.Max(0.01f, runController.EncounterCompletionBeatDuration);

            return Mathf.Max(0.01f, completionBeatDisplaySeconds);
        }

        private static string GetEncounterSubtitle(
            ArcHunterMovementMode movementMode)
        {
            switch (movementMode)
            {
                case ArcHunterMovementMode.Strafe:
                    return "STRAFE HUNTER\nKeep rotating the link.";
                case ArcHunterMovementMode.WetRoutePressure:
                    return "WET-ROUTE HUNTER\nUse your wet routes.";
                default:
                    return "ARC HUNTER\nEliminate it with the central split link.";
            }
        }

        private void DrawCompletePanel()
        {
            float width = Mathf.Min(620f, Screen.width - 48f);
            const float height = 230f;
            Rect box = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            float panelAlpha = Mathf.Clamp01(
                _completePanelElapsed /
                Mathf.Max(completePanelFadeSeconds, 0.01f));
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, panelAlpha);
            GUI.Box(box, GUIContent.none);

            GUI.color = new Color(0.38f, 1f, 0.78f, panelAlpha);
            GUI.DrawTexture(
                new Rect(box.x, box.y, box.width, 6f),
                Texture2D.whiteTexture);

            GUI.Label(
                new Rect(box.x + 24f, box.y + 24f, box.width - 48f, 54f),
                "RUN COMPLETE",
                _completeTitle);

            string buildName = buildController != null
                ? GetBuildName(buildController.SelectedBuild)
                : "UNKNOWN";
            int completedRunEncounters = ResolveCompletionEncounterTotal(
                runController.EncounterCount,
                runController.CurrentWorldEncounterCount);

            GUI.Label(
                new Rect(box.x + 34f, box.y + 92f, box.width - 68f, 86f),
                $"{FormatWorldName(runController.CurrentWorldId)} STABILIZED\n" +
                $"{completedRunEncounters} / {completedRunEncounters} RESONANCE ANCHORS STABILIZED\n" +
                $"BUILD: {buildName}\n" +
                $"TIME: {FormatTime(runController.ElapsedSeconds)}",
                _completeBody);
            GUI.color = previousColor;
        }

        public static int ResolveCompletionEncounterTotal(
            int runEncounterCount,
            int currentWorldEncounterCount)
        {
            if (runEncounterCount > 0)
                return runEncounterCount;

            return Mathf.Max(1, currentWorldEncounterCount);
        }

        public static string FormatWorldName(string worldId)
        {
            return string.IsNullOrWhiteSpace(worldId)
                ? "WINDOW"
                : worldId.Trim().Replace('_', ' ');
        }

        private void DrawFinalPressureAccent()
        {
            float introAlpha = Mathf.Clamp01(
                _finalPressureIntroRemaining /
                Mathf.Max(finalPressureIntroSeconds, 0.01f));
            float pulse = 0.5f + 0.5f *
                Mathf.Sin(Time.unscaledTime * 8f);

            Color previousColor = GUI.color;
            GUI.color = new Color(
                0.30f,
                0.05f,
                0.14f,
                0.055f + pulse * 0.025f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);

            if (introAlpha > 0f)
            {
                GUI.color = new Color(1f, 0.72f, 0.48f, introAlpha);
                GUI.Label(
                    new Rect(
                        0f,
                        Screen.height * 0.18f,
                        Screen.width,
                        64f),
                    "FINAL PRESSURE",
                    _finalIntroStyle);
            }

            GUI.color = previousColor;
        }

        private void EnsureStyles()
        {
            if (_objectiveTitle != null)
                return;

            _objectiveTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _objectiveTitle.normal.textColor = Color.white;

            _objectiveBody = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            _objectiveBody.normal.textColor =
                new Color(0.92f, 0.95f, 1f, 1f);

            _progressStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            _progressStyle.normal.textColor =
                new Color(0.70f, 0.86f, 1f, 1f);

            _completeTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _completeTitle.normal.textColor =
                new Color(0.70f, 1f, 0.90f, 1f);

            _completeBody = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _completeBody.normal.textColor = Color.white;

            _massTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _massTitle.normal.textColor =
                new Color(0.80f, 0.95f, 1f, 1f);

            _massBody = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            _massBody.normal.textColor =
                new Color(0.78f, 0.86f, 0.94f, 1f);

            _beatStyle = new GUIStyle(_completeTitle)
            {
                fontSize = 27
            };
            _finalIntroStyle = new GUIStyle(_completeTitle)
            {
                fontSize = 32
            };
            _failureTitle = new GUIStyle(_completeTitle);
            _failureTitle.normal.textColor =
                new Color(1f, 0.62f, 0.68f, 1f);
            _failureBody = new GUIStyle(_completeBody)
            {
                fontSize = 19
            };
            _failureBody.normal.textColor =
                new Color(1f, 0.92f, 0.94f, 1f);
        }

        private void OnValidate()
        {
            completionBeatDisplaySeconds = Mathf.Max(
                0.01f,
                completionBeatDisplaySeconds);
            finalPressureIntroSeconds = Mathf.Max(
                0.01f,
                finalPressureIntroSeconds);
            completePanelFadeSeconds = Mathf.Max(
                0.01f,
                completePanelFadeSeconds);
        }

        private static string GetRewardName(MidRunReward reward)
        {
            switch (reward)
            {
                case MidRunReward.EfficientDivision:
                    return "EFFICIENT DIVISION";
                case MidRunReward.AbsorbentCore:
                    return "ABSORBENT CORE";
                case MidRunReward.StableLink:
                    return "STABLE LINK";
                case MidRunReward.ConductiveResidue:
                    return "CONDUCTIVE RESIDUE";
                case MidRunReward.GravitationalDrop:
                    return "GRAVITATIONAL DROP";
                case MidRunReward.DenseCore:
                    return "DENSE CORE";
                case MidRunReward.HighCohesion:
                    return "HIGH COHESION";
                case MidRunReward.ElasticMembrane:
                    return "ELASTIC MEMBRANE";
                case MidRunReward.SlowEvaporation:
                    return "SLOW EVAPORATION";
                default:
                    return null;
            }
        }

        private static string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int minutes = totalSeconds / 60;
            int remainingSeconds = totalSeconds % 60;
            return $"{minutes:00}:{remainingSeconds:00}";
        }

        private static string GetBuildName(MicroBuildChoice choice)
        {
            switch (choice)
            {
                case MicroBuildChoice.ConductiveFocus:
                    return "CONDUCTIVE FOCUS";
                case MicroBuildChoice.BroadArc:
                    return "BROAD ARC";
                case MicroBuildChoice.Capacitor:
                    return "CAPACITOR";
                default:
                    return "NONE";
            }
        }
    }
}
