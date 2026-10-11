using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Glassfall.Tests
{
    public sealed class NeonWindowVideoBackgroundTests
    {
        private const string BackgroundTypeName =
            "Glassfall.Presentation.NeonWindowVideoBackground, Assembly-CSharp";
        private const string PresentationTypeName =
            "Glassfall.Presentation.NeonWindowWorldPresentation, Assembly-CSharp";

        [Test]
        public void ActivationGateOnlyAllowsConfiguredNeonWorld()
        {
            Type type = Type.GetType(BackgroundTypeName, true);
            Assert.That(
                (bool)InvokeStatic(
                    type,
                    "ShouldPlayForWorld",
                    "NEON_WINDOW",
                    true,
                    true),
                Is.True);
            Assert.That(
                (bool)InvokeStatic(
                    type,
                    "ShouldPlayForWorld",
                    "FOGGED_WINDOW",
                    true,
                    true),
                Is.False);
            Assert.That(
                (bool)InvokeStatic(
                    type,
                    "ShouldPlayForWorld",
                    "NEON_WINDOW",
                    false,
                    true),
                Is.False);
            Assert.That(
                (bool)InvokeStatic(
                    type,
                    "ShouldPlayForWorld",
                    "NEON_WINDOW",
                    true,
                    false),
                Is.False);
        }

        [Test]
        public void FirstFrameGateRejectsBlankAndFailedVideo()
        {
            Type type = Type.GetType(BackgroundTypeName, true);
            Assert.That(
                (bool)InvokeStatic(
                    type,
                    "ShouldRevealFirstFrame",
                    true,
                    true,
                    0L,
                    false),
                Is.True);
            Assert.That(
                (bool)InvokeStatic(
                    type,
                    "ShouldRevealFirstFrame",
                    true,
                    false,
                    0L,
                    false),
                Is.False);
            Assert.That(
                (bool)InvokeStatic(
                    type,
                    "ShouldRevealFirstFrame",
                    true,
                    true,
                    -1L,
                    false),
                Is.False);
            Assert.That(
                (bool)InvokeStatic(
                    type,
                    "ShouldRevealFirstFrame",
                    true,
                    true,
                    0L,
                    true),
                Is.False);
        }

        [Test]
        public void MissingVideoDoesNotRemoveProceduralFallback()
        {
            GameObject root = new GameObject("NeonFallbackTest");
            GameObject surface = new GameObject("NeonWindowVideoSurface");
            surface.transform.SetParent(root.transform, false);
            try
            {
                Type presentationType = Type.GetType(
                    PresentationTypeName,
                    true);
                Type backgroundType = Type.GetType(BackgroundTypeName, true);
                root.AddComponent(presentationType);
                Component background = root.AddComponent(backgroundType);
                MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
                MeshFilter filter = surface.AddComponent<MeshFilter>();

                Invoke(
                    background,
                    "Configure",
                    null,
                    null,
                    null,
                    null,
                    renderer,
                    filter,
                    Shader.Find("Glassfall/NeonVideoBackground"));

                Assert.That(
                    (bool)GetProperty(background, "HasProceduralFallback"),
                    Is.True);
                Assert.That(
                    root.GetComponent(presentationType),
                    Is.Not.Null);
                Assert.That(renderer.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VideoReadyHidesProceduralRectanglesButKeepsFallback()
        {
            GameObject root = new GameObject("NeonLayerSwitchTest");
            GameObject runtimeVisuals = new GameObject("RuntimeNeonVisuals");
            runtimeVisuals.transform.SetParent(root.transform, false);
            try
            {
                Type presentationType = Type.GetType(
                    PresentationTypeName,
                    true);
                Component presentation = root.AddComponent(presentationType);
                SetPrivateField(
                    presentation,
                    "_runtimeRoot",
                    runtimeVisuals.transform);

                Invoke(
                    presentation,
                    "SetVideoBackgroundActive",
                    true);
                Assert.That(
                    (bool)GetProperty(
                        presentation,
                        "AreProceduralVisualsVisible"),
                    Is.False);
                Assert.That(runtimeVisuals, Is.Not.Null);

                Invoke(
                    presentation,
                    "SetVideoBackgroundActive",
                    false);
                Assert.That(
                    (bool)GetProperty(
                        presentation,
                        "AreProceduralVisualsVisible"),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }

        private static object Invoke(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, methodName);
            ParameterInfo[] parameters = method.GetParameters();
            object[] completed = new object[parameters.Length];
            for (int i = 0; i < completed.Length; i++)
            {
                completed[i] = i < arguments.Length
                    ? arguments[i]
                    : parameters[i].DefaultValue;
            }
            return method.Invoke(instance, completed);
        }

        private static object GetProperty(
            object instance,
            string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(instance);
        }

        private static void SetPrivateField(
            object instance,
            string fieldName,
            object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(instance, value);
        }
    }
}
