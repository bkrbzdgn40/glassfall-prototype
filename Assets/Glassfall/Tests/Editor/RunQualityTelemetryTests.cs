using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Glassfall.Tests
{
    public sealed class RunQualityTelemetryTests
    {
        private const string SessionTypeName =
            "Glassfall.Diagnostics.RunQualityTelemetrySession, Assembly-CSharp";
        private const string ProbeTypeName =
            "Glassfall.Diagnostics.RuntimeEvidenceProbe, Assembly-CSharp";

        [Test]
        public void EncounterAndWorldTransitionsCaptureTwoWorldRun()
        {
            object session = CreateSession("transition-run");
            Assert.That((bool)Invoke(
                session,
                "Begin",
                0d,
                "FOGGED_WINDOW",
                0), Is.True);

            Observe(
                session,
                1d,
                "Encounter",
                "FOGGED_WINDOW",
                0,
                1,
                1,
                0.10f,
                10f);
            Observe(
                session,
                4d,
                "RewardChoice",
                "FOGGED_WINDOW",
                0,
                1,
                1,
                0.20f,
                9f);
            Observe(
                session,
                6d,
                "Encounter",
                "NEON_WINDOW",
                1,
                4,
                1,
                0.30f,
                11f);
            Assert.That((bool)Invoke(
                session,
                "FinalizeRun",
                10d,
                "Complete",
                string.Empty,
                "NEON_WINDOW",
                6,
                0.40f,
                8f), Is.True);

            IList worlds = (IList)GetProperty(session, "Worlds");
            IList encounters = (IList)GetProperty(session, "Encounters");
            Assert.That(worlds.Count, Is.EqualTo(2));
            Assert.That(
                GetProperty(worlds[0], "WorldId").ToString(),
                Is.EqualTo("FOGGED_WINDOW"));
            Assert.That(
                (double)GetProperty(worlds[0], "DurationSeconds"),
                Is.EqualTo(6d));
            Assert.That(
                GetProperty(worlds[1], "WorldId").ToString(),
                Is.EqualTo("NEON_WINDOW"));
            Assert.That(encounters.Count, Is.EqualTo(2));
            Assert.That(
                (int)GetProperty(encounters[1], "GlobalEncounter"),
                Is.EqualTo(4));
            Assert.That(
                (int)GetProperty(encounters[1], "WorldEncounter"),
                Is.EqualTo(1));
        }

        [Test]
        public void FormAndLinkUsageAccumulateWithoutStringSampling()
        {
            object session = CreateSession("usage-run");
            Invoke(session, "Begin", 0d, "FOGGED_WINDOW", 0);
            Invoke(
                session,
                "SampleUsage",
                2d,
                false,
                false,
                false,
                0f);
            Invoke(
                session,
                "SampleUsage",
                3d,
                true,
                true,
                true,
                5f);
            Invoke(session, "RecordSplitTransition");
            Invoke(session, "RecordMergeTransition");

            Assert.That(
                (double)GetProperty(session, "MergedSeconds"),
                Is.EqualTo(2d));
            Assert.That(
                (double)GetProperty(session, "SplitSeconds"),
                Is.EqualTo(3d));
            Assert.That(
                (double)GetProperty(session, "LinkDamageActiveSeconds"),
                Is.EqualTo(3d));
            Assert.That(
                (double)GetProperty(session, "ConductionActiveSeconds"),
                Is.EqualTo(3d));
            Assert.That(
                (double)GetProperty(session, "IntegratedAppliedOutput"),
                Is.EqualTo(15d));
            Assert.That(
                (int)GetProperty(session, "SplitTransitionCount"),
                Is.EqualTo(1));
            Assert.That(
                (int)GetProperty(session, "MergeTransitionCount"),
                Is.EqualTo(1));
        }

        [Test]
        public void RewardSelectionKeepsTheOfferedTriple()
        {
            object session = CreateSession("reward-run");
            Invoke(session, "Begin", 0d, "FOGGED_WINDOW", 0);
            Invoke(
                session,
                "RecordRewardOffer",
                2,
                "StableLink",
                "DenseCore",
                "SlowEvaporation");
            Invoke(
                session,
                "RecordRewardSelection",
                2,
                "DenseCore");

            IList rewards = (IList)GetProperty(session, "Rewards");
            Assert.That(rewards.Count, Is.EqualTo(1));
            Assert.That(
                (int)GetProperty(rewards[0], "RewardStep"),
                Is.EqualTo(2));
            Assert.That(
                GetProperty(rewards[0], "SelectedReward").ToString(),
                Is.EqualTo("DenseCore"));
            Assert.That(
                GetProperty(rewards[0], "OfferedRewards").ToString(),
                Is.EqualTo("StableLink|DenseCore|SlowEvaporation"));
        }

        [Test]
        public void CompleteAndFailedOutcomesCaptureTerminalContext()
        {
            object complete = CreateSession("complete-run");
            Invoke(complete, "Begin", 1d, "FOGGED_WINDOW", 0);
            Assert.That((bool)Invoke(
                complete,
                "FinalizeRun",
                11d,
                "Complete",
                string.Empty,
                "NEON_WINDOW",
                6,
                0.55f,
                12f), Is.True);
            Assert.That(GetProperty(complete, "Outcome").ToString(),
                Is.EqualTo("Complete"));
            Assert.That(
                (double)GetProperty(complete, "TotalDurationSeconds"),
                Is.EqualTo(10d));

            object failed = CreateSession("failed-run");
            Invoke(failed, "Begin", 2d, "FOGGED_WINDOW", 0);
            Assert.That((bool)Invoke(
                failed,
                "FinalizeRun",
                8d,
                "Failed",
                "Encounter",
                "FOGGED_WINDOW",
                2,
                1f,
                5f), Is.True);
            Assert.That(GetProperty(failed, "Outcome").ToString(),
                Is.EqualTo("Failed"));
            Assert.That(GetProperty(failed, "FailurePhase").ToString(),
                Is.EqualTo("Encounter"));
            Assert.That(
                (float)GetProperty(failed, "FinalContamination"),
                Is.EqualTo(1f));
        }

        [Test]
        public void FinalizationIsIdempotentAndCsvContainsOneRunRow()
        {
            object session = CreateSession("single-final-run");
            Invoke(session, "Begin", 0d, "FOGGED_WINDOW", 0);
            Assert.That((bool)Invoke(
                session,
                "FinalizeRun",
                5d,
                "Complete",
                string.Empty,
                "NEON_WINDOW",
                6,
                0.25f,
                14f), Is.True);
            Assert.That((bool)Invoke(
                session,
                "FinalizeRun",
                7d,
                "Failed",
                "Encounter",
                "NEON_WINDOW",
                6,
                1f,
                1f), Is.False);

            string path = Path.Combine(
                Path.GetTempPath(),
                "GlassfallM27_" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                using (StreamWriter writer = new StreamWriter(path, false))
                    Invoke(session, "WriteCsv", writer);

                string[] lines = File.ReadAllLines(path);
                int runRows = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("RUN,", StringComparison.Ordinal))
                        runRows++;
                }

                Assert.That(runRows, Is.EqualTo(1));
                Assert.That(File.ReadAllText(path), Does.Contain("Complete"));
                Assert.That(File.ReadAllText(path), Does.Not.Contain("Failed"));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void QualityTelemetryGateRejectsReleaseRuntime()
        {
            Type type = Type.GetType(ProbeTypeName, true);
            MethodInfo method = type.GetMethod(
                "IsQualityTelemetryEnabledFor",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(
                (bool)method.Invoke(null, new object[] { true, false }),
                Is.True);
            Assert.That(
                (bool)method.Invoke(null, new object[] { false, true }),
                Is.True);
            Assert.That(
                (bool)method.Invoke(null, new object[] { false, false }),
                Is.False);
        }

        private static object CreateSession(string runId)
        {
            Type type = Type.GetType(SessionTypeName, true);
            return Activator.CreateInstance(type, runId);
        }

        private static void Observe(
            object session,
            double now,
            string phase,
            string worldId,
            int worldIndex,
            int globalEncounter,
            int worldEncounter,
            float contamination,
            float mass)
        {
            Invoke(
                session,
                "ObserveState",
                now,
                phase,
                worldId,
                worldIndex,
                globalEncounter,
                worldEncounter,
                contamination,
                mass);
        }

        private static object Invoke(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
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
