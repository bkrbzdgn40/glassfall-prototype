using System;
using System.Collections.Generic;
using UnityEngine;
using SystemRandom = System.Random;

namespace Glassfall.Gameplay.Droplet
{
    [DisallowMultipleComponent]
    public sealed class NeutralDropletSpawner : MonoBehaviour
    {
        private const float SpawnEdgePadding = 0.12f;
        private const float PlayerRelevantRadius = 7.5f;

        [Header("References")]
        [SerializeField] private NeutralDroplet neutralDropletPrefab;
        [SerializeField] private Collider2D spawnArea;
        [SerializeField] private DropletSplitSystem playerSplitSystem;

        [Header("Run Population")]
        [SerializeField] private bool runControlled;

        [Min(0)]
        [SerializeField] private int initialSpawnCount = 10;

        [Min(0)]
        [SerializeField] private int targetAliveCount = 10;

        [Min(0.1f)]
        [SerializeField] private float refillIntervalSeconds = 2.5f;

        [Min(1)]
        [SerializeField] private int maxAliveCount = 14;

        [Min(1)]
        [SerializeField] private int runSpawnBudget = 30;

        [Min(0f)]
        [SerializeField] private float minimumPlayerDistance = 1.5f;

        [Min(0f)]
        [SerializeField] private float minimumSpacing = 0.35f;

        [Min(0.01f)]
        [SerializeField] private float minMass = 0.5f;

        [Min(0.01f)]
        [SerializeField] private float maxMass = 3.5f;

        [Min(1)]
        [SerializeField] private int maxSpawnAttemptsPerDroplet = 30;

        [SerializeField] private LayerMask blockingLayers = ~0;

        [Header("Mass Tiers")]
        [Min(0.01f)]
        [SerializeField] private float smallMassMin = 0.5f;

        [Min(0.01f)]
        [SerializeField] private float smallMassMax = 1.25f;

        [Min(0f)]
        [SerializeField] private float smallWeight = 0.65f;

        [Min(0.01f)]
        [SerializeField] private float mediumMassMin = 1.25f;

        [Min(0.01f)]
        [SerializeField] private float mediumMassMax = 2.25f;

        [Min(0f)]
        [SerializeField] private float mediumWeight = 0.25f;

        [Min(0.01f)]
        [SerializeField] private float largeMassMin = 2.25f;

        [Min(0.01f)]
        [SerializeField] private float largeMassMax = 3.5f;

        [Min(0f)]
        [SerializeField] private float largeWeight = 0.10f;

        [Header("Randomness")]
        [Tooltip("Enabled: vary the runtime seed each Play session. Disabled: use the fixed seed below.")]
        [SerializeField] private bool useRandomSeed = true;
        [SerializeField] private int seed = 12345;

        [Header("Runtime")]
        [SerializeField] private int spawnedCount;
        [SerializeField] private int aliveCount;
        [SerializeField] private int remainingRunBudget;
        [SerializeField] private bool populationRefillActive;
        [SerializeField] private float refillCountdown;

        private readonly List<NeutralDroplet> _trackedDroplets =
            new List<NeutralDroplet>();

        private SystemRandom _random;
        private bool _hasPopulated;

        public int SpawnedCount => spawnedCount;
        public int AliveCount
        {
            get
            {
                PruneTrackedDroplets();
                return _trackedDroplets.Count;
            }
        }
        public int RemainingRunBudget => Mathf.Max(
            0,
            runSpawnBudget - spawnedCount);
        public int TargetAliveCount => Mathf.Min(
            Mathf.Max(0, targetAliveCount),
            maxAliveCount);
        public int MaxAliveCount => maxAliveCount;
        public int RunSpawnBudget => runSpawnBudget;
        public float RefillIntervalSeconds => refillIntervalSeconds;
        public bool PopulationRefillActive => populationRefillActive;
        public bool HasPopulated => _hasPopulated;
        public Collider2D SpawnArea => spawnArea;

        private void Start()
        {
            if (runControlled)
                return;

            BeginRun();
            SetPopulationRefillActive(true);
        }

        private void Update()
        {
            if (!_hasPopulated || !populationRefillActive)
                return;

            int request = NeutralSpawnPlanner.GetPopulationRefillRequest(
                AliveCount,
                TargetAliveCount,
                maxAliveCount,
                spawnedCount,
                runSpawnBudget);

            if (request <= 0)
            {
                refillCountdown = refillIntervalSeconds;
                UpdateRuntimeDiagnostics();
                return;
            }

            refillCountdown = Mathf.Max(
                0f,
                refillCountdown - Time.unscaledDeltaTime);

            if (refillCountdown > 0f)
            {
                UpdateRuntimeDiagnostics();
                return;
            }

            SpawnMany(request);
            refillCountdown = refillIntervalSeconds;
            UpdateRuntimeDiagnostics();
        }

