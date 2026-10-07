using UnityEngine;

namespace Glassfall.Gameplay.Link
{
    public enum SurfaceTensionBand
    {
        Close = 0,
        Stable = 1,
        Overstretched = 2
    }

    public static class SurfaceTensionRules
    {
        private const float Epsilon = 0.0001f;

        public static SurfaceTensionBand CalculateBand(
            float separation,
            float closeMaxDistance,
            float stableMaxDistance)
        {
            float safeSeparation = Mathf.Max(0f, separation);
            float safeClose = Mathf.Max(0f, closeMaxDistance);
            float safeStable = Mathf.Max(safeClose, stableMaxDistance);

            if (safeSeparation < safeClose)
                return SurfaceTensionBand.Close;
            if (safeSeparation <= safeStable)
                return SurfaceTensionBand.Stable;

            return SurfaceTensionBand.Overstretched;
        }

        public static float CalculateOverstretch01(
            float separation,
            float stableMaxDistance,
            float fullPenaltyDistance)
        {
            float safeStable = Mathf.Max(0f, stableMaxDistance);
            float safeFullPenalty = Mathf.Max(
                safeStable + Epsilon,
                fullPenaltyDistance);

            if (separation <= safeStable)
                return 0f;

            return Mathf.InverseLerp(
                safeStable,
                safeFullPenalty,
                Mathf.Max(0f, separation));
        }

        public static float CalculateDamageMultiplier(
            float separation,
            float stableMaxDistance,
            float fullPenaltyDistance,
            float minimumDamageMultiplier)
        {
            float overstretch01 = CalculateOverstretch01(
                separation,
                stableMaxDistance,
                fullPenaltyDistance);
            float minimum = Mathf.Clamp01(minimumDamageMultiplier);

            return Mathf.Lerp(1f, minimum, overstretch01);
        }
    }
}
