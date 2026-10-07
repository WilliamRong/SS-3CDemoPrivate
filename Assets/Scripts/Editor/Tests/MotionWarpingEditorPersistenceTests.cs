using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Reflection;
using Character.Config;
using Character.EditorTools.Execution;
using Character.Execution;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Character.EditorTests.Execution
{
    public sealed class MotionWarpingEditorPersistenceTests
    {
        private string _testFolder;
        private string _testAssetPath;
        private CharacterCombatConfig _config;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();

            string folderName =
                "__MotionWarpingEditorTests_" +
                System.Guid.NewGuid().ToString("N");

            _testFolder =
                $"Assets/{folderName}";

            string folderGuid =
                AssetDatabase.CreateFolder(
                    "Assets",
                    folderName);

            Assert.That(
                folderGuid,
                Is.Not.Empty,
                "无法创建测试资产目录。");

            _testAssetPath =
                $"{_testFolder}/CombatConfig.asset";

            _config =
                ScriptableObject.CreateInstance<
                    CharacterCombatConfig>();

            _config.executionMaxWarpTranslation =
                0.5f;

            AssetDatabase.CreateAsset(
                _config,
                _testAssetPath);

            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssetIfDirty(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();

            _config = null;

            if (!string.IsNullOrEmpty(_testFolder) &&
                AssetDatabase.IsValidFolder(_testFolder))
            {
                AssetDatabase.DeleteAsset(_testFolder);
            }

            AssetDatabase.Refresh();
        }

        [Test]
        public void CombatConfigSupportsUndoRedoAndPersistence()
        {
            const float initialValue = 0.5f;
            const float editedValue = 1.25f;

            using var serialized =
                new SerializedObject(_config);

            serialized.Update();

            SerializedProperty translationProperty =
                serialized.FindProperty(
                    "executionMaxWarpTranslation");

            Assert.That(
                translationProperty,
                Is.Not.Null);

            Undo.RecordObject(
                _config,
                "Test Motion Warping Config Edit");

            translationProperty.floatValue =
                editedValue;

            Assert.That(
                serialized.ApplyModifiedPropertiesWithoutUndo(),
                Is.True);

            EditorUtility.SetDirty(_config);
            Undo.FlushUndoRecordObjects();

            Assert.That(
                _config.executionMaxWarpTranslation,
                Is.EqualTo(editedValue).Within(0.0001f));

            Assert.That(
                EditorUtility.IsDirty(_config),
                Is.True);

            Undo.PerformUndo();

            Assert.That(
                _config.executionMaxWarpTranslation,
                Is.EqualTo(initialValue).Within(0.0001f));

            Undo.PerformRedo();

            Assert.That(
                _config.executionMaxWarpTranslation,
                Is.EqualTo(editedValue).Within(0.0001f));

            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssetIfDirty(_config);

            Assert.That(
                EditorUtility.IsDirty(_config),
                Is.False);

            AssetDatabase.ImportAsset(
                _testAssetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            CharacterCombatConfig reloaded =
                AssetDatabase.LoadAssetAtPath<
                    CharacterCombatConfig>(
                    _testAssetPath);

            Assert.That(reloaded, Is.Not.Null);

            Assert.That(
                reloaded.executionMaxWarpTranslation,
                Is.EqualTo(editedValue).Within(0.0001f));
        }

        [Test]
        public void TargetWarpConfigHasSafeDefaultsAndPersists()
        {
            Assert.That(
                _config.executedMaxWarpTranslation,
                Is.GreaterThanOrEqualTo(0f));
            Assert.That(
                _config.executedMaxWarpYaw,
                Is.InRange(0f, 180f));
            Assert.That(
                _config.executedWarpWindowEndNormalized,
                Is.GreaterThanOrEqualTo(
                    _config.executedWarpWindowStartNormalized));
            Assert.That(_config.executedWarpCurve, Is.Not.Null);

            Vector3 expectedOffset =
                new Vector3(0.25f, 0f, -2.75f);

            Vector3 initialOffset =
                _config.executedDeathAnchorOffset;

            using (var serialized = new SerializedObject(_config))
            {
                serialized.Update();
                SerializedProperty offsetProperty =
                    serialized.FindProperty(
                        "executedDeathAnchorOffset");

                Assert.That(offsetProperty, Is.Not.Null);
                Undo.RecordObject(
                    _config,
                    "Test Target Warp Config Edit");
                offsetProperty.vector3Value = expectedOffset;
                Assert.That(
                    serialized.ApplyModifiedPropertiesWithoutUndo(),
                    Is.True);
            }

            EditorUtility.SetDirty(_config);
            Undo.FlushUndoRecordObjects();
            Assert.That(EditorUtility.IsDirty(_config), Is.True);

            Undo.PerformUndo();
            Assert.That(
                _config.executedDeathAnchorOffset,
                Is.EqualTo(initialOffset));

            Undo.PerformRedo();
            Assert.That(
                _config.executedDeathAnchorOffset,
                Is.EqualTo(expectedOffset));

            _config.executedDeathAnchorYawOffset = 12f;
            _config.executedMaxWarpTranslation = 5f;
            _config.executedWarpWindowStartNormalized = 0.15f;
            _config.executedWarpWindowEndNormalized = 0.85f;

            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssetIfDirty(_config);
            AssetDatabase.ImportAsset(
                _testAssetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            CharacterCombatConfig reloaded =
                AssetDatabase.LoadAssetAtPath<CharacterCombatConfig>(
                    _testAssetPath);

            Assert.That(reloaded, Is.Not.Null);
            Assert.That(
                reloaded.executedDeathAnchorOffset,
                Is.EqualTo(expectedOffset));
            Assert.That(
                reloaded.executedDeathAnchorYawOffset,
                Is.EqualTo(12f).Within(0.0001f));
            Assert.That(
                reloaded.executedMaxWarpTranslation,
                Is.EqualTo(5f).Within(0.0001f));
            Assert.That(
                reloaded.executedWarpWindowStartNormalized,
                Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(
                reloaded.executedWarpWindowEndNormalized,
                Is.EqualTo(0.85f).Within(0.0001f));
        }

        [Test]
        public void PreviewLayoutSupportsAssetPersistence()
        {
            string layoutPath =
                $"{_testFolder}/PreviewLayout.asset";

            var layout =
                ScriptableObject.CreateInstance<
                    MotionWarpingPreviewLayout>();

            AssetDatabase.CreateAsset(layout, layoutPath);

            MotionWarpingVisualEditorWindow window =
                ScriptableObject.CreateInstance<
                    MotionWarpingVisualEditorWindow>();

            Vector3 executorPosition =
                new Vector3(1.25f, 0.1f, -2.5f);
            Vector3 executorEulerAngles =
                new Vector3(3f, 145f, -4f);
            Vector3 targetPosition =
                new Vector3(-0.5f, 0.2f, 4f);
            Vector3 targetEulerAngles =
                new Vector3(-2f, 25f, 6f);

            try
            {
                SetPrivateField(
                    window,
                    "_previewLayout",
                    layout);

                SetPrivateField(
                    window,
                    "_configuredExecutorPosition",
                    executorPosition);

                SetPrivateField(
                    window,
                    "_configuredExecutorEulerAngles",
                    executorEulerAngles);

                SetPrivateField(
                    window,
                    "_configuredTargetPosition",
                    targetPosition);

                SetPrivateField(
                    window,
                    "_configuredTargetEulerAngles",
                    targetEulerAngles);

                InvokePrivateMethod(
                    window,
                    "SaveSelectedPreviewLayout");

                AssetDatabase.ImportAsset(
                    layoutPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                MotionWarpingPreviewLayout reloaded =
                    AssetDatabase.LoadAssetAtPath<
                        MotionWarpingPreviewLayout>(layoutPath);

                Assert.That(reloaded, Is.Not.Null);
                Assert.That(
                    reloaded.executorPosition,
                    Is.EqualTo(executorPosition));
                Assert.That(
                    reloaded.executorEulerAngles,
                    Is.EqualTo(executorEulerAngles));
                Assert.That(
                    reloaded.targetPosition,
                    Is.EqualTo(targetPosition));
                Assert.That(
                    reloaded.targetEulerAngles,
                    Is.EqualTo(targetEulerAngles));

                SetPrivateField(
                    window,
                    "_previewLayout",
                    reloaded);

                SetPrivateField(
                    window,
                    "_configuredExecutorPosition",
                    Vector3.zero);

                SetPrivateField(
                    window,
                    "_configuredTargetPosition",
                    Vector3.zero);

                InvokePrivateMethod(
                    window,
                    "LoadSelectedPreviewLayout");

                Assert.That(
                    GetPrivateField<Vector3>(
                        window,
                        "_configuredExecutorPosition"),
                    Is.EqualTo(executorPosition));
                Assert.That(
                    GetPrivateField<Vector3>(
                        window,
                        "_configuredTargetPosition"),
                    Is.EqualTo(targetPosition));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MissingCombatConfigBlocksPreviewValidity()
        {
            MotionWarpingVisualEditorWindow window =
               ScriptableObject.CreateInstance<
                   MotionWarpingVisualEditorWindow>();

            try
            {
                MethodInfo evaluateMethod =
                    typeof(MotionWarpingVisualEditorWindow)
                        .GetMethod(
                            "TryEvaluatePreviewState",
                            BindingFlags.Instance |
                            BindingFlags.NonPublic);

                Assert.That(
                    evaluateMethod,
                    Is.Not.Null);

                object[] arguments =
                {
                    null,
                    null,
                };

                bool previewValid =
                    (bool)evaluateMethod.Invoke(
                        window,
                        arguments);

                Assert.That(previewValid, Is.False);
                Assert.That(arguments[0], Is.Null);
                Assert.That(arguments[1], Is.TypeOf<string>());

                StringAssert.Contains(
                    "Config Invalid",
                    (string)arguments[1]);

                StringAssert.Contains(
                    "Preview Blocked",
                    (string)arguments[1]);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DefaultPreviewPoseIsFaceToFaceAndSpatiallyEligible()
        {
            Scene previewScene =
                EditorSceneManager.NewPreviewScene();

            MotionWarpingVisualEditorWindow window =
                ScriptableObject.CreateInstance<
                    MotionWarpingVisualEditorWindow>();

            GameObject executor = null;
            GameObject target = null;

            try
            {
                executor = new GameObject("Executor");
                target = new GameObject("Target");

                SceneManager.MoveGameObjectToScene(
                    executor,
                    previewScene);

                SceneManager.MoveGameObjectToScene(
                    target,
                    previewScene);

                SetPrivateField(
                    window,
                    "_combatConfig",
                    _config);

                SetPrivateField(
                    window,
                    "_previewExecutorInstance",
                    executor);

                SetPrivateField(
                    window,
                    "_previewTargetInstance",
                    target);

                InvokePrivateMethod(
                    window,
                    "ConfigureDefaultPreviewPoses");

                ExecutionEligibilityResult eligibility =
                    ExecutionSpatialValidator.Evaluate(
                        executor.transform,
                        target.transform,
                        _config);

                Vector3 executorToTarget =
                    (target.transform.position -
                     executor.transform.position).normalized;

                Vector3 targetToExecutor =
                    (executor.transform.position -
                     target.transform.position).normalized;

                Assert.That(eligibility.IsEligible, Is.True);
                Assert.That(
                    Vector3.Dot(
                        executor.transform.forward,
                        executorToTarget),
                    Is.EqualTo(1f).Within(0.0001f));
                Assert.That(
                    Vector3.Dot(
                        target.transform.forward,
                        targetToExecutor),
                    Is.EqualTo(1f).Within(0.0001f));
                Assert.That(
                    eligibility.WarpTranslationError,
                    Is.EqualTo(0f).Within(0.0001f));
                Assert.That(
                    eligibility.WarpYawError,
                    Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                if (window != null)
                {
                    Object.DestroyImmediate(window);
                }

                if (executor != null)
                {
                    Object.DestroyImmediate(executor);
                }

                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }

                if (previewScene.IsValid() &&
                    previewScene.isLoaded)
                {
                    EditorSceneManager.ClosePreviewScene(
                        previewScene);
                }
            }
        }

        [Test]
        public void CustomPreviewLayoutAppliesBothTransforms()
        {
            Scene previewScene =
                EditorSceneManager.NewPreviewScene();

            MotionWarpingVisualEditorWindow window =
                ScriptableObject.CreateInstance<
                    MotionWarpingVisualEditorWindow>();

            GameObject executor = null;
            GameObject target = null;

            Vector3 targetPosition =
                new Vector3(5f, 0.2f, -2f);
            Vector3 targetEulerAngles =
                new Vector3(4f, 35f, -2f);

            Vector3 executorPosition =
                targetPosition +
                Quaternion.Euler(0f, 35f, 0f) *
                _config.executorAnchorOffset;

            Vector3 executorEulerAngles =
                new Vector3(7f, 215f, -3f);

            try
            {
                executor = new GameObject("Executor");
                target = new GameObject("Target");

                SceneManager.MoveGameObjectToScene(
                    executor,
                    previewScene);

                SceneManager.MoveGameObjectToScene(
                    target,
                    previewScene);

                SetPrivateField(
                    window,
                    "_combatConfig",
                    _config);

                SetPrivateField(
                    window,
                    "_previewExecutorInstance",
                    executor);

                SetPrivateField(
                    window,
                    "_previewTargetInstance",
                    target);

                SetPrivateField(
                    window,
                    "_configuredExecutorPosition",
                    executorPosition);

                SetPrivateField(
                    window,
                    "_configuredExecutorEulerAngles",
                    executorEulerAngles);

                SetPrivateField(
                    window,
                    "_configuredTargetPosition",
                    targetPosition);

                SetPrivateField(
                    window,
                    "_configuredTargetEulerAngles",
                    targetEulerAngles);

                SetPrivateField(
                    window,
                    "_useCombatDefaultPreviewLayout",
                    false);

                InvokePrivateMethod(
                    window,
                    "ApplyConfiguredPreviewPoses");

                Assert.That(
                    executor.transform.position,
                    Is.EqualTo(executorPosition));
                Assert.That(
                    target.transform.position,
                    Is.EqualTo(targetPosition));
                Assert.That(
                    Quaternion.Angle(
                        executor.transform.rotation,
                        Quaternion.Euler(
                            executorEulerAngles)),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(
                        target.transform.rotation,
                        Quaternion.Euler(targetEulerAngles)),
                    Is.LessThan(0.001f));

                ExecutionEligibilityResult eligibility =
                    ExecutionSpatialValidator.Evaluate(
                        executor.transform,
                        target.transform,
                        _config);

                Assert.That(eligibility.IsEligible, Is.True);
            }
            finally
            {
                if (window != null)
                {
                    Object.DestroyImmediate(window);
                }

                if (executor != null)
                {
                    Object.DestroyImmediate(executor);
                }

                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }

                if (previewScene.IsValid() &&
                    previewScene.isLoaded)
                {
                    EditorSceneManager.ClosePreviewScene(
                        previewScene);
                }
            }
        }

        [Test]
        public void PreviewTimingUsesSharedElapsedSecondsForDifferentDurations()
        {
            Assert.That(
                MotionWarpingPreviewTiming.TryEvaluate(
                    2.7f,
                    2.7f,
                    4.3f,
                    out MotionWarpingPreviewTimeSample sample),
                Is.True);

            Assert.That(
                sample.ElapsedTime,
                Is.EqualTo(2.7f).Within(0.0001f));
            Assert.That(
                sample.PreviewDuration,
                Is.EqualTo(4.3f).Within(0.0001f));
            Assert.That(
                sample.PreviewNormalizedTime,
                Is.EqualTo(2.7f / 4.3f).Within(0.0001f));
            Assert.That(
                sample.ExecutorNormalizedTime,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                sample.TargetNormalizedTime,
                Is.EqualTo(2.7f / 4.3f).Within(0.0001f));

            Assert.That(
                MotionWarpingPreviewTiming.TryEvaluate(
                    4.3f,
                    2.7f,
                    4.3f,
                    out sample),
                Is.True);

            Assert.That(
                sample.ExecutorNormalizedTime,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                sample.TargetNormalizedTime,
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void PreviewWindowMapsRuntimeDurationsToIndependentNormalizedTimes()
        {
            _config.executingDuration = 2.7f;
            _config.executedDuration = 4.3f;

            MotionWarpingVisualEditorWindow window =
                ScriptableObject.CreateInstance<
                    MotionWarpingVisualEditorWindow>();

            try
            {
                SetPrivateField(
                    window,
                    "_combatConfig",
                    _config);

                InvokePrivateMethod(
                    window,
                    "SetScrubElapsedTime",
                    2.7f,
                    false);

                Assert.That(
                    GetPrivateField<float>(
                        window,
                        "_previewElapsedTime"),
                    Is.EqualTo(2.7f).Within(0.0001f));

                Assert.That(
                    GetPrivateField<float>(
                        window,
                        "_scrubNormalizedTime"),
                    Is.EqualTo(1f).Within(0.0001f));

                Assert.That(
                    GetPrivateField<float>(
                        window,
                        "_targetPreviewNormalizedTime"),
                    Is.EqualTo(2.7f / 4.3f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PreviewTimingRejectsInvalidDurations()
        {
            Assert.That(
                MotionWarpingPreviewTiming.TryEvaluate(
                    1f,
                    0f,
                    1f,
                    out _),
                Is.False);

            Assert.That(
                MotionWarpingPreviewTiming.TryEvaluate(
                    1f,
                    1f,
                    float.NaN,
                    out _),
                Is.False);
        }

        [Test]
        public void PreviewPlayableSamplesHumanoidPose()
        {
            const string prefabPath =
                "Assets/Prefabs/Characters/Player.prefab";

            const string clipPath =
                "Assets/ARPGSamurai/Animations/Humanoid/rig_Execute.anim";

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    clipPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(clip, Is.Not.Null);

            Scene previewScene =
                EditorSceneManager.NewPreviewScene();

            GameObject instance = null;
            var player =
                new MotionWarpingAnimationPreviewPlayer();

            try
            {
                instance =
                    PrefabUtility.InstantiatePrefab(
                        prefab,
                        previewScene) as GameObject;

                Assert.That(instance, Is.Not.Null);

                Animator animator =
                    instance.GetComponentInChildren<Animator>(true);

                Assert.That(animator, Is.Not.Null);

                Transform arm =
                    animator.GetBoneTransform(
                        HumanBodyBones.RightUpperArm);

                Assert.That(arm, Is.Not.Null);

                Assert.That(
                    player.TrySample(
                        instance,
                        clip,
                        0f,
                        out string startError),
                    Is.True,
                    startError);

                Quaternion startRotation =
                    arm.localRotation;

                Assert.That(
                    player.TrySample(
                        instance,
                        clip,
                        0.5f,
                        out string halfError),
                    Is.True,
                    halfError);

                Quaternion halfRotation =
                    arm.localRotation;

                Assert.That(
                    Quaternion.Angle(
                        startRotation,
                        halfRotation),
                    Is.GreaterThan(1f));
            }
            finally
            {
                player.Dispose();

                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }

                if (previewScene.IsValid() &&
                    previewScene.isLoaded)
                {
                    EditorSceneManager.ClosePreviewScene(
                        previewScene);
                }
            }
        }

        [Test]
        public void VisualEditorBuildsTargetWarpTrajectoryForBothBranches()
        {
            _config.executedAnchorOffset =
                new Vector3(0f, 0f, -2f);
            _config.executedDeathAnchorOffset =
                new Vector3(0f, 0f, -3f);
            _config.executedMaxWarpTranslation = 4f;
            _config.executedMaxWarpYaw = 180f;
            _config.executedWarpWindowStartNormalized = 0f;
            _config.executedWarpWindowEndNormalized = 1f;
            _config.executedWarpCurve =
                AnimationCurve.Linear(0f, 0f, 1f, 1f);

            var clip = new AnimationClip();
            var target = new GameObject("TargetTrajectoryPreviewTest");
            MotionWarpingVisualEditorWindow window =
                ScriptableObject.CreateInstance<
                    MotionWarpingVisualEditorWindow>();

            try
            {
                var samples = new[]
                {
                    new ExecutionWarpInputSample(
                        1f,
                        new Vector3(0f, 0f, -1f),
                        0f),
                };

                var rootMotionSample =
                    new MotionWarpingRootMotionSample(
                        clip,
                        60,
                        samples,
                        new[] { Vector3.zero, Vector3.back },
                        new[] { 0f, 0f },
                        Vector3.back,
                        0f);

                SetPrivateField(window, "_combatConfig", _config);
                SetPrivateField(
                    window,
                    "_previewTargetInstance",
                    target);
                SetPrivateField(
                    window,
                    "_previewTargetInitialPosition",
                    Vector3.zero);
                SetPrivateField(
                    window,
                    "_previewTargetInitialYaw",
                    0f);
                SetPrivateField(
                    window,
                    "_targetRootMotionSample",
                    rootMotionSample);

                MethodInfo buildMethod =
                    typeof(MotionWarpingVisualEditorWindow)
                        .GetMethod(
                            "TryBuildTargetTrajectory",
                            BindingFlags.Instance |
                            BindingFlags.NonPublic);

                Assert.That(buildMethod, Is.Not.Null);

                object[] survivingArgs = { null };
                Assert.That(
                    (bool)buildMethod.Invoke(
                        window,
                        survivingArgs),
                    Is.True);

                var survivingTrajectory =
                    (ExecutionWarpTrajectory)survivingArgs[0];
                Assert.That(
                    survivingTrajectory.FinalPose.Position.z,
                    Is.EqualTo(-2f).Within(0.0001f));

                FieldInfo branchField =
                    typeof(MotionWarpingVisualEditorWindow)
                        .GetField(
                            "_targetPreviewBranch",
                            BindingFlags.Instance |
                            BindingFlags.NonPublic);

                Assert.That(branchField, Is.Not.Null);
                branchField.SetValue(
                    window,
                    System.Enum.ToObject(
                        branchField.FieldType,
                        1));

                object[] lethalArgs = { null };
                Assert.That(
                    (bool)buildMethod.Invoke(
                        window,
                        lethalArgs),
                    Is.True);

                var lethalTrajectory =
                    (ExecutionWarpTrajectory)lethalArgs[0];
                Assert.That(
                    lethalTrajectory.FinalPose.Position.z,
                    Is.EqualTo(-3f).Within(0.0001f));
                Assert.That(
                    lethalTrajectory.FinalPositionError.magnitude,
                    Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void DisposePreviewSceneDestroysObjectsAndClosesOwnedScene()
        {
            const string previewSceneSourcePath =
                "Assets/Scenes/MotionWarpingVisualToolPreview.unity";

            Scene previousActiveScene = SceneManager.GetActiveScene();

            Scene previewScene = default;

            MotionWarpingVisualEditorWindow window =
                ScriptableObject.CreateInstance<
                    MotionWarpingVisualEditorWindow>();

            GameObject previewRoot = null;
            GameObject executor = null;
            GameObject target = null;

            try
            {
                string previewSceneAssetPath =
                    $"{_testFolder}/PreviewScene.unity";

                Assert.That(
                    AssetDatabase.CopyAsset(
                        previewSceneSourcePath,
                        previewSceneAssetPath),
                    Is.True,
                    "无法复制预览场景到测试临时目录。");

                AssetDatabase.ImportAsset(
                    previewSceneAssetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                previewScene =
                    EditorSceneManager.OpenScene(
                        previewSceneAssetPath,
                        OpenSceneMode.Additive);

                Assert.That(
                    previewScene.IsValid(),
                    Is.True);

                Assert.That(
                    previewScene.isLoaded,
                    Is.True);

                previewRoot =
                    new GameObject(
                        "MotionWarpingPreviewRoot");

                executor =
                    new GameObject(
                        "MotionWarpingPreview_Executor");

                target =
                    new GameObject(
                        "MotionWarpingPreview_Target");

                previewRoot.hideFlags =
                    HideFlags.HideAndDontSave;

                executor.hideFlags =
                    HideFlags.HideAndDontSave;

                target.hideFlags =
                    HideFlags.HideAndDontSave;

                SceneManager.MoveGameObjectToScene(
                    previewRoot,
                    previewScene);

                SceneManager.MoveGameObjectToScene(
                    executor,
                    previewScene);

                SceneManager.MoveGameObjectToScene(
                    target,
                    previewScene);

                executor.transform.SetParent(
                    previewRoot.transform,
                    false);

                target.transform.SetParent(
                    previewRoot.transform,
                    false);

                SetPrivateField(
                    window,
                    "_previewScene",
                    previewScene);

                SetPrivateField(
                    window,
                    "_previewSceneOpenedByWindow",
                    true);

                SetPrivateField(
                    window,
                    "_previewScenePath",
                    "Temporary Test Scene");

                SetPrivateField(
                    window,
                    "_previewRoot",
                    previewRoot);

                SetPrivateField(
                    window,
                    "_previewExecutorInstance",
                    executor);

                SetPrivateField(
                    window,
                    "_previewTargetInstance",
                    target);

                Selection.activeObject = executor;

                InvokePrivateMethod(
                    window,
                    "DisposePreviewScene");

                // UnityEngine.Object 使用 fake-null 语义，
                // 因此这里显式使用 == null。
                Assert.That(
                    previewRoot == null,
                    Is.True);

                Assert.That(
                    executor == null,
                    Is.True);

                Assert.That(
                    target == null,
                    Is.True);

                Assert.That(
                    Selection.activeObject,
                    Is.Null);

                Assert.That(
                    previewScene.isLoaded,
                    Is.False);

                Scene storedScene =
                    GetPrivateField<Scene>(
                        window,
                        "_previewScene");

                Assert.That(
                    storedScene.IsValid(),
                    Is.False);

                Assert.That(
                    GetPrivateField<GameObject>(
                        window,
                        "_previewRoot"),
                    Is.Null);

                Assert.That(
                    GetPrivateField<GameObject>(
                        window,
                        "_previewExecutorInstance"),
                    Is.Null);

                Assert.That(
                    GetPrivateField<GameObject>(
                        window,
                        "_previewTargetInstance"),
                    Is.Null);

                Assert.That(
                    GetPrivateField<bool>(
                        window,
                        "_previewSceneOpenedByWindow"),
                    Is.False);

                Assert.That(
                    GetPrivateField<string>(
                        window,
                        "_previewScenePath"),
                    Is.Empty);
            }
            finally
            {
                Selection.activeObject = null;

                // 断言中途失败时也必须清理测试环境。
                if (executor != null)
                {
                    Object.DestroyImmediate(executor);
                }

                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }

                if (previewRoot != null)
                {
                    Object.DestroyImmediate(previewRoot);
                }

                if (previewScene.IsValid() &&
                    previewScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(
                        previewScene,
                        true);
                }

                if (previousActiveScene.IsValid() &&
                    previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(
                        previousActiveScene);
                }

                if (window != null)
                {
                    Object.DestroyImmediate(window);
                }
            }
        }

        private static void SetPrivateField<T>(
            MotionWarpingVisualEditorWindow window,
            string fieldName,
            T value)
        {
            FieldInfo field =
                typeof(MotionWarpingVisualEditorWindow)
                    .GetField(
                        fieldName,
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"找不到字段：{fieldName}");

            field.SetValue(window, value);
        }

        private static T GetPrivateField<T>(
            MotionWarpingVisualEditorWindow window,
            string fieldName)
        {
            FieldInfo field =
                typeof(MotionWarpingVisualEditorWindow)
                    .GetField(
                        fieldName,
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"找不到字段：{fieldName}");

            return (T)field.GetValue(window);
        }

        private static void InvokePrivateMethod(
            MotionWarpingVisualEditorWindow window,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method =
                typeof(MotionWarpingVisualEditorWindow)
                    .GetMethod(
                        methodName,
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

            Assert.That(
                method,
                Is.Not.Null,
                $"找不到方法：{methodName}");

            method.Invoke(window, arguments);
        }
    }
}
