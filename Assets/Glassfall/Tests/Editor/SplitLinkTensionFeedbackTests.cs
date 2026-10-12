using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Glassfall.Tests
{
    public sealed class SplitLinkTensionFeedbackTests
    {
        private const string CombatTypeName =
            "Glassfall.Gameplay.Combat.SplitLinkCombat, Assembly-CSharp";

        [Test]
        public void InactiveEncounterStillShowsTensionForAValidSplitPair()
        {
            GameObject gameObject = new GameObject("TensionFeedbackTest");
            try
            {
                Component combat = AddCombat(gameObject);
                Invoke(
                    combat,
                    "ConfigureSurfaceTension",
                    1.5f,
                    5.5f,
                    9f,
                    0.45f);
                Invoke(
                    combat,
                    "RefreshSurfaceTensionDiagnosticsForPair",
                    Vector2.zero,
                    new Vector2(8f, 0f));
                float activeValue = (float)GetProperty(
                    combat,
                    "SurfaceTensionOverstretch01");

                Invoke(combat, "SetEncounterActive", false);
                Invoke(
                    combat,
                    "RefreshSurfaceTensionDiagnosticsForPair",
                    Vector2.zero,
                    new Vector2(8f, 0f));
                float inactiveValue = (float)GetProperty(
                    combat,
                    "SurfaceTensionOverstretch01");

                Assert.That(activeValue, Is.GreaterThan(0f));
                Assert.That(inactiveValue, Is.EqualTo(activeValue).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DistanceTransitionAndRemergeReturnToStableVisualState()
        {
            GameObject gameObject = new GameObject("TensionTransitionTest");
            try
            {
                Component combat = AddCombat(gameObject);
                Invoke(
                    combat,
                    "ConfigureSurfaceTension",
                    1.5f,
                    5.5f,
                    9f,
                    0.45f);

                Invoke(
                    combat,
                    "RefreshSurfaceTensionDiagnosticsForPair",
                    Vector2.zero,
                    new Vector2(8f, 0f));
                Assert.That(
                    (string)GetProperty(combat, "CurrentSurfaceTensionBand"),
                    Is.EqualTo("Overstretched"));

                Invoke(
                    combat,
                    "RefreshSurfaceTensionDiagnosticsForPair",
                    Vector2.zero,
                    new Vector2(4f, 0f));
                Assert.That(
                    (string)GetProperty(combat, "CurrentSurfaceTensionBand"),
                    Is.EqualTo("Stable"));

                Invoke(
                    combat,
                    "RefreshSurfaceTensionDiagnosticsForPair",
                    Vector2.zero,
                    Vector2.zero);
                Assert.That(
                    (float)GetProperty(combat, "SurfaceTensionOverstretch01"),
                    Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static Component AddCombat(GameObject gameObject)
        {
            return gameObject.AddComponent(
                Type.GetType(CombatTypeName, true));
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
            object value = property.GetValue(instance);
            return value is Enum ? value.ToString() : value;
        }
    }
}
