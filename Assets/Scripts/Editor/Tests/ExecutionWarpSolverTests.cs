using System.Collections.Generic;
using Character.Config;
using Character.Execution;
using NUnit.Framework;
using UnityEngine;

namespace Character.EditorTests.Execution
{
    public sealed class ExecutionWarpSolverTests
    {
        private readonly List<ScriptableObject> _createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                if (_createdAssets[i] != null)
                    Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void TryCreateRejectsInvalidWarpWindow()
        {
            CharacterCombatConfig config = CreateConfig();
            config.executionWarpWindowStartNormalized = 0.8f;
            config.executionWarpWindowEndNormalized = 0.2f;

            bool created = ExecutionWarpSolver.TryCreate(
                new ExecutionPose(Vector3.forward, 0f),
                new ExecutionPose(Vector3.zero, 0f),
                config,
                out ExecutionWarpSolver solver);

            Assert.That(created, Is.False);
            Assert.That(solver, Is.Null);
        }

        [Test]
        public void TryCreateRejectsInitialPoseOutsideTranslationBudget()
        {
            CharacterCombatConfig config = CreateConfig();
            config.executionMaxWarpTranslation = 0.5f;

            bool created = ExecutionWarpSolver.TryCreate(
                new ExecutionPose(new Vector3(2f, 0f, 0f), 0f),
                new ExecutionPose(Vector3.zero, 0f),
                config,
                out ExecutionWarpSolver solver);

            Assert.That(created, Is.False);
            Assert.That(solver, Is.Null);
        }

