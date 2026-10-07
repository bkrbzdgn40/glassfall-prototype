using System;

namespace Glassfall.Gameplay.Droplet
{
    public readonly struct NeutralMassTier
    {
        public NeutralMassTier(float minMass, float maxMass, float weight)
        {
            MinMass = minMass;
            MaxMass = maxMass;
            Weight = weight;
        }

        public float MinMass { get; }
        public float MaxMass { get; }
        public float Weight { get; }
    }

    public enum NeutralMassTierKind
    {
        Small,
        Medium,
        Large
    }

    public static class NeutralDropletMassDistribution
    {
        public static NeutralMassTierKind SelectTier(
            Random random,
            float smallWeight,
            float mediumWeight,
            float largeWeight)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            float small = Math.Max(0f, smallWeight);
            float medium = Math.Max(0f, mediumWeight);
            float large = Math.Max(0f, largeWeight);
            float total = small + medium + large;

            if (total <= 0f)
                return NeutralMassTierKind.Small;

            double roll = random.NextDouble() * total;
            if (roll < small)
                return NeutralMassTierKind.Small;

            if (roll < small + medium)
                return NeutralMassTierKind.Medium;

            return NeutralMassTierKind.Large;
        }

        public static float Sample(
            Random random,
            float minMass,
            float maxMass,
            NeutralMassTier small,
            NeutralMassTier medium,
            NeutralMassTier large)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            float minimum = Math.Max(
                0.01f,
                Math.Min(minMass, maxMass));
            float maximum = Math.Max(
                minimum,
                Math.Max(minMass, maxMass));

            NeutralMassTierKind kind = SelectTier(
                random,
                small.Weight,
                medium.Weight,
                large.Weight);
            NeutralMassTier selected = kind == NeutralMassTierKind.Small
                ? small
                : kind == NeutralMassTierKind.Medium
                    ? medium
                    : large;

            float tierMinimum = Clamp(
                selected.MinMass,
                minimum,
                maximum);
            float tierMaximum = Clamp(
                Math.Max(selected.MinMass, selected.MaxMass),
                minimum,
                maximum);

            if (tierMaximum <= tierMinimum)
                return tierMinimum;

            return tierMinimum +
                (float)random.NextDouble() *
                (tierMaximum - tierMinimum);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
