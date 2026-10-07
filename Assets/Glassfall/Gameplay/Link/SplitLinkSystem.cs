using Glassfall.Gameplay.Droplet;
using UnityEngine;

namespace Glassfall.Gameplay.Link
{
    [DisallowMultipleComponent]
    public sealed class SplitLinkSystem : MonoBehaviour
    {
        [SerializeField] private DropletSplitSystem splitSystem;

        public DropletSplitSystem SplitSystem => splitSystem;
        public bool IsSplit => splitSystem != null && splitSystem.IsSplit;

        public void Configure(DropletSplitSystem source)
        {
            splitSystem = source;
        }

        private void Awake()
        {
            if (splitSystem == null)
                splitSystem = FindAnyObjectByType<DropletSplitSystem>();
        }

        public bool TryGetFullSegment(
            out Vector2 start,
            out Vector2 end)
        {
            start = default;
            end = default;

            if (!IsSplit)
                return false;

            Transform primary = splitSystem.PrimaryTransform;
            Transform mirror = splitSystem.MirrorTransform;
            if (primary == null || mirror == null)
                return false;

            start = primary.position;
            end = mirror.position;
            return true;
        }

        public bool TryGetTrimmedSegment(
            float insetFromEachEnd,
            out Vector2 start,
            out Vector2 end)
        {
            start = default;
            end = default;

            return TryGetFullSegment(
                    out Vector2 fullStart,
                    out Vector2 fullEnd) &&
                SplitLinkGeometry.TryTrimSegment(
                    fullStart,
                    fullEnd,
                    insetFromEachEnd,
                    out start,
                    out end);
        }

        public bool IsCircleOnFullLink(
            Vector2 center,
            float radius)
        {
            return TryGetFullSegment(
                    out Vector2 start,
                    out Vector2 end) &&
                SplitLinkGeometry.IsCircleTouchingSegment(
                    center,
                    radius,
                    start,
                    end);
        }

        public bool TryGetNearestActiveDropletPosition(
            Vector2 fromPosition,
            out Vector2 targetPosition)
        {
            targetPosition = default;
            if (splitSystem == null || splitSystem.PrimaryTransform == null)
                return false;

            Vector2 primary = splitSystem.PrimaryTransform.position;
            if (!IsSplit || splitSystem.MirrorTransform == null)
            {
                targetPosition = primary;
                return true;
            }

            Vector2 mirror = splitSystem.MirrorTransform.position;
            targetPosition =
                (fromPosition - primary).sqrMagnitude <=
                (fromPosition - mirror).sqrMagnitude
                    ? primary
                    : mirror;
            return true;
        }

        public bool TryAddCombinedMassReward(float combinedReward)
        {
            if (splitSystem == null || combinedReward <= 0f)
                return false;

            DropletController primary = splitSystem.PrimaryController;
            if (primary == null)
                return false;

            if (IsSplit && splitSystem.MirrorController != null)
            {
                float halfReward = combinedReward * 0.5f;
                primary.SetMass(primary.CurrentMass + halfReward);

                DropletController mirror = splitSystem.MirrorController;
                mirror.SetMass(mirror.CurrentMass + halfReward);
                return true;
            }

            primary.SetMass(primary.CurrentMass + combinedReward);
            return true;
        }
    }
}
