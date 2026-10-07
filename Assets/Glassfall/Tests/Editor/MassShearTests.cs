using System;
using System.Reflection;
using NUnit.Framework;

namespace Glassfall.Tests
{
    public sealed class MassShearTests
    {
        private const string ShearTypeName =
            "Glassfall.Gameplay.Combat.MassShearSystem, Assembly-CSharp";
        private const string MergeTypeName =
            "Glassfall.Gameplay.Droplet.DropletMergeSystem, Assembly-CSharp";

        [Test]
        public void SplitHitRequestsMoreMassThanMergedHit()
        {
            float merged = GetRequestedShearMass(false, 0.5f, 1f);
            float split = GetRequestedShearMass(true, 0.5f, 1f);

            Assert.That(merged, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(split, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(split, Is.GreaterThan(merged));
        }

        [Test]
        public void ShearNeverDropsBelowMinimumRetainedMass()
        {
            float sheared = CalculateShearedMass(1.4f, 1f, 1f);

            Assert.That(sheared, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void ShearReturnsZeroAtMinimumRetainedMass()
        {
            float sheared = CalculateShearedMass(1f, 1f, 1f);

            Assert.That(sheared, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void NegativeShearInputsCannotCreateMassLoss()
        {
            float requested = GetRequestedShearMass(false, -5f, -10f);
            float sheared = CalculateShearedMass(10f, requested, 1f);

            Assert.That(requested, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(sheared, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void RecoveryFragmentReturnsExactShearedMassInEitherForm()
        {
            float mergedRecovery = CalculateCollectionMassGain(
                1f,
                false,
                true,
                1.25f,
                0.6f);
            float splitRecovery = CalculateCollectionMassGain(
                1f,
                true,
                true,
                1.25f,
                0.6f);

            Assert.That(mergedRecovery, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(splitRecovery, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ContactRangeIncludesBothBodyRadiiAndPadding()
        {
            Assert.That(
                IsWithinContactRange(1.02f, 0.55f, 0.32f, 0.16f),
                Is.True);
            Assert.That(
                IsWithinContactRange(1.04f, 0.55f, 0.32f, 0.16f),
                Is.False);
        }

        private static float GetRequestedShearMass(
            bool isSplit,
            float mergedLoss,
            float splitLoss)
        {
            return (float)InvokeStatic(
                ShearTypeName,
                "GetRequestedShearMass",
                isSplit,
                mergedLoss,
                splitLoss);
        }

        private static float CalculateShearedMass(
            float currentMass,
            float requestedShear,
            float minimumMass)
        {
            return (float)InvokeStatic(
                ShearTypeName,
                "CalculateShearedMass",
                currentMass,
                requestedShear,
                minimumMass);
        }

        private static bool IsWithinContactRange(
            float distance,
            float dropletRadius,
            float hunterRadius,
            float padding)
        {
            return (bool)InvokeStatic(
                ShearTypeName,
                "IsWithinContactRange",
                distance,
                dropletRadius,
                hunterRadius,
                padding);
        }

        private static float CalculateCollectionMassGain(
            float sourceMass,
            bool isSplit,
            bool isRecovery,
            float mergedMultiplier,
            float splitMultiplier)
        {
            return (float)InvokeStatic(
                MergeTypeName,
                "CalculateCollectionMassGain",
                sourceMass,
                isSplit,
                isRecovery,
                mergedMultiplier,
                splitMultiplier);
        }

        private static object InvokeStatic(
            string typeName,
            string methodName,
            params object[] arguments)
        {
            Type type = Type.GetType(typeName, true);
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }
    }
}
