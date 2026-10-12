using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Glassfall.Gameplay.Combat;
using UnityEngine;

namespace Glassfall.Gameplay.Run
{
    [Serializable]
    public sealed class SeededEncounterCandidatePool
    {
        [SerializeField] private Vector2[] positions = Array.Empty<Vector2>();

        public int Count => positions != null ? positions.Length : 0;

        public SeededEncounterCandidatePool()
        {
        }

        public SeededEncounterCandidatePool(params Vector2[] values)
        {
            Configure(values);
        }

        public void Configure(Vector2[] values)
        {
            positions = values != null
                ? (Vector2[])values.Clone()
                : Array.Empty<Vector2>();
        }

        public Vector2 GetPosition(int index)
        {
            if (positions == null || positions.Length == 0)
                return Vector2.zero;

            return positions[Mathf.Clamp(index, 0, positions.Length - 1)];
        }

        public Vector2[] CopyPositions()
        {
            return positions != null
                ? (Vector2[])positions.Clone()
                : Array.Empty<Vector2>();
        }
    }

    public readonly struct SeededEncounterPlanEntry
    {
        public SeededEncounterPlanEntry(
            string worldId,
            int encounterIndex,
            int localEncounterIndex,
            ArcHunterMovementMode movementMode,
            Vector2 targetPosition,
            bool usedFallback)
        {
            WorldId = string.IsNullOrWhiteSpace(worldId)
                ? "WORLD"
                : worldId;
            EncounterIndex = encounterIndex;
            LocalEncounterIndex = localEncounterIndex;
            MovementMode = movementMode;
            TargetPosition = targetPosition;
            UsedFallback = usedFallback;
        }

        public string WorldId { get; }
        public int EncounterIndex { get; }
        public int LocalEncounterIndex { get; }
        public ArcHunterMovementMode MovementMode { get; }
        public Vector2 TargetPosition { get; }
        public bool UsedFallback { get; }
    }

    public sealed class SeededEncounterPlan
    {
        private readonly SeededEncounterPlanEntry[] _entries;

        public SeededEncounterPlan(
            int seed,
            SeededEncounterPlanEntry[] entries)
        {
            Seed = seed;
            _entries = entries != null
                ? (SeededEncounterPlanEntry[])entries.Clone()
                : Array.Empty<SeededEncounterPlanEntry>();
            Signature = BuildSignature(_entries);
        }

        public int Seed { get; }
        public int Count => _entries.Length;
        public string Signature { get; }

        public bool TryGetEntry(
            int encounterIndex,
            out SeededEncounterPlanEntry entry)
        {
            int index = encounterIndex - 1;
            if (index < 0 || index >= _entries.Length)
            {
                entry = default;
                return false;
            }

            entry = _entries[index];
            return true;
        }

        public SeededEncounterPlanEntry GetEntry(int encounterIndex)
        {
            return TryGetEntry(encounterIndex, out SeededEncounterPlanEntry entry)
                ? entry
                : default;
        }

        public SeededEncounterPlanEntry[] CopyEntries()
        {
            return (SeededEncounterPlanEntry[])_entries.Clone();
        }

