using System;
using System.Reflection;
using NUnit.Framework;

namespace Glassfall.Tests
{
    public sealed class SurfaceTensionTests
    {
        private const string RulesTypeName =
            "Glassfall.Gameplay.Link.SurfaceTensionRules, Assembly-CSharp";

        [Test]
        public void CloseDistanceUsesCloseBand()
        {
            Assert.That(
                CalculateBandName(1f, 1.5f, 5.5f),
                Is.EqualTo("Close"));
        }

        [Test]
        public void MiddleDistanceUsesStableBand()
        {
            Assert.That(
                CalculateBandName(4f, 1.5f, 5.5f),
                Is.EqualTo("Stable"));
        }

        [Test]
        public void DistanceBeyondStableRangeIsOverstretched()
        {
            Assert.That(
                CalculateBandName(6f, 1.5f, 5.5f),
                Is.EqualTo("Overstretched"));
        }

        [Test]
        public void StableRangeKeepsFullDamageEfficiency()
        {
            float multiplier = CalculateDamageMultiplier(
                5.5f,
                5.5f,
                9f,
                0.45f);

            Assert.That(multiplier, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void HalfwayOverstretchInterpolatesDamagePenalty()
        {
            float multiplier = CalculateDamageMultiplier(
                7.25f,
                5.5f,
                9f,
                0.45f);

            Assert.That(multiplier, Is.EqualTo(0.725f).Within(0.0001f));
        }

        [Test]
        public void ExtremeSeparationClampsAtMinimumDamageEfficiency()
        {
            float multiplier = CalculateDamageMultiplier(
                100f,
                5.5f,
                9f,
                0.45f);

            Assert.That(multiplier, Is.EqualTo(0.45f).Within(0.0001f));
        }

        private static string CalculateBandName(
            float separation,
            float closeMaxDistance,
            float stableMaxDistance)
        {
            object result = InvokeStatic(
                "CalculateBand",
                separation,
                closeMaxDistance,
                stableMaxDistance);
            return result.ToString();
        }

        private static float CalculateDamageMultiplier(
            float separation,
            float stableMaxDistance,
            float fullPenaltyDistance,
            float minimumDamageMultiplier)
        {
            return (float)InvokeStatic(
                "CalculateDamageMultiplier",
                separation,
                stableMaxDistance,
                fullPenaltyDistance,
                minimumDamageMultiplier);
        }

        private static object InvokeStatic(
            string methodName,
            params object[] arguments)
        {
            Type type = Type.GetType(RulesTypeName, true);
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }
    }
}