        public void ConfigureRunSpawning(
            NeutralDroplet prefab,
            Collider2D area,
            DropletSplitSystem player,
            int initialCount = 10,
            int targetAlive = 10,
            int maxAlive = 14,
            int budget = 30,
            float refillInterval = 2.5f,
            float playerDistance = 1.5f,
            float spacing = 0.35f,
            bool controlledByRun = true,
            bool randomizeSeed = true)
        {
            neutralDropletPrefab = prefab;
            spawnArea = area;
            playerSplitSystem = player;
            initialSpawnCount = Mathf.Max(0, initialCount);
            maxAliveCount = Mathf.Max(1, maxAlive);
            targetAliveCount = Mathf.Clamp(
                targetAlive,
                0,
                maxAliveCount);
            runSpawnBudget = Mathf.Max(1, budget);
            refillIntervalSeconds = Mathf.Max(0.1f, refillInterval);
            minimumPlayerDistance = Mathf.Max(0f, playerDistance);
            minimumSpacing = Mathf.Max(0f, spacing);
            runControlled = controlledByRun;
            useRandomSeed = randomizeSeed;
        }

        public void ConfigureSpawnArea(Collider2D area)
        {
            spawnArea = area;
        }

        // Small tuning surface for future run modifiers such as Heavy Rain.
        // This deliberately changes population pressure only; it is not a
        // weather system and it does not bypass the finite run budget.
        public void ConfigurePopulationProfile(
            int targetAlive,
            int maxAlive,
            float refillInterval)
        {
            maxAliveCount = Mathf.Max(1, maxAlive);
            targetAliveCount = Mathf.Clamp(
                targetAlive,
                0,
                maxAliveCount);
            refillIntervalSeconds = Mathf.Max(0.1f, refillInterval);
            refillCountdown = Mathf.Min(
                refillCountdown,
                refillIntervalSeconds);
            UpdateRuntimeDiagnostics();
        }

        public void SetPopulationRefillActive(
            bool active,
            bool allowImmediateAttempt = false)
        {
            populationRefillActive = active;

            if (!active)
            {
                refillCountdown = refillIntervalSeconds;
                UpdateRuntimeDiagnostics();
                return;
            }

            if (allowImmediateAttempt)
                refillCountdown = 0f;
            else if (refillCountdown <= 0f)
                refillCountdown = refillIntervalSeconds;

            UpdateRuntimeDiagnostics();
        }

        public int Populate()
        {
            BeginRun();
            return spawnedCount;
        }

        public int BeginRun()
        {
            if (_hasPopulated)
                return AliveCount;

            _hasPopulated = true;
            spawnedCount = 0;
            _trackedDroplets.Clear();
            InitializeRandom();
            CacheExistingNeutralDroplets();

            if (!IsSetupValid())
            {
                UpdateRuntimeDiagnostics();
                return AliveCount;
            }

            int initialTarget = Mathf.Min(
                initialSpawnCount,
                maxAliveCount);
            int requested = Mathf.Max(0, initialTarget - AliveCount);
            SpawnMany(requested);
            refillCountdown = refillIntervalSeconds;
            UpdateRuntimeDiagnostics();
            return AliveCount;
        }

        // Kept as a narrow compatibility hook. Intermission refill is now
        // population-based and never asks for more than one droplet at once.
        public int ReplenishForIntermission()
        {
            if (!_hasPopulated)
                BeginRun();

            int request = NeutralSpawnPlanner.GetPopulationRefillRequest(
                AliveCount,
                TargetAliveCount,
                maxAliveCount,
                spawnedCount,
                runSpawnBudget);
            int spawned = SpawnMany(request);
            refillCountdown = refillIntervalSeconds;
            UpdateRuntimeDiagnostics();
            return spawned;
        }

