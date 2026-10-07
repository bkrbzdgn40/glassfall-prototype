using System;
using System.Reflection;
using NUnit.Framework;

namespace Glassfall.Tests
{
    public sealed class MergedFormIdentityTests
    {
        private const string IdentityTypeName =
            "Glassfall.Gameplay.Droplet.MergedFormIdentity, Assembly-CSharp";

        [Test]
        public void MergedAbilitiesAreDisabledForSplitForm()
        {
            Assert.That(CanUseMergedAbilities(false), Is.True);
            Assert.That(CanUseMergedAbilities(true), Is.False);
        }

        [Test]
        public void AttractionStopsOutsideConfiguredRadius()
        {
            Assert.That(
                CalculateAttractionSpeed(1.25f, 1.25f, 1.6f),
                Is.Zero);
            Assert.That(
                CalculateAttractionSpeed(2f, 1.25f, 1.6f),
                Is.Zero);
        }

        [Test]
        public void AttractionGetsStrongerCloserToMergedBody()
        {
            float far = CalculateAttractionSpeed(1f, 1.25f, 1.6f);
            float near = CalculateAttractionSpeed(0.2f, 1.25f, 1.6f);

            Assert.That(far, Is.GreaterThan(0f));
            Assert.That(near, Is.GreaterThan(far));
            Assert.That(near, Is.LessThanOrEqualTo(1.6f));
        }

        [Test]
        public void BodyShoveScalesWithMassButRemainsCapped()
        {
            float baseline = CalculateBodyShoveSpeed(
                10f,
                10f,
                3.2f,
                0.35f,
                1.75f);
            float heavier = CalculateBodyShoveSpeed(
                20f,
                10f,
                3.2f,
                0.35f,
                1.75f);
            float extreme = CalculateBodyShoveSpeed(
                100000f,
                10f,
                3.2f,
                0.35f,
                1.75f);

            Assert.That(baseline, Is.EqualTo(3.2f).Within(0.0001f));
            Assert.That(heavier, Is.GreaterThan(baseline));
            Assert.That(extreme, Is.EqualTo(5.6f).Within(0.0001f));
        }

        [Test]
        public void InvalidTuningCannotCreateNegativeMovement()
        {
            Assert.That(
                CalculateAttractionSpeed(0.1f, -1f, -2f),
                Is.Zero);
            Assert.That(
                CalculateBodyShoveSpeed(10f, 10f, -3f, 0.35f, 1.75f),
                Is.Zero);
        }

        private static bool CanUseMergedAbilities(bool isSplitForm)
        {
            return (bool)Invoke(
                "CanUseMergedAbilities",
                isSplitForm);
        }

        private static float CalculateAttractionSpeed(
            float distanceFromSurface,
            float attractionRadius,
            float maximumSpeed)
        {
            return (float)Invoke(
                "CalculateAttractionSpeed",
                distanceFromSurface,
                attractionRadius,
                maximumSpeed,
                1.25f);
        }

        private static float CalculateBodyShoveSpeed(
            float currentMass,
            float referenceMass,
            float baseSpeed,
            float massExponent,
            float maximumMultiplier)
        {
            return (float)Invoke(
                "CalculateBodyShoveSpeed",
                currentMass,
                referenceMass,
                baseSpeed,
                massExponent,
                maximumMultiplier);
        }

        private static object Invoke(
            string methodName,
            params object[] arguments)
        {
            Type type = Type.GetType(IdentityTypeName, true);
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }
    }
}
