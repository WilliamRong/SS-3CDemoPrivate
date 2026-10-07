using System.Collections.Generic;
using AI;
using AI.NpcStates;
using Character.Config;
using Character.Core;
using Character.Execution;
using Character.Motor;
using Character.StateMachine.States;
using Character.Sync;
using Mirror;
using NUnit.Framework;
using UnityEngine;

namespace Character.EditorTests.Execution
{
    public sealed class ExecutionTargetWarpRuntimeTests
    {
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _createdObjects.Count - 1;
                 index >= 0;
                 index--)
            {
                if (_createdObjects[index] != null)
                    Object.DestroyImmediate(_createdObjects[index]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void PlayerExecutedStateProducesAndConsumesTargetWarpDelta()
        {
            CharacterCombatConfig combat = CreateCombatConfig();
            CharacterLocomotionConfig locomotion =
                CreateAsset<CharacterLocomotionConfig>();
            locomotion.gravity = 0f;
            GameObject root = CreateGameObject("PlayerTargetWarpTest");
            CharacterController controller =
                root.AddComponent<CharacterController>();
            controller.detectCollisions = false;
            var context = new CharacterContext(
                controller,
                root.transform,
                null);
            var motor = new CharacterMotor(context, locomotion);
            var state = new ExecutedState(motor, combat);
            ExecutionSession session = CreateSession(false);

            Assert.That(state.TryPrepare(session, 20), Is.True);
            state.Enter();
            root.transform.position = Vector3.up * 10f;
            state.Tick(default, 1f);

            Assert.That(
                state.TryWarpRootMotion(
                    new Vector3(0f, 0f, -0.25f),
                    Quaternion.identity,
                    out Vector3 correctedDelta,
                    out Quaternion correctedRotation),
                Is.True);
            Assert.That(correctedDelta.z, Is.LessThan(-0.25f));

            float beforeZ = root.transform.position.z;
            motor.SetAttackRootMotionDelta(
                correctedDelta,
                correctedRotation);
            state.Tick(default, 0.1f);

            Assert.That(root.transform.position.z, Is.LessThan(beforeZ));
            state.Exit();
        }

        [Test]
        public void NpcExecutedStateUsesSameTargetWarpContext()
        {
            CharacterCombatConfig combat = CreateCombatConfig();
            GameObject root = CreateGameObject("NpcTargetWarpTest");
            root.AddComponent<UnityEngine.AI.NavMeshAgent>();
            NpcMotor motor = root.AddComponent<NpcMotor>();
            var state = new NpcExecutedState(motor, combat);
            ExecutionSession session = CreateSession(true);

            Assert.That(state.TryPrepare(session, 20), Is.True);
            state.Enter();
            state.Tick(default, 1f);

            Assert.That(
                state.TryWarpRootMotion(
                    new Vector3(0f, 0f, -0.25f),
                    Quaternion.identity,
                    out Vector3 correctedDelta,
                    out _),
                Is.True);
            Assert.That(correctedDelta.z, Is.LessThan(-0.25f));
            Assert.That(
                state.WarpContext.Participant,
                Is.EqualTo(ExecutionWarpParticipant.Target));
            Assert.That(
                state.WarpContext.AnchorPose.Position.z,
                Is.EqualTo(-3f).Within(0.0001f));
            state.Exit();
        }

        [Test]
        public void ExecutionStateMessageRoundTripsBothCurrentPoses()
        {
            var message = new ExecutionStateMsg
            {
                RequestSeq = 7,
                RequestedActorId = 10,
                HasState = 1,
                ExecutionId = 3001UL,
                ExecutorActorId = 10,
                TargetActorId = 20,
                ExecutorPx = 1.25f,
                ExecutorPy = 2.5f,
                ExecutorPz = 3.75f,
                ExecutorYaw = 45f,
                TargetPx = -4.5f,
                TargetPy = 0.25f,
                TargetPz = 6.75f,
                TargetYaw = 135f,
            };

            using NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.Write(message);

            using NetworkReaderPooled reader =
                NetworkReaderPool.Get(writer.ToArraySegment());
            ExecutionStateMsg restored =
                reader.Read<ExecutionStateMsg>();

            Assert.That(restored.ExecutorPx, Is.EqualTo(1.25f));
            Assert.That(restored.ExecutorPy, Is.EqualTo(2.5f));
            Assert.That(restored.ExecutorPz, Is.EqualTo(3.75f));
            Assert.That(restored.ExecutorYaw, Is.EqualTo(45f));
            Assert.That(restored.TargetPx, Is.EqualTo(-4.5f));
            Assert.That(restored.TargetPy, Is.EqualTo(0.25f));
            Assert.That(restored.TargetPz, Is.EqualTo(6.75f));
            Assert.That(restored.TargetYaw, Is.EqualTo(135f));
        }

        private CharacterCombatConfig CreateCombatConfig()
        {
            CharacterCombatConfig config =
                CreateAsset<CharacterCombatConfig>();
            config.executedDuration = 2f;
            config.executedDeathDuration = 2f;
            config.executedAnchorOffset =
                new Vector3(0f, 0f, -2f);
            config.executedDeathAnchorOffset =
                new Vector3(0f, 0f, -3f);
            config.executedMaxWarpTranslation = 4f;
            config.executedMaxWarpYaw = 180f;
            config.executedWarpWindowStartNormalized = 0f;
            config.executedWarpWindowEndNormalized = 1f;
            config.executedWarpCurve =
                AnimationCurve.Linear(0f, 0f, 1f, 1f);
            return config;
        }

        private static ExecutionSession CreateSession(bool lethal)
        {
            return new ExecutionSession(
                3001UL,
                10,
                20,
                new ExecutionPose(Vector3.zero, 0f),
                new ExecutionPose(Vector3.forward, 180f),
                0d,
                2d,
                50f,
                lethal);
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        private T CreateAsset<T>()
            where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(asset);
            return asset;
        }
    }
}
