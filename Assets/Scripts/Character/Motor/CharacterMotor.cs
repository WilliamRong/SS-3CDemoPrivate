using Character.Core;
using Character.Intent;
using UnityEngine;

namespace Character.Motor
{
    public class CharacterMotor
    {
        private CharacterContext _context;

        private Vector3 _currentHorizontalVelocity;
        private Vector3 _horizontalVelocityRef;

        //todo之后转移到配置表
        public float MoveSpeed = 5f;
        public float SprintSpeed = 10f;
        public float RotationSlerpSpeed = 30f;
        public float SmoothTime = 0.1f;
        public float Gravity = -9.81f;
        public float JumpHeight = 1f;

        public CharacterMotor(CharacterContext context)
        {
            _context = context;
        }

        public void Tick(CharacterIntent intent, float dt, Transform actorTransform)
        {
            TickHorizontal(intent, dt, actorTransform);
            TickVertical(intent, dt, actorTransform);
            _context.Controller.Move(_context.Velocity * dt);
        }

        private void TickHorizontal(CharacterIntent intent, float dt, Transform actorTransform)
        {
            _context.GetCameraBasis(out var camForward, out var camRight);

            var inputDir = camRight * intent.Move.x + camForward * intent.Move.y;
            var hasMoveInput = intent.Move.sqrMagnitude > 0.0001f && inputDir.sqrMagnitude > 0.0001f;

            if(hasMoveInput){
                inputDir.Normalize();
                var targetRot = Quaternion.LookRotation(inputDir);
                actorTransform.rotation = Quaternion.Slerp(actorTransform.rotation, targetRot, RotationSlerpSpeed * dt);
            }

            var speed = intent.IsSprintHeld ? SprintSpeed : MoveSpeed;
            var targetHorizontal = inputDir * speed;

            _currentHorizontalVelocity = Vector3.SmoothDamp(_currentHorizontalVelocity, targetHorizontal, ref _horizontalVelocityRef, SmoothTime);
            var v = _context.Velocity;
            v.x = _currentHorizontalVelocity.x;
            v.z = _currentHorizontalVelocity.z;
            _context.Velocity = v;
        }

        private void TickVertical(CharacterIntent intent, float dt, Transform actorTransform)
        {
            //跳跃和重力
            var v = _context.Velocity;

            if(_context.IsGrounded && v.y < 0f) v.y = -2f;

            if(intent.IsJumpPressed && _context.IsGrounded) 
            {
                v.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            }

            v.y += Gravity * dt;
            _context.Velocity = v;
        }
        


    }
}
