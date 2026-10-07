using UnityEngine;

namespace Glassfall.Gameplay.Wetness
{
    public enum WetTrailLifecycleBand
    {
        Dry = 0,
        Faded = 1,
        Aging = 2,
        Fresh = 3
    }

    public static class WetTrailLifecycleRules
    {
        public static WetTrailLifecycleBand CalculateBand(
            float wetness,
            float fadedThreshold,
            float freshThreshold)
        {
            float value = Mathf.Clamp01(wetness);
            float faded = Mathf.Clamp01(fadedThreshold);
            float fresh = Mathf.Clamp(freshThreshold, faded, 1f);

            if (value <= 0f)
                return WetTrailLifecycleBand.Dry;
            if (value < faded)
                return WetTrailLifecycleBand.Faded;
            if (value < fresh)
                return WetTrailLifecycleBand.Aging;
            return WetTrailLifecycleBand.Fresh;
        }

        public static float DecayValue(
            float wetness,
            float decayRate,
            float deltaTime)
        {
            float amount =
                Mathf.Max(0f, decayRate) * Mathf.Max(0f, deltaTime);
            return Mathf.Max(0f, Mathf.Clamp01(wetness) - amount);
        }

        public static float SecondsUntilThreshold(
            float wetness,
            float threshold,
            float decayRate)
        {
            float rate = Mathf.Max(0f, decayRate);
            if (rate <= 0f)
                return float.PositiveInfinity;

            float value = Mathf.Clamp01(wetness);
            float target = Mathf.Clamp01(threshold);
            if (value <= target)
                return 0f;

            return (value - target) / rate;
        }
    }
}
