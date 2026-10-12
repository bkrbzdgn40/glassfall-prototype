using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Glassfall.Tests
{
    public sealed class SeededEncounterPlanTests
    {
        private const string BuilderTypeName =
            "Glassfall.Gameplay.Run.SeededEncounterPlanBuilder, Assembly-CSharp";
        private const string ControllerTypeName =
            "Glassfall.Gameplay.Run.MicroRunController, Assembly-CSharp";
        private const string SegmentTypeName =
            "Glassfall.Gameplay.Run.MicroRunWorldSegment, Assembly-CSharp";
        private const string ProgressTypeName =
            "Glassfall.Gameplay.Run.MicroRunProgress, Assembly-CSharp";
        private const string ModeTypeName =
            "Glassfall.Gameplay.Combat.ArcHunterMovementMode, Assembly-CSharp";
        private const string PoolTypeName =
            "Glassfall.Gameplay.Run.SeededEncounterCandidatePool, Assembly-CSharp";

        [Test]
        public void SameSeedProducesIdenticalPlan()
        {
            object first = BuildPlan(4182);
            object second = BuildPlan(4182);

            Assert.That(GetProperty(first, "Signature"),
                Is.EqualTo(GetProperty(second, "Signature")));
            Assert.That(GetProperty(first, "Count"), Is.EqualTo(6));
        }

        [Test]
        public void OneHundredSeedsProduceValidSixSlotPlansAndVariety()
        {
            HashSet<string> signatures = new HashSet<string>();
            HashSet<string> movementSequences = new HashSet<string>();
            HashSet<string> positionSequences = new HashSet<string>();
            for (int seed = 1; seed <= 100; seed++)
            {
                object plan = BuildPlan(seed);
                signatures.Add((string)GetProperty(plan, "Signature"));
                Assert.That(GetProperty(plan, "Count"), Is.EqualTo(6));

                Array entries = (Array)Invoke(plan, "CopyEntries");
                movementSequences.Add(BuildSequence(entries, "MovementMode"));
                positionSequences.Add(BuildPositionSequence(entries));
                Assert.That(GetEntryString(entries, 0, "WorldId"),
                    Is.EqualTo("FOGGED_WINDOW"));
                Assert.That(GetEntryString(entries, 3, "WorldId"),
                    Is.EqualTo("NEON_WINDOW"));
                Assert.That(GetEntryString(entries, 5, "WorldId"),
                    Is.EqualTo("NEON_WINDOW"));
                Assert.That(GetEntryInt(entries, 0, "EncounterIndex"),
                    Is.EqualTo(1));
                Assert.That(GetEntryInt(entries, 5, "EncounterIndex"),
                    Is.EqualTo(6));
                Assert.That(GetEntryInt(entries, 0, "LocalEncounterIndex"),
                    Is.EqualTo(0));
                Assert.That(GetEntryInt(entries, 2, "LocalEncounterIndex"),
                    Is.EqualTo(2));
                Assert.That(GetEntryInt(entries, 3, "LocalEncounterIndex"),
                    Is.EqualTo(0));
                Assert.That(GetEntryInt(entries, 5, "LocalEncounterIndex"),
                    Is.EqualTo(2));
                Assert.That(GetEntryString(entries, 0, "MovementMode"),
                    Is.EqualTo("Chase"));
                Assert.That(
                    new[]
                    {
                        "Strafe",
                        "WetRoutePressure"
                    },
                    Does.Contain(GetEntryString(entries, 1, "MovementMode")));
                Assert.That(
                    new[]
                    {
                        "Chase",
                        "WetRoutePressure"
                    },
                    Does.Contain(GetEntryString(entries, 2, "MovementMode")));
                Assert.That(
                    new[]
                    {
                        "Chase",
                        "Strafe"
                    },
                    Does.Contain(GetEntryString(entries, 3, "MovementMode")));
                Assert.That(
                    new[]
                    {
                        "Chase",
                        "Strafe",
                        "WetRoutePressure"
                    },
                    Does.Contain(GetEntryString(entries, 4, "MovementMode")));
                Assert.That(
                    new[]
                    {
                        "Chase",
                        "WetRoutePressure"
                    },
                    Does.Contain(GetEntryString(entries, 5, "MovementMode")));
                Assert.That(GetEntryString(entries, 0, "MovementMode"),
                    Is.Not.EqualTo(GetEntryString(
                        entries,
                        1,
                        "MovementMode")));
                Assert.That(GetEntryString(entries, 1, "MovementMode"),
                    Is.Not.EqualTo(GetEntryString(
                        entries,
                        2,
                        "MovementMode")));
                Assert.That(GetEntryString(entries, 3, "MovementMode"),
                    Is.Not.EqualTo(GetEntryString(
                        entries,
                        4,
                        "MovementMode")));
                Assert.That(GetEntryString(entries, 4, "MovementMode"),
                    Is.Not.EqualTo(GetEntryString(
                        entries,
                        5,
                        "MovementMode")));
            }

            Assert.That(signatures.Count, Is.GreaterThan(1));
            Assert.That(movementSequences.Count, Is.GreaterThan(4));
            Assert.That(positionSequences.Count, Is.GreaterThan(20));
        }

        [Test]
        public void HundredSeedVarietySeparatesMovementAndSpatialDiversity()
        {
            HashSet<string> movementSequences = new HashSet<string>();
            HashSet<string> positionSequences = new HashSet<string>();

            for (int seed = 1; seed <= 100; seed++)
            {
                Array entries = (Array)Invoke(
                    BuildPlan(seed),
                    "CopyEntries");
                movementSequences.Add(BuildSequence(entries, "MovementMode"));
                positionSequences.Add(BuildPositionSequence(entries));
            }

            Assert.That(movementSequences.Count, Is.GreaterThan(4));
            Assert.That(positionSequences.Count, Is.GreaterThan(20));
        }

        [Test]
        public void DefaultCandidatePoolsStayInsidePrototypeBounds()
        {
            Type builderType = Type.GetType(BuilderTypeName, true);
            MethodInfo createPool = builderType.GetMethod(
                "CreateDefaultCandidatePool",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(createPool, Is.Not.Null);

            Vector2[] authoredPositions =
            {
                new Vector2(-11.5f, -5.5f),
                new Vector2(0f, 7f),
                new Vector2(13f, 1.8f),
                new Vector2(-12f, 6.2f),
                new Vector2(6.8f, -6.4f),
                new Vector2(14.2f, 5.5f)
            };
            List<Vector2> allCandidates = new List<Vector2>();

            for (int slot = 0; slot < authoredPositions.Length; slot++)
            {
                object pool = createPool.Invoke(
                    null,
                    new object[] { slot, authoredPositions[slot] });
                Vector2[] positions = (Vector2[])Invoke(
                    pool,
                    "CopyPositions");
                Assert.That(positions.Length, Is.EqualTo(4));
                for (int i = 0; i < positions.Length; i++)
                {
                    Assert.That(positions[i].x, Is.InRange(-20.82f, 20.82f));
                    Assert.That(positions[i].y, Is.InRange(-11.82f, 11.82f));
                    allCandidates.Add(positions[i]);
                }
            }

            float minimumDistance = float.PositiveInfinity;
            for (int i = 0; i < allCandidates.Count; i++)
            {
                for (int j = i + 1; j < allCandidates.Count; j++)
                {
                    minimumDistance = Mathf.Min(
                        minimumDistance,
                        Vector2.Distance(
                            allCandidates[i],
                            allCandidates[j]));
                }
            }

            Assert.That(minimumDistance, Is.GreaterThan(0.75f));
        }

        [Test]
        public void InvalidCandidateFallsBackDeterministically()
        {
            object plan = BuildPlan(
                9,
                new Vector2[6],
                new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f)
                },
                singleCandidatePool: true);
            Array entries = (Array)Invoke(plan, "CopyEntries");

            Assert.That(GetEntryBool(entries, 0, "UsedFallback"), Is.False);
            Assert.That(GetEntryBool(entries, 1, "UsedFallback"), Is.True);
            Assert.That(GetEntryBool(entries, 5, "UsedFallback"), Is.True);
        }

        [Test]
        public void ControllerTargetMatchesActivePlanEntry()
        {
            GameObject gameObject = new GameObject("M28APlanControllerTest");
            try
            {
                Type controllerType = Type.GetType(ControllerTypeName, true);
                Type segmentType = Type.GetType(SegmentTypeName, true);
                Type modeType = Type.GetType(ModeTypeName, true);
                Component controller = gameObject.AddComponent(controllerType);

                object fogged = CreateSegment(
                    segmentType,
                    modeType,
                    "FOGGED_WINDOW",
                    new[]
                    {
                        new Vector2(-11f, -5f),
                        new Vector2(0f, 7f),
                        new Vector2(13f, 2f)
                    },
                    new[] { "Chase", "Strafe", "WetRoutePressure" });
                object neon = CreateSegment(
                    segmentType,
                    modeType,
                    "NEON_WINDOW",
                    new[]
                    {
                        new Vector2(-12f, 6f),
                        new Vector2(6f, -6f),
                        new Vector2(14f, 5f)
                    },
                    new[] { "Strafe", "WetRoutePressure", "Chase" });

                Array segments = Array.CreateInstance(segmentType, 2);
                segments.SetValue(fogged, 0);
                segments.SetValue(neon, 1);
                Invoke(controller, "ConfigureWorldSegments", segments);
                Invoke(controller, "ConfigureDevelopmentFixedSeed", 42);
                Invoke(controller, "RegenerateEncounterPlanForDevelopment");

                object[] targetArgs = { 1, Vector2.zero };
                Assert.That(
                    (bool)Invoke(controller, "TryGetEncounterTargetPosition", targetArgs),
                    Is.True);
                Vector2 target = (Vector2)targetArgs[1];

                object[] entryArgs = { 1, null };
                Assert.That(
                    (bool)Invoke(controller, "TryGetEncounterPlanEntry", entryArgs),
                    Is.True);
                Vector2 planned = (Vector2)GetProperty(
                    entryArgs[1],
                    "TargetPosition");
                Assert.That(target, Is.EqualTo(planned));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void FixedSeedAndWorldHandoffPreservePlanAndNextTarget()
        {
            GameObject gameObject = new GameObject("M28APlanLifecycleTest");
            try
            {
                Type controllerType = Type.GetType(ControllerTypeName, true);
                Type segmentType = Type.GetType(SegmentTypeName, true);
                Type modeType = Type.GetType(ModeTypeName, true);
                Type progressType = Type.GetType(ProgressTypeName, true);
                Component controller = gameObject.AddComponent(controllerType);

                Array segments = CreateSixSlotSegments(
                    segmentType,
                    modeType);
                Invoke(controller, "ConfigureWorldSegments", segments);
                Invoke(controller, "ConfigureDevelopmentFixedSeed", 2801);
                Invoke(controller, "RegenerateEncounterPlanForDevelopment");
                string firstSignature = (string)GetProperty(
                    controller,
                    "EncounterPlanSignature");

                Invoke(controller, "RegenerateEncounterPlanForDevelopment");
                Assert.That(
                    GetProperty(controller, "EncounterPlanSignature"),
                    Is.EqualTo(firstSignature));

                object progress = Activator.CreateInstance(progressType, 6);
                Invoke(progress, "SelectBuild");
                Invoke(progress, "CompletePreparation");
                Invoke(progress, "CompleteResonance");
                CompleteEncounterCycle(progress);
                CompleteEncounterCycle(progress);
                SetPrivateField(controller, "_progress", progress);

                object[] nextTargetArgs = { Vector2.zero };
                Assert.That(
                    (bool)Invoke(
                        controller,
                        "TryGetNextEncounterTargetPosition",
                        nextTargetArgs),
                    Is.True);

                object[] nextEntryArgs = { 4, null };
                Assert.That(
                    (bool)Invoke(
                        controller,
                        "TryGetEncounterPlanEntry",
                        nextEntryArgs),
                    Is.True);
                Assert.That(
                    (Vector2)nextTargetArgs[0],
                    Is.EqualTo((Vector2)GetProperty(
                        nextEntryArgs[1],
                        "TargetPosition")));

                CompleteEncounterCycle(progress);
                Invoke(controller, "ApplyWorldHandoffIfNeeded");
                Assert.That(GetProperty(controller, "WorldHandoffCount"),
                    Is.EqualTo(1));
                Assert.That(
                    GetProperty(controller, "EncounterPlanSignature"),
                    Is.EqualTo(firstSignature));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static object BuildPlan(
            int seed,
            Vector2[] positions = null,
            Vector2[] candidatePositions = null,
            bool singleCandidatePool = false)
        {
            Type builderType = Type.GetType(BuilderTypeName, true);
            Type modeType = Type.GetType(ModeTypeName, true);
            Type poolType = Type.GetType(PoolTypeName, true);
            positions ??= new[]
            {
                new Vector2(-11f, -5f),
                new Vector2(0f, 7f),
                new Vector2(13f, 2f),
                new Vector2(-12f, 6f),
                new Vector2(6f, -6f),
                new Vector2(14f, 5f)
            };
            candidatePositions ??= positions;

            Array modes = Array.CreateInstance(modeType, 6);
            string[] names =
            {
                "Chase", "Strafe", "WetRoutePressure",
                "Strafe", "WetRoutePressure", "Chase"
            };
            for (int i = 0; i < names.Length; i++)
            {
                modes.SetValue(Enum.Parse(modeType, names[i]), i);
            }

            Array pools = Array.CreateInstance(poolType, 6);
            for (int i = 0; i < 6; i++)
            {
                Vector2 basePosition = candidatePositions[i];
                Vector2[] offsets = GetDefaultCandidateOffsets(i);
                object pool = Activator.CreateInstance(
                    poolType,
                    new object[]
                    {
                        singleCandidatePool
                            ? new[] { basePosition }
                            : new[]
                            {
                                basePosition,
                                basePosition + offsets[1],
                                basePosition + offsets[2],
                                basePosition + offsets[3]
                            }
                    });
                pools.SetValue(pool, i);
            }

            MethodInfo build = builderType.GetMethod(
                "Build",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(build, Is.Not.Null);
            return build.Invoke(
                null,
                new object[]
                {
                    seed,
                    null,
                    positions,
                    modes,
                    pools,
                    null,
                    0.18f,
                    0.75f
                });
        }

        private static Vector2[] GetDefaultCandidateOffsets(int slot)
        {
            switch (slot)
            {
                case 0:
                    return new[]
                    {
                        Vector2.zero,
                        new Vector2(-3.5f, 2.5f),
                        new Vector2(3.5f, -2f),
                        new Vector2(3f, 2.5f)
                    };
                case 1:
                    return new[]
                    {
                        Vector2.zero,
                        new Vector2(-4.5f, -1.5f),
                        new Vector2(4.5f, -1.5f),
                        new Vector2(0f, -3.5f)
                    };
                case 2:
                    return new[]
                    {
                        Vector2.zero,
                        new Vector2(3.5f, 3.2f),
                        new Vector2(3.5f, -3.8f),
                        new Vector2(-3.5f, -3.3f)
                    };
                case 3:
                    return new[]
                    {
                        Vector2.zero,
                        new Vector2(-4f, 1.3f),
                        new Vector2(4f, 1.3f),
                        new Vector2(2f, -3.7f)
                    };
                case 4:
                    return new[]
                    {
                        Vector2.zero,
                        new Vector2(-4.3f, 1.5f),
                        new Vector2(4.2f, 1.5f),
                        new Vector2(0f, 3.9f)
                    };
                default:
                    return new[]
                    {
                        Vector2.zero,
                        new Vector2(3.3f, 2.3f),
                        new Vector2(-4.2f, 2.3f),
                        new Vector2(-2.7f, -3.5f)
                    };
            }
        }

        private static string BuildSequence(Array entries, string property)
        {
            string[] values = new string[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                values[i] = GetEntryString(entries, i, property);
            return string.Join(">", values);
        }

        private static string BuildPositionSequence(Array entries)
        {
            string[] values = new string[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                Vector2 position = (Vector2)GetProperty(
                    entries.GetValue(i),
                    "TargetPosition");
                values[i] = position.x.ToString("F3") + "," +
                    position.y.ToString("F3");
            }

            return string.Join(">", values);
        }

        private static object CreateSegment(
            Type segmentType,
            Type modeType,
            string worldId,
            Vector2[] positions,
            string[] modeNames)
        {
            Array modes = Array.CreateInstance(modeType, modeNames.Length);
            for (int i = 0; i < modeNames.Length; i++)
            {
                modes.SetValue(Enum.Parse(modeType, modeNames[i]), i);
            }

            return Activator.CreateInstance(
                segmentType,
                worldId,
                positions,
                modes);
        }

        private static Array CreateSixSlotSegments(
            Type segmentType,
            Type modeType)
        {
            Array segments = Array.CreateInstance(segmentType, 2);
            segments.SetValue(
                CreateSegment(
                    segmentType,
                    modeType,
                    "FOGGED_WINDOW",
                    new[]
                    {
                        new Vector2(-11f, -5f),
                        new Vector2(0f, 7f),
                        new Vector2(13f, 2f)
                    },
                    new[] { "Chase", "Strafe", "WetRoutePressure" }),
                0);
            segments.SetValue(
                CreateSegment(
                    segmentType,
                    modeType,
                    "NEON_WINDOW",
                    new[]
                    {
                        new Vector2(-12f, 6f),
                        new Vector2(6f, -6f),
                        new Vector2(14f, 5f)
                    },
                    new[] { "Strafe", "WetRoutePressure", "Chase" }),
                1);
            return segments;
        }

        private static void CompleteEncounterCycle(object progress)
        {
            Assert.That((bool)Invoke(progress, "DefeatEncounter"), Is.True);
            Assert.That(
                (bool)Invoke(progress, "CompleteRewardChoice"),
                Is.True);
            Assert.That((bool)Invoke(progress, "CompleteRecovery"), Is.True);
            Assert.That((bool)Invoke(progress, "FinishIntermission"), Is.True);
        }

        private static void SetPrivateField(
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

        private static object GetProperty(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(instance);
        }

        private static string GetEntryString(
            Array entries,
            int index,
            string propertyName)
        {
            return GetProperty(entries.GetValue(index), propertyName).ToString();
        }

        private static int GetEntryInt(
            Array entries,
            int index,
            string propertyName)
        {
            return (int)GetProperty(entries.GetValue(index), propertyName);
        }

        private static bool GetEntryBool(
            Array entries,
            int index,
            string propertyName)
        {
            return (bool)GetProperty(entries.GetValue(index), propertyName);
        }
    }
}
