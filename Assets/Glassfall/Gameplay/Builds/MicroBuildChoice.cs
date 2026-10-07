using UnityEngine;

namespace Glassfall.Gameplay.Builds
{
    public enum MicroBuildChoice
    {
        None = 0,
        ConductiveFocus = 1,
        BroadArc = 2,
        Capacitor = 3
    }

    public static class MicroBuildCapacitor
    {
        public static float CalculateCharge01(
            float splitDuration,
            float chargeDelay,
            float chargeDuration)
        {
            float duration = Mathf.Max(0f, splitDuration);
            float delay = Mathf.Max(0f, chargeDelay);
            float charge = Mathf.Max(0f, chargeDuration);

            if (duration <= delay || charge <= 0f)
                return 0f;

            return Mathf.Clamp01((duration - delay) / charge);
        }

        public static float CalculateMultiplier(
            float charge01,
            float maximumMultiplier)
        {
            return Mathf.Lerp(
                1f,
                Mathf.Max(1f, maximumMultiplier),
                Mathf.Clamp01(charge01));
        }
    }
}
