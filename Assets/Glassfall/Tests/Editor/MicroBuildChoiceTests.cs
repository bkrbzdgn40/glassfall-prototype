using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Glassfall.Tests
{
    public sealed class MicroBuildChoiceTests
    {
        private const string CombatTypeName =
            "Glassfall.Gameplay.Combat.SplitLinkCombat, Assembly-CSharp";
        private const string ControllerTypeName =
            "Glassfall.Gameplay.Builds.MicroBuildChoiceController, Assembly-CSharp";
        private const string ChoiceTypeName =
            "Glassfall.Gameplay.Builds.MicroBuildChoice, Assembly-CSharp";

        private GameObject _gameObject;
        private Component _combat;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("MicroBuildChoiceTests");
            _combat = AddComponent(CombatTypeName);
            Invoke(_combat, "ResetRuntimeBuildTuning");
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void BaselineTuningIsUnchangedWithoutBuildChoice()
        {
            Assert.That(GetFloat(_combat, "EndpointInset"), Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "HitPadding"), Is.EqualTo(0.08f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "DamagePerSecond"), Is.EqualTo(7f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "ConductivityActivationThreshold"), Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "FullEffectWetness"), Is.EqualTo(0.50f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "FullConductivityDamageMultiplier"), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(GetBool(_combat, "CapacitorEnabled"), Is.False);
        }

        [Test]
        public void ConductiveFocusMakesWetnessEarlierAndStronger()
        {
            Invoke(_combat, "ApplyConductiveFocusTuning");

            Assert.That(GetFloat(_combat, "DamagePerSecond"), Is.EqualTo(7f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "ConductivityActivationThreshold"), Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "FullEffectWetness"), Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "FullConductivityDamageMultiplier"), Is.EqualTo(2.4f).Within(0.0001f));
        }

        [Test]
        public void BroadArcWidensCentralHitButReducesBaseDamage()
        {
            float endpointInsetBefore = GetFloat(_combat, "EndpointInset");
            Invoke(_combat, "ApplyBroadArcTuning");

            Assert.That(GetFloat(_combat, "HitPadding"), Is.EqualTo(0.17f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "DamagePerSecond"), Is.EqualTo(5.775f).Within(0.001f));
            Assert.That(GetFloat(_combat, "EndpointInset"), Is.EqualTo(endpointInsetBefore).Within(0.0001f));
            Assert.That(GetFloat(_combat, "ConductivityActivationThreshold"), Is.EqualTo(0.15f).Within(0.0001f));
        }

        [Test]
        public void CapacitorChargesOnlyAfterDelayAndResetsWhenSplitEnds()
        {
            Invoke(_combat, "ConfigureCapacitor");
            Assert.That(GetBool(_combat, "CapacitorEnabled"), Is.True);
            Assert.That(GetFloat(_combat, "CapacitorDamageMultiplier"), Is.EqualTo(1f).Within(0.0001f));

            Invoke(_combat, "AdvanceCapacitor", true, 1.5f);
            Assert.That(GetFloat(_combat, "CapacitorCharge01"), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "CapacitorDamageMultiplier"), Is.EqualTo(1f).Within(0.0001f));

            Invoke(_combat, "AdvanceCapacitor", true, 1.25f);
            Assert.That(GetFloat(_combat, "CapacitorCharge01"), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "CapacitorDamageMultiplier"), Is.EqualTo(1.3f).Within(0.0001f));

            Invoke(_combat, "AdvanceCapacitor", true, 1.25f);
            Assert.That(GetFloat(_combat, "CapacitorCharge01"), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "CapacitorDamageMultiplier"), Is.EqualTo(1.6f).Within(0.0001f));

            Invoke(_combat, "AdvanceCapacitor", false, 0.02f);
            Assert.That(GetFloat(_combat, "CapacitorCharge01"), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(GetFloat(_combat, "CapacitorDamageMultiplier"), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ControllerAcceptsOnlyOneChoicePerSession()
        {
            Component controller = AddComponent(ControllerTypeName);
            Invoke(controller, "Configure", _combat);

            Type choiceType = Type.GetType(ChoiceTypeName, true);
            object conductive = Enum.Parse(choiceType, "ConductiveFocus");
            object broadArc = Enum.Parse(choiceType, "BroadArc");

            bool first = (bool)Invoke(controller, "TryChoose", conductive);
            bool second = (bool)Invoke(controller, "TryChoose", broadArc);

            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(GetBool(controller, "HasChoice"), Is.True);
            Assert.That(GetProperty(controller, "SelectedBuild").ToString(), Is.EqualTo("ConductiveFocus"));
        }

        private Component AddComponent(string typeName)
        {
            Type type = Type.GetType(typeName, true);
            return _gameObject.AddComponent(type);
        }

        private static object Invoke(Component component, string methodName, params object[] arguments)
        {
            MethodInfo method = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);

            ParameterInfo[] parameters = method.GetParameters();
            Assert.That(
                arguments.Length,
                Is.LessThanOrEqualTo(parameters.Length),
                $"{methodName} received too many arguments.");

            if (arguments.Length == parameters.Length)
                return method.Invoke(component, arguments);

            object[] completedArguments = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < arguments.Length)
                {
                    completedArguments[i] = arguments[i];
                    continue;
                }

                Assert.That(
                    parameters[i].IsOptional,
                    Is.True,
                    $"{methodName} is missing required parameter '{parameters[i].Name}'.");

                completedArguments[i] = parameters[i].DefaultValue;
            }

            return method.Invoke(component, completedArguments);
        }

        private static object GetProperty(Component component, string propertyName)
        {
            PropertyInfo property = component.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(component);
        }

        private static float GetFloat(Component component, string propertyName)
        {
            return (float)GetProperty(component, propertyName);
        }

        private static bool GetBool(Component component, string propertyName)
        {
            return (bool)GetProperty(component, propertyName);
        }
    }
}