        private static string BuildSignature(
            SeededEncounterPlanEntry[] entries)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < entries.Length; i++)
            {
                if (i > 0)
                    builder.Append('|');

                SeededEncounterPlanEntry entry = entries[i];
                builder.Append(entry.WorldId)
                    .Append(':')
                    .Append(entry.EncounterIndex)
                    .Append(':')
                    .Append(entry.MovementMode)
                    .Append(':')
                    .Append(entry.TargetPosition.x.ToString(
                        "F3",
                        CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(entry.TargetPosition.y.ToString(
                        "F3",
                        CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }

    public static class SeededEncounterPlanBuilder
    {
        public const int RequiredSlotCount = 6;

        private const float DefaultContainmentPadding = 0.18f;
        private const float DefaultMinimumSpacing = 0.75f;

        // These are deliberate approach-angle alternatives, not random
        // coordinate noise. They keep each authored encounter landmark in
        // its local neighbourhood while changing the angle from which the
        // player has to enter the encounter.
        private static readonly Vector2[][] DefaultCandidateOffsets =
        {
            new[]
            {
                Vector2.zero,
                new Vector2(-3.5f, 2.5f),
                new Vector2(3.5f, -2.0f),
                new Vector2(3.0f, 2.5f)
            },
            new[]
            {
                Vector2.zero,
                new Vector2(-4.5f, -1.5f),
                new Vector2(4.5f, -1.5f),
                new Vector2(0f, -3.5f)
            },
            new[]
            {
                Vector2.zero,
                new Vector2(3.5f, 3.2f),
                new Vector2(3.5f, -3.8f),
                new Vector2(-3.5f, -3.3f)
            },
            new[]
            {
                Vector2.zero,
                new Vector2(-4.0f, 1.3f),
                new Vector2(4.0f, 1.3f),
                new Vector2(2.0f, -3.7f)
            },
            new[]
            {
                Vector2.zero,
                new Vector2(-4.3f, 1.5f),
                new Vector2(4.2f, 1.5f),
                new Vector2(0f, 3.9f)
            },
            new[]
            {
                Vector2.zero,
                new Vector2(3.3f, 2.3f),
                new Vector2(-4.2f, 2.3f),
                new Vector2(-2.7f, -3.5f)
            }
        };

        /// <summary>
        /// Returns the authored-anchor alternatives used when Setup 26/28A
        /// did not serialize explicit candidate pools. The alternatives are
        /// intentionally shaped per encounter slot so a seed changes the
        /// approach angle and link placement decision, not just a tiny offset.
        /// </summary>
        public static SeededEncounterCandidatePool
            CreateDefaultCandidatePool(
                int slot,
                Vector2 authoredPosition)
        {
            int safeSlot = Mathf.Clamp(
                slot,
                0,
                DefaultCandidateOffsets.Length - 1);
            Vector2[] offsets = DefaultCandidateOffsets[safeSlot];
            Vector2[] positions = new Vector2[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
                positions[i] = authoredPosition + offsets[i];

            return new SeededEncounterCandidatePool(positions);
        }

        private readonly struct AuthoredSlot
        {
            public AuthoredSlot(
                string worldId,
                int localEncounterIndex,
                ArcHunterMovementMode movementMode,
                Vector2 authoredPosition)
            {
                WorldId = worldId;
                LocalEncounterIndex = localEncounterIndex;
                MovementMode = movementMode;
                AuthoredPosition = authoredPosition;
            }

            public string WorldId { get; }
            public int LocalEncounterIndex { get; }
            public ArcHunterMovementMode MovementMode { get; }
            public Vector2 AuthoredPosition { get; }
        }

        public static SeededEncounterPlan Build(
            int seed,
            MicroRunWorldSegment[] authoredSegments,
            Vector2[] legacyPositions,
            ArcHunterMovementMode[] legacyMovementModes,
            SeededEncounterCandidatePool[] candidatePools,
            Collider2D playableBounds,
            float containmentPadding = DefaultContainmentPadding,
            float minimumSpacing = DefaultMinimumSpacing)
        {
            System.Random random = new System.Random(seed);
            List<AuthoredSlot> authoredSlots = CollectAuthoredSlots(
                authoredSegments,
                legacyPositions,
                legacyMovementModes);
            List<SeededEncounterPlanEntry> entries =
                new List<SeededEncounterPlanEntry>(RequiredSlotCount);

            for (int slot = 0; slot < RequiredSlotCount; slot++)
            {
                AuthoredSlot authored = slot < authoredSlots.Count
                    ? authoredSlots[slot]
                    : CreateFallbackAuthoredSlot(
                        slot,
                        legacyPositions,
                        legacyMovementModes);
                Vector2 position = authored.AuthoredPosition;
                bool usedFallback = true;

                SeededEncounterCandidatePool pool = candidatePools != null &&
                    slot < candidatePools.Length
                    ? candidatePools[slot]
                    : null;
                if (pool != null && pool.Count > 0)
                {
                    List<int> candidateOrder = BuildCandidateOrder(
                        pool.Count,
                        random);
                    for (int i = 0; i < candidateOrder.Count; i++)
                    {
                        Vector2 candidate = pool.GetPosition(candidateOrder[i]);
                        if (!IsValidCandidate(
                                candidate,
                                entries,
                                playableBounds,
                                containmentPadding,
                                minimumSpacing))
                        {
                            continue;
                        }

                        position = candidate;
                        usedFallback = false;
                        break;
                    }
                }

                if (usedFallback)
                {
                    position = ConstrainToPlayableBounds(
                        position,
                        playableBounds,
                        containmentPadding);
                }

                ArcHunterMovementMode movementMode =
                    SelectMovementMode(
                        slot,
                        authored.MovementMode,
                        entries.Count > 0
                            ? entries[entries.Count - 1].MovementMode
                            : (ArcHunterMovementMode?)null,
                        random);
                entries.Add(new SeededEncounterPlanEntry(
                    authored.WorldId,
                    slot + 1,
                    authored.LocalEncounterIndex,
                    movementMode,
                    position,
                    usedFallback));
            }

            return new SeededEncounterPlan(seed, entries.ToArray());
        }

        private static List<AuthoredSlot> CollectAuthoredSlots(
            MicroRunWorldSegment[] authoredSegments,
            Vector2[] legacyPositions,
            ArcHunterMovementMode[] legacyMovementModes)
        {
            List<AuthoredSlot> slots = new List<AuthoredSlot>(RequiredSlotCount);
            if (authoredSegments != null)
            {
                for (int segmentIndex = 0;
                     segmentIndex < authoredSegments.Length &&
                     slots.Count < RequiredSlotCount;
                     segmentIndex++)
                {
                    MicroRunWorldSegment segment = authoredSegments[segmentIndex];
                    if (segment == null || segment.EncounterCount <= 0)
                        continue;

                    for (int local = 0;
                         local < segment.EncounterCount &&
                         slots.Count < RequiredSlotCount;
                         local++)
                    {
                        slots.Add(new AuthoredSlot(
                            segment.WorldId,
                            local,
                            segment.GetMovementMode(local),
                            segment.GetEncounterPosition(local)));
                    }
                }
            }

            if (slots.Count >= RequiredSlotCount)
                return slots;

            for (int slot = slots.Count; slot < RequiredSlotCount; slot++)
                slots.Add(CreateFallbackAuthoredSlot(
                    slot,
                    legacyPositions,
                    legacyMovementModes));
            return slots;
        }

        private static AuthoredSlot CreateFallbackAuthoredSlot(
            int slot,
            Vector2[] legacyPositions,
            ArcHunterMovementMode[] legacyMovementModes)
        {
            Vector2 position = legacyPositions != null &&
                legacyPositions.Length > 0
                ? legacyPositions[Mathf.Clamp(
                    slot,
                    0,
                    legacyPositions.Length - 1)]
                : Vector2.zero;
            ArcHunterMovementMode mode = legacyMovementModes != null &&
                slot < legacyMovementModes.Length
                ? legacyMovementModes[slot]
                : GetDefaultMovementMode(slot);
            return new AuthoredSlot(
                slot < 3 ? "FOGGED_WINDOW" : "NEON_WINDOW",
                slot % 3,
                mode,
                position);
        }

        private static List<int> BuildCandidateOrder(
            int count,
            System.Random random)
        {
            List<int> order = new List<int>(count);
            for (int i = 0; i < count; i++)
                order.Add(i);

            for (int i = order.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                int value = order[i];
                order[i] = order[swapIndex];
                order[swapIndex] = value;
            }

            return order;
        }

        private static bool IsValidCandidate(
            Vector2 candidate,
            List<SeededEncounterPlanEntry> selectedEntries,
            Collider2D playableBounds,
            float containmentPadding,
            float minimumSpacing)
        {
            if (playableBounds != null &&
                !IsInsidePlayableBounds(
                    candidate,
                    playableBounds,
                    containmentPadding))
            {
                return false;
            }

            float minDistance = Mathf.Max(0f, minimumSpacing);
            float minDistanceSquared = minDistance * minDistance;
            for (int i = 0; i < selectedEntries.Count; i++)
            {
                if ((candidate - selectedEntries[i].TargetPosition).sqrMagnitude <
                    minDistanceSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsInsidePlayableBounds(
            Vector2 position,
            Collider2D playableBounds,
            float containmentPadding)
        {
            Bounds bounds = playableBounds.bounds;
            float margin = Mathf.Max(0.05f, containmentPadding);
            return bounds.size.x > margin * 2f &&
                bounds.size.y > margin * 2f &&
                position.x >= bounds.min.x + margin &&
                position.x <= bounds.max.x - margin &&
                position.y >= bounds.min.y + margin &&
                position.y <= bounds.max.y - margin;
        }

        private static Vector2 ConstrainToPlayableBounds(
            Vector2 position,
            Collider2D playableBounds,
            float containmentPadding)
        {
            if (playableBounds == null)
                return position;

            Bounds bounds = playableBounds.bounds;
            float margin = Mathf.Max(0.05f, containmentPadding);
            if (bounds.size.x <= margin * 2f ||
                bounds.size.y <= margin * 2f)
            {
                return bounds.center;
            }

            return new Vector2(
                Mathf.Clamp(
                    position.x,
                    bounds.min.x + margin,
                    bounds.max.x - margin),
                Mathf.Clamp(
                    position.y,
                    bounds.min.y + margin,
                    bounds.max.y - margin));
        }

        private static ArcHunterMovementMode SelectMovementMode(
            int slot,
            ArcHunterMovementMode authoredMode,
            ArcHunterMovementMode? previousMode,
            System.Random random)
        {
            List<ArcHunterMovementMode> options =
                GetRoleConstrainedModes(slot);

            // Authored modes define the baseline rhythm, while the slot
            // table is the hard design constraint. Keep an authored option
            // prominent when it is valid, but never widen a role by adding
            // an incompatible mode.
            if (options.Remove(authoredMode))
                options.Insert(0, authoredMode);

            if (previousMode.HasValue && options.Count > 1)
            {
                options.Remove(previousMode.Value);
                if (options.Count == 0)
                    options.Add(previousMode.Value);
            }

            return options[random.Next(options.Count)];
        }

        private static List<ArcHunterMovementMode> GetRoleConstrainedModes(
            int slot)
        {
            switch (slot)
            {
                case 0:
                    return new List<ArcHunterMovementMode>
                    {
                        ArcHunterMovementMode.Chase
                    };
                case 1:
                    return new List<ArcHunterMovementMode>
                    {
                        ArcHunterMovementMode.Strafe,
                        ArcHunterMovementMode.WetRoutePressure
                    };
                case 2:
                    return new List<ArcHunterMovementMode>
                    {
                        ArcHunterMovementMode.Chase,
                        ArcHunterMovementMode.WetRoutePressure
                    };
                case 3:
                    return new List<ArcHunterMovementMode>
                    {
                        ArcHunterMovementMode.Strafe,
                        ArcHunterMovementMode.Chase
                    };
                case 4:
                    return new List<ArcHunterMovementMode>
                    {
                        ArcHunterMovementMode.Chase,
                        ArcHunterMovementMode.Strafe,
                        ArcHunterMovementMode.WetRoutePressure
                    };
                default:
                    return new List<ArcHunterMovementMode>
                    {
                        ArcHunterMovementMode.Chase,
                        ArcHunterMovementMode.WetRoutePressure
                    };
            }
        }

        private static ArcHunterMovementMode GetDefaultMovementMode(int slot)
        {
            switch (slot)
            {
                case 1:
                    return ArcHunterMovementMode.Strafe;
                case 2:
                    return ArcHunterMovementMode.WetRoutePressure;
                case 3:
                    return ArcHunterMovementMode.Strafe;
                case 4:
                    return ArcHunterMovementMode.WetRoutePressure;
                default:
                    return ArcHunterMovementMode.Chase;
            }
        }
    }
}