        public int SpawnMany(int requestedCount)
        {
            if (requestedCount <= 0 || !IsSetupValid())
                return 0;

            if (_random == null)
                InitializeRandom();

            int spawnedNow = 0;
            for (int i = 0; i < requestedCount; i++)
            {
                int currentAlive = AliveCount;
                if (!NeutralSpawnPlanner.CanSpawn(
                        currentAlive,
                        maxAliveCount,
                        spawnedCount,
                        runSpawnBudget))
                {
                    break;
                }

                float mass = SampleMass();
                float radius = neutralDropletPrefab.GetVisualRadiusForMass(mass);
                Bounds bounds = spawnArea.bounds;
                float edgePadding = radius + SpawnEdgePadding;
                Bounds samplingBounds =
                    NeutralSpawnPlanner.InsetBounds(bounds, edgePadding);

                if (ShouldPreferPlayerRelevantRegion())
                {
                    samplingBounds =
                        NeutralSpawnPlanner.CreatePlayerRelevantBounds(
                            bounds,
                            GetPlayerSpawnFocus(),
                            PlayerRelevantRadius,
                            edgePadding);
                }

                bool found = NeutralSpawnPlanner.TryFindPosition(
                    maxSpawnAttemptsPerDroplet,
                    () => NeutralSpawnPlanner.SamplePoint(
                        _random,
                        samplingBounds,
                        0f),
                    candidate => IsCandidateClear(candidate, radius),
                    out Vector2 position);

                if (!found)
                {
                    Debug.LogWarning(
                        $"{name}: Could not find a valid neutral droplet " +
                        $"position after {maxSpawnAttemptsPerDroplet} attempts.",
                        this);
                    continue;
                }

                SpawnNeutralDroplet(position, mass);
                spawnedNow++;
            }

            UpdateRuntimeDiagnostics();
            return spawnedNow;
        }

        private void OnValidate()
        {
            initialSpawnCount = Mathf.Max(0, initialSpawnCount);
            maxAliveCount = Mathf.Max(1, maxAliveCount);
            targetAliveCount = Mathf.Clamp(
                targetAliveCount,
                0,
                maxAliveCount);
            runSpawnBudget = Mathf.Max(1, runSpawnBudget);
            refillIntervalSeconds = Mathf.Max(
                0.1f,
                refillIntervalSeconds);
            minimumPlayerDistance = Mathf.Max(0f, minimumPlayerDistance);
            minimumSpacing = Mathf.Max(0f, minimumSpacing);
            minMass = Mathf.Max(0.01f, minMass);
            maxMass = Mathf.Max(minMass, maxMass);
            maxSpawnAttemptsPerDroplet = Mathf.Max(
                1,
                maxSpawnAttemptsPerDroplet);

            smallMassMin = Mathf.Max(0.01f, smallMassMin);
            smallMassMax = Mathf.Max(smallMassMin, smallMassMax);
            mediumMassMin = Mathf.Max(0.01f, mediumMassMin);
            mediumMassMax = Mathf.Max(mediumMassMin, mediumMassMax);
            largeMassMin = Mathf.Max(0.01f, largeMassMin);
            largeMassMax = Mathf.Max(largeMassMin, largeMassMax);
            smallWeight = Mathf.Max(0f, smallWeight);
            mediumWeight = Mathf.Max(0f, mediumWeight);
            largeWeight = Mathf.Max(0f, largeWeight);
        }

        private bool IsSetupValid()
        {
            if (neutralDropletPrefab == null)
            {
                Debug.LogWarning(
                    $"{name}: Neutral Droplet Prefab is not assigned.",
                    this);
                return false;
            }

            if (spawnArea == null || !spawnArea.enabled)
            {
                Debug.LogWarning(
                    $"{name}: Spawn Area must reference an enabled Collider2D.",
                    this);
                return false;
            }

            if (neutralDropletPrefab.GetComponent<CircleCollider2D>() == null)
            {
                Debug.LogWarning(
                    $"{name}: Neutral Droplet Prefab needs a CircleCollider2D.",
                    this);
                return false;
            }

            return true;
        }

        private bool ShouldPreferPlayerRelevantRegion()
        {
            return playerSplitSystem != null &&
                playerSplitSystem.PrimaryTransform != null &&
                spawnedCount % 2 == 0;
        }

        private Vector2 GetPlayerSpawnFocus()
        {
            Vector2 primary = playerSplitSystem.PrimaryTransform.position;
            if (!playerSplitSystem.IsSplit ||
                playerSplitSystem.MirrorTransform == null)
            {
                return primary;
            }

            return (primary +
                (Vector2)playerSplitSystem.MirrorTransform.position) * 0.5f;
        }

        private void InitializeRandom()
        {
            int runtimeSeed = useRandomSeed
                ? unchecked(Environment.TickCount * 397 ^
                    (int)DateTime.UtcNow.Ticks)
                : seed;
            _random = new SystemRandom(runtimeSeed);
        }

