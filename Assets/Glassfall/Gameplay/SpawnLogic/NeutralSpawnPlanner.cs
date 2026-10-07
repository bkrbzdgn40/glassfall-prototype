using System;
using System.Collections.Generic;
using UnityEngine;

namespace Glassfall.Gameplay.Droplet
{
    public static class NeutralSpawnPlanner
    {
        public static bool TryFindPosition(
            int maxAttempts,
            Func<Vector2> candidateFactory,
            Func<Vector2, bool> validator,
            out Vector2 position)
        {
            position = Vector2.zero;

            if (maxAttempts <= 0 ||
                candidateFactory == null ||
                validator == null)
            {
                return false;
            }

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector2 candidate = candidateFactory();
                if (!validator(candidate))
                    continue;

                position = candidate;
                return true;
            }

            return false;
        }

        public static Vector2 SamplePoint(System.Random random, Bounds bounds)
        {
            return SamplePoint(random, bounds, 0f);
        }

        public static Vector2 SamplePoint(
            System.Random random,
            Bounds bounds,
            float edgePadding)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            float padding = Mathf.Max(0f, edgePadding);
            float minimumX = bounds.min.x + padding;
            float maximumX = bounds.max.x - padding;
            float minimumY = bounds.min.y + padding;
            float maximumY = bounds.max.y - padding;

            if (minimumX > maximumX)
                minimumX = maximumX = bounds.center.x;
            if (minimumY > maximumY)
                minimumY = maximumY = bounds.center.y;

            return new Vector2(
                Mathf.Lerp(
                    minimumX,
                    maximumX,
                    (float)random.NextDouble()),
                Mathf.Lerp(
                    minimumY,
                    maximumY,
                    (float)random.NextDouble()));
        }

        public static Bounds InsetBounds(Bounds bounds, float padding)
        {
            float safePadding = Mathf.Max(0f, padding);
            Vector3 size = bounds.size;
            size.x = Mathf.Max(0f, size.x - safePadding * 2f);
            size.y = Mathf.Max(0f, size.y - safePadding * 2f);
            return new Bounds(bounds.center, size);
        }

        public static Bounds CreatePlayerRelevantBounds(
            Bounds worldBounds,
            Vector2 playerPosition,
            float radius,
            float edgePadding)
        {
            Bounds safeWorld = InsetBounds(worldBounds, edgePadding);
            float safeRadius = Mathf.Max(0f, radius);
            float width = Mathf.Min(safeWorld.size.x, safeRadius * 2f);
            float height = Mathf.Min(safeWorld.size.y, safeRadius * 2f);

            if (width <= Mathf.Epsilon || height <= Mathf.Epsilon)
                return safeWorld;

            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            Vector2 minimum = new Vector2(
                safeWorld.min.x + halfWidth,
                safeWorld.min.y + halfHeight);
            Vector2 maximum = new Vector2(
                safeWorld.max.x - halfWidth,
                safeWorld.max.y - halfHeight);
            Vector2 center = new Vector2(
                Mathf.Clamp(playerPosition.x, minimum.x, maximum.x),
                Mathf.Clamp(playerPosition.y, minimum.y, maximum.y));

            return new Bounds(
                center,
                new Vector3(width, height, safeWorld.size.z));
        }

        public static bool IsInsideBounds(Vector2 point, Bounds bounds)
        {
            return point.x >= bounds.min.x &&
                point.x <= bounds.max.x &&
                point.y >= bounds.min.y &&
                point.y <= bounds.max.y;
        }

        public static bool IsFarEnough(
            Vector2 point,
            Vector2 other,
            float minimumDistance)
        {
            float distance = Mathf.Max(0f, minimumDistance);
            return (point - other).sqrMagnitude >= distance * distance;
        }

        public static bool IsFarEnoughFromAll(
            Vector2 point,
            IReadOnlyList<Vector2> others,
            float minimumDistance)
        {
            if (others == null)
                return true;

            for (int i = 0; i < others.Count; i++)
            {
                if (!IsFarEnough(point, others[i], minimumDistance))
                    return false;
            }

            return true;
        }

        public static int GetPopulationRefillRequest(
            int aliveCount,
            int targetAliveCount,
            int maxAliveCount,
            int spawnedThisRun,
            int runSpawnBudget)
        {
            int safeMax = Mathf.Max(0, maxAliveCount);
            int safeTarget = Mathf.Clamp(
                targetAliveCount,
                0,
                safeMax);
            int safeAlive = Mathf.Max(0, aliveCount);

            if (safeAlive >= safeTarget)
                return 0;

            return CanSpawn(
                    safeAlive,
                    safeMax,
                    spawnedThisRun,
                    runSpawnBudget)
                ? 1
                : 0;
        }

        public static bool CanSpawn(
            int aliveCount,
            int maxAliveCount,
            int spawnedThisRun,
            int runSpawnBudget)
        {
            return maxAliveCount > 0 &&
                runSpawnBudget > 0 &&
                Mathf.Max(0, aliveCount) < maxAliveCount &&
                Mathf.Max(0, spawnedThisRun) < runSpawnBudget;
        }
    }
}
