using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Glassfall.Tests
{
    public sealed class SplitSeparationConstraintTests
    {
        private const string SplitTypeName =
            "Glassfall.Gameplay.Droplet.DropletSplitSystem, Assembly-CSharp";

        [Test]
        public void OutwardVelocityIsReducedBeforeTheMaximum()
        {
            float allowed = CalculateAllowedOutwardSpeed(
                separation: 9f,
                outwardSpeed: 5f,
                resistanceStartDistance: 8f,
                maximumDistance: 10f,
                deltaTime: 0.02f);

            Assert.That(allowed, Is.GreaterThan(0f));
            Assert.That(allowed, Is.LessThan(5f));
        }

        [Test]
        public void OutwardVelocityIsBlockedAtTheMaximum()
        {
            Assert.That(
                CalculateAllowedOutwardSpeed(
                    10f,
                    5f,
                    8f,
                    10f,
                    0.02f),
                Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void InwardVelocityRemainsUnrestricted()
        {
            Assert.That(
                CalculateAllowedOutwardSpeed(
                    9.5f,
                    -4f,
                    8f,
                    10f,
                    0.02f),
                Is.EqualTo(-4f).Within(0.0001f));
        }

        [Test]
        public void TangentialVelocityIsPreservedNearTheLimit()
        {
            Vector2 constrained = CalculateConstrainedRelativeVelocity(
                new Vector2(9f, 0f),
                new Vector2(5f, 3f),
                8f,
                10f,
                0.02f);

            Assert.That(constrained.x, Is.GreaterThan(0f));
            Assert.That(constrained.x, Is.LessThan(5f));
            Assert.That(constrained.y, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void BlockedPartnerCannotBePulledFartherApartAtTheCap()
        {
            Vector2 constrained = CalculateConstrainedRelativeVelocity(
                new Vector2(10f, 0f),
                new Vector2(5f, 2f),
                8f,
                10f,
                0.02f);

            Assert.That(constrained.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(constrained.y, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void RemergedPairHasNoResidualTetherResistance()
        {
            GameObject gameObject = new GameObject("SplitRemergeStateTest");
            try
            {
                Component split = gameObject.AddComponent(
                    Type.GetType(SplitTypeName, true));

                Assert.That(
                    (float)GetProperty(split, "CurrentSeparation"),
                    Is.EqualTo(0f).Within(0.0001f));
                Assert.That(
                    (float)GetProperty(split, "SeparationResistance01"),
                    Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ElasticMembraneExtendsResistanceAndMaximumTogether()
        {
            GameObject gameObject = new GameObject("SplitSeparationLimitTest");
            try
            {
                Type splitType = Type.GetType(SplitTypeName, true);
                Component split = gameObject.AddComponent(splitType);
                Invoke(split, "ConfigureSeparationLimit", 8f, 10f);
                Invoke(split, "ResetRuntimeRewardTuning");
                Invoke(split, "ApplyElasticMembraneSeparationBonus", 1.1f);

                Assert.That(
                    (float)GetProperty(
                        split,
                        "SeparationResistanceStartDistance"),
                    Is.EqualTo(9.1f).Within(0.0001f));
                Assert.That(
                    (float)GetProperty(split, "MaximumSeparationDistance"),
                    Is.EqualTo(11.1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ResetRewardTuningRestoresBaselineLimit()
        {
            GameObject gameObject = new GameObject("SplitSeparationResetTest");
            try
            {
                Type splitType = Type.GetType(SplitTypeName, true);
                Component split = gameObject.AddComponent(splitType);
                Invoke(split, "ApplyElasticMembraneSeparationBonus", 1.1f);
                Invoke(split, "ResetRuntimeRewardTuning");

                Assert.That(
                    (float)GetProperty(split, "MaximumSeparationDistance"),
                    Is.EqualTo(10f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static float CalculateAllowedOutwardSpeed(
            float separation,
            float outwardSpeed,
            float resistanceStartDistance,
            float maximumDistance,
            float deltaTime)
        {
            Type type = Type.GetType(SplitTypeName, true);
            MethodInfo method = type.GetMethod(
                "CalculateAllowedOutwardSpeed",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(
                null,
                new object[]
                {
                    separation,
                    outwardSpeed,
                    resistanceStartDistance,
                    maximumDistance,
                    deltaTime
                });
        }

        private static Vector2 CalculateConstrainedRelativeVelocity(
            Vector2 separationVector,
            Vector2 relativeVelocity,
            float resistanceStartDistance,
            float maximumDistance,
            float deltaTime)
        {
            Type type = Type.GetType(SplitTypeName, true);
            MethodInfo method = type.GetMethod(
                "CalculateConstrainedRelativeVelocity",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (Vector2)method.Invoke(
                null,
                new object[]
                {
                    separationVector,
                    relativeVelocity,
                    resistanceStartDistance,
                    maximumDistance,
                    deltaTime
                });
        }

        private static object Invoke(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(instance, arguments);
        }

        private static object GetProperty(
            object instance,
            string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(instance);
        }
    }
}
