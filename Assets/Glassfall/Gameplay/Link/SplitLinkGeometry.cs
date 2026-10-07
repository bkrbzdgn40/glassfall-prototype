using UnityEngine;

namespace Glassfall.Gameplay.Link
{
    public static class SplitLinkGeometry
    {
        private const float Epsilon = 0.0001f;

        public static float DistancePointToSegment(
            Vector2 point,
            Vector2 segmentStart,
            Vector2 segmentEnd)
        {
            Vector2 segment = segmentEnd - segmentStart;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Epsilon * Epsilon)
                return Vector2.Distance(point, segmentStart);

            float t = Vector2.Dot(point - segmentStart, segment) /
                lengthSquared;
            t = Mathf.Clamp01(t);
            Vector2 closest = segmentStart + segment * t;
            return Vector2.Distance(point, closest);
        }

        public static bool IsCircleTouchingSegment(
            Vector2 circleCenter,
            float circleRadius,
            Vector2 segmentStart,
            Vector2 segmentEnd)
        {
            return DistancePointToSegment(
                    circleCenter,
                    segmentStart,
                    segmentEnd) <=
                Mathf.Max(0f, circleRadius);
        }

        public static bool TryTrimSegment(
            Vector2 segmentStart,
            Vector2 segmentEnd,
            float insetFromEachEnd,
            out Vector2 trimmedStart,
            out Vector2 trimmedEnd)
        {
            trimmedStart = segmentStart;
            trimmedEnd = segmentEnd;

            Vector2 delta = segmentEnd - segmentStart;
            float length = delta.magnitude;
            float inset = Mathf.Max(0f, insetFromEachEnd);

            if (length <= Epsilon || length <= inset * 2f + Epsilon)
                return false;

            Vector2 direction = delta / length;
            trimmedStart = segmentStart + direction * inset;
            trimmedEnd = segmentEnd - direction * inset;
            return true;
        }
    }
}
