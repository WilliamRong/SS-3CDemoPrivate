using Character.Combat;
using Character.Config;
using Character.LockOn;
using UnityEngine;


namespace Character.Execution
{
    /// <summary>
    /// 从锁定目标或附近角色中确定性地选择唯一处决候选。
    /// 只负责查询，不消费输入、不占用角色、不创建会话。
    /// </summary>
    public sealed class ExecutionCandidateResolver
    {
        private const int MaxNearbyColliderCount = 64;

        private readonly Collider[] _nearbyColliders =
            new Collider[MaxNearbyColliderCount];

        private readonly CombatActor[] _seenActors =
            new CombatActor[MaxNearbyColliderCount + 1];

        public bool DidLastQueryOverflow { get; private set; }



        public bool TrySelect(
            CombatActor executor,
            PlayerLockOnController lockOn,
            CharacterCombatConfig config,
            out CombatActor candidate,
            out ExecutionEligibilityResult eligibility,
            IExecutionOccupancyQuery occupancyQuery = null)
        {
            candidate = null;
            eligibility = default;
            DidLastQueryOverflow = false;

            if (executor == null || config == null)
                return false;

            int seenCount = 0;
            CombatActor lockedActor = ResolveLockedActor(lockOn);

            // 合法锁定目标无条件优先于附近搜索结果。
            if (lockedActor != null && lockedActor != executor)
            {
                TryRememberActor(
                    lockedActor,
                    _seenActors,
                    ref seenCount);

                ExecutionEligibilityResult lockedEligibility =
                    ExecutionEligibilityService.EvaluateCurrent(
                        executor,
                        lockedActor,
                        config,
                        occupancyQuery);

                if (lockedEligibility.IsEligible)
                {
                    candidate = lockedActor;
                    eligibility = lockedEligibility;
                    return true;
                }
            }

            if (!TryGetSearchRadius(config, out float searchRadius))
                return false;

            int hitCount = Physics.OverlapSphereNonAlloc(
                executor.transform.position,
                searchRadius,
                _nearbyColliders,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            // 截断集合的内容顺序不确定，因此缓冲区满时整次保守失败。
            if (hitCount >= _nearbyColliders.Length)
            {
                DidLastQueryOverflow = true;
                return false;
            }

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _nearbyColliders[i];
                if (hit == null)
                    continue;

                CombatActor actor =
                    hit.GetComponentInParent<CombatActor>();

                if (actor == null || actor == executor)
                    continue;

                // 同一角色通常具有多个 Collider，只评估一次。
                if (!TryRememberActor(
                        actor,
                        _seenActors,
                        ref seenCount))
                {
                    continue;
                }

                ExecutionEligibilityResult actorEligibility =
                    ExecutionEligibilityService.EvaluateCurrent(
                        executor,
                        actor,
                        config,
                        occupancyQuery);

                if (!actorEligibility.IsEligible)
                    continue;

                if (candidate == null ||
                    IsBetterCandidate(
                        actor,
                        actorEligibility,
                        candidate,
                        eligibility))
                {
                    candidate = actor;
                    eligibility = actorEligibility;
                }
            }

            return candidate != null;
        }

        private static CombatActor ResolveLockedActor(PlayerLockOnController lockOn)
        {
            if (lockOn == null)
                return null;

            ILockOnTarget lockOnTarget =
                lockOn.CurrentLockOnTarget;

            if (lockOnTarget == null)
                return null;

            Transform root = lockOnTarget.Root;
            if (root == null)
                return null;

            CombatActor actor =
                root.GetComponentInParent<CombatActor>();

            if (actor == null)
            {
                actor =
                    root.GetComponentInChildren<CombatActor>();
            }

            return actor;
        }

        private static bool TryRememberActor(
            CombatActor actor,
            CombatActor[] seenActors,
            ref int seenCount)
        {
            for (int i = 0; i < seenCount; i++)
            {
                CombatActor seen = seenActors[i];

                if (seen == actor ||
                    (seen != null &&
                     seen.ActorId == actor.ActorId))
                {
                    return false;
                }
            }

            if (seenCount >= seenActors.Length)
                return false;

            seenActors[seenCount] = actor;
            seenCount++;
            return true;
        }

        private static bool IsBetterCandidate(
            CombatActor actor,
            in ExecutionEligibilityResult actorEligibility,
            CombatActor currentBest,
            in ExecutionEligibilityResult currentBestEligibility)
        {
            if (actorEligibility.HorizontalDistance <
                           currentBestEligibility.HorizontalDistance)
            {
                return true;
            }

            if (actorEligibility.HorizontalDistance >
                currentBestEligibility.HorizontalDistance)
            {
                return false;
            }

            if (actorEligibility.TargetFrontAngle <
                currentBestEligibility.TargetFrontAngle)
            {
                return true;
            }

            if (actorEligibility.TargetFrontAngle >
                currentBestEligibility.TargetFrontAngle)
            {
                return false;
            }

            return actor.ActorId < currentBest.ActorId;
        }


        /// <summary>
        /// Broad Phase 使用三维球体，半径覆盖水平距离和允许高度差。
        /// 完整限制仍由 EligibilityService 重新验证。
        /// </summary>
        private static bool TryGetSearchRadius(
            CharacterCombatConfig config,
            out float radius)
        {
            radius = 0f;

            float maxDistance = config.executionMaxDistance;
            float maxHeight =
                config.executionMaxHeightDifference;

            if (!IsNonNegativeFinite(maxDistance) ||
                !IsNonNegativeFinite(maxHeight))
            {
                return false;
            }

            radius = Mathf.Sqrt(
                maxDistance * maxDistance +
                maxHeight * maxHeight);

            return IsNonNegativeFinite(radius);
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return value >= 0f &&
                  !float.IsNaN(value) &&
                  !float.IsInfinity(value);
        }
    }
}