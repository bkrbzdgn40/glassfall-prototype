using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Glassfall.Tests
{
    public sealed class SplitLinkGeometryTests
    {
        private const string GeometryTypeName =
            "Glassfall.Gameplay.Link.SplitLinkGeometry, Assembly-CSharp";

        [Test]
        public void CrossingPointTouchesSegment()
        {
            Assert.That(
                IsCircleTouchingSegment(
                    Vector2.zero,
                    0.1f,
                    new Vector2(-2f, 0f),
                    new Vector2(2f, 0f)),
                Is.True);
        }

        [Test]
        public void DistantPointMissesSegment()
        {
            Assert.That(
                IsCircleTouchingSegment(
                    new Vector2(0f, 1f),
                    0.2f,
                    new Vector2(-2f, 0f),
                    new Vector2(2f, 0f)),
                Is.False);
        }

        [Test]
        public void EndpointCanBeDetectedForResonanceInteraction()
        {
            Assert.That(
                IsCircleTouchingSegment(
                    new Vector2(2.05f, 0f),
                    0.1f,
                    new Vector2(-2f, 0f),
                    new Vector2(2f, 0f)),
                Is.True);
        }

        [Test]
        public void ZeroLengthSegmentIsHandledSafely()
        {
            float distance = DistancePointToSegment(
                new Vector2(1f, 0f),
                Vector2.zero,
                Vector2.zero);

            Assert.That(distance, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void TrimmedSegmentExcludesEndpoints()
        {
            object[] arguments =
            {
                new Vector2(-2f, 0f),
                new Vector2(2f, 0f),
                0.5f,
                Vector2.zero,
                Vector2.zero
            };

            bool result = (bool)Invoke("TryTrimSegment", arguments);

            Assert.That(result, Is.True);
            Assert.That(
                (Vector2)arguments[3],
                Is.EqualTo(new Vector2(-1.5f, 0f)));
            Assert.That(
                (Vector2)arguments[4],
                Is.EqualTo(new Vector2(1.5f, 0f)));
        }

        [Test]
        public void SegmentTooShortForInsetsIsRejected()
        {
            object[] arguments =
            {
                new Vector2(-0.2f, 0f),
                new Vector2(0.2f, 0f),
                0.25f,
                Vector2.zero,
                Vector2.zero
            };

            Assert.That(
                (bool)Invoke("TryTrimSegment", arguments),
                Is.False);
        }

        private static bool IsCircleTouchingSegment(
            Vector2 center,
            float radius,
            Vector2 start,
            Vector2 end)
        {
            return (bool)Invoke(
                "IsCircleTouchingSegment",
                new object[] { center, radius, start, end });
        }

        private static float DistancePointToSegment(
            Vector2 point,
            Vector2 start,
            Vector2 end)
        {
            return (float)Invoke(
                "DistancePointToSegment",
                new object[] { point, start, end });
        }

        private static object Invoke(
            string methodName,
            object[] arguments)
        {
            Type type = Type.GetType(GeometryTypeName, true);
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, arguments);
        }
    }
}
