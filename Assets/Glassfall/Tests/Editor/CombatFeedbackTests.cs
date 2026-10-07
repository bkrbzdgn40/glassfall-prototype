using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Glassfall.Tests.Editor
{
    public sealed class CombatFeedbackTests
    {
        private const string SplitLinkCombatTypeName =
            "Glassfall.Gameplay.Combat.SplitLinkCombat, Assembly-CSharp";
        private const string ArcHunterTypeName =
            "Glassfall.Gameplay.Combat.ArcHunter, Assembly-CSharp";

        [Test]
        public void AppliedOutputIsZeroWhenLinkIsNotActuallyHitting()
        {
            Type combatType = RequireType(SplitLinkCombatTypeName);
            GameObject gameObject = new GameObject("CombatFeedbackOutputTest");

            try
            {
                Component combat = gameObject.AddComponent(combatType);

                InvokePrivate(
                    combat,
                    "UpdateDiagnostics",
                    0.5f,
                    1f,
                    2f,
                    false,
                    14f);

                Assert.That(
                    ReadFloatProperty(
                        combat,
                        "CurrentAppliedOutputPerSecond"),
                    Is.EqualTo(0f).Within(0.0001f));
                Assert.That(
                    ReadBoolProperty(
                        combat,
                        "IsApplyingLinkDamage"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AppliedOutputReportsAuthoritativeValueWhileLinkIsHitting()
        {
            Type combatType = RequireType(SplitLinkCombatTypeName);
            GameObject gameObject = new GameObject("CombatFeedbackHitTest");

            try
            {
                Component combat = gameObject.AddComponent(combatType);

                InvokePrivate(
                    combat,
                    "UpdateDiagnostics",
                    0.5f,
                    1f,
                    2.1f,
                    true,
                    14.7f);

                Assert.That(
                    ReadFloatProperty(
                        combat,
                        "CurrentAppliedOutputPerSecond"),
                    Is.EqualTo(14.7f).Within(0.0001f));
                Assert.That(
                    ReadBoolProperty(
                        combat,
                        "IsApplyingLinkDamage"),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void HunterHealth01TracksDamageAndReset()
        {
            Type hunterType = RequireType(ArcHunterTypeName);
            GameObject gameObject = new GameObject("HunterHealthReadabilityTest");

            try
            {
                Component hunter = gameObject.AddComponent(hunterType);
                InvokePublic(hunter, "ResetHunter");

                Assert.That(
                    ReadFloatProperty(hunter, "Health01"),
                    Is.EqualTo(1f).Within(0.0001f));

                InvokePublic(hunter, "ApplyLinkDamage", 2.5f);

                Assert.That(
                    ReadFloatProperty(hunter, "Health01"),
                    Is.EqualTo(0.75f).Within(0.0001f));

                InvokePublic(hunter, "ResetHunter");

                Assert.That(
                    ReadFloatProperty(hunter, "Health01"),
                    Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static Type RequireType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName);
            Assert.That(
                type,
                Is.Not.Null,
                "Runtime type bulunamadı: " + assemblyQualifiedName);
            return type;
        }

        private static void InvokePrivate(
            Component component,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(
                method,
                Is.Not.Null,
                "Private method bulunamadı: " + methodName);
            method.Invoke(component, arguments);
        }

        private static void InvokePublic(
            Component component,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(
                method,
                Is.Not.Null,
                "Public method bulunamadı: " + methodName);
            method.Invoke(component, arguments);
        }

        private static float ReadFloatProperty(
            Component component,
            string propertyName)
        {
            object value = ReadProperty(component, propertyName);
            Assert.That(value, Is.TypeOf<float>());
            return (float)value;
        }

        private static bool ReadBoolProperty(
            Component component,
            string propertyName)
        {
            object value = ReadProperty(component, propertyName);
            Assert.That(value, Is.TypeOf<bool>());
            return (bool)value;
        }

        private static object ReadProperty(
            Component component,
            string propertyName)
        {
            PropertyInfo property = component.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(
                property,
                Is.Not.Null,
                "Public property bulunamadı: " + propertyName);
            return property.GetValue(component);
        }
    }
}
