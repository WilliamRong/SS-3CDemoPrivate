using Character.Combat;
using Character.Config;
using UnityEngine;

namespace Character.Execution
{
    /// <summary>
    /// 检查参与者之间的视线，以及处决者到锚点的环境路径。
    /// 仅允许在 Unity 主线程调用。
    /// </summary>
    public static class ExecutionPhysicsValidator
    {
        private const int MaxPhysicsHits = 32;
        private const float DistanceEpsilon = 0.0001f;

        private static readonly RaycastHit[] LineHits =
            new RaycastHit[MaxPhysicsHits];

        private static readonly RaycastHit[] CapsuleCastHits =
            new RaycastHit[MaxPhysicsHits];

        private static readonly Collider[] CapsuleOverlaps =
            new Collider[MaxPhysicsHits];

        public static ExecutionEligibilityResult Evaluate(
            CombatActor executor,
            CombatActor target,
            CharacterCombatConfig config,
            in ExecutionEligibilityResult precheck)
        {
            if (!precheck.IsEvaluated)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.InvalidSpatialComputation);
            }

            if (!precheck.IsEligible)
                return precheck;

            if (executor == null)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.ExecutorMissing);
            }

            if (target == null)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.TargetMissing);
            }

            if (config == null)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.InvalidConfiguration);
            }

            if (!HasClearLineOfSight(
                    executor,
                    target,
                    config.executionLineOfSightMask))
            {
                return precheck.WithRejection(
                    ExecutionRejectionReason.LineOfSightBlocked);
            }

            if (!TryBuildMovementCapsule(
                    executor,
                    out Vector3 pointA,
                    out Vector3 pointB,
                    out float radius))
            {
                return precheck.WithRejection(
                    ExecutionRejectionReason
                        .ExecutorCollisionShapeMissing);
            }

            if (!HasClearPathToAnchor(
                    executor,
                    target,
                    precheck.ExecutorAnchorPose.Position,
                    pointA,
                    pointB,
                    radius,
                    config.executionPathObstructionMask))
            {
                return precheck.WithRejection(
                    ExecutionRejectionReason.PathBlocked);
            }

            return precheck;
        }

        private static bool HasClearLineOfSight(
            CombatActor executor,
            CombatActor target,
            LayerMask layerMask)
        {
            Vector3 origin = ResolveBodyCenter(executor);
            Vector3 destination = ResolveBodyCenter(target);
            Vector3 delta = destination - origin;
            float distance = delta.magnitude;

            if (distance <= DistanceEpsilon)
                return true;

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                delta / distance,
                LineHits,
                distance,
                layerMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                if (IsBlockingCollider(
                        LineHits[i].collider,
                        executor,
                        target))
                {
                    return false;
                }
            }

            // 缓冲区填满意味着可能还有未返回的阻挡物，保守拒绝。
            return hitCount < LineHits.Length;
        }

        private static bool HasClearPathToAnchor(
            CombatActor executor,
            CombatActor target,
            Vector3 anchorPosition,
            Vector3 pointA,
            Vector3 pointB,
            float radius,
            LayerMask layerMask)
        {
            Vector3 delta =
                anchorPosition - executor.transform.position;

            // 当前 Motor 的处决位移只处理水平平移。
            delta.y = 0f;

            float distance = delta.magnitude;
            Vector3 offset = Vector3.zero;

            if (distance > DistanceEpsilon)
            {
                Vector3 direction = delta / distance;

                int castCount = Physics.CapsuleCastNonAlloc(
                    pointA,
                    pointB,
                    radius,
                    direction,
                    CapsuleCastHits,
                    distance,
                    layerMask,
                    QueryTriggerInteraction.Ignore);

                for (int i = 0; i < castCount; i++)
                {
                    if (IsBlockingCollider(
                            CapsuleCastHits[i].collider,
                            executor,
                            target))
                    {
                        return false;
                    }
                }

                if (castCount >= CapsuleCastHits.Length)
                    return false;

                offset = direction * distance;
            }

            int overlapCount = Physics.OverlapCapsuleNonAlloc(
                pointA + offset,
                pointB + offset,
                radius,
                CapsuleOverlaps,
                layerMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlapCount; i++)
            {
                if (IsBlockingCollider(
                        CapsuleOverlaps[i],
                        executor,
                        target))
                {
                    return false;
                }
            }

            return overlapCount < CapsuleOverlaps.Length;
        }

        private static bool TryBuildMovementCapsule(
            CombatActor actor,
            out Vector3 pointA,
            out Vector3 pointB,
            out float radius)
        {
            pointA = default;
            pointB = default;
            radius = 0f;

            Collider movementCollider =
                ResolveMovementCollider(actor);

            if (movementCollider == null)
                return false;

            Bounds bounds = movementCollider.bounds;

            if (!IsFinite(bounds.center) ||
                !IsFinite(bounds.extents))
            {
                return false;
            }

            radius = Mathf.Min(
                bounds.extents.x,
                bounds.extents.z);

            if (!IsFinite(radius) ||
                radius <= DistanceEpsilon)
            {
                return false;
            }

            float halfSegment = Mathf.Max(
                0f,
                bounds.extents.y - radius);

            pointA = bounds.center +
                     Vector3.up * halfSegment;

            pointB = bounds.center -
                     Vector3.up * halfSegment;

            return IsFinite(pointA) &&
                   IsFinite(pointB);
        }

        private static Collider ResolveMovementCollider(
            CombatActor actor)
        {
            if (actor == null)
                return null;

            CharacterController controller =
                actor.GetComponent<CharacterController>();

            if (controller != null &&
                controller.enabled)
            {
                return controller;
            }

            CapsuleCollider capsule =
                actor.GetComponent<CapsuleCollider>();

            if (capsule != null &&
                capsule.enabled &&
                !capsule.isTrigger)
            {
                return capsule;
            }

            return null;
        }

        private static Vector3 ResolveBodyCenter(
            CombatActor actor)
        {
            Collider movementCollider =
                ResolveMovementCollider(actor);

            if (movementCollider != null)
                return movementCollider.bounds.center;

            return actor.transform.position +
                   Vector3.up * 0.9f;
        }

        private static bool IsBlockingCollider(
            Collider collider,
            CombatActor executor,
            CombatActor target)
        {
            if (collider == null)
                return false;

            CombatActor owner =
                collider.GetComponentInParent<CombatActor>();

            // 双方自身的移动碰撞体不视为环境阻挡。
            return owner != executor &&
                   owner != target;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}