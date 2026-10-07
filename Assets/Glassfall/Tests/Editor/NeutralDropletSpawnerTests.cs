using System;
using Glassfall.Gameplay.Droplet;
using NUnit.Framework;
using UnityEngine;

namespace Glassfall.Tests
{
    public sealed class NeutralDropletSpawnerTests
    {
        private const string NeutralDropletTypeName =
            "Glassfall.Gameplay.Droplet.NeutralDroplet, Assembly-CSharp";

        [Test]
        public void FixedSeedProducesTheSameMassSequence()
        {
            float[] first = CreateMassSequence(12345, 32);
            float[] second = CreateMassSequence(12345, 32);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void WeightedTiersPreferSmallMasses()
        {
            System.Random random = new System.Random(12345);
            int small = 0;
            int medium = 0;
            int large = 0;

            for (int i = 0; i < 10000; i++)
            {
                NeutralMassTierKind tier =
                    NeutralDropletMassDistribution.SelectTier(
                        random,
                        0.65f,
                        0.25f,
                        0.10f);

                if (tier == NeutralMassTierKind.Small)
                    small++;
                else if (tier == NeutralMassTierKind.Medium)
                    medium++;
                else
                    large++;
            }

            Assert.That(small, Is.GreaterThan(medium));
            Assert.That(medium, Is.GreaterThan(large));
        }

        [Test]
        public void SampledMassStaysInsideConfiguredRange()
        {
            System.Random random = new System.Random(7);
            NeutralMassTier small = new NeutralMassTier(0.5f, 1.25f, 0.65f);
            NeutralMassTier medium = new NeutralMassTier(1.25f, 2.25f, 0.25f);
            NeutralMassTier large = new NeutralMassTier(2.25f, 3.5f, 0.10f);

            for (int i = 0; i < 10000; i++)
            {
                float mass = NeutralDropletMassDistribution.Sample(
                    random,
                    0.75f,
                    3f,
                    small,
                    medium,
                    large);

                Assert.That(mass, Is.InRange(0.75f, 3f));
            }
        }

        [Test]
        public void SpawnPlannerStopsAtAttemptLimit()
        {
            int attempts = 0;

            bool found = NeutralSpawnPlanner.TryFindPosition(
                7,
                () =>
                {
                    attempts++;
                    return Vector2.zero;
                },
                _ => false,
                out _);

            Assert.That(found, Is.False);
            Assert.That(attempts, Is.EqualTo(7));
        }

        [Test]
        public void HigherMassProducesHigherVisualAndColliderRadius()
        {
            Component droplet = CreateNeutralDroplet(
                out GameObject gameObject,
                out CircleCollider2D collider);

            try
            {
                Initialize(droplet, 0.5f);
                float smallVisualRadius = GetVisualRadius(droplet);
                float smallColliderRadius = GetWorldRadius(collider);

                Initialize(droplet, 3.5f);

                Assert.That(
                    GetVisualRadius(droplet),
                    Is.GreaterThan(smallVisualRadius));
                Assert.That(
                    GetWorldRadius(collider),
                    Is.GreaterThan(smallColliderRadius));
                Assert.That(
                    GetWorldRadius(collider),
                    Is.EqualTo(GetVisualRadius(droplet)).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InitializeUpdatesVisualScaleAndColliderSizeImmediately()
        {
            Component droplet = CreateNeutralDroplet(
                out GameObject gameObject,
                out CircleCollider2D collider);

            try
            {
                Initialize(droplet, 0.5f);
                Vector3 smallScale = gameObject.transform.localScale;
                float smallColliderRadius = GetWorldRadius(collider);

                Initialize(droplet, 3.5f);

                Assert.That(
                    gameObject.transform.localScale.x,
                    Is.GreaterThan(smallScale.x));
                Assert.That(
                    gameObject.transform.localScale.y,
                    Is.GreaterThan(smallScale.y));
                Assert.That(
                    GetWorldRadius(collider),
                    Is.GreaterThan(smallColliderRadius));
                Assert.That(
                    GetWorldRadius(collider),
                    Is.EqualTo(GetVisualRadius(droplet)).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SameMassProducesTheSameSize()
        {
            Component droplet = CreateNeutralDroplet(
                out GameObject gameObject,
                out CircleCollider2D collider);

            try
            {
                Initialize(droplet, 2.25f);
                Vector3 firstScale = gameObject.transform.localScale;
                float firstColliderRadius = GetWorldRadius(collider);

                Initialize(droplet, 0.5f);
                Initialize(droplet, 2.25f);

                Assert.That(gameObject.transform.localScale, Is.EqualTo(firstScale));
                Assert.That(
                    GetWorldRadius(collider),
                    Is.EqualTo(firstColliderRadius));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TryConsumeStillReturnsInitializedMass()
        {
            Component droplet = CreateNeutralDroplet(
                out GameObject gameObject,
                out _);

            try
            {
                const float expectedMass = 2.75f;
                Initialize(droplet, expectedMass);

                object[] firstArguments = { 0f };
                bool firstResult = (bool)droplet.GetType()
                    .GetMethod("TryConsume")
                    .Invoke(droplet, firstArguments);

                object[] secondArguments = { 0f };
                bool secondResult = (bool)droplet.GetType()
                    .GetMethod("TryConsume")
                    .Invoke(droplet, secondArguments);

                Assert.That(firstResult, Is.True);
                Assert.That((float)firstArguments[0], Is.EqualTo(expectedMass));
                Assert.That(secondResult, Is.False);
                Assert.That((float)secondArguments[0], Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PresentationPulseStartsAndEndsAtCanonicalMultiplier()
        {
            for (int pulseKind = 0; pulseKind < 3; pulseKind++)
            {
                Assert.That(
                    EvaluatePresentationPulse(pulseKind, 0f),
                    Is.EqualTo(1f).Within(0.0001f));
                Assert.That(
                    EvaluatePresentationPulse(pulseKind, 1f),
                    Is.EqualTo(1f).Within(0.0001f));
            }
        }

        [Test]
        public void PresentationPulseProvidesAnOutwardPeak()
        {
            Assert.That(
                EvaluatePresentationPulse(0, 0.5f),
                Is.GreaterThan(1f));
            Assert.That(
                EvaluatePresentationPulse(1, 0.5f),
                Is.GreaterThan(1f));
            Assert.That(
                EvaluatePresentationPulse(2, 0.5f),
                Is.GreaterThan(1f));
        }

        [Test]
        public void MergedCollectionUsesFullNeutralMass()
        {
            float gained = NeutralCollectionRules.CalculateMassGain(
                2.5f,
                false);

            Assert.That(gained, Is.EqualTo(2.5f).Within(0.0001f));
        }

        [Test]
        public void SplitCollectionUsesReducedNeutralMass()
        {
            float merged = NeutralCollectionRules.CalculateMassGain(2.5f, false);
            float split = NeutralCollectionRules.CalculateMassGain(2.5f, true);

            Assert.That(split, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(split, Is.LessThan(merged));
        }

        [Test]
        public void CollectionRuleNeverReturnsNegativeMass()
        {
            Assert.That(
                NeutralCollectionRules.CalculateMassGain(-5f, false),
                Is.Zero);
            Assert.That(
                NeutralCollectionRules.CalculateMassGain(2f, true, 1f, -1f),
                Is.Zero);
        }

        [Test]
        public void SpawnCandidateInsideBoundsIsAccepted()
        {
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(10f, 6f, 0f));

            Assert.That(
                NeutralSpawnPlanner.IsInsideBounds(new Vector2(4f, 2f), bounds),
                Is.True);
        }

        [Test]
        public void SpawnCandidateOutsideBoundsIsRejected()
        {
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(10f, 6f, 0f));

            Assert.That(
                NeutralSpawnPlanner.IsInsideBounds(new Vector2(6f, 0f), bounds),
                Is.False);
        }

        [Test]
        public void SpawnCandidateTooCloseToPlayerIsRejected()
        {
            Assert.That(
                NeutralSpawnPlanner.IsFarEnough(
                    new Vector2(0.5f, 0f),
                    Vector2.zero,
                    1.5f),
                Is.False);
        }

        [Test]
        public void SpawnCandidateTooCloseToNeutralIsRejected()
        {
            Vector2[] occupied =
            {
                new Vector2(1f, 1f),
                new Vector2(-2f, 0f)
            };

            Assert.That(
                NeutralSpawnPlanner.IsFarEnoughFromAll(
                    new Vector2(1.2f, 1f),
                    occupied,
                    0.5f),
                Is.False);
        }

        [Test]
        public void SeededSpawnCandidateGenerationIsDeterministic()
        {
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(10f, 6f, 0f));
            System.Random first = new System.Random(12345);
            System.Random second = new System.Random(12345);

            Vector2 firstPoint = NeutralSpawnPlanner.SamplePoint(first, bounds);
            Vector2 secondPoint = NeutralSpawnPlanner.SamplePoint(second, bounds);

            Assert.That(secondPoint, Is.EqualTo(firstPoint));
        }

        [Test]
        public void SpawnBudgetStopsAtMaxAliveAndRunBudget()
        {
            Assert.That(
                NeutralSpawnPlanner.CanSpawn(7, 8, 5, 14),
                Is.True);
            Assert.That(
                NeutralSpawnPlanner.CanSpawn(8, 8, 5, 14),
                Is.False);
            Assert.That(
                NeutralSpawnPlanner.CanSpawn(2, 8, 14, 14),
                Is.False);
        }

        [Test]
        public void PopulationRefillRequestsOneDropletBelowTarget()
        {
            Assert.That(
                NeutralSpawnPlanner.GetPopulationRefillRequest(
                    4,
                    6,
                    8,
                    5,
                    14),
                Is.EqualTo(1));
        }

        [Test]
        public void PopulationRefillStopsWhenTargetIsReached()
        {
            Assert.That(
                NeutralSpawnPlanner.GetPopulationRefillRequest(
                    6,
                    6,
                    8,
                    5,
                    14),
                Is.Zero);
            Assert.That(
                NeutralSpawnPlanner.GetPopulationRefillRequest(
                    7,
                    6,
                    8,
                    5,
                    14),
                Is.Zero);
        }

        [Test]
        public void PopulationRefillRespectsHardCapAndRunBudget()
        {
            Assert.That(
                NeutralSpawnPlanner.GetPopulationRefillRequest(
                    7,
                    20,
                    8,
                    5,
                    14),
                Is.EqualTo(1));
            Assert.That(
                NeutralSpawnPlanner.GetPopulationRefillRequest(
                    8,
                    20,
                    8,
                    5,
                    14),
                Is.Zero);
            Assert.That(
                NeutralSpawnPlanner.GetPopulationRefillRequest(
                    2,
                    6,
                    8,
                    14,
                    14),
                Is.Zero);
        }

        [Test]
        public void PopulationRefillNeverRequestsBulkReplacement()
        {
            Assert.That(
                NeutralSpawnPlanner.GetPopulationRefillRequest(
                    0,
                    6,
                    8,
                    0,
                    14),
                Is.EqualTo(1));
        }

        private static float[] CreateMassSequence(int seed, int count)
        {
            System.Random random = new System.Random(seed);
            NeutralMassTier small = new NeutralMassTier(0.5f, 1.25f, 0.65f);
            NeutralMassTier medium = new NeutralMassTier(1.25f, 2.25f, 0.25f);
            NeutralMassTier large = new NeutralMassTier(2.25f, 3.5f, 0.10f);
            float[] sequence = new float[count];

            for (int i = 0; i < sequence.Length; i++)
            {
                sequence[i] = NeutralDropletMassDistribution.Sample(
                    random,
                    0.5f,
                    3.5f,
                    small,
                    medium,
                    large);
            }

            return sequence;
        }

        private static Component CreateNeutralDroplet(
            out GameObject gameObject,
            out CircleCollider2D collider)
        {
            Type neutralDropletType = Type.GetType(
                NeutralDropletTypeName,
                true);
            gameObject = new GameObject("NeutralDropletTest");
            gameObject.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            return gameObject.AddComponent(neutralDropletType);
        }

        private static void Initialize(Component droplet, float mass)
        {
            droplet.GetType()
                .GetMethod("Initialize")
                .Invoke(droplet, new object[] { mass });
        }

        private static float GetVisualRadius(Component droplet)
        {
            return (float)droplet.GetType()
                .GetProperty("VisualRadius")
                .GetValue(droplet);
        }

        private static float GetWorldRadius(CircleCollider2D collider)
        {
            Vector3 scale = collider.transform.lossyScale;
            float maxScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y));
            return collider.radius * maxScale;
        }

        private static float EvaluatePresentationPulse(
            int pulseKind,
            float normalizedTime)
        {
            Type pulseMathType = Type.GetType(
                "Glassfall.Presentation.PresentationPulseMath, Assembly-CSharp",
                true);
            return (float)pulseMathType
                .GetMethod("Evaluate")
                .Invoke(
                    null,
                    new object[]
                    {
                        pulseKind,
                        normalizedTime,
                        1.10f,
                        0.94f,
                        1.08f,
                        0.93f,
                        1.12f,
                        0.86f,
                        1.05f
                    });
        }
    }
}
