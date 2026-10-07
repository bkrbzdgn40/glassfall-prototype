using System;
using Glassfall.Gameplay.Combat;
using UnityEngine;

namespace Glassfall.Gameplay.Run
{
    [Serializable]
    public sealed class MicroRunWorldSegment
    {
        [SerializeField] private string worldId = "WORLD";
        [SerializeField] private Vector2[] encounterPositions =
            Array.Empty<Vector2>();
        [SerializeField] private ArcHunterMovementMode[] movementModes =
            Array.Empty<ArcHunterMovementMode>();

        public string WorldId => string.IsNullOrWhiteSpace(worldId)
            ? "WORLD"
            : worldId;
        public int EncounterCount => encounterPositions != null
            ? encounterPositions.Length
            : 0;

        public MicroRunWorldSegment()
        {
        }

        public MicroRunWorldSegment(
            string id,
            Vector2[] positions,
            ArcHunterMovementMode[] modes)
        {
            Configure(id, positions, modes);
        }

        public void Configure(
            string id,
            Vector2[] positions,
            ArcHunterMovementMode[] modes)
        {
            worldId = string.IsNullOrWhiteSpace(id)
                ? "WORLD"
                : id.Trim();
            encounterPositions = positions != null
                ? (Vector2[])positions.Clone()
                : Array.Empty<Vector2>();
            movementModes = modes != null
                ? (ArcHunterMovementMode[])modes.Clone()
                : Array.Empty<ArcHunterMovementMode>();
        }

        public Vector2 GetEncounterPosition(int localEncounterIndex)
        {
            if (encounterPositions == null || encounterPositions.Length == 0)
                return Vector2.zero;

            int index = Mathf.Clamp(
                localEncounterIndex,
                0,
                encounterPositions.Length - 1);
            return encounterPositions[index];
        }

        public ArcHunterMovementMode GetMovementMode(int localEncounterIndex)
        {
            if (movementModes == null ||
                localEncounterIndex < 0 ||
                localEncounterIndex >= movementModes.Length)
            {
                return ArcHunterMovementMode.Chase;
            }

            return movementModes[localEncounterIndex];
        }

        public Vector2[] CopyEncounterPositions()
        {
            return encounterPositions != null
                ? (Vector2[])encounterPositions.Clone()
                : Array.Empty<Vector2>();
        }

        public ArcHunterMovementMode[] CopyMovementModes()
        {
            return movementModes != null
                ? (ArcHunterMovementMode[])movementModes.Clone()
                : Array.Empty<ArcHunterMovementMode>();
        }

        public MicroRunWorldSegment Clone()
        {
            return new MicroRunWorldSegment(
                WorldId,
                CopyEncounterPositions(),
                CopyMovementModes());
        }
    }
}
