using Character.Config;
using Character.Core;
using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace Character.Presentation
{
    public enum DodgeMode : byte
    {
        None = 0,
        NeutralBackward = 1,
        ForwardAlongMove = 2,
        LockOn8Way = 3,
    }

    public readonly struct DodgePresentationContext
    {
        public DodgePresentationContext(
            DodgeMode mode,
            Vector2 blendLocal,
            float duration,
            float moveDuration,
            Vector3 worldMoveDirection)
        {
            Mode = mode;
            BlendLocal = blendLocal;
            Duration = duration;
            MoveDuration = moveDuration;
            WorldMoveDirection = worldMoveDirection;
        }

        public DodgeMode Mode { get; }
        public Vector2 BlendLocal { get; }
        public float Duration { get; }
        public float MoveDuration { get; }
        public Vector3 WorldMoveDirection { get; }
        public bool IsValid => Duration > 0f && WorldMoveDirection.sqrMagnitude > 0.0001f;
    }

    public static class DodgeModeResolver
    {
        public static DodgePresentationContext Resolve(
            CharacterIntent intent, CharacterStateId fromStateId,bool isLockOn, 
            CharacterContext context, CharacterPresentationConfig presentationConfig, CharacterCombatConfig combatConfig,
            float moveDeadZone = 0.2f)
        {
            bool hasMove = intent.Move.sqrMagnitude >= moveDeadZone * moveDeadZone;
            bool fromSprint = fromStateId == CharacterStateId.Sprint;

            if (!hasMove)
            {
                var backDir = GetBackwardWorldDirection(context);
                return new DodgePresentationContext(
                    DodgeMode.NeutralBackward,
                    Vector2.zero,
                    combatConfig.dodgeBackwardDuration,
                    combatConfig.GetDodgeMoveDuration(DodgeMode.NeutralBackward),
                    backDir);
            }

            if (!isLockOn || fromSprint)
            {
                var forwardDir = StickToWorldDirection(intent.Move, context);
                return new DodgePresentationContext(
                    DodgeMode.ForwardAlongMove,
                    Vector2.zero,
                    combatConfig.dodgeEvadeDuration,
                    combatConfig.GetDodgeMoveDuration(DodgeMode.ForwardAlongMove),
                    forwardDir);
            }

            var blendLocal = StickToCharacterLocalBlend(intent.Move, context, presentationConfig);
            var eightWayDir = LocalBlendToWorldDirection(context, blendLocal);
            return new DodgePresentationContext(
                DodgeMode.LockOn8Way,
                blendLocal,
                combatConfig.dodgeEvadeDuration,
                combatConfig.GetDodgeMoveDuration(DodgeMode.LockOn8Way),
                eightWayDir);
        }

        public static Vector3 GetBackwardWorldDirection(CharacterContext context)
        {
            var back = -context.Root.forward;
            back.y = 0f;
            return back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.back;
        }

        public static Vector3 StickToWorldDirection(Vector2 move, CharacterContext context)
        {
            context.GetCameraBasis(out var camForward, out var camRight);
            var world = camRight * move.x + camForward * move.y;
            world.y = 0f;
            return world.sqrMagnitude > 0.0001f ? world.normalized : context.Root.forward;
        }

        public static Vector3 LocalBlendToWorldDirection(CharacterContext context, Vector2 blendLocal)
        {
            if (blendLocal.sqrMagnitude < 0.0001f)
                return context.Root.forward;

            var local = new Vector3(blendLocal.x, 0f, blendLocal.y);
            var world = context.Root.TransformDirection(local.normalized);
            world.y = 0f;
            return world.sqrMagnitude > 0.0001f ? world.normalized : context.Root.forward;
        }

        public static Vector2 StickToCharacterLocalBlend(
            Vector2 move,
            CharacterContext context,
            CharacterPresentationConfig presentation)
        {
            context.GetCameraBasis(out var camForward, out var camRight);
            var world = camRight * move.x + camForward * move.y;
            world.y = 0f;

            if (world.sqrMagnitude < 0.0001f)
                return new Vector2(0f, presentation.dodgeBlendAxisMax);

            var local = context.Root.InverseTransformDirection(world.normalized);
            float scale = presentation.dodgeBlendAxisMax;
            return new Vector2(local.x * scale, local.z * scale);
        }
    }
}
