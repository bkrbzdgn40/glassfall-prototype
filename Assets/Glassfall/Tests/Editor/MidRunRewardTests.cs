using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Glassfall.Tests
{
    public sealed class MidRunRewardTests
    {
        private const string RewardTypeName =
            "Glassfall.Gameplay.Rewards.MidRunReward, Assembly-CSharp";
        private const string RewardRulesTypeName =
            "Glassfall.Gameplay.Rewards.MidRunRewardRules, Assembly-CSharp";
        private const string RewardControllerTypeName =
            "Glassfall.Gameplay.Rewards.MidRunRewardController, Assembly-CSharp";
        private const string SplitTypeName =
            "Glassfall.Gameplay.Droplet.DropletSplitSystem, Assembly-CSharp";
        private const string MergeTypeName =
            "Glassfall.Gameplay.Droplet.DropletMergeSystem, Assembly-CSharp";
        private const string CombatTypeName =
            "Glassfall.Gameplay.Combat.SplitLinkCombat, Assembly-CSharp";
        private const string IdentityTypeName =
            "Glassfall.Gameplay.Droplet.MergedFormIdentity, Assembly-CSharp";
        private const string ShearTypeName =
            "Glassfall.Gameplay.Combat.MassShearSystem, Assembly-CSharp";
        private const string WetnessFieldTypeName =
            "Glassfall.Gameplay.Wetness.WetnessField, Assembly-CSharp";
        private const string ContaminationSystemTypeName =
            "Glassfall.Gameplay.Run.ContaminationSystem, Assembly-CSharp";

        [Test]
        public void M25PoolContainsTenCategorizedCoreRewards()
        {
            Type rules = Type.GetType(RewardRulesTypeName, true);
            Array pool = (Array)InvokeStatic(rules, "GetAllRewards");

            Assert.That(pool.Length, Is.EqualTo(10));
            Assert.That(
                InvokeStatic(
                    rules,
                    "GetCategory",
                    Enum.Parse(
                        Type.GetType(RewardTypeName, true),
                        "PurifyingResonance")).ToString(),
                Is.EqualTo("RecoveryResonance"));
        }

        [Test]
        public void ChoiceBuilderAvoidsSelectedRewardsAndDuplicateCards()
        {
            Type rules = Type.GetType(RewardRulesTypeName, true);
            Array pool = (Array)InvokeStatic(rules, "GetAllRewards");
            Type rewardType = Type.GetType(RewardTypeName, true);
            Array selected = Array.CreateInstance(rewardType, 1);
            selected.SetValue(
                Enum.Parse(rewardType, "GravitationalDrop"),
                0);

            Array choices = (Array)InvokeStatic(
                rules,
                "BuildChoices",
                2,
                pool,
                selected,
                0);

            Assert.That(choices.Length, Is.EqualTo(3));
            Assert.That(
                choices.GetValue(0).ToString(),
                Is.Not.EqualTo("GravitationalDrop"));
            Assert.That(
                choices.GetValue(0),
                Is.Not.EqualTo(choices.GetValue(1)));
            Assert.That(
                choices.GetValue(1),
                Is.Not.EqualTo(choices.GetValue(2)));
        }

        [Test]
        public void ChoiceBuilderPrefersCategoryVarietyWhenPoolAllows()
        {
            Type rules = Type.GetType(RewardRulesTypeName, true);
            Type rewardType = Type.GetType(RewardTypeName, true);
            Array pool = Array.CreateInstance(rewardType, 3);
            pool.SetValue(Enum.Parse(rewardType, "AbsorbentCore"), 0);
            pool.SetValue(Enum.Parse(rewardType, "StableLink"), 1);
            pool.SetValue(
                Enum.Parse(rewardType, "ConductiveResidue"),
                2);
            Array selected = Array.CreateInstance(rewardType, 0);
            Array choices = (Array)InvokeStatic(
                rules,
                "BuildChoices",
                1,
                pool,
                selected,
                0);

            Assert.That(choices.Length, Is.EqualTo(3));
            string[] categories = new string[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                categories[i] = InvokeStatic(
                    rules,
                    "GetCategory",
                    choices.GetValue(i)).ToString();
            }

            for (int i = 0; i < categories.Length; i++)
            {
                int categoryCount = 0;
                for (int j = 0; j < categories.Length; j++)
                {
                    if (categories[i] == categories[j])
                        categoryCount++;
                }

                Assert.That(categoryCount, Is.LessThanOrEqualTo(2));
            }
        }

        [Test]
        public void ChoiceBuilderFallsBackWhenEligiblePoolIsNarrow()
        {
            Type rules = Type.GetType(RewardRulesTypeName, true);
            Type rewardType = Type.GetType(RewardTypeName, true);
            Array available = Array.CreateInstance(rewardType, 1);
            available.SetValue(
                Enum.Parse(rewardType, "DenseCore"),
                0);
            Array selected = Array.CreateInstance(rewardType, 0);

            Array choices = (Array)InvokeStatic(
                rules,
                "BuildChoices",
                2,
                available,
                selected,
                0);

            Assert.That(choices.Length, Is.EqualTo(1));
            Assert.That(
                choices.GetValue(0).ToString(),
                Is.EqualTo("DenseCore"));

            Array exhausted = Array.CreateInstance(rewardType, 1);
            exhausted.SetValue(
                Enum.Parse(rewardType, "DenseCore"),
                0);
            choices = (Array)InvokeStatic(
                rules,
                "BuildChoices",
                2,
                available,
                exhausted,
                0);
            Assert.That(choices.Length, Is.EqualTo(0));
        }

        [Test]
        public void M25RewardsDefaultToOneTimePolicy()
        {
            Type rules = Type.GetType(RewardRulesTypeName, true);
            Array pool = (Array)InvokeStatic(rules, "GetAllRewards");

            for (int i = 0; i < pool.Length; i++)
            {
                object policy = InvokeStatic(
                    rules,
                    "GetRepeatPolicy",
                    pool.GetValue(i));
                Assert.That(policy.ToString(), Is.EqualTo("OneTime"));
            }
        }

        [Test]
        public void ChoiceBuilderUsesPoolBeyondLegacyTriples()
        {
            Type rules = Type.GetType(RewardRulesTypeName, true);
            Array pool = (Array)InvokeStatic(rules, "GetAllRewards");
            Type rewardType = Type.GetType(RewardTypeName, true);
            Array selected = Array.CreateInstance(rewardType, 0);
            HashSet<string> offeredAcrossRotations = new HashSet<string>();

            for (int rotation = 0; rotation < 12; rotation++)
            {
                Array choices = (Array)InvokeStatic(
                    rules,
                    "BuildChoices",
                    1,
                    pool,
                    selected,
                    rotation);
                Assert.That(choices.Length, Is.EqualTo(3));
                for (int i = 0; i < choices.Length; i++)
                    offeredAcrossRotations.Add(choices.GetValue(i).ToString());
            }

            Assert.That(offeredAcrossRotations.Count, Is.GreaterThan(3));
        }

        [Test]
        public void EfficientDivisionChangesFutureSplitCostToSevenPercent()
        {
            GameObject gameObject = new GameObject("EfficientDivisionTest");
            try
            {
                Component split = AddComponent(gameObject, SplitTypeName);
                Invoke(split, "ResetRuntimeRewardTuning");
                Invoke(split, "ApplyEfficientDivisionTuning", 0.07f);

                Assert.That(
                    (float)GetProperty(split, "SplitCostRatio"),
                    Is.EqualTo(0.07f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AbsorbentCoreRaisesOnlyMergedCollectionToOneTwentyFive()
        {
            GameObject gameObject = new GameObject("AbsorbentCoreTest");
            try
            {
                Component merge = AddComponent(gameObject, MergeTypeName);
                Invoke(merge, "ConfigureNeutralCollection", 1f, 0.60f);
                Invoke(merge, "ApplyAbsorbentCoreTuning", 1.25f);

                Assert.That(
                    (float)GetProperty(
                        merge,
                        "MergedCollectionMultiplier"),
                    Is.EqualTo(1.25f).Within(0.0001f));
                Assert.That(
                    (float)GetProperty(
                        merge,
                        "SplitCollectionMultiplier"),
                    Is.EqualTo(0.60f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void StableLinkExtendsCenterWithoutRemovingEndpointExclusion()
        {
            GameObject gameObject = new GameObject("StableLinkTest");
            try
            {
                Component combat = AddComponent(gameObject, CombatTypeName);
                Invoke(combat, "ResetRuntimeBuildTuning");
                Invoke(combat, "ResetRuntimeRewardTuning");
                Invoke(combat, "ApplyStableLinkReward", 0.32f);

                float endpointInset =
                    (float)GetProperty(combat, "EndpointInset");
                Assert.That(
                    endpointInset,
                    Is.EqualTo(0.32f).Within(0.0001f));
                Assert.That(endpointInset, Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConductiveResidueStartsAtPointTenButDoesNotNerfFocus()
        {
            GameObject gameObject = new GameObject("ConductiveResidueTest");
            try
            {
                Component combat = AddComponent(gameObject, CombatTypeName);
                Invoke(combat, "ResetRuntimeBuildTuning");
                Invoke(combat, "ResetRuntimeRewardTuning");
                Invoke(combat, "ApplyConductiveResidueReward", 0.10f);

                Assert.That(
                    (float)GetProperty(
                        combat,
                        "ConductivityActivationThreshold"),
                    Is.EqualTo(0.10f).Within(0.0001f));

                Invoke(combat, "ApplyConductiveFocusTuning");
                Assert.That(
                    (float)GetProperty(
                        combat,
                        "ConductivityActivationThreshold"),
                    Is.EqualTo(0.05f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void GravitationalDropExtendsMergedAttractionRadius()
        {
            GameObject gameObject = new GameObject("GravitationalDropTest");
            try
            {
                Component identity = AddComponent(gameObject, IdentityTypeName);
                Invoke(identity, "ResetRuntimeRewardTuning");
                Invoke(identity, "ApplyGravitationalDropReward", 1.35f);

                Assert.That(
                    (float)GetProperty(identity, "AttractionRadiusFromSurface"),
                    Is.EqualTo(1.6875f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DenseCoreRaisesMergedBodyShoveByTwentyFivePercent()
        {
            GameObject gameObject = new GameObject("DenseCoreTest");
            try
            {
                Component identity = AddComponent(gameObject, IdentityTypeName);
                Invoke(identity, "ResetRuntimeRewardTuning");
                Invoke(identity, "ApplyDenseCoreReward", 1.25f);

                Assert.That(
                    (float)GetProperty(identity, "BaseBodyShoveSpeed"),
                    Is.EqualTo(4f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void HighCohesionReducesOnlyMergedMassShear()
        {
            GameObject gameObject = new GameObject("HighCohesionTest");
            try
            {
                Component shear = AddComponent(gameObject, ShearTypeName);
                Invoke(shear, "ResetRuntimeRewardTuning");
                Invoke(shear, "ApplyHighCohesionReward", 0.75f);

                Assert.That(
                    (float)GetProperty(shear, "MergedShearMass"),
                    Is.EqualTo(0.375f).Within(0.0001f));
                Assert.That(
                    (float)GetProperty(shear, "SplitShearMass"),
                    Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ElasticMembraneExtendsStableRangeWithoutCompressingPenaltyRamp()
        {
            GameObject gameObject = new GameObject("ElasticMembraneTest");
            try
            {
                Component combat = AddComponent(gameObject, CombatTypeName);
                Invoke(
                    combat,
                    "ConfigureSurfaceTension",
                    1.5f,
                    5.5f,
                    9f,
                    0.45f);
                Invoke(combat, "ResetRuntimeRewardTuning");
                Invoke(combat, "ApplyElasticMembraneReward", 1.20f);

                Assert.That(
                    (float)GetProperty(combat, "StableMaxDistance"),
                    Is.EqualTo(6.6f).Within(0.0001f));
                Assert.That(
                    (float)GetProperty(combat, "FullPenaltyDistance"),
                    Is.EqualTo(10.1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SlowEvaporationReducesWetTrailDecayByTwentyFivePercent()
        {
            GameObject gameObject = new GameObject("SlowEvaporationTest");
            try
            {
                Component wetness = AddComponent(
                    gameObject,
                    WetnessFieldTypeName);
                Invoke(wetness, "ConfigureLifecycle", 0.11f);
                Invoke(wetness, "ApplySlowEvaporationReward", 0.75f);

                Assert.That(
                    (float)GetProperty(wetness, "DecayRate"),
                    Is.EqualTo(0.0825f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PurifyingResonanceRaisesOnlyRecoveryCleanseAmount()
        {
            GameObject gameObject = new GameObject("PurifyingResonanceTest");
            try
            {
                Component contamination = AddComponent(
                    gameObject,
                    ContaminationSystemTypeName);
                Invoke(contamination, "ResetRuntimeRewardTuning");
                Invoke(
                    contamination,
                    "ApplyPurifyingResonanceReward",
                    1.15f);

                Assert.That(
                    (float)GetProperty(
                        contamination,
                        "RecoveryCleanseAmount"),
                    Is.EqualTo(0.276f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ControllerAppliesAndResetsNewSystemicRewards()
        {
            GameObject gameObject = new GameObject("RewardV2ControllerTest");
            try
            {
                Component split = AddComponent(gameObject, SplitTypeName);
                Component merge = gameObject.GetComponent(
                    Type.GetType(MergeTypeName, true));
                if (merge == null)
                    merge = AddComponent(gameObject, MergeTypeName);

                Component combat = AddComponent(gameObject, CombatTypeName);
                Component identity = gameObject.GetComponent(
                    Type.GetType(IdentityTypeName, true));
                if (identity == null)
                    identity = AddComponent(gameObject, IdentityTypeName);

                Component shear = gameObject.GetComponent(
                    Type.GetType(ShearTypeName, true));
                if (shear == null)
                    shear = AddComponent(gameObject, ShearTypeName);

                Component wetness = AddComponent(
                    gameObject,
                    WetnessFieldTypeName);
                Component contamination = AddComponent(
                    gameObject,
                    ContaminationSystemTypeName);
                Component controller =
                    AddComponent(gameObject, RewardControllerTypeName);

                Invoke(
                    controller,
                    "Configure",
                    split,
                    merge,
                    combat,
                    identity,
                    shear,
                    wetness,
                    contamination);
                Invoke(controller, "ResetRewardsForSession");

                Assert.That(
                    (bool)Invoke(controller, "BeginRewardChoice", 1),
                    Is.True);
                Assert.That(
                    (int)GetProperty(controller, "SupportedRewardCount"),
                    Is.EqualTo(10));
                Assert.That(
                    (int)GetProperty(controller, "ActiveChoiceCount"),
                    Is.EqualTo(3));
                object firstChoice = Invoke(
                    controller,
                    "GetActiveChoice",
                    0);
                object secondOffer = Invoke(
                    controller,
                    "GetActiveChoice",
                    1);
                object thirdOffer = Invoke(
                    controller,
                    "GetActiveChoice",
                    2);
                Assert.That(firstChoice.ToString(), Is.Not.EqualTo("None"));
                Assert.That(secondOffer, Is.Not.EqualTo(firstChoice));
                Assert.That(thirdOffer, Is.Not.EqualTo(firstChoice));
                Assert.That(thirdOffer, Is.Not.EqualTo(secondOffer));
                Assert.That(
                    (bool)Invoke(controller, "TryChoose", firstChoice),
                    Is.True);

                Assert.That(
                    (bool)Invoke(controller, "BeginRewardChoice", 2),
                    Is.True);
                object secondChoice = Invoke(
                    controller,
                    "GetActiveChoice",
                    0);
                Assert.That(secondChoice.ToString(), Is.Not.EqualTo("None"));
                Assert.That(secondChoice, Is.Not.EqualTo(firstChoice));
                Assert.That(
                    (bool)Invoke(controller, "TryChoose", secondChoice),
                    Is.True);

                Assert.That(
                    GetProperty(controller, "FirstReward"),
                    Is.EqualTo(firstChoice));
                Assert.That(
                    GetProperty(controller, "SecondReward"),
                    Is.EqualTo(secondChoice));

                Invoke(controller, "ResetRewardsForSession");

                Assert.That(
                    (float)GetProperty(identity, "AttractionRadiusFromSurface"),
                    Is.EqualTo(1.25f).Within(0.0001f));
                Assert.That(
                    (float)GetProperty(shear, "MergedShearMass"),
                    Is.EqualTo(0.5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SplitSystemResolvesPrimaryControllerWithoutAwakeCache()
        {
            GameObject gameObject = new GameObject("SplitPrimaryResolveTest");
            try
            {
                Component split = AddComponent(gameObject, SplitTypeName);
                FieldInfo primaryField = split.GetType().GetField(
                    "_primaryController",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(primaryField, Is.Not.Null);
                primaryField.SetValue(split, null);

                object primary = GetProperty(split, "PrimaryController");
                Assert.That(primary, Is.Not.Null);
                Assert.That(primary, Is.InstanceOf<Component>());
                Assert.That(((Component)primary).gameObject, Is.SameAs(gameObject));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ControllerKeepsOneTimeRewardsExcludedAcrossFourSteps()
        {
            GameObject gameObject = new GameObject("RewardPersistenceTest");
            try
            {
                Component split = AddComponent(gameObject, SplitTypeName);
                Component merge = gameObject.GetComponent(
                    Type.GetType(MergeTypeName, true));
                if (merge == null)
                    merge = AddComponent(gameObject, MergeTypeName);

                Component combat = AddComponent(gameObject, CombatTypeName);
                Component identity = gameObject.GetComponent(
                    Type.GetType(IdentityTypeName, true));
                if (identity == null)
                    identity = AddComponent(gameObject, IdentityTypeName);

                Component shear = gameObject.GetComponent(
                    Type.GetType(ShearTypeName, true));
                if (shear == null)
                    shear = AddComponent(gameObject, ShearTypeName);

                Component wetness = AddComponent(
                    gameObject,
                    WetnessFieldTypeName);
                Component contamination = AddComponent(
                    gameObject,
                    ContaminationSystemTypeName);
                Component controller =
                    AddComponent(gameObject, RewardControllerTypeName);

                Invoke(
                    controller,
                    "Configure",
                    split,
                    merge,
                    combat,
                    identity,
                    shear,
                    wetness,
                    contamination);
                Invoke(controller, "ResetRewardsForSession");

                HashSet<string> selected = new HashSet<string>();
                for (int rewardStep = 1; rewardStep <= 4; rewardStep++)
                {
                    Assert.That(
                        (bool)Invoke(
                            controller,
                            "BeginRewardChoice",
                            rewardStep),
                        Is.True,
                        $"reward step {rewardStep}");

                    Assert.That(
                        (int)GetProperty(controller, "ActiveChoiceCount"),
                        Is.EqualTo(3));

                    for (int choiceIndex = 0; choiceIndex < 3; choiceIndex++)
                    {
                        string offered = Invoke(
                            controller,
                            "GetActiveChoice",
                            choiceIndex).ToString();
                        Assert.That(offered, Is.Not.EqualTo("None"));
                        Assert.That(
                            selected.Contains(offered),
                            Is.False,
                            $"one-time reward {offered} reappeared at step {rewardStep}");
                    }

                    object chosen = Invoke(
                        controller,
                        "GetActiveChoice",
                        0);
                    Assert.That(
                        (bool)Invoke(controller, "TryChoose", chosen),
                        Is.True);
                    selected.Add(chosen.ToString());

                    Assert.That(
                        (bool)Invoke(
                            controller,
                            "HasCompletedStep",
                            rewardStep),
                        Is.True);
                }

                Assert.That(
                    (int)GetProperty(controller, "CompletedRewardStepCount"),
                    Is.EqualTo(4));
                Assert.That(
                    (int)GetProperty(controller, "SelectedOneTimeRewardCount"),
                    Is.EqualTo(4));
                Assert.That(
                    Invoke(controller, "GetRewardForStep", 4).ToString(),
                    Is.Not.EqualTo("None"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ControllerRejectsDuplicateChoiceForSameRewardStep()
        {
            GameObject gameObject = new GameObject("RewardControllerTest");
            try
            {
                Component split = AddComponent(gameObject, SplitTypeName);
                Component merge = gameObject.GetComponent(
                    Type.GetType(MergeTypeName, true));
                if (merge == null)
                    merge = AddComponent(gameObject, MergeTypeName);
                Component combat = AddComponent(gameObject, CombatTypeName);
                Component controller =
                    AddComponent(gameObject, RewardControllerTypeName);

                Invoke(controller, "Configure", split, merge, combat);
                Invoke(controller, "ResetRewardsForSession");

                Assert.That(
                    (bool)Invoke(
                        controller,
                        "BeginRewardChoice",
                        1),
                    Is.True);

                object selected = Invoke(
                    controller,
                    "GetActiveChoice",
                    0);
                Assert.That(selected.ToString(), Is.Not.EqualTo("None"));

                Assert.That(
                    (bool)Invoke(
                        controller,
                        "TryChoose",
                        selected),
                    Is.True);
                Assert.That(
                    (bool)Invoke(
                        controller,
                        "TryChoose",
                        selected),
                    Is.False);
                Assert.That(
                    (bool)Invoke(
                        controller,
                        "HasCompletedStep",
                        1),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RewardChoiceFreezesGameplayUntilAChoiceIsMade()
        {
            float originalTimeScale = Time.timeScale;
            GameObject gameObject = new GameObject("RewardDecisionFreezeTest");
            try
            {
                Time.timeScale = 0.75f;

                Component split = AddComponent(gameObject, SplitTypeName);
                Component merge = gameObject.GetComponent(
                    Type.GetType(MergeTypeName, true));
                if (merge == null)
                    merge = AddComponent(gameObject, MergeTypeName);
                Component combat = AddComponent(gameObject, CombatTypeName);
                Component controller =
                    AddComponent(gameObject, RewardControllerTypeName);

                Invoke(controller, "Configure", split, merge, combat);
                Invoke(controller, "ConfigureDecisionFreeze", true);

                Assert.That(
                    (bool)Invoke(controller, "BeginRewardChoice", 1),
                    Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(
                    (bool)GetProperty(controller, "IsGameplayFrozen"),
                    Is.True);

                object selected = Invoke(
                    controller,
                    "GetActiveChoice",
                    0);
                Assert.That(
                    (bool)Invoke(controller, "TryChoose", selected),
                    Is.True);

                Assert.That(
                    Time.timeScale,
                    Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(
                    (bool)GetProperty(controller, "IsGameplayFrozen"),
                    Is.False);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ResetRewardsReleasesAnActiveDecisionFreeze()
        {
            float originalTimeScale = Time.timeScale;
            GameObject gameObject = new GameObject("RewardFreezeResetTest");
            try
            {
                Time.timeScale = 0.5f;

                Component split = AddComponent(gameObject, SplitTypeName);
                Component merge = gameObject.GetComponent(
                    Type.GetType(MergeTypeName, true));
                if (merge == null)
                    merge = AddComponent(gameObject, MergeTypeName);
                Component combat = AddComponent(gameObject, CombatTypeName);
                Component controller =
                    AddComponent(gameObject, RewardControllerTypeName);

                Invoke(controller, "Configure", split, merge, combat);
                Invoke(controller, "ConfigureDecisionFreeze", true);
                Invoke(controller, "BeginRewardChoice", 1);

                Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f));

                Invoke(controller, "ResetRewardsForSession");

                Assert.That(
                    Time.timeScale,
                    Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(
                    (bool)GetProperty(controller, "IsGameplayFrozen"),
                    Is.False);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ResetRewardsRestoresBaselineRuntimeTuning()
        {
            GameObject gameObject = new GameObject("RewardResetTest");
            try
            {
                Component split = AddComponent(gameObject, SplitTypeName);
                Component merge = gameObject.GetComponent(
                    Type.GetType(MergeTypeName, true));
                if (merge == null)
                    merge = AddComponent(gameObject, MergeTypeName);
                Component combat = AddComponent(gameObject, CombatTypeName);
                Component controller =
                    AddComponent(gameObject, RewardControllerTypeName);

                Invoke(controller, "Configure", split, merge, combat);
                Invoke(controller, "ResetRewardsForSession");

                Invoke(controller, "BeginRewardChoice", 1);
                object firstChoice = Invoke(
                    controller,
                    "GetActiveChoice",
                    0);
                Assert.That(
                    (bool)Invoke(controller, "TryChoose", firstChoice),
                    Is.True);

                Invoke(controller, "BeginRewardChoice", 2);
                object secondChoice = Invoke(
                    controller,
                    "GetActiveChoice",
                    0);
                Assert.That(
                    (bool)Invoke(controller, "TryChoose", secondChoice),
                    Is.True);

                Invoke(controller, "ResetRewardsForSession");

                Assert.That(
                    (float)GetProperty(split, "SplitCostRatio"),
                    Is.EqualTo(0.10f).Within(0.0001f));
                Assert.That(
                    (float)GetProperty(
                        merge,
                        "MergedCollectionMultiplier"),
                    Is.EqualTo(1f).Within(0.0001f));
                Assert.That(
                    (float)GetProperty(combat, "EndpointInset"),
                    Is.EqualTo(0.45f).Within(0.0001f));
                Assert.That(
                    GetProperty(controller, "FirstReward").ToString(),
                    Is.EqualTo("None"));
                Assert.That(
                    GetProperty(controller, "SecondReward").ToString(),
                    Is.EqualTo("None"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static Component AddComponent(
            GameObject gameObject,
            string typeName)
        {
            Type type = Type.GetType(typeName, true);
            return gameObject.AddComponent(type);
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
