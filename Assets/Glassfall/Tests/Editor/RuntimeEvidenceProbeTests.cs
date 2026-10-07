using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Glassfall.Tests
{
    public sealed class RuntimeEvidenceProbeTests
    {
        private const string ProbeTypeName =
            "Glassfall.Diagnostics.RuntimeEvidenceProbe, Assembly-CSharp";

        [Test]
        public void Percentile_UsesObservedFrameSamples()
        {
            Type type = Type.GetType(ProbeTypeName);
            Assert.That(type, Is.Not.Null);

            MethodInfo method = type.GetMethod(
                "CalculatePercentileMilliseconds",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            float[] samples =
            {
                10f, 11f, 12f, 13f, 14f,
                15f, 16f, 17f, 18f, 40f
            };

            float p95 = (float)method.Invoke(
                null,
                new object[] { samples, samples.Length, 0.95f });

            Assert.That(p95, Is.EqualTo(40f));
        }

        [Test]
        public void Percentile_EmptyInput_ReturnsZero()
        {
            Type type = Type.GetType(ProbeTypeName);
            Assert.That(type, Is.Not.Null);

            MethodInfo method = type.GetMethod(
                "CalculatePercentileMilliseconds",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            float result = (float)method.Invoke(
                null,
                new object[] { Array.Empty<float>(), 0, 0.95f });

            Assert.That(result, Is.Zero);
        }

        [Test]
        public void DeveloperControls_PreserveF8F9F10Actions()
        {
            Type type = Type.GetType(ProbeTypeName, true);
            MethodInfo method = type.GetMethod(
                "ResolveDeveloperControls",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            object result = method.Invoke(
                null,
                new object[] { true, true, true });

            Assert.That(Convert.ToInt32(result), Is.EqualTo(7));
        }

        [Test]
        public void ReportRefresh_IncludesSnapshotsCapturedAfterTerminalWrite()
        {
            Type type = Type.GetType(ProbeTypeName, true);
            string directory = Path.Combine(
                Path.GetTempPath(),
                "GlassfallEvidenceTest_" + Guid.NewGuid().ToString("N"));
            GameObject gameObject = new GameObject("RuntimeEvidenceProbeTest");

            try
            {
                Component probe = gameObject.AddComponent(type);
                SetField(probe, "_sessionDirectory", directory);
                SetField(probe, "_sceneRunNumber", 1);

                Invoke(probe, "CaptureSnapshot", "run_complete");
                Invoke(probe, "WriteReport");
                Invoke(probe, "CaptureSnapshot", "manual");
                Invoke(probe, "WriteReport");
                Invoke(probe, "CaptureSnapshot", "application_quit");
                Invoke(probe, "WriteReport");

                string csvPath = Path.Combine(
                    directory,
                    "run_01_summary.csv");
                Assert.That(File.Exists(csvPath), Is.True);
                string csv = File.ReadAllText(csvPath);
                Assert.That(csv, Does.Contain("run_complete"));
                Assert.That(csv, Does.Contain("manual"));
                Assert.That(csv, Does.Contain("application_quit"));
                Assert.That(
                    File.Exists(csvPath + ".tmp"),
                    Is.False,
                    "Atomic refresh must not leave a partial report behind.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SceneRunNumber_AdvancesAcrossSameProcessReloads()
        {
            Type type = Type.GetType(ProbeTypeName, true);
            MethodInfo method = type.GetMethod(
                "CalculateNextSceneRunNumber",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            int run = 0;
            run = (int)method.Invoke(null, new object[] { run });
            Assert.That(run, Is.EqualTo(1));
            run = (int)method.Invoke(null, new object[] { run });
            Assert.That(run, Is.EqualTo(2));
            run = (int)method.Invoke(null, new object[] { run });
            Assert.That(run, Is.EqualTo(3));
        }

        private static void Invoke(object instance, string methodName)
        {
            Invoke(instance, methodName, Array.Empty<object>());
        }

        private static void Invoke(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(instance, arguments);
        }

        private static void SetField(
            object instance,
            string fieldName,
            object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(instance, value);
        }
    }
}
