using System.Collections.Generic;

namespace Glassfall.Gameplay.Rewards
{
    public enum MidRunReward
    {
        None = 0,
        EfficientDivision = 1,
        AbsorbentCore = 2,
        StableLink = 3,
        ConductiveResidue = 4,
        GravitationalDrop = 5,
        DenseCore = 6,
        HighCohesion = 7,
        ElasticMembrane = 8,
        SlowEvaporation = 9,
        PurifyingResonance = 10
    }

    public enum MidRunRewardCategory
    {
        None = 0,
        LinkSplit = 1,
        MergedMass = 2,
        WetnessConductivity = 3,
        RecoveryResonance = 4
    }

    public enum MidRunRewardRepeatPolicy
    {
        OneTime = 0,
        LimitedStack = 1
    }

    public static class MidRunRewardRules
    {
        // Existing V1/V2 reward values are preserved as the authoritative
        // gameplay knobs. M25 only expands how they are offered.
        public const float EfficientDivisionSplitCostRatio = 0.07f;
        public const float AbsorbentCoreMergedCollectionMultiplier = 1.25f;
        public const float StableLinkEndpointInset = 0.32f;
        public const float ConductiveResidueActivationThreshold = 0.10f;

        public const float GravitationalDropAttractionRadiusMultiplier = 1.35f;
        public const float DenseCoreShoveMultiplier = 1.25f;
        public const float HighCohesionMergedShearMultiplier = 0.75f;
        public const float ElasticMembraneStableDistanceMultiplier = 1.20f;
        public const float SlowEvaporationDecayMultiplier = 0.75f;
        public const float PurifyingResonanceRecoveryCleanseMultiplier = 1.15f;

        private static readonly MidRunReward[] Pool =
        {
            MidRunReward.EfficientDivision,
            MidRunReward.AbsorbentCore,
            MidRunReward.StableLink,
            MidRunReward.ConductiveResidue,
            MidRunReward.GravitationalDrop,
            MidRunReward.DenseCore,
            MidRunReward.HighCohesion,
            MidRunReward.ElasticMembrane,
            MidRunReward.SlowEvaporation,
            MidRunReward.PurifyingResonance
        };

        public static int PoolSize => Pool.Length;

        public static MidRunReward[] GetAllRewards()
        {
            return (MidRunReward[])Pool.Clone();
        }

        public static MidRunRewardCategory GetCategory(
            MidRunReward reward)
        {
            switch (reward)
            {
                case MidRunReward.EfficientDivision:
                case MidRunReward.StableLink:
                case MidRunReward.ElasticMembrane:
                    return MidRunRewardCategory.LinkSplit;
                case MidRunReward.AbsorbentCore:
                case MidRunReward.GravitationalDrop:
                case MidRunReward.DenseCore:
                case MidRunReward.HighCohesion:
                    return MidRunRewardCategory.MergedMass;
                case MidRunReward.ConductiveResidue:
                case MidRunReward.SlowEvaporation:
                    return MidRunRewardCategory.WetnessConductivity;
                case MidRunReward.PurifyingResonance:
                    return MidRunRewardCategory.RecoveryResonance;
                default:
                    return MidRunRewardCategory.None;
            }
        }

        public static MidRunRewardRepeatPolicy GetRepeatPolicy(
            MidRunReward reward)
        {
            return MidRunRewardRepeatPolicy.OneTime;
        }

        public static MidRunReward[] BuildChoices(
            int rewardStep,
            IReadOnlyList<MidRunReward> availablePool,
            IReadOnlyList<MidRunReward> selectedRewards,
            int rotation = 0)
        {
            if (availablePool == null || availablePool.Count == 0)
                return new MidRunReward[0];

            List<MidRunReward> candidates = new List<MidRunReward>(
                availablePool.Count);
            for (int i = 0; i < availablePool.Count; i++)
            {
                MidRunReward candidate = availablePool[i];
                if (!Contains(selectedRewards, candidate) &&
                    !candidates.Contains(candidate))
                {
                    candidates.Add(candidate);
                }
            }

            ShuffleCandidates(candidates, rewardStep, rotation);
            List<MidRunReward> choices = new List<MidRunReward>(3);

            // Prefer at most two cards from one category, but do not fail if
            // a narrow dependency-safe pool cannot satisfy that preference.
            for (int pass = 0; pass < 2 && choices.Count < 3; pass++)
            {
                for (int i = 0;
                     i < candidates.Count && choices.Count < 3;
                     i++)
                {
                    MidRunReward candidate = candidates[i];
                    if (choices.Contains(candidate))
                        continue;

                    MidRunRewardCategory category = GetCategory(candidate);
                    if (pass == 0 && CountCategory(choices, category) >= 2)
                        continue;

                    choices.Add(candidate);
                }
            }

            return choices.ToArray();
        }

        // Legacy fixed-option helpers remain for existing tests and any
        // external tooling. Runtime reward UI uses controller-generated cards.
        public static bool IsChoiceForStep(
            MidRunReward reward,
            int rewardStep)
        {
            switch (rewardStep)
            {
                case 1:
                    return reward == MidRunReward.GravitationalDrop ||
                        reward == MidRunReward.EfficientDivision ||
                        reward == MidRunReward.SlowEvaporation;
                case 2:
                    return reward == MidRunReward.DenseCore ||
                        reward == MidRunReward.HighCohesion ||
                        reward == MidRunReward.ElasticMembrane;
                default:
                    return false;
            }
        }

        public static MidRunReward GetFirstOption(int rewardStep)
        {
            return rewardStep == 1
                ? MidRunReward.GravitationalDrop
                : rewardStep == 2
                    ? MidRunReward.DenseCore
                    : MidRunReward.None;
        }

        public static MidRunReward GetSecondOption(int rewardStep)
        {
            return rewardStep == 1
                ? MidRunReward.EfficientDivision
                : rewardStep == 2
                    ? MidRunReward.HighCohesion
                    : MidRunReward.None;
        }

        public static MidRunReward GetThirdOption(int rewardStep)
        {
            return rewardStep == 1
                ? MidRunReward.SlowEvaporation
                : rewardStep == 2
                    ? MidRunReward.ElasticMembrane
                    : MidRunReward.None;
        }

        private static int CountCategory(
            IReadOnlyList<MidRunReward> choices,
            MidRunRewardCategory category)
        {
            int count = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                if (GetCategory(choices[i]) == category)
                    count++;
            }

            return count;
        }

        private static bool Contains(
            IReadOnlyList<MidRunReward> values,
            MidRunReward value)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }

            return false;
        }

        private static void ShuffleCandidates(
            List<MidRunReward> candidates,
            int rewardStep,
            int rotation)
        {
            if (candidates == null || candidates.Count < 2)
                return;

            unchecked
            {
                int seed = rotation * 486187739 + rewardStep * 31;
                seed = (int)((uint)seed & 0x7fffffff);
                System.Random random = new System.Random(seed);
                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int swapIndex = random.Next(i + 1);
                    MidRunReward value = candidates[i];
                    candidates[i] = candidates[swapIndex];
                    candidates[swapIndex] = value;
                }
            }
        }
    }
}
