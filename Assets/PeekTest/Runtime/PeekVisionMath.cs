using UnityEngine;

namespace PeekTest
{
    public static class PeekVisionMath
    {
        public static float EvaluateCone(
            Vector3 worldPosition,
            Vector3 origin,
            Vector3 direction,
            float distance,
            float distanceFeather,
            float innerAngleDegrees,
            float outerAngleDegrees,
            bool active)
        {
            Vector2 direction2D = new Vector2(direction.x, direction.y);
            if (!active || distance <= 0f || direction2D.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            // The side-view wedge is extruded through room depth so the back wall and actors share one mask.
            Vector2 toPoint = new Vector2(worldPosition.x - origin.x, worldPosition.y - origin.y);
            float pointDistance = toPoint.magnitude;
            if (pointDistance <= 0.0001f || pointDistance >= distance)
            {
                return 0f;
            }

            float inner = Mathf.Min(innerAngleDegrees, outerAngleDegrees);
            float outer = Mathf.Max(innerAngleDegrees, outerAngleDegrees);
            float innerCos = Mathf.Cos(inner * Mathf.Deg2Rad);
            float outerCos = Mathf.Cos(outer * Mathf.Deg2Rad);
            float alignment = Vector2.Dot(toPoint / pointDistance, direction2D.normalized);
            float angleMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(outerCos, innerCos, alignment));

            float feather = Mathf.Max(0.0001f, distanceFeather);
            float distanceT = Mathf.InverseLerp(distance - feather, distance, pointDistance);
            float distanceMask = 1f - Mathf.SmoothStep(0f, 1f, distanceT);
            return Mathf.Clamp01(angleMask * distanceMask);
        }

        public static int ResolvePatrolDirection(float positionX, float minX, float maxX, int currentDirection)
        {
            if (positionX >= maxX)
            {
                return -1;
            }

            if (positionX <= minX)
            {
                return 1;
            }

            return currentDirection < 0 ? -1 : 1;
        }
    }
}