        private void CacheExistingNeutralDroplets()
        {
            NeutralDroplet[] existing =
                FindObjectsByType<NeutralDroplet>();

            for (int i = 0; i < existing.Length; i++)
                TrackDroplet(existing[i]);
        }

        private void TrackDroplet(NeutralDroplet droplet)
        {
            if (droplet == null ||
                droplet.IsConsumed ||
                _trackedDroplets.Contains(droplet))
            {
                return;
            }

            _trackedDroplets.Add(droplet);
            droplet.Consumed += HandleNeutralConsumed;
        }

        private void HandleNeutralConsumed(NeutralDropletConsumed consumed)
        {
            for (int i = _trackedDroplets.Count - 1; i >= 0; i--)
            {
                NeutralDroplet droplet = _trackedDroplets[i];
                if (droplet == null || droplet.IsConsumed)
                {
                    if (droplet != null)
                        droplet.Consumed -= HandleNeutralConsumed;
                    _trackedDroplets.RemoveAt(i);
                }
            }

            // Deliberately do not spawn here. Population recovery is paced by
            // the refill timer so collection never causes an immediate 1:1
            // replacement.
            UpdateRuntimeDiagnostics();
        }

        private void PruneTrackedDroplets()
        {
            for (int i = _trackedDroplets.Count - 1; i >= 0; i--)
            {
                NeutralDroplet droplet = _trackedDroplets[i];
                if (droplet != null && !droplet.IsConsumed)
                    continue;

                if (droplet != null)
                    droplet.Consumed -= HandleNeutralConsumed;
                _trackedDroplets.RemoveAt(i);
            }
        }

        private float SampleMass()
        {
            return NeutralDropletMassDistribution.Sample(
                _random,
                minMass,
                maxMass,
                new NeutralMassTier(
                    smallMassMin,
                    smallMassMax,
                    smallWeight),
                new NeutralMassTier(
                    mediumMassMin,
                    mediumMassMax,
                    mediumWeight),
                new NeutralMassTier(
                    largeMassMin,
                    largeMassMax,
                    largeWeight));
        }

        private bool IsCandidateClear(Vector2 position, float radius)
        {
            if (!NeutralSpawnPlanner.IsInsideBounds(position, spawnArea.bounds) ||
                !spawnArea.OverlapPoint(position))
            {
                return false;
            }

            if (!IsFarEnoughFromPlayer(position, radius))
                return false;

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(
                position,
                radius,
                blockingLayers);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider2D overlap = overlaps[i];
                if (overlap == null ||
                    overlap == spawnArea ||
                    overlap.isTrigger)
                {
                    continue;
                }

                return false;
            }

            PruneTrackedDroplets();
            for (int i = 0; i < _trackedDroplets.Count; i++)
            {
                NeutralDroplet droplet = _trackedDroplets[i];
                float minimumDistance =
                    radius +
                    droplet.VisualRadius +
                    minimumSpacing;

                if (!NeutralSpawnPlanner.IsFarEnough(
                        position,
                        droplet.transform.position,
                        minimumDistance))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsFarEnoughFromPlayer(Vector2 position, float radius)
        {
            if (playerSplitSystem == null)
                return true;

            DropletController primary = playerSplitSystem.PrimaryController;
            if (primary != null)
            {
                float primaryDistance =
                    minimumPlayerDistance + radius + primary.CurrentRadius;
                if (!NeutralSpawnPlanner.IsFarEnough(
                        position,
                        primary.transform.position,
                        primaryDistance))
                {
                    return false;
                }
            }

            DropletController mirror = playerSplitSystem.MirrorController;
            if (mirror != null)
            {
                float mirrorDistance =
                    minimumPlayerDistance + radius + mirror.CurrentRadius;
                if (!NeutralSpawnPlanner.IsFarEnough(
                        position,
                        mirror.transform.position,
                        mirrorDistance))
                {
                    return false;
                }
            }

            return true;
        }

        private void SpawnNeutralDroplet(Vector2 position, float mass)
        {
            NeutralDroplet instance = Instantiate(
                neutralDropletPrefab,
                position,
                Quaternion.identity,
                transform);
            instance.Initialize(mass);
            TrackDroplet(instance);
            spawnedCount++;
        }

        private void UpdateRuntimeDiagnostics()
        {
            aliveCount = AliveCount;
            remainingRunBudget = RemainingRunBudget;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _trackedDroplets.Count; i++)
            {
                if (_trackedDroplets[i] != null)
                    _trackedDroplets[i].Consumed -= HandleNeutralConsumed;
            }
        }
    }
}
