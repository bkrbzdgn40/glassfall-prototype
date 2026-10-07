using System;
using System.Reflection;
using NUnit.Framework;

namespace Glassfall.Tests
{
    public sealed class WetTrailLifecycleTests
    {
        private const string RulesTypeName =
            "Glassfall.Gameplay.Wetness.WetTrailLifecycleRules, Assembly-CSharp";

        [Test]
        public void HighWetnessIsFresh()
        {
            Assert.That(
                CalculateBandName(0.80f, 0.30f, 0.65f),
                Is.EqualTo("Fresh"));
        }

        [Test]
        public void MidWetnessIsAging()
        {
            Assert.That(
                CalculateBandName(0.50f, 0.30f, 0.65f),
                Is.EqualTo("Aging"));
        }

        [Test]
        public void LowPositiveWetnessIsFaded()
        {
            Assert.That(
                CalculateBandName(0.15f, 0.30f, 0.65f),
                Is.EqualTo("Faded"));
        }

        [Test]
        public void ZeroWetnessIsDry()
        {
            Assert.That(
                CalculateBandName(0f, 0.30f, 0.65f),
                Is.EqualTo("Dry"));
        }

        [Test]
        public void DecayUsesConfiguredLinearGameplayRate()
        {
            float remaining = DecayValue(1f, 0.11f, 2f);

            Assert.That(remaining, Is.EqualTo(0.78f).Within(0.0001f));
        }

        [Test]
        public void DecayNeverProducesNegativeWetness()
        {
            float remaining = DecayValue(0.05f, 0.11f, 10f);

            Assert.That(remaining, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void FullWetnessRemainsConductiveForAboutSevenPointSevenSeconds()
        {
            float seconds = SecondsUntilThreshold(
                1f,
                0.15f,
                0.11f);

            Assert.That(seconds, Is.EqualTo(7.7272725f).Within(0.001f));
        }

        private static string CalculateBandName(
            float wetness,
            float fadedThreshold,
            float freshThreshold)
        {
            object result = InvokeStatic(
                "CalculateBand",
                wetness,
                fadedThreshold,
                freshThreshold);
            return result.ToString();
        }

        private static float DecayValue(
            float wetness,
            float decayRate,
            float deltaTime)
        {
            return (float)InvokeStatic(
                "DecayValue",
                wetness,
                decayRate,
                deltaTime);
        }

        private static float SecondsUntilThreshold(
            float wetness,
            float threshold,
            float decayRate)
        {
            return (float)InvokeStatic(
                "SecondsUntilThreshold",
                wetness,
                threshold,
                decayRate);
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
