using System;

namespace Glassfall.Gameplay.Droplet
{
    public static class NeutralCollectionRules
    {
        public const float DefaultMergedMultiplier = 1.0f;
        public const float DefaultSplitMultiplier = 0.60f;

        public static float CalculateMassGain(
            float neutralMass,
            bool isSplitForm,
            float mergedMultiplier = DefaultMergedMultiplier,
            float splitMultiplier = DefaultSplitMultiplier)
        {
            float safeMass = Math.Max(0f, neutralMass);
            float multiplier = isSplitForm
                ? Math.Max(0f, splitMultiplier)
                : Math.Max(0f, mergedMultiplier);

            return safeMass * multiplier;
        }
    }
}