        [Test]
        public void TryWarpConvergesPositionAcrossWarpWindow()
        {
            CharacterCombatConfig config = CreateConfig();
            config.executionWarpWindowStartNormalized = 0f;
            config.executionWarpWindowEndNormalized = 0.5f;
            config.executionWarpCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            config.executionMaxWarpTranslation = 2f;

            bool created = ExecutionWarpSolver.TryCreate(
                new ExecutionPose(new Vector3(1f, 0f, 0f), 0f),
                new ExecutionPose(Vector3.zero, 0f),
                config,
                out ExecutionWarpSolver solver);

            Assert.That(created, Is.True);

            var pose = new ExecutionPose(Vector3.zero, 0f);

            Assert.That(solver.TryWarp(
                pose,
                Vector3.zero,
                0f,
                0.25f,
                out Vector3 firstDelta,
                out float firstYaw), Is.True);

            pose = Apply(pose, firstDelta, firstYaw);
            Assert.That(solver.RefreshRemainingError(pose), Is.True);

            Assert.That(pose.Position.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(solver.LastCumulativeWeight, Is.EqualTo(0.5f).Within(0.0001f));

            Assert.That(solver.TryWarp(
                pose,
                Vector3.zero,
                0f,
                0.5f,
                out Vector3 secondDelta,
                out float secondYaw), Is.True);

            pose = Apply(pose, secondDelta, secondYaw);
            Assert.That(solver.RefreshRemainingError(pose), Is.True);

            Assert.That(pose.Position.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(solver.ActualRemainingPositionError.magnitude, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(solver.IsWarpWindowComplete, Is.True);
        }

        [Test]
        public void TryWarpConvergesYawAcrossWarpWindow()
        {
            CharacterCombatConfig config = CreateConfig();
            config.executionWarpWindowStartNormalized = 0f;
            config.executionWarpWindowEndNormalized = 0.5f;
            config.executionWarpCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            config.executionMaxWarpYaw = 180f;

            bool created = ExecutionWarpSolver.TryCreate(
                new ExecutionPose(Vector3.zero, 90f),
                new ExecutionPose(Vector3.zero, 0f),
                config,
                out ExecutionWarpSolver solver);

            Assert.That(created, Is.True);

            var pose = new ExecutionPose(Vector3.zero, 0f);

            Assert.That(solver.TryWarp(
                pose,
                Vector3.zero,
                0f,
                0.5f,
                out Vector3 correctedDelta,
                out float correctedYaw), Is.True);

            pose = Apply(pose, correctedDelta, correctedYaw);
            Assert.That(solver.RefreshRemainingError(pose), Is.True);

            Assert.That(pose.Yaw, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(solver.ActualRemainingYawError, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TrajectorySamplerGeneratesFramesAndFinalResidual()
        {
            CharacterCombatConfig config = CreateConfig();
            config.executionWarpWindowStartNormalized = 0f;
            config.executionWarpWindowEndNormalized = 0.5f;
            config.executionWarpCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            config.executionMaxWarpTranslation = 2f;

            var samples = new[]
            {
                new ExecutionWarpInputSample(0.25f, Vector3.zero, 0f),
                new ExecutionWarpInputSample(0.5f, Vector3.zero, 0f),
            };

            bool sampled = ExecutionWarpTrajectorySampler.TrySample(
                new ExecutionPose(new Vector3(1f, 0f, 0f), 0f),
                new ExecutionPose(Vector3.zero, 0f),
                config,
                samples,
                out ExecutionWarpTrajectory trajectory);

            Assert.That(sampled, Is.True);
            Assert.That(trajectory, Is.Not.Null);
            Assert.That(trajectory.Frames.Count, Is.EqualTo(2));
            Assert.That(trajectory.FinalPose.Position.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(trajectory.FinalPositionError.magnitude, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TrajectorySamplerRejectsNonFiniteSamples()
        {
            CharacterCombatConfig config = CreateConfig();

            var samples = new[]
            {
                new ExecutionWarpInputSample(float.NaN, Vector3.zero, 0f),
            };

            bool sampled = ExecutionWarpTrajectorySampler.TrySample(
                new ExecutionPose(Vector3.forward, 0f),
                new ExecutionPose(Vector3.zero, 0f),
                config,
                samples,
                out ExecutionWarpTrajectory trajectory);

            Assert.That(sampled, Is.False);
            Assert.That(trajectory, Is.Null);
        }

        private CharacterCombatConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<CharacterCombatConfig>();
            _createdAssets.Add(config);

            config.executionMaxWarpTranslation = 2f;
            config.executionMaxWarpYaw = 180f;
            config.executionWarpWindowStartNormalized = 0f;
            config.executionWarpWindowEndNormalized = 0.5f;
            config.executionWarpCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            return config;
        }

        private static ExecutionPose Apply(
            in ExecutionPose pose,
            Vector3 deltaPosition,
            float deltaYaw)
        {
            Vector3 position =
                pose.Position +
                ExecutionWarpSolver.ProjectToHorizontalPlane(deltaPosition);

            float yaw = NormalizeYaw(pose.Yaw + deltaYaw);

            return new ExecutionPose(position, yaw);
        }

        private static float NormalizeYaw(float yaw)
        {
            yaw %= 360f;
            if (yaw < 0f)
                yaw += 360f;

            return yaw;
        }

        [Test]
        public void RuntimeContextMatchesSharedSolverForSameInputs()
        {
            CharacterCombatConfig config = CreateConfig();
            config.executionMaxWarpTranslation = 3f;
            config.executionMaxWarpYaw = 180f;
            config.executionWarpWindowStartNormalized = 0f;
            config.executionWarpWindowEndNormalized = 0.6f;
            config.executionWarpCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            var anchorPose = new ExecutionPose(new Vector3(1.25f, 0f, 0.75f), 90f);
            var initialPose = new ExecutionPose(Vector3.zero, 0f);

            var session = new ExecutionSession(
                1001UL,
                10,
                20,
                new ExecutionPose(new Vector3(2f, 0f, 2f), 180f),
                anchorPose,
                0d,
                1.5d,
                100f,
                false);

            Assert.That(
                ExecutionWarpContext.TryCreate(
                    session,
                    10,
                    config,
                    initialPose,
                    out ExecutionWarpContext context),
                Is.True);

            Assert.That(
                ExecutionWarpSolver.TryCreate(
                    anchorPose,
                    initialPose,
                    config,
                    out ExecutionWarpSolver solver),
                Is.True);

            var pose = initialPose;

            var samples = new[]
            {
        new ExecutionWarpInputSample(0.1f, new Vector3(0.1f, 0f, 0.05f), 5f),
        new ExecutionWarpInputSample(0.3f, new Vector3(0.1f, 0f, 0.05f), 10f),
        new ExecutionWarpInputSample(0.6f, new Vector3(0.1f, 0f, 0.05f), 15f),
    };

            for (int i = 0; i < samples.Length; i++)
            {
                ExecutionWarpInputSample sample = samples[i];

                Assert.That(
                    context.TryWarp(
                        pose,
                        sample.DeltaPosition,
                        sample.DeltaYaw,
                        sample.NormalizedTime,
                        out Vector3 contextDeltaPosition,
                        out float contextDeltaYaw),
                    Is.True);

                Assert.That(
                    solver.TryWarp(
                        pose,
                        sample.DeltaPosition,
                        sample.DeltaYaw,
                        sample.NormalizedTime,
                        out Vector3 solverDeltaPosition,
                        out float solverDeltaYaw),
                    Is.True);

                AssertVectorApproximately(contextDeltaPosition, solverDeltaPosition);
                Assert.That(contextDeltaYaw, Is.EqualTo(solverDeltaYaw).Within(0.0001f));

                pose = Apply(pose, contextDeltaPosition, contextDeltaYaw);

                Assert.That(context.RefreshRemainingError(pose), Is.True);
                Assert.That(solver.RefreshRemainingError(pose), Is.True);

                AssertVectorApproximately(
                    context.ActualRemainingPositionError,
                    solver.ActualRemainingPositionError);

                Assert.That(
                    context.ActualRemainingYawError,
                    Is.EqualTo(solver.ActualRemainingYawError).Within(0.0001f));
            }

            Assert.That(context.ExecutionId, Is.EqualTo(1001UL));
            Assert.That(context.IsWarpWindowComplete, Is.EqualTo(solver.IsWarpWindowComplete));
            Assert.That(context.TryEnd(9999UL), Is.False);
            Assert.That(context.TryEnd(1001UL), Is.True);
            Assert.That(context.IsActive, Is.False);
        }

        [Test]
        public void RuntimeContextRejectsWrongExecutorOwner()
        {
            CharacterCombatConfig config = CreateConfig();

            var session = new ExecutionSession(
                1002UL,
                10,
                20,
                new ExecutionPose(Vector3.forward, 0f),
                new ExecutionPose(Vector3.forward, 0f),
                0d,
                1d,
                100f,
                false);

            bool created = ExecutionWarpContext.TryCreate(
                session,
                20,
                config,
                new ExecutionPose(Vector3.zero, 0f),
                out ExecutionWarpContext context);

            Assert.That(created, Is.False);
            Assert.That(context, Is.Null);
        }

        private static void AssertVectorApproximately(
            Vector3 actual,
            Vector3 expected,
            float tolerance = 0.0001f)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance));
        }
    }
}