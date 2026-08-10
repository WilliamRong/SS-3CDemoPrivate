using NUnit.Framework;
using UnityEngine;

namespace PeekTest.Tests
{
    public sealed class PeekVisionMathTests
    {
        [Test]
        public void EvaluateCone_ReturnsZero_WhenInactive()
        {
            float result = PeekVisionMath.EvaluateCone(
                new Vector3(2f, 0f, 0f),
                Vector3.zero,
                Vector3.right,
                10f,
                1f,
                10f,
                20f,
                false);

            Assert.That(result, Is.Zero);
        }

        [Test]
        public void EvaluateCone_ReturnsOne_ForPointInsideInnerAngle()
        {
            float result = PeekVisionMath.EvaluateCone(
                new Vector3(4f, 0f, 0f),
                Vector3.zero,
                Vector3.right,
                10f,
                1f,
                10f,
                20f,
                true);

            Assert.That(result, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void EvaluateCone_ReturnsZero_ForPointOutsideOuterAngle()
        {
            float result = PeekVisionMath.EvaluateCone(
                new Vector3(1f, 4f, 0f),
                Vector3.zero,
                Vector3.right,
                10f,
                1f,
                10f,
                20f,
                true);

            Assert.That(result, Is.Zero);
        }

        [Test]
        public void EvaluateCone_IgnoresDepth_ForSideViewWedge()
        {
            float result = PeekVisionMath.EvaluateCone(
                new Vector3(4f, 0f, 3f),
                Vector3.zero,
                Vector3.right,
                10f,
                1f,
                10f,
                20f,
                true);

            Assert.That(result, Is.EqualTo(1f).Within(0.001f));
        }

        [TestCase(9.5f, 2.5f, 9.5f, 1, -1)]
        [TestCase(2.5f, 2.5f, 9.5f, -1, 1)]
        [TestCase(6f, 2.5f, 9.5f, -1, -1)]
        public void ResolvePatrolDirection_UsesBounds(
            float positionX,
            float minX,
            float maxX,
            int current,
            int expected)
        {
            Assert.That(
                PeekVisionMath.ResolvePatrolDirection(positionX, minX, maxX, current),
                Is.EqualTo(expected));
        }
    }
}
