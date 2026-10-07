using System;
using System.Reflection;
using NUnit.Framework;

namespace Glassfall.Tests
{
    public sealed class CameraFeedbackTests
    {
        private const string CameraMathTypeName =
            "Glassfall.Presentation.CameraFeedbackMath, Assembly-CSharp";

        [Test]
        public void CameraEnvelopeStartsAndEndsAtZero()
        {
            Assert.That(
                EvaluateEnvelope(0f),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                EvaluateEnvelope(1f),
                Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void CameraEnvelopeRemainsBounded()
        {
            for (int i = 0; i <= 100; i++)
            {
                float value = EvaluateEnvelope(i / 100f);
                Assert.That(value, Is.InRange(-1f, 1f));
            }
        }

        [Test]
        public void LargerCollectMassProducesStrongerButClampedAmplitude()
        {
            const float baseAmplitude = 0.025f;
            const float maxAmplitude = 0.075f;

            float small = ComputeCollectAmplitude(
                baseAmplitude,
                0.5f,
                maxAmplitude);
            float large = ComputeCollectAmplitude(
                baseAmplitude,
                3.5f,
                maxAmplitude);

            Assert.That(large, Is.GreaterThan(small));
            Assert.That(small, Is.GreaterThan(0f));
            Assert.That(large, Is.LessThanOrEqualTo(maxAmplitude));
        }

        [Test]
        public void CameraAmplitudeClampRejectsNegativeAndCapsLargeValues()
        {
            Assert.That(
                ClampAmplitude(-1f, 0.075f),
                Is.Zero);
            Assert.That(
                ClampAmplitude(1f, 0.075f),
                Is.EqualTo(0.075f).Within(0.0001f));
        }

        private static float EvaluateEnvelope(float normalizedTime)
        {
            return InvokeFloat(
                "EvaluateEnvelope",
                normalizedTime);
        }

        private static float ComputeCollectAmplitude(
            float baseAmplitude,
            float consumedMass,
            float maxAmplitude)
        {
            return InvokeFloat(
                "ComputeCollectAmplitude",
                baseAmplitude,
                consumedMass,
                maxAmplitude);
        }

        private static float ClampAmplitude(
            float amplitude,
            float maxAmplitude)
        {
            return InvokeFloat(
                "ClampAmplitude",
                amplitude,
                maxAmplitude);
        }

        private static float InvokeFloat(
            string methodName,
            params object[] arguments)
        {
            Type type = Type.GetType(CameraMathTypeName, true);
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(null, arguments);
        }
    }
}
