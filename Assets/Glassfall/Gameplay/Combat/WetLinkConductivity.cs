using UnityEngine;

namespace Glassfall.Gameplay.Combat
{
    public static class WetLinkConductivity
    {
        private const float MinimumWetnessRange = 0.0001f;

        public static float CalculateConductivity01(
            float wetness,
            float activationThreshold,
            float fullEffectWetness)
        {
            float clampedWetness = Mathf.Clamp01(wetness);
            float threshold = Mathf.Clamp01(activationThreshold);
            float fullEffect = Mathf.Clamp01(fullEffectWetness);

            if (fullEffect <= threshold + MinimumWetnessRange)
                fullEffect = Mathf.Min(1f, threshold + MinimumWetnessRange);

            if (clampedWetness <= threshold)
                return 0f;

            if (clampedWetness >= fullEffect)
                return 1f;

            return Mathf.InverseLerp(
                threshold,
                fullEffect,
                clampedWetness);
        }

        public static float CalculateDamageMultiplier(
            float conductivity01,
            float fullConductivityMultiplier)
        {
            float multiplier = Mathf.Max(1f, fullConductivityMultiplier);
            return Mathf.Lerp(
                1f,
                multiplier,
                Mathf.Clamp01(conductivity01));
        }
    }
}
