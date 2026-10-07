using System;
using System.Reflection;
using NUnit.Framework;

namespace Glassfall.Tests
{
    public sealed class WetLinkConductivityTests
    {
        private const string ConductivityTypeName =
            "Glassfall.Gameplay.Combat.WetLinkConductivity, Assembly-CSharp";

        [Test]
        public void DrySurfaceHasNoConductivity()
        {
            Assert.That(
                CalculateConductivity(0f, 0.15f, 0.50f),
                Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void WetnessAtThresholdHasNoConductivity()
        {
            Assert.That(
                CalculateConductivity(0.15f, 0.15f, 0.50f),
                Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void FreshTrailWetnessProducesMeaningfulConductivity()
        {
            float value = CalculateConductivity(
                0.35f,
                0.15f,
                0.50f);

            Assert.That(value, Is.EqualTo(0.5714f).Within(0.001f));
        }

        [Test]
        public void FullEffectWetnessCapsAtOne()
        {
            Assert.That(
                CalculateConductivity(0.75f, 0.15f, 0.50f),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ConductivityScalesDamageFromOneToConfiguredMaximum()
        {
            Assert.That(
                CalculateMultiplier(0f, 2f),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                CalculateMultiplier(0.5f, 2f),
                Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(
                CalculateMultiplier(1f, 2f),
                Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void DamageMultiplierNeverDropsBelowOne()
        {
            Assert.That(
                CalculateMultiplier(1f, 0.25f),
                Is.EqualTo(1f).Within(0.0001f));
        }

        private static float CalculateConductivity(
            float wetness,
            float activationThreshold,
            float fullEffectWetness)
        {
            return (float)Invoke(
                "CalculateConductivity01",
                new object[]
                {
                    wetness,
                    activationThreshold,
                    fullEffectWetness
                });
        }

        private static float CalculateMultiplier(
            float conductivity01,
            float maximumMultiplier)
        {
            return (float)Invoke(
                "CalculateDamageMultiplier",
                new object[]
                {
                    conductivity01,
                    maximumMultiplier
                });
        }

        private static object Invoke(
            string methodName,
            object[] arguments)
        {
            Type type = Type.GetType(ConductivityTypeName, true);
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, arguments);
        }
    }
}
