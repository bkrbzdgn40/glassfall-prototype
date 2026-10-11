using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Glassfall.Tests
{
    public sealed class MicroRunTests
    {
        private const string ProgressTypeName =
            "Glassfall.Gameplay.Run.MicroRunProgress, Assembly-CSharp";
        private const string CombatTypeName =
            "Glassfall.Gameplay.Combat.SplitLinkCombat, Assembly-CSharp";
        private const string ControllerTypeName =
            "Glassfall.Gameplay.Run.MicroRunController, Assembly-CSharp";
        private const string WorldSegmentTypeName =
            "Glassfall.Gameplay.Run.MicroRunWorldSegment, Assembly-CSharp";
        private const string HunterTypeName =
            "Glassfall.Gameplay.Combat.ArcHunter, Assembly-CSharp";
        private const string MovementTypeName =
            "Glassfall.Gameplay.Combat.ArcHunterMovement, Assembly-CSharp";
        private const string MovementModeTypeName =
            "Glassfall.Gameplay.Combat.ArcHunterMovementMode, Assembly-CSharp";
        private const string ContaminationTypeName =
            "Glassfall.Gameplay.Run.ContaminationSystem, Assembly-CSharp";
        private const string FoggedBackgroundTypeName =
            "Glassfall.Presentation.FoggedWindowWorldBackground, Assembly-CSharp";
        private const string RunOverlayTypeName =
            "Glassfall.Presentation.MicroRunOverlay, Assembly-CSharp";
        private const string WetnessFieldTypeName =
            "Glassfall.Gameplay.Wetness.WetnessField, Assembly-CSharp";
        private const string WorldRouterTypeName =
            "Glassfall.Presentation.WorldPresentationRouter, Assembly-CSharp";

        [Test]
        public void RunProgressMovesThroughPreparationBeforeResonance()
        {
            object progress = CreateProgress(3);

            Assert.That(GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("ChooseBuild"));
            Assert.That((bool)Invoke(progress, "SelectBuild"), Is.True);
            Assert.That(GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("Preparation"));

            Assert.That((bool)Invoke(
                progress,
                "CompletePreparation"), Is.True);
            Assert.That(GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("ArmResonance"));

            Assert.That((bool)Invoke(progress, "CompleteResonance"), Is.True);
            Assert.That(GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("Encounter"));
            Assert.That((int)GetProperty(progress, "CurrentEncounter"),
                Is.EqualTo(1));
        }

        [Test]
        public void ThreeEncounterRunRequiresRecoveryAfterEachReward()
        {
            object progress = CreateProgress(3);
            Invoke(progress, "SelectBuild");
            Invoke(progress, "CompletePreparation");
            Invoke(progress, "CompleteResonance");

            Assert.That((bool)Invoke(progress, "DefeatEncounter"), Is.True);
            Assert.That(GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("RewardChoice"));

            Assert.That((bool)Invoke(
                progress,
                "CompleteRewardChoice"), Is.True);
            Assert.That(GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("Recovery"));
            Assert.That((bool)Invoke(
                progress,
                "FinishIntermission"), Is.False);

            Assert.That((bool)Invoke(
                progress,
                "CompleteRecovery"), Is.True);
            Assert.That(GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("Intermission"));
            Assert.That((bool)Invoke(progress, "FinishIntermission"), Is.True);
            Assert.That((int)GetProperty(progress, "CurrentEncounter"),
                Is.EqualTo(2));

            Assert.That((bool)Invoke(progress, "DefeatEncounter"), Is.True);
            Assert.That((bool)Invoke(
                progress,
                "CompleteRewardChoice"), Is.True);
            Assert.That((bool)Invoke(
                progress,
                "CompleteRecovery"), Is.True);
            Assert.That((bool)Invoke(progress, "FinishIntermission"), Is.True);
            Assert.That((int)GetProperty(progress, "CurrentEncounter"),
                Is.EqualTo(3));

            Assert.That((bool)Invoke(progress, "DefeatEncounter"), Is.True);
            Assert.That(GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("Complete"));
        }

        [Test]
        public void ContaminationFailureIsTerminalAndDoesNotSkipRewardChoice()
        {
            object progress = CreateProgress(3);

            Assert.That((bool)Invoke(progress, "FailRun"), Is.False);
            Invoke(progress, "SelectBuild");
            Invoke(progress, "CompletePreparation");
            Invoke(progress, "CompleteResonance");

            Assert.That((bool)Invoke(progress, "FailRun"), Is.True);
            Assert.That(GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("Failed"));
            Assert.That((bool)Invoke(progress, "FailRun"), Is.False);
            Assert.That((bool)Invoke(
                progress,
                "CompleteRewardChoice"), Is.False);
            Assert.That(GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("Failed"));
        }

        [Test]
        public void InvalidProgressCallsDoNotSkipObjectives()
        {
            object progress = CreateProgress(3);

            Assert.That((bool)Invoke(progress, "CompletePreparation"), Is.False);
            Assert.That((bool)Invoke(progress, "CompleteResonance"), Is.False);
            Assert.That((bool)Invoke(progress, "CompleteRecovery"), Is.False);
            Assert.That((bool)Invoke(progress, "DefeatEncounter"), Is.False);
            Assert.That(GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("ChooseBuild"));
            Assert.That((int)GetProperty(progress, "CurrentEncounter"),
                Is.EqualTo(0));
        }

        [Test]
        public void DisablingEncounterResetsCapacitorCharge()
        {
            GameObject gameObject = new GameObject("MicroRunCombatGateTest");
            try
            {
                Type combatType = Type.GetType(CombatTypeName, true);
                Component combat = gameObject.AddComponent(combatType);

                Invoke(combat, "ConfigureCapacitor");
                Invoke(combat, "AdvanceCapacitor", true, 4f);

                float charged = (float)GetProperty(
                    combat,
                    "CapacitorCharge01");
                Assert.That(charged, Is.GreaterThan(0.99f));

                Invoke(combat, "SetEncounterActive", false);

                Assert.That((bool)GetProperty(
                    combat,
                    "IsEncounterActive"), Is.False);
                Assert.That((float)GetProperty(
                    combat,
                    "CapacitorCharge01"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That((float)GetProperty(
                    combat,
                    "CapacitorDamageMultiplier"),
                    Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PhaseMassTargetAddsOnlyNonNegativeGain()
        {
            Type controllerType = Type.GetType(ControllerTypeName, true);

            Assert.That(
                (float)InvokeStatic(
                    controllerType,
                    "CalculatePhaseMassTarget",
                    10f,
                    2f),
                Is.EqualTo(12f).Within(0.0001f));

            Assert.That(
                (float)InvokeStatic(
                    controllerType,
                    "CalculatePhaseMassTarget",
                    10f,
                    -4f),
                Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void PhaseMassTargetAcceptsExactOrHigherMass()
        {
            Type controllerType = Type.GetType(ControllerTypeName, true);

            Assert.That(
                (bool)InvokeStatic(
                    controllerType,
                    "HasReachedPhaseMassTarget",
                    11.9f,
                    12f),
                Is.False);
            Assert.That(
                (bool)InvokeStatic(
                    controllerType,
                    "HasReachedPhaseMassTarget",
                    12f,
                    12f),
                Is.True);
            Assert.That(
                (bool)InvokeStatic(
                    controllerType,
                    "HasReachedPhaseMassTarget",
                    13f,
                    12f),
                Is.True);
        }

        [Test]
        public void ContaminationStatesProgressWithoutAHealthBar()
        {
            Type contaminationType = Type.GetType(ContaminationTypeName, true);
            Type stateType = Type.GetType(
                "Glassfall.Gameplay.Run.ContaminationState, Assembly-CSharp",
                true);

            Assert.That(
                InvokeStatic(
                    contaminationType,
                    "GetStateFor",
                    0.05f,
                    0.12f,
                    0.38f,
                    0.72f,
                    1f).ToString(),
                Is.EqualTo("Clean"));
            Assert.That(
                InvokeStatic(
                    contaminationType,
                    "GetStateFor",
                    0.45f,
                    0.12f,
                    0.38f,
                    0.72f,
                    1f).ToString(),
                Is.EqualTo("Corrupted"));
            Assert.That(
                InvokeStatic(
                    contaminationType,
                    "GetStateFor",
                    1f,
                    0.12f,
                    0.38f,
                    0.72f,
                    1f).ToString(),
                Is.EqualTo("Contaminated"));
            Assert.That(stateType, Is.Not.Null);
        }

        [Test]
        public void ContaminationHitAndCleanseAmountsAreBounded()
        {
            Type contaminationType = Type.GetType(ContaminationTypeName, true);

            Assert.That(
                (float)InvokeStatic(
                    contaminationType,
                    "CalculateAfterGain",
                    0.18f,
                    0.23f),
                Is.EqualTo(0.41f).Within(0.0001f));
            Assert.That(
                (float)InvokeStatic(
                    contaminationType,
                    "CalculateAfterCleanse",
                    0.18f,
                    0.24f),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                (float)InvokeStatic(
                    contaminationType,
                    "CalculateAfterGain",
                    0.95f,
                    0.23f),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ContaminationPersistsAcrossSplitMergeWithoutReset()
        {
            Type contaminationType = Type.GetType(ContaminationTypeName, true);
            GameObject gameObject = new GameObject("ContaminationPersistenceTest");
            try
            {
                Component contamination = gameObject.AddComponent(contaminationType);
                Invoke(contamination, "Configure", 0.18f, 0.23f, 0.04f, 0.24f,
                    0.12f, 0.38f, 0.72f, 1f);
                Invoke(contamination, "ApplyArcHunterHit", false);

                Assert.That(
                    (float)GetProperty(contamination, "Value01"),
                    Is.EqualTo(0.18f).Within(0.0001f));
                Assert.That(
                    (float)GetProperty(contamination, "Value01"),
                    Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SplitContaminationRequiresMultipleUncleansedHitsToFail()
        {
            Type contaminationType = Type.GetType(ContaminationTypeName, true);
            GameObject gameObject = new GameObject("ContaminationFailurePacingTest");
            try
            {
                Component contamination = gameObject.AddComponent(contaminationType);
                Invoke(contamination, "Configure", 0.18f, 0.23f, 0.04f, 0.24f,
                    0.12f, 0.38f, 0.72f, 1f);

                for (int index = 0; index < 4; index++)
                    Invoke(contamination, "ApplyArcHunterHit", true);

                Assert.That(
                    (float)GetProperty(contamination, "Value01"),
                    Is.EqualTo(0.92f).Within(0.0001f));
                Assert.That(
                    GetProperty(contamination, "State").ToString(),
                    Is.EqualTo("Critical"));
                Assert.That(
                    (bool)GetProperty(contamination, "HasFailed"),
                    Is.False);

                Invoke(contamination, "ApplyArcHunterHit", true);

                Assert.That(
                    (float)GetProperty(contamination, "Value01"),
                    Is.EqualTo(1f).Within(0.0001f));
                Assert.That(
                    GetProperty(contamination, "State").ToString(),
                    Is.EqualTo("Contaminated"));
                Assert.That(
                    (bool)GetProperty(contamination, "HasFailed"),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void NeutralAndRecoveryCleansePullContaminationBackFromCritical()
        {
            Type contaminationType = Type.GetType(ContaminationTypeName, true);
            GameObject gameObject = new GameObject("ContaminationCleanseTest");
            try
            {
                Component contamination = gameObject.AddComponent(contaminationType);
                Invoke(contamination, "Configure", 0.18f, 0.23f, 0.04f, 0.24f,
                    0.12f, 0.38f, 0.72f, 1f);

                for (int index = 0; index < 4; index++)
                    Invoke(contamination, "ApplyArcHunterHit", true);

                Invoke(contamination, "ApplyNeutralCleanse");
                Assert.That(
                    (float)GetProperty(contamination, "Value01"),
                    Is.EqualTo(0.88f).Within(0.0001f));
                Assert.That(
                    GetProperty(contamination, "State").ToString(),
                    Is.EqualTo("Critical"));

                Invoke(contamination, "ApplyRecoveryCleanse");
                Assert.That(
                    (float)GetProperty(contamination, "Value01"),
                    Is.EqualTo(0.64f).Within(0.0001f));
                Assert.That(
                    GetProperty(contamination, "State").ToString(),
                    Is.EqualTo("Corrupted"));
                Assert.That(
                    (bool)GetProperty(contamination, "HasFailed"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RecoveryPurificationGuaranteesAtLeastOneTierDrop()
        {
            Type contaminationType = Type.GetType(ContaminationTypeName, true);
            GameObject gameObject = new GameObject("ContaminationRecoveryTierTest");
            try
            {
                Component contamination = gameObject.AddComponent(contaminationType);
                Invoke(contamination, "Configure", 0.18f, 0.23f, 0.04f, 0.24f,
                    0.12f, 0.38f, 0.72f, 1f);

                for (int index = 0; index < 3; index++)
                    Invoke(contamination, "ApplyArcHunterHit", true);

                Assert.That(
                    (float)GetProperty(contamination, "Value01"),
                    Is.EqualTo(0.69f).Within(0.0001f));
                Assert.That(
                    GetProperty(contamination, "State").ToString(),
                    Is.EqualTo("Corrupted"));

                Invoke(contamination, "ApplyRecoveryCleanse");

                Assert.That(
                    (float)GetProperty(contamination, "Value01"),
                    Is.LessThan(0.38f));
                Assert.That(
                    GetProperty(contamination, "State").ToString(),
                    Is.EqualTo("Tainted"));
                Assert.That(
                    (bool)GetProperty(contamination, "HasFailed"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void NewRunResetClearsContaminationFailure()
        {
            Type contaminationType = Type.GetType(ContaminationTypeName, true);
            GameObject gameObject = new GameObject("ContaminationResetTest");
            try
            {
                Component contamination = gameObject.AddComponent(contaminationType);
                Invoke(contamination, "Configure", 0.18f, 0.23f, 0.04f, 0.24f,
                    0.12f, 0.38f, 0.72f, 1f);

                for (int index = 0; index < 5; index++)
                    Invoke(contamination, "ApplyArcHunterHit", true);

                Assert.That(
                    (bool)GetProperty(contamination, "HasFailed"),
                    Is.True);

                Invoke(contamination, "ResetForNewRun");

                Assert.That(
                    (float)GetProperty(contamination, "Value01"),
                    Is.EqualTo(0f).Within(0.0001f));
                Assert.That(
                    GetProperty(contamination, "State").ToString(),
                    Is.EqualTo("Clean"));
                Assert.That(
                    (bool)GetProperty(contamination, "HasFailed"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BestPhaseMassGainKeepsHighestProgressAfterMassDrops()
        {
            Type controllerType = Type.GetType(ControllerTypeName, true);

            float best = (float)InvokeStatic(
                controllerType,
                "CalculateBestPhaseMassGain",
                10f,
                2.5f,
                11f);

            Assert.That(best, Is.EqualTo(2.5f).Within(0.0001f));
        }

        [Test]
        public void BestPhaseMassGainTracksNewHigherMass()
        {
            Type controllerType = Type.GetType(ControllerTypeName, true);

            float best = (float)InvokeStatic(
                controllerType,
                "CalculateBestPhaseMassGain",
                10f,
                1f,
                13.5f);

            Assert.That(best, Is.EqualTo(3.5f).Within(0.0001f));
        }

        [Test]
        public void PhaseMassGainTargetUsesBestRecoveredAmount()
        {
            Type controllerType = Type.GetType(ControllerTypeName, true);

            Assert.That(
                (bool)InvokeStatic(
                    controllerType,
                    "HasReachedPhaseMassGainTarget",
                    3.49f,
                    3.5f),
                Is.False);
            Assert.That(
                (bool)InvokeStatic(
                    controllerType,
                    "HasReachedPhaseMassGainTarget",
                    3.5f,
                    3.5f),
                Is.True);
        }

        [Test]
        public void EncounterOneUsesChaseMode()
        {
            Assert.That(
                InvokeStatic(
                    Type.GetType(ControllerTypeName, true),
                    "GetMovementModeForEncounter",
                    1).ToString(),
                Is.EqualTo("Chase"));
        }

        [Test]
        public void EncounterTwoUsesStrafeMode()
        {
            Assert.That(
                InvokeStatic(
                    Type.GetType(ControllerTypeName, true),
                    "GetMovementModeForEncounter",
                    2).ToString(),
                Is.EqualTo("Strafe"));
        }

        [Test]
        public void EncounterThreeUsesWetRouteMode()
        {
            Assert.That(
                InvokeStatic(
                    Type.GetType(ControllerTypeName, true),
                    "GetMovementModeForEncounter",
                    3).ToString(),
                Is.EqualTo("WetRoutePressure"));
        }

        [Test]
        public void NeutralPopulationRefillRunsAcrossActiveRunPhases()
        {
            Type controllerType = Type.GetType(ControllerTypeName, true);
            Type phaseType = Type.GetType(
                "Glassfall.Gameplay.Run.MicroRunPhase, Assembly-CSharp",
                true);

            foreach (string phaseName in new[]
            {
                "Preparation",
                "ArmResonance",
                "Encounter",
                "Recovery",
                "Intermission"
            })
            {
                object phase = Enum.Parse(phaseType, phaseName);
                Assert.That(
                    (bool)InvokeStatic(
                        controllerType,
                        "ShouldNeutralPopulationRefill",
                        phase),
                    Is.True,
                    phaseName);
            }
        }

        [Test]
        public void NeutralPopulationRefillPausesDuringRewardChoice()
        {
            Type controllerType = Type.GetType(ControllerTypeName, true);
            Type phaseType = Type.GetType(
                "Glassfall.Gameplay.Run.MicroRunPhase, Assembly-CSharp",
                true);

            object rewardChoice = Enum.Parse(phaseType, "RewardChoice");
            Assert.That(
                (bool)InvokeStatic(
                    controllerType,
                    "ShouldNeutralPopulationRefill",
                    rewardChoice),
                Is.False);
        }

        [Test]
        public void NeutralPopulationRefillStopsOutsideActiveRun()
        {
            Type controllerType = Type.GetType(ControllerTypeName, true);
            Type phaseType = Type.GetType(
                "Glassfall.Gameplay.Run.MicroRunPhase, Assembly-CSharp",
                true);

            foreach (string phaseName in new[]
            {
                "ChooseBuild",
                "Complete"
            })
            {
                object phase = Enum.Parse(phaseType, phaseName);
                Assert.That(
                    (bool)InvokeStatic(
                        controllerType,
                        "ShouldNeutralPopulationRefill",
                        phase),
                    Is.False,
                    phaseName);
            }
        }

        [Test]
        public void RewardChoiceDoesNotEnterFailedState()
        {
            object progress = CreateProgress(3);
            Invoke(progress, "SelectBuild");
            Invoke(progress, "CompletePreparation");
            Invoke(progress, "CompleteResonance");
            Assert.That((bool)Invoke(progress, "DefeatEncounter"), Is.True);
            Assert.That(
                GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("RewardChoice"));

            Assert.That((bool)Invoke(progress, "FailRun"), Is.False);
            Assert.That(
                GetProperty(progress, "Phase").ToString(),
                Is.EqualTo("RewardChoice"));
        }

        [Test]
        public void ControllerReportsProgressWithinConfiguredWorldSegment()
        {
            GameObject gameObject = new GameObject("WorldProgressScopeTest");
            try
            {
                Type controllerType = Type.GetType(ControllerTypeName, true);
                Component controller = gameObject.AddComponent(controllerType);
                object progress = CreateProgress(6);

                Invoke(progress, "SelectBuild");
                Invoke(progress, "CompletePreparation");
                Invoke(progress, "CompleteResonance");
                for (int encounter = 1; encounter <= 3; encounter++)
                {
                    Assert.That(
                        (bool)Invoke(progress, "DefeatEncounter"),
                        Is.True);
                    Assert.That(
                        (bool)Invoke(progress, "CompleteRewardChoice"),
                        Is.True);
                    Assert.That(
                        (bool)Invoke(progress, "CompleteRecovery"),
                        Is.True);
                    Assert.That(
                        (bool)Invoke(progress, "FinishIntermission"),
                        Is.True);
                }

                FieldInfo progressField = controllerType.GetField(
                    "_progress",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(progressField, Is.Not.Null);
                progressField.SetValue(controller, progress);

                Invoke(controller, "ConfigureWorldProgressScope", 4, 3);

                Assert.That(
                    (int)GetProperty(controller, "CurrentWorldEncounter"),
                    Is.EqualTo(1));
                Assert.That(
                    (int)GetProperty(controller, "CurrentWorldEncounterCount"),
                    Is.EqualTo(3));
                Assert.That(
                    (int)GetProperty(
                        controller,
                        "CompletedCurrentWorldEncounterCount"),
                    Is.EqualTo(0));
                Assert.That(
                    (bool)GetProperty(controller, "IsCurrentWorldFinalEncounter"),
                    Is.False);

                Assert.That(
                    (bool)Invoke(progress, "DefeatEncounter"),
                    Is.True);
                Assert.That(
                    (int)GetProperty(
                        controller,
                        "CompletedCurrentWorldEncounterCount"),
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SingleWorldSegmentCapturesLegacyRouteWithoutChangingCount()
        {
            GameObject gameObject = new GameObject("SingleWorldSegmentTest");
            try
            {
                Type controllerType = Type.GetType(ControllerTypeName, true);
                Component controller = gameObject.AddComponent(controllerType);

                Invoke(controller, "ConfigureSingleWorldSegment", "FOGGED_WINDOW");

                Assert.That(
                    (int)GetProperty(controller, "WorldSegmentCount"),
                    Is.EqualTo(1));
                Assert.That(
                    (int)GetProperty(controller, "EncounterCount"),
                    Is.EqualTo(3));
                Assert.That(
                    GetProperty(controller, "CurrentWorldId").ToString(),
                    Is.EqualTo("FOGGED_WINDOW"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void WorldSegmentsMapGlobalEncounterToLocalWorldProgress()
        {
            GameObject gameObject = new GameObject("MultiWorldSegmentTest");
            try
            {
                Type controllerType = Type.GetType(ControllerTypeName, true);
                Type segmentType = Type.GetType(WorldSegmentTypeName, true);
                Type movementModeType = Type.GetType(
                    MovementModeTypeName,
                    true);
                Component controller = gameObject.AddComponent(controllerType);

                object worldOne = CreateWorldSegment(
                    segmentType,
                    movementModeType,
                    "FOGGED_WINDOW",
                    new[]
                    {
                        new Vector2(-11f, -5f),
                        new Vector2(0f, 7f),
                        new Vector2(13f, 2f)
                    },
                    new[] { "Chase", "Strafe", "WetRoutePressure" });
                object worldTwo = CreateWorldSegment(
                    segmentType,
                    movementModeType,
                    "NEON_TEST",
                    new[]
                    {
                        new Vector2(-6f, 4f),
                        new Vector2(8f, -3f)
                    },
                    new[] { "Strafe", "Chase" });

                Array segments = Array.CreateInstance(segmentType, 2);
                segments.SetValue(worldOne, 0);
                segments.SetValue(worldTwo, 1);
                Invoke(controller, "ConfigureWorldSegments", segments);

                object progress = CreateProgress(5);
                Invoke(progress, "SelectBuild");
                Invoke(progress, "CompletePreparation");
                Invoke(progress, "CompleteResonance");
                for (int encounter = 1; encounter <= 3; encounter++)
                {
                    Assert.That(
                        (bool)Invoke(progress, "DefeatEncounter"),
                        Is.True);
                    Assert.That(
                        (bool)Invoke(progress, "CompleteRewardChoice"),
                        Is.True);
                    Assert.That(
                        (bool)Invoke(progress, "CompleteRecovery"),
                        Is.True);
                    Assert.That(
                        (bool)Invoke(progress, "FinishIntermission"),
                        Is.True);
                }

                FieldInfo progressField = controllerType.GetField(
                    "_progress",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(progressField, Is.Not.Null);
                progressField.SetValue(controller, progress);

                Assert.That(
                    (int)GetProperty(controller, "EncounterCount"),
                    Is.EqualTo(5));
                Assert.That(
                    (int)GetProperty(controller, "WorldSegmentCount"),
                    Is.EqualTo(2));
                Assert.That(
                    (int)GetProperty(controller, "CurrentWorldSegmentIndex"),
                    Is.EqualTo(1));
                Assert.That(
                    GetProperty(controller, "CurrentWorldId").ToString(),
                    Is.EqualTo("NEON_TEST"));
                Assert.That(
                    (int)GetProperty(controller, "CurrentWorldStartEncounter"),
                    Is.EqualTo(4));
                Assert.That(
                    (int)GetProperty(controller, "CurrentWorldEncounterCount"),
                    Is.EqualTo(2));
                Assert.That(
                    (int)GetProperty(controller, "CurrentWorldEncounter"),
                    Is.EqualTo(1));
                Assert.That(
                    (Vector2)GetProperty(
                        controller,
                        "CurrentEncounterTargetPosition"),
                    Is.EqualTo(new Vector2(-6f, 4f)));

                object[] nextTargetArguments = { Vector2.zero };
                MethodInfo nextTargetMethod = controllerType.GetMethod(
                    "TryGetNextCurrentWorldEncounterTargetPosition",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(nextTargetMethod, Is.Not.Null);
                Assert.That(
                    (bool)nextTargetMethod.Invoke(controller, nextTargetArguments),
                    Is.True);
                Assert.That(
                    (Vector2)nextTargetArguments[0],
                    Is.EqualTo(new Vector2(8f, -3f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void M26WorldSequenceSeparatesWorldCompletionFromRunCompletion()
        {
            GameObject gameObject = new GameObject("M26WorldSequenceTest");
            try
            {
                Type controllerType = Type.GetType(ControllerTypeName, true);
                Type segmentType = Type.GetType(WorldSegmentTypeName, true);
                Type movementModeType = Type.GetType(
                    MovementModeTypeName,
                    true);
                Component controller = gameObject.AddComponent(controllerType);
                ConfigureM26Segments(
                    controller,
                    segmentType,
                    movementModeType);

                object progress = CreateProgress(6);
                Invoke(progress, "SelectBuild");
                Invoke(progress, "CompletePreparation");
                Invoke(progress, "CompleteResonance");
                SetPrivateField(controller, "_progress", progress);

                Assert.That((int)GetProperty(controller, "EncounterCount"),
                    Is.EqualTo(6));
                Assert.That((int)GetProperty(controller, "WorldSegmentCount"),
                    Is.EqualTo(2));
                Assert.That((bool)GetProperty(controller, "HasNextWorldSegment"),
                    Is.True);
                Assert.That(GetProperty(controller, "NextWorldId").ToString(),
                    Is.EqualTo("NEON_WINDOW"));

                CompleteEncounterCycle(progress);
                CompleteEncounterCycle(progress);
                Assert.That((int)GetProperty(controller, "CurrentWorldEncounter"),
                    Is.EqualTo(3));
                Assert.That((bool)GetProperty(controller, "IsRunFinalEncounter"),
                    Is.False);

                SetPrivateField(
                    controller,
                    "_encounterCompletionBeatRemaining",
                    0.8f);
                Assert.That((bool)Invoke(progress, "DefeatEncounter"), Is.True);
                Assert.That(GetProperty(progress, "Phase").ToString(),
                    Is.EqualTo("RewardChoice"));
                Assert.That((bool)GetProperty(controller, "IsWorldCompletionBeat"),
                    Is.True);

                Assert.That((bool)Invoke(progress, "CompleteRewardChoice"), Is.True);
                Assert.That((bool)Invoke(progress, "CompleteRecovery"), Is.True);
                Assert.That((bool)Invoke(progress, "FinishIntermission"), Is.True);
                Assert.That(GetProperty(controller, "CurrentWorldId").ToString(),
                    Is.EqualTo("NEON_WINDOW"));
                Assert.That((int)GetProperty(controller, "CurrentWorldEncounter"),
                    Is.EqualTo(1));

                CompleteEncounterCycle(progress);
                CompleteEncounterCycle(progress);
                Assert.That((bool)GetProperty(controller, "IsRunFinalEncounter"),
                    Is.True);
                SetPrivateField(
                    controller,
                    "_encounterCompletionBeatRemaining",
                    0.8f);
                Assert.That((bool)Invoke(progress, "DefeatEncounter"), Is.True);
                Assert.That(GetProperty(progress, "Phase").ToString(),
                    Is.EqualTo("Complete"));
                Assert.That((bool)GetProperty(controller, "IsComplete"), Is.True);
                Assert.That((bool)GetProperty(controller, "IsWorldCompletionBeat"),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void WorldHandoffClearsWetnessButPreservesContamination()
        {
            GameObject gameObject = new GameObject("M26WorldHandoffTest");
            try
            {
                Type wetnessType = Type.GetType(WetnessFieldTypeName, true);
                Type contaminationType = Type.GetType(
                    ContaminationTypeName,
                    true);
                Type controllerType = Type.GetType(ControllerTypeName, true);
                Type segmentType = Type.GetType(WorldSegmentTypeName, true);
                Type movementModeType = Type.GetType(
                    MovementModeTypeName,
                    true);

                Component wetness = gameObject.AddComponent(wetnessType);
                Component contamination = gameObject.AddComponent(contaminationType);
                Component controller = gameObject.AddComponent(controllerType);
                InvokeNonPublic(wetness, "Initialize");
                ConfigureM26Segments(
                    controller,
                    segmentType,
                    movementModeType);
                Invoke(controller, "ConfigureWorldHandoff", wetness);

                Invoke(contamination, "Configure");
                float contaminationBefore = (float)Invoke(
                    contamination,
                    "ApplyArcHunterHit",
                    false);
                Invoke(wetness, "DepositWorld", Vector2.zero, 1f, 1f);
                Assert.That((float)Invoke(wetness, "SampleWorld", Vector2.zero),
                    Is.GreaterThan(0f));

                object progress = CreateProgress(6);
                Invoke(progress, "SelectBuild");
                Invoke(progress, "CompletePreparation");
                Invoke(progress, "CompleteResonance");
                CompleteEncounterCycle(progress);
                CompleteEncounterCycle(progress);
                CompleteEncounterCycle(progress);
                SetPrivateField(controller, "_progress", progress);
                SetPrivateField(controller, "_activeWorldSegmentIndex", 0);

                InvokeNonPublic(controller, "ApplyWorldHandoffIfNeeded");

                Assert.That((float)Invoke(wetness, "SampleWorld", Vector2.zero),
                    Is.EqualTo(0f));
                Assert.That((float)GetProperty(contamination, "Value01"),
                    Is.EqualTo(contaminationBefore));
                Assert.That((int)GetProperty(controller, "WorldHandoffCount"),
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PresentationRouterActivatesOnlyTheCurrentWorld()
        {
            GameObject gameObject = new GameObject("M26WorldRouterTest");
            GameObject foggedRoot = new GameObject("FoggedWindowWorldV1");
            GameObject neonRoot = new GameObject("NeonWindowWorldV1");
            try
            {
                Type controllerType = Type.GetType(ControllerTypeName, true);
                Type segmentType = Type.GetType(WorldSegmentTypeName, true);
                Type movementModeType = Type.GetType(
                    MovementModeTypeName,
                    true);
                Type routerType = Type.GetType(WorldRouterTypeName, true);
                Component controller = gameObject.AddComponent(controllerType);
                ConfigureM26Segments(
                    controller,
                    segmentType,
                    movementModeType);

                object progress = CreateProgress(6);
                Invoke(progress, "SelectBuild");
                Invoke(progress, "CompletePreparation");
                Invoke(progress, "CompleteResonance");
                SetPrivateField(controller, "_progress", progress);

                Component router = gameObject.AddComponent(routerType);
                Invoke(
                    router,
                    "Configure",
                    controller,
                    foggedRoot,
                    neonRoot,
                    0,
                    1);
                Assert.That(foggedRoot.activeSelf, Is.True);
                Assert.That(neonRoot.activeSelf, Is.False);

                CompleteEncounterCycle(progress);
                CompleteEncounterCycle(progress);
                CompleteEncounterCycle(progress);
                InvokeNonPublic(router, "Update");

                Assert.That(foggedRoot.activeSelf, Is.False);
                Assert.That(neonRoot.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(foggedRoot);
                UnityEngine.Object.DestroyImmediate(neonRoot);
            }
        }

        [Test]
        public void FoggedBackgroundStopsFollowingProgressAfterWorldOne()
        {
            GameObject gameObject = new GameObject("WorldBoundBackgroundTest");
            try
            {
                Type controllerType = Type.GetType(ControllerTypeName, true);
                Type segmentType = Type.GetType(WorldSegmentTypeName, true);
                Type movementModeType = Type.GetType(
                    MovementModeTypeName,
                    true);
                Type backgroundType = Type.GetType(
                    FoggedBackgroundTypeName,
                    true);
                Component controller = gameObject.AddComponent(controllerType);

                object worldOne = CreateWorldSegment(
                    segmentType,
                    movementModeType,
                    "FOGGED_WINDOW",
                    new[]
                    {
                        new Vector2(-2f, 0f),
                        new Vector2(0f, 0f),
                        new Vector2(2f, 0f)
                    },
                    new[] { "Chase", "Strafe", "WetRoutePressure" });
                object worldTwo = CreateWorldSegment(
                    segmentType,
                    movementModeType,
                    "NEON_TEST",
                    new[] { new Vector2(5f, 0f) },
                    new[] { "Chase" });
                Array segments = Array.CreateInstance(segmentType, 2);
                segments.SetValue(worldOne, 0);
                segments.SetValue(worldTwo, 1);
                Invoke(controller, "ConfigureWorldSegments", segments);

                object progress = CreateProgress(4);
                Invoke(progress, "SelectBuild");
                Invoke(progress, "CompletePreparation");
                Invoke(progress, "CompleteResonance");
                for (int encounter = 1; encounter <= 3; encounter++)
                {
                    Assert.That(
                        (bool)Invoke(progress, "DefeatEncounter"),
                        Is.True);
                    Assert.That(
                        (bool)Invoke(progress, "CompleteRewardChoice"),
                        Is.True);
                    Assert.That(
                        (bool)Invoke(progress, "CompleteRecovery"),
                        Is.True);
                    Assert.That(
                        (bool)Invoke(progress, "FinishIntermission"),
                        Is.True);
                }

                FieldInfo progressField = controllerType.GetField(
                    "_progress",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(progressField, Is.Not.Null);
                progressField.SetValue(controller, progress);
                Assert.That(
                    (int)GetProperty(controller, "CurrentWorldSegmentIndex"),
                    Is.EqualTo(1));

                Component background = gameObject.AddComponent(backgroundType);
                SetPrivateField(background, "progressSource", controller);
                Invoke(background, "ConfigureWorldProgressBinding", 0);

                MethodInfo resolveBlurPhase = backgroundType.GetMethod(
                    "ResolveBlurPhase",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(resolveBlurPhase, Is.Not.Null);
                Assert.That(
                    (int)resolveBlurPhase.Invoke(background, null),
                    Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void FoggedBackgroundFallsBackWhenPrepareOrFirstFrameTimesOut()
        {
            Type backgroundType = Type.GetType(
                FoggedBackgroundTypeName,
                true);
            MethodInfo method = backgroundType.GetMethod(
                "ShouldActivateVideoPrepareTimeoutFallback",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            Assert.That(
                (bool)method.Invoke(
                    null,
                    new object[] { false, false, 5f, 5f }),
                Is.True,
                "An unprepared video must fall back at the timeout.");
            Assert.That(
                (bool)method.Invoke(
                    null,
                    new object[] { true, false, 5f, 5f }),
                Is.True,
                "A prepared video without a usable first frame must also fall back.");
            Assert.That(
                (bool)method.Invoke(
                    null,
                    new object[] { true, true, 8f, 5f }),
                Is.False,
                "A ready video surface must keep the successful path.");
            Assert.That(
                (bool)method.Invoke(
                    null,
                    new object[] { false, false, 4.9f, 5f }),
                Is.False);
        }

        [Test]
        public void CompletionSummaryUsesRunGlobalEncounterCount()
        {
            Type overlayType = Type.GetType(RunOverlayTypeName, true);
            MethodInfo method = overlayType.GetMethod(
                "ResolveCompletionEncounterTotal",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            Assert.That(
                (int)method.Invoke(null, new object[] { 5, 2 }),
                Is.EqualTo(5),
                "The last world's local count must not replace the run total.");
            Assert.That(
                (int)method.Invoke(null, new object[] { 0, 2 }),
                Is.EqualTo(2),
                "Legacy single-world data still needs a safe fallback.");
        }

        [Test]
        public void ResetHunterClearsMovementAndCombatRuntimeState()
        {
            GameObject gameObject = new GameObject("ArcHunterResetTest");
            try
            {
                Type hunterType = Type.GetType(HunterTypeName, true);
                Component hunter = gameObject.AddComponent(hunterType);
                Type modeType = Type.GetType(MovementModeTypeName, true);
                object strafe = Enum.Parse(modeType, "Strafe");

                Invoke(hunter, "ConfigureMovementMode", strafe);
                Invoke(hunter, "SetTakingLinkDamage", true);
                Invoke(hunter, "SetConductivity", 0.8f);
                Invoke(hunter, "ResetHunter");

                Assert.That(
                    GetProperty(hunter, "MovementMode").ToString(),
                    Is.EqualTo("Chase"));
                Assert.That(
                    (bool)GetProperty(hunter, "IsTakingLinkDamage"),
                    Is.False);
                Assert.That(
                    (float)GetProperty(hunter, "Conductivity01"),
                    Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ArcHunterCombatCycleExposesTelegraphedStatesInOrder()
        {
            Type stateType = Type.GetType(
                "Glassfall.Gameplay.Combat.ArcHunterCombatState, Assembly-CSharp",
                true);

            string[] expected =
            {
                "Hunt",
                "TargetLock",
                "Telegraph",
                "CommitAttack",
                "Recover",
                "Reposition"
            };

            Assert.That(
                System.Enum.GetNames(stateType),
                Is.EqualTo(expected));
        }

        [Test]
        public void StrafeDirectionIsDeterministicAndTangential()
        {
            Type movementType = Type.GetType(MovementTypeName, true);
            Vector2 result = (Vector2)InvokeStatic(
                movementType,
                "CalculateStrafeDirection",
                new Vector2(0f, 2f),
                Vector2.zero,
                1,
                2.25f);

            Assert.That(result.x, Is.LessThan(-0.9f));
            Assert.That(Mathf.Abs(result.y), Is.LessThan(0.2f));
        }

        [Test]
        public void WetRouteDirectionFollowsTheWetCandidate()
        {
            Type movementType = Type.GetType(MovementTypeName, true);
            Func<Vector2, float> sample = position =>
                position.y > 0.8f ? 1f : 0f;
            Vector2 result = (Vector2)InvokeStatic(
                movementType,
                "CalculateWetRouteDirection",
                Vector2.zero,
                Vector2.right,
                sample,
                1,
                1.25f,
                0.7f);

            Assert.That(result.y, Is.GreaterThan(0.7f));
        }

        private static void SetPrivateField(
            object instance,
            string fieldName,
            object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(instance, value);
        }

        private static object CreateWorldSegment(
            Type segmentType,
            Type movementModeType,
            string worldId,
            Vector2[] positions,
            string[] movementModeNames)
        {
            Array movementModes = Array.CreateInstance(
                movementModeType,
                movementModeNames.Length);
            for (int i = 0; i < movementModeNames.Length; i++)
            {
                movementModes.SetValue(
                    Enum.Parse(movementModeType, movementModeNames[i]),
                    i);
            }

            return Activator.CreateInstance(
                segmentType,
                worldId,
                positions,
                movementModes);
        }

        private static void ConfigureM26Segments(
            object controller,
            Type segmentType,
            Type movementModeType)
        {
            object fogged = CreateWorldSegment(
                segmentType,
                movementModeType,
                "FOGGED_WINDOW",
                new[]
                {
                    new Vector2(-11f, -5f),
                    new Vector2(0f, 7f),
                    new Vector2(13f, 2f)
                },
                new[] { "Chase", "Strafe", "WetRoutePressure" });
            object neon = CreateWorldSegment(
                segmentType,
                movementModeType,
                "NEON_WINDOW",
                new[]
                {
                    new Vector2(-12f, 6.2f),
                    new Vector2(6.8f, -6.4f),
                    new Vector2(14.2f, 5.5f)
                },
                new[] { "Strafe", "WetRoutePressure", "Chase" });
            Array segments = Array.CreateInstance(segmentType, 2);
            segments.SetValue(fogged, 0);
            segments.SetValue(neon, 1);
            Invoke(controller, "ConfigureWorldSegments", segments);
        }

        private static void CompleteEncounterCycle(object progress)
        {
            Assert.That((bool)Invoke(progress, "DefeatEncounter"), Is.True);
            Assert.That((bool)Invoke(progress, "CompleteRewardChoice"), Is.True);
            Assert.That((bool)Invoke(progress, "CompleteRecovery"), Is.True);
            Assert.That((bool)Invoke(progress, "FinishIntermission"), Is.True);
        }

        private static object CreateProgress(int encounterCount)
        {
            Type type = Type.GetType(ProgressTypeName, true);
            return Activator.CreateInstance(type, encounterCount);
        }

        private static object Invoke(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);

            ParameterInfo[] parameters = method.GetParameters();
            Assert.That(
                arguments.Length,
                Is.LessThanOrEqualTo(parameters.Length),
                $"{methodName} received too many arguments.");

            if (arguments.Length == parameters.Length)
                return method.Invoke(instance, arguments);

            object[] completedArguments = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < arguments.Length)
                {
                    completedArguments[i] = arguments[i];
                    continue;
                }

                Assert.That(
                    parameters[i].IsOptional,
                    Is.True,
                    $"{methodName} is missing required parameter " +
                    $"'{parameters[i].Name}'.");

                completedArguments[i] = parameters[i].DefaultValue;
            }

            return method.Invoke(instance, completedArguments);
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }

        private static object InvokeNonPublic(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(instance, arguments);
        }

        private static object GetProperty(
            object instance,
            string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(instance);
        }
    }
}
