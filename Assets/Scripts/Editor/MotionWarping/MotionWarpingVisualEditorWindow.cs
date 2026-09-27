using Character.Config;
using Character.Execution;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Character.EditorTools.Execution
{
    public sealed class MotionWarpingVisualEditorWindow : EditorWindow
    {

        private const string DefaultCombatConfigPath =
            "Assets/Data/Character/DefaultCombat.asset";

        private const string DefaultPresentationConfigPath =
            "Assets/Data/Character/DefaultPresentation.asset";

        private const string DefaultPreviewPrefabPath =
            "Assets/Prefabs/Characters/Player.prefab";

        private const string DefaultExecuteClipPath =
            "Assets/ARPGSamurai/Animations/Humanoid/rig_Execute.anim";

        private const string DefaultExecutedClipPath =
            "Assets/ARPGSamurai/Animations/Humanoid/rig_Executed.anim";

        private const string DefaultExecutedDeathClipPath =
            "Assets/ARPGSamurai/Animations/Humanoid/rig_Executed_Death.anim";

        private string _defaultResourceMessage;

        private const string WindowTitle = "Motion Warping Visual Editor";

        private CharacterCombatConfig _combatConfig;
        private CharacterPresentationConfig _presentationConfig;

        private GameObject _executorPreviewPrefab;
        private GameObject _targetPreviewPrefab;

        private AnimationClip _executeClip;
        private AnimationClip _executedClip;
        private AnimationClip _executedDeathClip;

        private Vector2 _scroll;


        private const int ExecuteSampleRate = 60;

        private MotionWarpingRootMotionSample
            _executeRootMotionSample;

        private AnimationClip _sampledExecuteClip;

        private GameObject _sampledExecutorPrefab;

        private string _executeSamplingMessage;

        //========================Trajectory==============================
        private ExecutionWarpTrajectory _executeTrajectory;

        private bool _showTrajectoryDiagnostics = true;

        //==========================Timeline==============================

        private float _scrubNormalizedTime = 0f;

        //==========================PreivewScene==========================

        private const string DefaultPreviewScenePath = "Assets/Scenes/MotionWarpingVisualToolPreview.unity";

        private SceneAsset _previewSceneAsset;

        private Scene _previewScene;

        private bool _previewSceneOpenedByWindow;

        private GameObject _previewRoot;
        private GameObject _previewExecutorInstance;

        private GameObject _previewTargetInstance;

        private GameObject _previewExecutorSource;
        private GameObject _previewTargetSource;
        private string _previewScenePath;


        private CharacterCombatConfig _previewCombatSource;
        private CharacterPresentationConfig _previewPresentationSource;


        //========================Dirty===================================
        private string _saveStatusMessage = string.Empty;

        private MessageType _saveStatusMessageType = MessageType.Info;

        //========================TargetPosePreview========================

        private enum TargetPreviewBranch
        {
            Executed,
            ExecutedDeath,
        }

        private TargetPreviewBranch _targetPreviewBranch =
        TargetPreviewBranch.Executed;

        private float _targetPreviewNormalizedTime = 0f;

        private string _targetPreviewMessage = string.Empty;

        private bool _executeSamplingAttempted;
        private bool _targetPoseSamplingAttempted;
        private AnimationClip _lastTargetPoseClip;
        private TargetPreviewBranch _lastTargetPoseBranch;
        private float _lastTargetPoseNormalizedTime = -1f;

        private AnimationClip _sampledTargetPreviewClip;
        private TargetPreviewBranch _sampledTargetPreviewBranch;
        private float _sampledTargetPreviewNormalizedTime = -1f;


        [MenuItem("Tools/SS3C/Execution/Motion Warping Visual Editor")]
        public static void Open()
        {
            var window = GetWindow<MotionWarpingVisualEditorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(520f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();


            DrawDefaultResourceToolBar();
            DrawAssetSection();

            SyncPreviewScene();
            DrawPreviewSceneStatus();


            DrawCombatConfigSection();
            DrawPresentationConfigSection();
            DrawConfigSaveSection();
            DrawClipSection();
            DrawExecuteRootMotionSamplingSection();

            DrawTargetPosePreviewSection();
            DrawTimelineSection();
            DrawTrajectoryDiagnosticsSection();
            DrawDiagnosticsSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "在隔离 PreviewScene 中采样 Root Motion，并用 Scene View 轨迹和 Handle 校准 Motion Warping。",
                MessageType.Info);
        }

        private void DrawDefaultResourceToolBar()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("填充默认资源", GUILayout.Height(24f)))
                {
                    FillDefaultResources();
                }

                if (GUILayout.Button("清空选择", GUILayout.Height(24f)))
                {
                    ClearResourceSelection();
                }
            }

            if (!string.IsNullOrEmpty(_defaultResourceMessage))
            {
                EditorGUILayout.HelpBox(
                    _defaultResourceMessage,
                    MessageType.Info);
            }
        }

        private void FillDefaultResources()
        {
            _combatConfig =
                AssetDatabase.LoadAssetAtPath<CharacterCombatConfig>(
                    DefaultCombatConfigPath);

            _presentationConfig =
                AssetDatabase.LoadAssetAtPath<CharacterPresentationConfig>(
                    DefaultPresentationConfigPath);

            _executorPreviewPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    DefaultPreviewPrefabPath);

            _targetPreviewPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    DefaultPreviewPrefabPath);

            _executeClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    DefaultExecuteClipPath);

            _executedClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    DefaultExecutedClipPath);

            _executedDeathClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    DefaultExecutedDeathClipPath);

            _previewSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(DefaultPreviewScenePath);

            _defaultResourceMessage =
                "已尝试填充默认资源，请查看下方资源诊断。";

            Repaint();
        }

        private void ClearResourceSelection()
        {
            ClearExecuteRootMotionSample();
            DisposePreviewScene();
            _combatConfig = null;
            _presentationConfig = null;
            _executorPreviewPrefab = null;
            _targetPreviewPrefab = null;
            _executeClip = null;
            _executedClip = null;
            _executedDeathClip = null;
            _previewSceneAsset = null;

            _defaultResourceMessage = "已清空当前资源选择。";
            Repaint();
        }

        private void DrawAssetSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("资源选择", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                _combatConfig = (CharacterCombatConfig)EditorGUILayout.ObjectField(
                    "Combat Config",
                    _combatConfig,
                    typeof(CharacterCombatConfig),
                    false);

                _presentationConfig = (CharacterPresentationConfig)EditorGUILayout.ObjectField(
                    "Presentation Config",
                    _presentationConfig,
                    typeof(CharacterPresentationConfig),
                    false);

                _previewSceneAsset = (SceneAsset)EditorGUILayout.ObjectField("Preview Scene",
                    _previewSceneAsset,
                    typeof(SceneAsset),
                    false);

                _executorPreviewPrefab = DrawPrefabField("Executor Preview", _executorPreviewPrefab);

                _targetPreviewPrefab = DrawPrefabField("Target Preview", _targetPreviewPrefab);
            }
        }

        private static GameObject DrawPrefabField(string label, GameObject value)
        {
            GameObject selected =
                (GameObject)EditorGUILayout.ObjectField(
                    label,
                    value,
                    typeof(GameObject),
                    false);

            if (selected == null)
            {
                return null;
            }

            string path = AssetDatabase.GetAssetPath(selected);

            if (string.IsNullOrEmpty(path) ||
                PrefabUtility.GetPrefabAssetType(selected) ==
                PrefabAssetType.NotAPrefab)
            {
                return null;
            }

            return selected;
        }

        private static string ValidatePreviewPrefab(
    GameObject prefab)
        {
            if (prefab == null)
            {
                return "缺失 Prefab";
            }

            Animator animator =
                prefab.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                return "缺少 Animator";
            }

            if (animator.avatar == null)
            {
                return "Animator 缺少 Avatar";
            }

            if (animator.runtimeAnimatorController == null)
            {
                return "Animator 缺少 RuntimeAnimatorController";
            }

            return $"有效（Animator: {animator.name}）";
        }

        private static string ValidateClip(
            AnimationClip clip)
        {
            if (clip == null)
            {
                return "缺失动画资源";
            }

            if (clip.length <= 0f ||
                float.IsNaN(clip.length) ||
                float.IsInfinity(clip.length))
            {
                return "动画长度无效";
            }

            return $"有效（{clip.length:F3}s）";
        }

        private void DrawCombatConfigSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("战斗配置", EditorStyles.boldLabel);

            if (_combatConfig == null)
            {
                EditorGUILayout.HelpBox("请选择 CharacterCombatConfig。", MessageType.Warning);
                return;
            }

            using var serialized = new SerializedObject(_combatConfig);
            serialized.Update();

            DrawProperty(serialized, "executorAnchorOffset");
            DrawProperty(serialized, "executionMaxWarpTranslation");
            DrawProperty(serialized, "executionMaxWarpYaw");
            DrawProperty(serialized, "executionWarpWindowStartNormalized");
            DrawProperty(serialized, "executionWarpWindowEndNormalized");
            DrawProperty(serialized, "executionWarpCurve");
            DrawProperty(serialized, "executingDuration");
            DrawProperty(serialized, "executedDuration");
            DrawProperty(serialized, "executedDeathDuration");
            DrawProperty(serialized, "executionResultTime");

            if (serialized.ApplyModifiedProperties())
            {
                HandleSerializedConfigChanged(_combatConfig);
            }
        }

        private void DrawPresentationConfigSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("表现配置", EditorStyles.boldLabel);

            if (_presentationConfig == null)
            {
                EditorGUILayout.HelpBox("请选择 CharacterPresentationConfig。", MessageType.Warning);
                return;
            }

            using var serialized = new SerializedObject(_presentationConfig);
            serialized.Update();

            DrawProperty(serialized, "executingCrossFadeDuration");
            DrawProperty(serialized, "executedCrossFadeDuration");
            DrawProperty(serialized, "executedDeathCrossFadeDuration");
            DrawProperty(serialized, "executeClipDuration");
            DrawProperty(serialized, "executedClipDuration");
            DrawProperty(serialized, "executedDeathClipDuration");

            if (serialized.ApplyModifiedProperties())
            {
                HandleSerializedConfigChanged(_presentationConfig);
            }
        }

        private void DrawClipSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("动画资源", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                _executeClip = (AnimationClip)EditorGUILayout.ObjectField(
                    "rig_Execute",
                    _executeClip,
                    typeof(AnimationClip),
                    false);

                _executedClip = (AnimationClip)EditorGUILayout.ObjectField(
                    "rig_Executed",
                    _executedClip,
                    typeof(AnimationClip),
                    false);

                _executedDeathClip = (AnimationClip)EditorGUILayout.ObjectField(
                    "rig_Executed_Death",
                    _executedDeathClip,
                    typeof(AnimationClip),
                    false);
            }
        }
        private void DrawDiagnosticsSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("资源诊断", EditorStyles.boldLabel);

            bool combatAssigned = _combatConfig != null;
            bool combatValid = combatAssigned &&
                               ExecutionWarpSolver.IsValidConfiguration(
                                   _combatConfig);

            bool presentationValid = _presentationConfig != null;

            bool executorValid =
                TryValidatePreviewPrefab(
                    _executorPreviewPrefab,
                    out string executorMessage);

            bool targetValid =
                TryValidatePreviewPrefab(
                    _targetPreviewPrefab,
                    out string targetMessage);

            bool executeClipValid =
                TryValidateClip(
                    _executeClip,
                    out string executeClipMessage);

            bool executedClipValid =
                TryValidateClip(
                    _executedClip,
                    out string executedClipMessage);

            bool executedDeathClipValid =
                TryValidateClip(
                    _executedDeathClip,
                    out string executedDeathClipMessage);

            bool allInputsValid =
                combatValid &&
                presentationValid &&
                executorValid &&
                targetValid &&
                executeClipValid &&
                executedClipValid &&
                executedDeathClipValid;

            if (allInputsValid)
            {
                EditorGUILayout.HelpBox(
                    "Config Valid。资源输入完整，但尚未执行动画采样，因此不能标记为 Preview Valid。",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Preview Blocked：存在缺失或无效资源，不能生成有效预览。",
                    MessageType.Warning);
            }

            DrawDiagnosticRow(
                "Combat Config",
                combatAssigned && combatValid,
                !combatAssigned
                    ? "缺失 CharacterCombatConfig"
                    : combatValid
                        ? "Config Valid"
                        : "Motion Warping 配置无效");

            DrawDiagnosticRow(
                "Presentation Config",
                presentationValid,
                presentationValid ? "已选择" : "缺失 CharacterPresentationConfig");

            DrawDiagnosticRow(
                "Executor Preview",
                executorValid,
                executorMessage);

            DrawDiagnosticRow(
                "Target Preview",
                targetValid,
                targetMessage);

            DrawDiagnosticRow(
                "rig_Execute",
                executeClipValid,
                executeClipMessage);

            DrawDiagnosticRow(
                "rig_Executed",
                executedClipValid,
                executedClipMessage);

            DrawDiagnosticRow(
                "rig_Executed_Death",
                executedDeathClipValid,
                executedDeathClipMessage);

            if (_combatConfig != null &&
                _combatConfig.executionResultTime <= 0.05f)
            {
                EditorGUILayout.HelpBox(
                    $"executionResultTime 当前为 {_combatConfig.executionResultTime:F3}s，" +
                    "接近 0，需要在后续校准步骤中复核。",
                    MessageType.Warning);
            }
        }

        private static void DrawDiagnosticRow(
            string label,
            bool valid,
            string message)
        {
            EditorGUILayout.HelpBox(
                $"{label}: {message}",
                valid ? MessageType.Info : MessageType.Error);
        }
        private static void DrawProperty(
            SerializedObject serialized,
            string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);

            if (property == null)
            {
                EditorGUILayout.HelpBox(
                    $"找不到字段：{propertyName}",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.PropertyField(property, includeChildren: true);
        }


        private static bool TryValidatePreviewPrefab(
    GameObject prefab,
    out string message)
        {
            message = string.Empty;

            if (prefab == null)
            {
                message = "缺失 Prefab";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(prefab);

            if (string.IsNullOrEmpty(path) ||
                PrefabUtility.GetPrefabAssetType(prefab) ==
                PrefabAssetType.NotAPrefab)
            {
                message = "不是有效 Prefab 资产";
                return false;
            }

            Animator animator =
                prefab.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                message = "缺少 Animator";
                return false;
            }

            if (animator.avatar == null)
            {
                message = "Animator 缺少 Avatar";
                return false;
            }

            if (animator.runtimeAnimatorController == null)
            {
                message = "Animator 缺少 RuntimeAnimatorController";
                return false;
            }

            message =
                $"有效（Animator: {animator.name}，Avatar: {animator.avatar.name}）";

            return true;
        }

        private static bool TryValidateClip(
            AnimationClip clip,
            out string message)
        {
            message = string.Empty;

            if (clip == null)
            {
                message = "缺失动画资源";
                return false;
            }

            if (clip.length <= 0f ||
                float.IsNaN(clip.length) ||
                float.IsInfinity(clip.length))
            {
                message = "动画长度无效";
                return false;
            }

            message = $"有效（{clip.length:F3}s）";
            return true;
        }

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload +=
                HandleBeforeAssemblyReload;

            EditorApplication.quitting +=
                HandleEditorQuitting;

            Undo.undoRedoPerformed += HandleUndoRedo;
            SceneView.duringSceneGui += HandleSceneGUI;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -=
                HandleBeforeAssemblyReload;

            EditorApplication.quitting -=
                HandleEditorQuitting;

            Undo.undoRedoPerformed -= HandleUndoRedo;
            SceneView.duringSceneGui -= HandleSceneGUI;

            DisposePreviewScene();
        }

        private void HandleUndoRedo()
        {
            _executeTrajectory = null;

            Repaint();
            SceneView.RepaintAll();
        }

        private void HandleSerializedConfigChanged(
            UnityEngine.Object configAsset)
        {
            if (configAsset != null)
            {
                EditorUtility.SetDirty(configAsset);
            }

            _executeTrajectory = null;

            Repaint();
            SceneView.RepaintAll();
        }

        private void OnDestroy()
        {
            DisposePreviewScene();
        }

        private void HandleBeforeAssemblyReload()
        {
            DisposePreviewScene();
        }

        private void HandleEditorQuitting()
        {
            DisposePreviewScene();
        }


        private void SyncPreviewScene()
        {
            string scenePath =
                _previewSceneAsset == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(_previewSceneAsset);

            bool hasRequiredResources =
                !string.IsNullOrEmpty(scenePath) &&
                _executorPreviewPrefab != null &&
                _targetPreviewPrefab != null;

            if (!hasRequiredResources)
            {
                DisposePreviewScene();
                return;
            }

            bool sourceChanged =
                _previewScenePath != scenePath ||
                _previewExecutorSource != _executorPreviewPrefab ||
                _previewTargetSource != _targetPreviewPrefab ||
                _previewCombatSource != _combatConfig ||
                _previewPresentationSource != _presentationConfig;

            if (_previewScene.IsValid() &&
                _previewScene.isLoaded &&
                _previewExecutorInstance != null &&
                _previewTargetInstance != null &&
                !sourceChanged)
            {
                return;
            }

            DisposePreviewScene();

            if (!TryValidatePreviewPrefab(
                    _executorPreviewPrefab,
                    out _))
            {
                return;
            }

            if (!TryValidatePreviewPrefab(
                    _targetPreviewPrefab,
                    out _))
            {
                return;
            }

            if (!TryOpenPreviewScene(scenePath))
            {
                return;
            }

            CreatePreviewInstances(scenePath);
        }

        private bool TryOpenPreviewScene(string scenePath)
        {
            Scene loadedScene =
                SceneManager.GetSceneByPath(scenePath);

            if (loadedScene.IsValid() &&
                loadedScene.isLoaded)
            {
                _previewScene = loadedScene;
                _previewSceneOpenedByWindow = false;
                _previewScenePath = scenePath;
                return true;
            }

            Scene previousActiveScene =
                SceneManager.GetActiveScene();

            _previewScene =
                EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);

            if (!_previewScene.IsValid() ||
                !_previewScene.isLoaded)
            {
                _previewScene = default;
                _previewScenePath = string.Empty;
                return false;
            }

            _previewSceneOpenedByWindow = true;
            _previewScenePath = scenePath;

            if (previousActiveScene.IsValid() &&
                previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }

            return true;
        }

        private void CreatePreviewInstances(string scenePath)
        {
            _previewRoot =
                new GameObject("MotionWarpingPreviewRoot");

            _previewRoot.hideFlags =
                HideFlags.HideAndDontSave;

            SceneManager.MoveGameObjectToScene(
                _previewRoot,
                _previewScene);

            _previewExecutorInstance =
                PrefabUtility.InstantiatePrefab(
                    _executorPreviewPrefab,
                    _previewScene) as GameObject;

            _previewTargetInstance =
                PrefabUtility.InstantiatePrefab(
                    _targetPreviewPrefab,
                    _previewScene) as GameObject;

            if (_previewExecutorInstance == null ||
                _previewTargetInstance == null)
            {
                DisposePreviewScene();
                return;
            }

            _previewExecutorInstance.name =
                "MotionWarpingPreview_Executor";

            _previewTargetInstance.name =
                "MotionWarpingPreview_Target";

            _previewExecutorInstance.hideFlags =
                HideFlags.HideAndDontSave;

            _previewTargetInstance.hideFlags =
                HideFlags.HideAndDontSave;

            _previewExecutorInstance.transform.SetParent(
                _previewRoot.transform,
                false);

            _previewTargetInstance.transform.SetParent(
                _previewRoot.transform,
                false);

            _previewTargetInstance.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            _previewExecutorInstance.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, -1f),
                Quaternion.identity);

            _previewExecutorSource =
                _executorPreviewPrefab;

            _previewTargetSource =
                _targetPreviewPrefab;

            _previewCombatSource = _combatConfig;

            _previewPresentationSource = _presentationConfig;
        }



        private void DisposePreviewScene()
        {
            Scene sceneToClose = _previewScene;
            bool closeScene =
                _previewSceneOpenedByWindow;

            if (_previewExecutorInstance != null)
            {
                DestroyImmediate(_previewExecutorInstance);
            }

            if (_previewTargetInstance != null)
            {
                DestroyImmediate(_previewTargetInstance);
            }

            if (_previewRoot != null)
            {
                DestroyImmediate(_previewRoot);
            }

            if (closeScene &&
                sceneToClose.IsValid() &&
                sceneToClose.isLoaded)
            {
                EditorSceneManager.CloseScene(
                    sceneToClose,
                    true);
            }

            _previewScene = default;
            _previewScenePath = string.Empty;
            _previewSceneOpenedByWindow = false;

            _previewRoot = null;
            _previewExecutorInstance = null;
            _previewTargetInstance = null;
            _previewExecutorSource = null;
            _previewTargetSource = null;
            _previewCombatSource = null;
            _previewPresentationSource = null;

            _targetPreviewMessage = string.Empty;
            _targetPoseSamplingAttempted = false;
            _lastTargetPoseClip = null;
            _lastTargetPoseBranch = TargetPreviewBranch.Executed;
            _lastTargetPoseNormalizedTime = -1f;
            ClearTargetPosePreviewState();
        }

        private void DrawPreviewSceneStatus()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Preview Scene",
                EditorStyles.boldLabel);

            if (!_previewScene.IsValid())
            {
                EditorGUILayout.HelpBox(
                    "Preview Scene 尚未加载。",
                    MessageType.Info);

                return;
            }

            bool valid =
                _previewExecutorInstance != null &&
                _previewTargetInstance != null;

            EditorGUILayout.HelpBox(
                valid
                    ? $"已加载：{_previewScenePath}\n预览对象已隔离到该场景。"
                    : "场景已加载，但预览对象创建失败。",
                valid ? MessageType.Info : MessageType.Error);
        }

        private void DrawExecuteRootMotionSamplingSection()
        {
            InvalidateStaleExecuteSample();

            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField(
                "rig_Execute Root Motion 采样",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "Sample Rate",
                $"{ExecuteSampleRate} FPS");

            bool canSample =
                _previewScene.IsValid() &&
                _previewScene.isLoaded &&
                _previewExecutorInstance != null &&
                _executeClip != null;

            using (new EditorGUI.DisabledScope(!canSample))
            {
                if (GUILayout.Button(
                        "采样 rig_Execute",
                        GUILayout.Height(26f)))
                {
                    SampleExecuteRootMotion();
                }
            }

            if (!canSample)
            {
                EditorGUILayout.HelpBox(
                    "需要有效的 Preview Scene、Executor Preview 和 rig_Execute。",
                    MessageType.Info);
            }

            if (!string.IsNullOrEmpty(
                    _executeSamplingMessage))
            {
                MessageType messageType =
                    _executeRootMotionSample != null &&
                    _executeRootMotionSample.HasRootMotion
                        ? MessageType.Info
                        : MessageType.Warning;

                EditorGUILayout.HelpBox(
                    _executeSamplingMessage,
                    messageType);
            }

            if (_executeRootMotionSample == null)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField(
                    "Clip",
                    _executeRootMotionSample.Clip.name);

                EditorGUILayout.LabelField(
                    "Duration",
                    $"{_executeRootMotionSample.Duration:F3}s");

                EditorGUILayout.LabelField(
                    "Sample Count",
                    _executeRootMotionSample
                        .InputSamples.Count
                        .ToString());

                EditorGUILayout.LabelField(
                    "Total Position",
                    _executeRootMotionSample
                        .TotalPosition
                        .ToString("F4"));

                EditorGUILayout.LabelField(
                    "Horizontal Distance",
                    ExecutionWarpSolver
                        .ProjectToHorizontalPlane(
                            _executeRootMotionSample
                                .TotalPosition)
                        .magnitude
                        .ToString("F4"));

                EditorGUILayout.LabelField(
                    "Total Yaw",
                    $"{_executeRootMotionSample.TotalYaw:F3}°");
            }
        }

        private void SampleExecuteRootMotion()
        {
            _executeSamplingAttempted = true;
            ClearExecuteRootMotionSample();

            bool success =
                MotionWarpingRootMotionSampler.TrySample(
                    _previewExecutorInstance,
                    _executeClip,
                    ExecuteSampleRate,
                    out MotionWarpingRootMotionSample sample,
                    out string error);

            if (!success)
            {
                _executeSamplingMessage =
                    $"rig_Execute 采样失败：{error}";

                SceneView.RepaintAll();
                Repaint();
                return;
            }

            if (sample == null)
            {
                _executeSamplingMessage =
                    "rig_Execute 采样失败：采样器没有返回结果。";

                SceneView.RepaintAll();
                Repaint();
                return;
            }

            _executeRootMotionSample = sample;
            _sampledExecuteClip = _executeClip;
            _sampledExecutorPrefab =
                _executorPreviewPrefab;

            if (sample.InputSamples == null ||
                sample.InputSamples.Count == 0)
            {
                _executeSamplingMessage =
                    "rig_Execute 采样失败：没有生成任何帧样本。";

                ClearExecuteRootMotionSample();
                SceneView.RepaintAll();
                Repaint();
                return;
            }

            if (sample.HasRootMotion)
            {
                _executeSamplingMessage =
                    $"采样完成：{sample.InputSamples.Count} 帧，" +
                    $"水平位移 " +
                    $"{ExecutionWarpSolver.ProjectToHorizontalPlane(sample.TotalPosition).magnitude:F4}m，" +
                    $"Yaw {sample.TotalYaw:F3}°。";
            }
            else
            {
                _executeSamplingMessage =
                    "rig_Execute 采样完成，但没有检测到有效 Root Motion。" +
                    "请检查 Avatar、Root Transform 和 Bake Into Pose 设置。";
            }

            SceneView.RepaintAll();
            Repaint();
        }

        private void InvalidateStaleExecuteSample()
        {
            if (_executeRootMotionSample == null)
            {
                return;
            }

            bool sourceChanged =
                _sampledExecuteClip != _executeClip ||
                _sampledExecutorPrefab !=
                _executorPreviewPrefab;

            if (sourceChanged)
            {
                ClearExecuteRootMotionSample();

                _executeSamplingMessage =
                    "采样来源已经变化，请重新采样 rig_Execute。";
            }
        }

        private void ClearExecuteRootMotionSample()
        {
            _executeRootMotionSample = null;
            _sampledExecuteClip = null;
            _sampledExecutorPrefab = null;
            _executeSamplingMessage = string.Empty;
        }

        private void DrawTargetPosePreviewSection()
        {
            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField(
                "目标姿态预览",
                EditorStyles.boldLabel);

            bool previewInstanceValid =
                _previewScene.IsValid() &&
                _previewScene.isLoaded &&
                _previewTargetInstance != null;

            if (!previewInstanceValid)
            {
                EditorGUILayout.HelpBox(
                    "需要先加载有效的 Preview Scene，并创建 Target Preview 实例。",
                    MessageType.Info);

                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginChangeCheck();

                TargetPreviewBranch nextBranch =
                    (TargetPreviewBranch)EditorGUILayout.EnumPopup(
                        "目标分支",
                        _targetPreviewBranch);

                float nextNormalizedTime =
                    EditorGUILayout.Slider(
                        "Normalized Time",
                        _targetPreviewNormalizedTime,
                        0f,
                        1f);

                bool changed = EditorGUI.EndChangeCheck();

                if (changed)
                {
                    _targetPreviewBranch = nextBranch;
                    _targetPreviewNormalizedTime = nextNormalizedTime;

                    ApplyTargetPosePreview();
                }

                AnimationClip selectedClip =
                    GetSelectedTargetPreviewClip();

                EditorGUILayout.LabelField(
                    "当前动画",
                    selectedClip == null
                        ? "未选择"
                        : selectedClip.name);

                EditorGUILayout.LabelField(
                    "目标位置",
                    _previewTargetInstance.transform.position.ToString("F3"));

                EditorGUILayout.LabelField(
                    "目标旋转",
                    _previewTargetInstance.transform.rotation.eulerAngles.ToString("F2"));

                EditorGUILayout.HelpBox(
                    "目标对象位置固定，仅采样姿态，不应用 Root Motion。",
                    MessageType.Info);

                if (!string.IsNullOrEmpty(_targetPreviewMessage))
                {
                    MessageType messageType =
                        _targetPreviewMessage.StartsWith("姿态采样完成")
                            ? MessageType.Info
                            : MessageType.Warning;

                    EditorGUILayout.HelpBox(
                        _targetPreviewMessage,
                        messageType);
                }
            }

            AnimationClip currentClip =
                GetSelectedTargetPreviewClip();

            bool needsRefresh =
                !_targetPoseSamplingAttempted ||
                currentClip != _lastTargetPoseClip ||
                _targetPreviewBranch != _lastTargetPoseBranch ||
                Mathf.Abs(
                    _targetPreviewNormalizedTime -
                    _lastTargetPoseNormalizedTime) > 0.0001f;

            if (needsRefresh)
            {
                ApplyTargetPosePreview();
            }

            DrawTargetPoseSamplingStatus();
        }

        private AnimationClip GetSelectedTargetPreviewClip()
        {
            switch (_targetPreviewBranch)
            {
                case TargetPreviewBranch.Executed:
                    return _executedClip;

                case TargetPreviewBranch.ExecutedDeath:
                    return _executedDeathClip;

                default:
                    return null;
            }
        }

        private void ApplyTargetPosePreview()
        {
            _targetPoseSamplingAttempted = true;
            _lastTargetPoseClip = GetSelectedTargetPreviewClip();
            _lastTargetPoseBranch = _targetPreviewBranch;
            _lastTargetPoseNormalizedTime = _targetPreviewNormalizedTime;
            _targetPreviewMessage = string.Empty;

            if (_previewTargetInstance == null)
            {
                _targetPreviewMessage =
                    "姿态采样失败：Target Preview 实例不存在。";

                ClearTargetPosePreviewState();
                return;
            }

            AnimationClip clip =
                GetSelectedTargetPreviewClip();

            if (clip == null)
            {
                _targetPreviewMessage =
                    _targetPreviewBranch ==
                    TargetPreviewBranch.Executed
                        ? "姿态采样失败：未指定 rig_Executed。"
                        : "姿态采样失败：未指定 rig_Executed_Death。";

                ClearTargetPosePreviewState();
                return;
            }

            if (clip.length <= 0f ||
                float.IsNaN(clip.length) ||
                float.IsInfinity(clip.length))
            {
                _targetPreviewMessage =
                    "姿态采样失败：动画长度无效。";

                ClearTargetPosePreviewState();
                return;
            }

            Animator animator =
                _previewTargetInstance.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                _targetPreviewMessage =
                    "姿态采样失败：Target Preview 缺少 Animator。";

                ClearTargetPosePreviewState();
                return;
            }

            Transform targetRoot =
                _previewTargetInstance.transform;

            Vector3 fixedPosition =
                targetRoot.position;

            Quaternion fixedRotation =
                targetRoot.rotation;

            bool animatorWasEnabled =
                animator.enabled;

            try
            {
                animator.enabled = false;

                float sampleTime =
                    Mathf.Clamp01(_targetPreviewNormalizedTime) *
                    clip.length;

                clip.SampleAnimation(
                    _previewTargetInstance,
                    sampleTime);

                // SampleAnimation 可能会写入根节点的动画位移和旋转。
                // 姿态预览只保留骨骼姿态，不允许目标对象发生位移。
                targetRoot.SetPositionAndRotation(
                    fixedPosition,
                    fixedRotation);

                _sampledTargetPreviewClip = clip;
                _sampledTargetPreviewBranch =
                    _targetPreviewBranch;
                _sampledTargetPreviewNormalizedTime =
                    _targetPreviewNormalizedTime;

                _targetPreviewMessage =
                    $"姿态采样完成：{clip.name}，" +
                    $"Normalized Time " +
                    $"{_targetPreviewNormalizedTime:F3}。";

                SceneView.RepaintAll();
                Repaint();
            }
            catch (System.Exception exception)
            {
                _targetPreviewMessage =
                    $"姿态采样失败：{exception.Message}";

                ClearTargetPosePreviewState();
            }
            finally
            {
                animator.enabled = animatorWasEnabled;
            }
        }

        private void ClearTargetPosePreviewState()
        {
            _sampledTargetPreviewClip = null;
            _sampledTargetPreviewBranch =
                TargetPreviewBranch.Executed;
            _sampledTargetPreviewNormalizedTime = -1f;

            SceneView.RepaintAll();
            Repaint();
        }

        private void DrawTargetPoseSamplingStatus()
        {
            if (!_targetPoseSamplingAttempted)
            {
                EditorGUILayout.HelpBox(
                    "目标姿态尚未采样。",
                    MessageType.Info);

                return;
            }

            AnimationClip currentClip =
                GetSelectedTargetPreviewClip();

            bool valid =
                _sampledTargetPreviewClip != null &&
                _sampledTargetPreviewClip == currentClip &&
                _sampledTargetPreviewBranch == _targetPreviewBranch &&
                Mathf.Abs(
                    _sampledTargetPreviewNormalizedTime -
                    _targetPreviewNormalizedTime) <= 0.0001f &&
                !string.IsNullOrEmpty(_targetPreviewMessage) &&
                _targetPreviewMessage.StartsWith("姿态采样完成");

            if (!valid)
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(_targetPreviewMessage)
                        ? "目标姿态采样无有效结果，请重新采样。"
                        : _targetPreviewMessage,
                    MessageType.Warning);

                return;
            }

            EditorGUILayout.HelpBox(
                "目标姿态采样有效。该结果仅表示编辑器姿态预览，" +
                "不等同于运行时处决验证通过。",
                MessageType.Info);
        }

        private float GetPreviewAnimationDuration()
        {
            if (_executeClip != null &&
                _executeClip.length > 0f &&
                !float.IsNaN(_executeClip.length) &&
                !float.IsInfinity(_executeClip.length))
            {
                return _executeClip.length;
            }

            if (_presentationConfig != null &&
                _presentationConfig.executeClipDuration > 0f &&
                !float.IsNaN(_presentationConfig.executeClipDuration) &&
                !float.IsInfinity(_presentationConfig.executeClipDuration))
            {
                return _presentationConfig.executeClipDuration;
            }

            return 0f;
        }

        private void DrawTimelineSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Motion Warping Timeline",
                EditorStyles.boldLabel);

            float duration = GetPreviewAnimationDuration();

            if (duration <= 0f)
            {
                EditorGUILayout.HelpBox(
                    "Timeline unavailable: assign a valid rig_Execute clip.",
                    MessageType.Info);
                return;
            }

            float warpStart = _combatConfig == null
                ? 0f
                : Mathf.Clamp01(
                    _combatConfig.executionWarpWindowStartNormalized);

            float warpEnd = _combatConfig == null
                ? 0f
                : Mathf.Clamp01(
                    _combatConfig.executionWarpWindowEndNormalized);

            float resultTime = _combatConfig == null
                ? 0f
                : _combatConfig.executionResultTime;

            bool resultTimeValid =
                !float.IsNaN(resultTime) &&
                !float.IsInfinity(resultTime) &&
                resultTime >= 0f &&
                resultTime <= duration;

            float resultNormalized = resultTimeValid
                ? Mathf.Clamp01(resultTime / duration)
                : 0f;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField(
                    "Animation Duration",
                    $"{duration:F3}s");

                EditorGUILayout.LabelField(
                    "Warp Window",
                    $"{warpStart:F3} - {warpEnd:F3} " +
                    $"({warpStart * duration:F3}s - " +
                    $"{warpEnd * duration:F3}s)");

                EditorGUILayout.LabelField(
                    "Execution Result",
                    $"{resultTime:F3}s " +
                    $"(Normalized {resultNormalized:F3})");

                DrawTimelineEditingControls(duration);

                EditorGUI.BeginChangeCheck();

                float currentScrub =
                    float.IsNaN(_scrubNormalizedTime) ||
                    float.IsInfinity(_scrubNormalizedTime)
                        ? 0f
                        : Mathf.Clamp01(_scrubNormalizedTime);

                float nextScrub = EditorGUILayout.Slider(
                    "Scrub Time",
                    currentScrub,
                    0f,
                    1f);

                if (EditorGUI.EndChangeCheck())
                {
                    _scrubNormalizedTime = Mathf.Clamp01(nextScrub);
                    _targetPreviewNormalizedTime = _scrubNormalizedTime;
                    ApplyTargetPosePreview();
                    SceneView.RepaintAll();
                }

                EditorGUILayout.LabelField(
                    "Current Scrub",
                    $"{_scrubNormalizedTime * duration:F3}s " +
                    $"(Normalized {_scrubNormalizedTime:F3})");

                if (!resultTimeValid)
                {
                    EditorGUILayout.HelpBox(
                        "executionResultTime is outside the execute clip duration.",
                        MessageType.Warning);
                }

                bool previewValid = TryEvaluatePreviewState(
                    out _,
                    out string previewMessage);

                EditorGUILayout.HelpBox(
                    previewMessage,
                    previewValid
                        ? MessageType.Info
                        : (_executeSamplingAttempted ||
                           _targetPoseSamplingAttempted
                            ? MessageType.Warning
                            : MessageType.Info));
            }
        }


        private void DrawTimelineEditingControls(float duration)
        {
            if (_combatConfig == null)
            {
                EditorGUILayout.HelpBox(
                    "选择 CharacterCombatConfig 后才能编辑 Timeline。",
                    MessageType.Info);

                return;
            }

            using var serialized =
                new SerializedObject(_combatConfig);

            serialized.Update();

            SerializedProperty warpStartProperty =
                serialized.FindProperty(
                    "executionWarpWindowStartNormalized");

            SerializedProperty warpEndProperty =
                serialized.FindProperty(
                    "executionWarpWindowEndNormalized");

            SerializedProperty resultTimeProperty =
                serialized.FindProperty(
                    "executionResultTime");

            if (warpStartProperty == null ||
                warpEndProperty == null ||
                resultTimeProperty == null)
            {
                EditorGUILayout.HelpBox(
                    "Timeline 配置字段不存在，无法编辑。",
                    MessageType.Error);

                return;
            }

            float nextWarpStart =
                IsFiniteScalar(warpStartProperty.floatValue)
                    ? Mathf.Clamp01(warpStartProperty.floatValue)
                    : 0f;

            float nextWarpEnd =
                IsFiniteScalar(warpEndProperty.floatValue)
                    ? Mathf.Clamp01(warpEndProperty.floatValue)
                    : 1f;

            float nextResultTime =
                IsFiniteScalar(resultTimeProperty.floatValue)
                    ? Mathf.Clamp(
                        resultTimeProperty.floatValue,
                        0f,
                        duration)
                    : 0f;

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.MinMaxSlider(
                "Edit Warp Window",
                ref nextWarpStart,
                ref nextWarpEnd,
                0f,
                1f);

            nextResultTime = EditorGUILayout.Slider(
                "Edit Result Time",
                nextResultTime,
                0f,
                duration);

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            // 这里才是 Timeline 修改进入 Unity Undo 栈的地方。
            Undo.SetCurrentGroupName(
                "Edit Motion Warping Timeline");

            Undo.RecordObject(
                _combatConfig,
                "Edit Motion Warping Timeline");

            warpStartProperty.floatValue =
                Mathf.Min(nextWarpStart, nextWarpEnd);

            warpEndProperty.floatValue =
                Mathf.Max(nextWarpStart, nextWarpEnd);

            resultTimeProperty.floatValue =
                nextResultTime;

            // Undo 已由 Undo.RecordObject 记录，避免重复生成记录。
            if (!serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                return;
            }

            HandleSerializedConfigChanged(_combatConfig);
        }


        private void DrawTrajectoryDiagnosticsSection()
        {
            EditorGUILayout.Space(8f);

            _showTrajectoryDiagnostics = EditorGUILayout.Foldout(
                _showTrajectoryDiagnostics,
                "逐帧与最终诊断");

            if (!_showTrajectoryDiagnostics)
            {
                return;
            }

            bool previewValid = TryEvaluatePreviewState(
                out ExecutionWarpTrajectory trajectory,
                out string previewMessage);

            _executeTrajectory = trajectory;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.HelpBox(
                    previewMessage,
                    previewValid
                        ? MessageType.Info
                        : MessageType.Warning);

                if (TryGetInitialBudgetUsage(
                        out float initialPositionError,
                        out float initialYawError,
                        out float translationUsage,
                        out float yawUsage))
                {
                    EditorGUILayout.LabelField(
                        "初始位置预算",
                        FormatBudgetUsage(
                            initialPositionError,
                            _combatConfig.executionMaxWarpTranslation));

                    EditorGUILayout.LabelField(
                        "初始 Yaw 预算",
                        FormatBudgetUsage(
                            initialYawError,
                            _combatConfig.executionMaxWarpYaw));

                    if (translationUsage > 1f || yawUsage > 1f)
                    {
                        EditorGUILayout.HelpBox(
                            "初始姿态超过 Motion Warping 预算，无法生成有效轨迹。",
                            MessageType.Error);
                    }
                }

                if (TryValidateExecutionResultTime(
                        out string resultTimeMessage))
                {
                    EditorGUILayout.LabelField(
                        "结果时刻",
                        resultTimeMessage);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        resultTimeMessage,
                        MessageType.Error);
                }

                if (trajectory == null ||
                    trajectory.Frames == null ||
                    trajectory.Frames.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "当前没有可显示的轨迹帧。",
                        MessageType.Info);
                    return;
                }

                int frameCount = trajectory.Frames.Count;

                int frameIndex = Mathf.Clamp(
                    Mathf.RoundToInt(
                        Mathf.Clamp01(_scrubNormalizedTime) *
                        (frameCount - 1)),
                    0,
                    frameCount - 1);

                ExecutionWarpTrajectoryFrame frame =
                    trajectory.Frames[frameIndex];

                Vector3 framePositionCorrection =
                    frame.CorrectedDeltaPosition -
                    frame.OriginalDeltaPosition;

                float frameYawCorrection = Mathf.DeltaAngle(
                    frame.OriginalDeltaYaw,
                    frame.CorrectedDeltaYaw);

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(
                    "当前 Scrub 帧",
                    EditorStyles.boldLabel);

                EditorGUILayout.LabelField(
                    "Sample",
                    $"{frame.Index + 1}/{frameCount}, " +
                    $"Normalized {frame.NormalizedTime:F4}");

                EditorGUILayout.LabelField(
                    "原始 Delta Position",
                    frame.OriginalDeltaPosition.ToString("F5"));

                EditorGUILayout.LabelField(
                    "原始 Delta Yaw",
                    $"{frame.OriginalDeltaYaw:F4}°");

                EditorGUILayout.LabelField(
                    "修正 Delta Position",
                    frame.CorrectedDeltaPosition.ToString("F5"));

                EditorGUILayout.LabelField(
                    "修正 Delta Yaw",
                    $"{frame.CorrectedDeltaYaw:F4}°");

                EditorGUILayout.LabelField(
                    "本帧位置修正",
                    framePositionCorrection.ToString("F5"));

                EditorGUILayout.LabelField(
                    "本帧 Yaw 修正",
                    $"{frameYawCorrection:F4}°");

                EditorGUILayout.LabelField(
                    "累计位置修正",
                    frame.RequestedTranslationCorrection.ToString("F5"));

                EditorGUILayout.LabelField(
                    "累计 Yaw 修正",
                    $"{frame.RequestedYawCorrection:F4}°");

                EditorGUILayout.LabelField(
                    "累计位置预算占用",
                    FormatBudgetUsage(
                        frame.RequestedTranslationCorrection.magnitude,
                        _combatConfig.executionMaxWarpTranslation));

                EditorGUILayout.LabelField(
                    "累计 Yaw 预算占用",
                    FormatBudgetUsage(
                        Mathf.Abs(frame.RequestedYawCorrection),
                        _combatConfig.executionMaxWarpYaw));

                EditorGUILayout.LabelField(
                    "预测位置残差",
                    frame.PredictedRemainingPositionError.ToString("F5"));

                EditorGUILayout.LabelField(
                    "预测 Yaw 残差",
                    $"{frame.PredictedRemainingYawError:F4}°");

                EditorGUILayout.LabelField(
                    "应用后位置残差",
                    frame.ActualRemainingPositionError.ToString("F5"));

                EditorGUILayout.LabelField(
                    "应用后 Yaw 残差",
                    $"{frame.ActualRemainingYawError:F4}°");

                ExecutionWarpTrajectoryFrame finalFrame =
                    trajectory.Frames[frameCount - 1];

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(
                    "最终诊断",
                    EditorStyles.boldLabel);

                EditorGUILayout.LabelField(
                    "最终姿态",
                    $"Position {trajectory.FinalPose.Position:F4}, " +
                    $"Yaw {trajectory.FinalPose.Yaw:F3}°");

                EditorGUILayout.LabelField(
                    "最终位置残差",
                    trajectory.FinalPositionError.ToString("F5"));

                EditorGUILayout.LabelField(
                    "最终 Yaw 残差",
                    $"{trajectory.FinalYawError:F4}°");

                EditorGUILayout.LabelField(
                    "最终累计位置修正",
                    finalFrame.RequestedTranslationCorrection.ToString("F5"));

                EditorGUILayout.LabelField(
                    "最终累计 Yaw 修正",
                    $"{finalFrame.RequestedYawCorrection:F4}°");

                EditorGUILayout.LabelField(
                    "Warp Window 完成",
                    finalFrame.IsWarpWindowComplete ? "是" : "否");
            }
        }

        private bool TryEvaluatePreviewState(
            out ExecutionWarpTrajectory trajectory,
            out string message)
        {
            trajectory = null;

            if (_combatConfig == null)
            {
                message =
                    "Config Invalid：缺失 CharacterCombatConfig。Preview Blocked。";
                return false;
            }

            if (!ExecutionWarpSolver.IsValidConfiguration(_combatConfig))
            {
                message =
                    "Config Invalid：Motion Warping 配置非法。Preview Blocked。";
                return false;
            }

            if (!TryValidatePreviewPrefab(
                    _executorPreviewPrefab,
                    out string executorMessage))
            {
                message =
                    $"Preview Blocked：Executor Preview {executorMessage}。";
                return false;
            }

            if (!TryValidateClip(
                    _executeClip,
                    out string executeClipMessage))
            {
                message =
                    $"Preview Blocked：rig_Execute {executeClipMessage}。";
                return false;
            }

            if (!_previewScene.IsValid() ||
                !_previewScene.isLoaded ||
                _previewExecutorInstance == null ||
                _previewTargetInstance == null)
            {
                message =
                    "Preview Blocked：隔离预览环境尚未就绪。";
                return false;
            }

            if (_executeRootMotionSample == null ||
                _executeRootMotionSample.InputSamples == null ||
                _executeRootMotionSample.InputSamples.Count == 0)
            {
                message =
                    "Preview Blocked：尚未取得 rig_Execute Root Motion 样本。";
                return false;
            }

            if (_sampledExecuteClip != _executeClip ||
                _sampledExecutorPrefab != _executorPreviewPrefab)
            {
                message =
                    "Preview Blocked：rig_Execute 采样来源已经变化，请重新采样。";
                return false;
            }

            if (!_executeRootMotionSample.HasRootMotion)
            {
                message =
                    "Preview Blocked：rig_Execute 未产生可信 Root Motion。";
                return false;
            }

            if (!TryBuildExecuteTrajectory(out trajectory))
            {
                if (TryGetInitialBudgetUsage(
                        out _,
                        out _,
                        out float translationUsage,
                        out float yawUsage) &&
                    (translationUsage > 1f || yawUsage > 1f))
                {
                    message =
                        "Preview Blocked：初始姿态超过位置或 Yaw 预算。";
                }
                else
                {
                    message =
                        "Preview Blocked：共享 ExecutionWarpSolver 无法生成轨迹。";
                }

                return false;
            }

            if (_presentationConfig == null)
            {
                message =
                    "Config Invalid：缺失 CharacterPresentationConfig。Preview Blocked。";
                return false;
            }

            if (!TryValidatePreviewPrefab(
                    _targetPreviewPrefab,
                    out string targetMessage))
            {
                message =
                    $"Preview Blocked：Target Preview {targetMessage}。";
                return false;
            }

            if (!TryValidateClip(
                    _executedClip,
                    out string executedMessage))
            {
                message =
                    $"Preview Blocked：rig_Executed {executedMessage}。";
                return false;
            }

            if (!TryValidateClip(
                    _executedDeathClip,
                    out string executedDeathMessage))
            {
                message =
                    $"Preview Blocked：rig_Executed_Death {executedDeathMessage}。";
                return false;
            }

            AnimationClip selectedTargetClip =
                GetSelectedTargetPreviewClip();

            bool targetSampleValid =
                selectedTargetClip != null &&
                _sampledTargetPreviewClip == selectedTargetClip &&
                _sampledTargetPreviewBranch == _targetPreviewBranch &&
                Mathf.Abs(
                    _sampledTargetPreviewNormalizedTime -
                    _targetPreviewNormalizedTime) <= 0.0001f;

            if (!targetSampleValid)
            {
                message =
                    "Preview Blocked：当前目标分支的姿态采样无效。";
                return false;
            }

            if (!TryValidateExecutionResultTime(
                    out string resultTimeMessage))
            {
                message =
                    $"Config Invalid：{resultTimeMessage} Preview Blocked。";
                return false;
            }

            message =
                "Config Valid。Preview Valid。Runtime Unverified。";

            return true;
        }

        private bool TryGetInitialBudgetUsage(
            out float positionError,
            out float yawError,
            out float translationUsage,
            out float yawUsage)
        {
            positionError = 0f;
            yawError = 0f;
            translationUsage = 0f;
            yawUsage = 0f;

            if (_combatConfig == null ||
                _previewExecutorInstance == null ||
                _previewTargetInstance == null)
            {
                return false;
            }

            Transform targetTransform =
                _previewTargetInstance.transform;

            Transform executorTransform =
                _previewExecutorInstance.transform;

            float targetYaw =
                targetTransform.eulerAngles.y;

            Quaternion targetRotation =
                Quaternion.Euler(0f, targetYaw, 0f);

            Vector3 anchorPosition =
                targetTransform.position +
                targetRotation *
                _combatConfig.executorAnchorOffset;

            positionError =
                ExecutionWarpSolver.ProjectToHorizontalPlane(
                    anchorPosition - executorTransform.position)
                .magnitude;

            yawError = Mathf.Abs(
                Mathf.DeltaAngle(
                    executorTransform.eulerAngles.y,
                    targetYaw));

            if (!IsFiniteScalar(positionError) ||
                !IsFiniteScalar(yawError))
            {
                return false;
            }

            translationUsage = CalculateBudgetUsage(
                positionError,
                _combatConfig.executionMaxWarpTranslation);

            yawUsage = CalculateBudgetUsage(
                yawError,
                _combatConfig.executionMaxWarpYaw);

            return true;
        }

        private bool TryValidateExecutionResultTime(
            out string message)
        {
            message = string.Empty;

            if (_combatConfig == null)
            {
                message = "缺失 CharacterCombatConfig。";
                return false;
            }

            float previewDuration =
                GetPreviewAnimationDuration();

            float configuredDuration = Mathf.Min(
                _combatConfig.executingDuration,
                Mathf.Min(
                    _combatConfig.executedDuration,
                    _combatConfig.executedDeathDuration));

            if (!IsFiniteScalar(previewDuration) ||
                previewDuration <= 0f ||
                !IsFiniteScalar(configuredDuration) ||
                configuredDuration <= 0f)
            {
                message = "无法确定有效处决时长。";
                return false;
            }

            float resultTime =
                _combatConfig.executionResultTime;

            if (!IsFiniteScalar(resultTime) ||
                resultTime < 0f)
            {
                message = "executionResultTime 不是有效非负数。";
                return false;
            }

            if (resultTime <= 0.05f)
            {
                message =
                    $"executionResultTime={resultTime:F3}s，过于接近 0。";
                return false;
            }

            float allowedDuration =
                Mathf.Min(previewDuration, configuredDuration);

            if (resultTime > allowedDuration + 0.0001f)
            {
                message =
                    $"executionResultTime={resultTime:F3}s，" +
                    $"超过有效时长 {allowedDuration:F3}s。";
                return false;
            }

            message =
                $"{resultTime:F3}s / {allowedDuration:F3}s";

            return true;
        }

        private static float CalculateBudgetUsage(
            float value,
            float budget)
        {
            if (!IsFiniteScalar(value) ||
                !IsFiniteScalar(budget) ||
                budget < 0f)
            {
                return float.PositiveInfinity;
            }

            if (budget <= 0.000001f)
            {
                return value <= 0.000001f
                    ? 0f
                    : float.PositiveInfinity;
            }

            return value / budget;
        }

        private static string FormatBudgetUsage(
            float value,
            float budget)
        {
            float usage =
                CalculateBudgetUsage(value, budget);

            if (float.IsNaN(usage))
            {
                return $"{value:F4} / {budget:F4}（无效）";
            }

            if (float.IsInfinity(usage))
            {
                return $"{value:F4} / {budget:F4}（超限）";
            }

            return
                $"{value:F4} / {budget:F4} " +
                $"({usage * 100f:F1}%)";
        }

        private static bool IsFiniteScalar(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private void HandleSceneGUI(SceneView sceneView)
        {
            if (Event.current == null)
            {
                return;
            }

            if (!_previewScene.IsValid() ||
                !_previewScene.isLoaded ||
                _combatConfig == null ||
                _previewTargetInstance == null)
            {
                return;
            }

            DrawAnchorHandle();

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (_executeRootMotionSample == null ||
                !_executeRootMotionSample.HasRootMotion)
            {
                return;
            }

            if (!TryBuildExecuteTrajectory(
                    out ExecutionWarpTrajectory trajectory))
            {
                return;
            }

            _executeTrajectory = trajectory;
            DrawTrajectoryScene(trajectory);
        }

        private void DrawAnchorHandle()
        {
            if (_combatConfig == null ||
                _previewTargetInstance == null)
            {
                return;
            }

            Transform targetTransform =
                _previewTargetInstance.transform;

            float targetYaw =
                targetTransform.eulerAngles.y;

            Quaternion targetRotation =
                Quaternion.Euler(0f, targetYaw, 0f);

            using var serialized =
                new SerializedObject(_combatConfig);

            serialized.Update();

            SerializedProperty offsetProperty =
                serialized.FindProperty("executorAnchorOffset");

            if (offsetProperty == null ||
                offsetProperty.propertyType !=
                SerializedPropertyType.Vector3)
            {
                return;
            }

            Vector3 currentOffset =
                offsetProperty.vector3Value;

            Vector3 worldAnchor =
                targetTransform.position +
                targetRotation * currentOffset;

            EditorGUI.BeginChangeCheck();

            Vector3 nextWorldAnchor =
                Handles.PositionHandle(
                    worldAnchor,
                    targetRotation);

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Vector3 nextOffset =
                Quaternion.Inverse(targetRotation) *
                (nextWorldAnchor - targetTransform.position);

            if (!IsFiniteVector3(nextOffset))
            {
                return;
            }

            Undo.RecordObject(
                _combatConfig,
                "Move Motion Warping Anchor");

            serialized.Update();
            offsetProperty.vector3Value = nextOffset;

            // 前面已经调用了 Undo.RecordObject，避免这里再生成一条重复 Undo。
            if (!serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                return;
            }

            HandleSerializedConfigChanged(_combatConfig);
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.z);
        }

        private bool TryBuildExecuteTrajectory(
            out ExecutionWarpTrajectory trajectory)
        {
            trajectory = null;

            if (_combatConfig == null ||
                _previewExecutorInstance == null ||
                _previewTargetInstance == null ||
                _executeRootMotionSample == null ||
                _executeRootMotionSample.InputSamples == null ||
                _executeRootMotionSample.InputSamples.Count == 0)
            {
                return false;
            }

            Transform targetTransform =
                _previewTargetInstance.transform;

            Transform executorTransform =
                _previewExecutorInstance.transform;

            float targetYaw =
                targetTransform.eulerAngles.y;

            Quaternion targetRotation =
                Quaternion.Euler(0f, targetYaw, 0f);

            Vector3 anchorPosition =
                targetTransform.position +
                targetRotation * _combatConfig.executorAnchorOffset;

            var anchorPose =
                new ExecutionPose(
                    anchorPosition,
                    targetYaw);

            var initialExecutorPose =
                new ExecutionPose(
                    executorTransform.position,
                    executorTransform.eulerAngles.y);

            return ExecutionWarpTrajectorySampler.TrySample(
                anchorPose,
                initialExecutorPose,
                _combatConfig,
                _executeRootMotionSample.InputSamples,
                out trajectory);
        }

        private void DrawTrajectoryScene(
            ExecutionWarpTrajectory trajectory)
        {
            if (trajectory == null ||
                _executeRootMotionSample == null ||
                _previewTargetInstance == null)
            {
                return;
            }

            MotionWarpingRootMotionSample sample =
                _executeRootMotionSample;

            DrawPoseMarker(
                "Target",
                _previewTargetInstance.transform.position,
                _previewTargetInstance.transform.eulerAngles.y,
                Color.cyan);

            DrawPoseMarker(
                "Executor Start",
                trajectory.InitialPose.Position,
                trajectory.InitialPose.Yaw,
                Color.white);

            DrawPoseMarker(
                "Anchor",
                trajectory.AnchorPose.Position,
                trajectory.AnchorPose.Yaw,
                new Color(1f, 0.65f, 0f));

            Handles.color = new Color(0.65f, 0.65f, 0.65f);

            if (sample.AccumulatedPositions != null)
            {
                for (int i = 0;
                     i + 1 < sample.AccumulatedPositions.Count;
                     i++)
                {
                    Vector3 start =
                        trajectory.InitialPose.Position +
                        sample.AccumulatedPositions[i];

                    Vector3 end =
                        trajectory.InitialPose.Position +
                        sample.AccumulatedPositions[i + 1];

                    Handles.DrawLine(start, end);
                }
            }

            Handles.color = Color.green;

            if (trajectory.Frames != null)
            {
                for (int i = 0;
                     i < trajectory.Frames.Count;
                     i++)
                {
                    Vector3 start =
                        i == 0
                            ? trajectory.InitialPose.Position
                            : trajectory.Frames[i - 1].PoseAfter.Position;

                    Vector3 end =
                        trajectory.Frames[i].PoseAfter.Position;

                    Handles.DrawLine(start, end);
                }
            }

            Handles.color = Color.red;
            Handles.DrawLine(
                trajectory.FinalPose.Position,
                trajectory.AnchorPose.Position);

            DrawPoseMarker(
                "Warp Final",
                trajectory.FinalPose.Position,
                trajectory.FinalPose.Yaw,
                Color.green);

            int frameCount =
                trajectory.Frames == null
                    ? 0
                    : trajectory.Frames.Count;

            if (frameCount > 0)
            {
                int scrubIndex =
                    Mathf.Clamp(
                        Mathf.RoundToInt(
                            Mathf.Clamp01(_scrubNormalizedTime) *
                            (frameCount - 1)),
                        0,
                        frameCount - 1);

                ExecutionWarpTrajectoryFrame frame =
                    trajectory.Frames[scrubIndex];

                DrawPoseMarker(
                    "Scrub",
                    frame.PoseAfter.Position,
                    frame.PoseAfter.Yaw,
                    Color.yellow);

                Handles.color = Color.magenta;
                Handles.DrawLine(
                    frame.PoseAfter.Position,
                    trajectory.AnchorPose.Position);
            }

            Handles.color = Color.white;
        }

        private static void DrawPoseMarker(
            string label,
            Vector3 position,
            float yaw,
            Color color)
        {
            Handles.color = color;

            Handles.DrawWireDisc(
                position,
                Vector3.up,
                0.12f);

            Handles.ArrowHandleCap(
                0,
                position,
                Quaternion.Euler(0f, yaw, 0f),
                0.45f,
                EventType.Repaint);

            Handles.Label(
                position + Vector3.up * 0.08f,
                label);
        }

        private void DrawConfigSaveSection()
        {
            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField(
                "配置资产保存",
                EditorStyles.boldLabel);

            bool combatDirty =
                IsDirtyPersistentAsset(_combatConfig);

            bool presentationDirty =
                IsDirtyPersistentAsset(_presentationConfig);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField(
                    "Combat Config",
                    GetAssetSaveState(_combatConfig));

                EditorGUILayout.LabelField(
                    "Presentation Config",
                    GetAssetSaveState(_presentationConfig));

                using (new EditorGUI.DisabledScope(
                           !combatDirty && !presentationDirty))
                {
                    if (GUILayout.Button(
                            "保存已修改配置资产",
                            GUILayout.Height(24f)))
                    {
                        SaveDirtyConfigAssets(
                            combatDirty,
                            presentationDirty);
                    }
                }

                if (!string.IsNullOrEmpty(_saveStatusMessage))
                {
                    EditorGUILayout.HelpBox(
                        _saveStatusMessage,
                        _saveStatusMessageType);
                }
            }
        }

        private static bool IsDirtyPersistentAsset(
            UnityEngine.Object asset)
        {
            return asset != null &&
                   EditorUtility.IsPersistent(asset) &&
                   EditorUtility.IsDirty(asset);
        }

        private static string GetAssetSaveState(
            UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return "未选择";
            }

            if (!EditorUtility.IsPersistent(asset))
            {
                return "不是可持久化资产";
            }

            return EditorUtility.IsDirty(asset)
                ? "Dirty（尚未保存）"
                : "Saved";
        }

        private void SaveDirtyConfigAssets(
            bool saveCombat,
            bool savePresentation)
        {
            int requestedSaveCount = 0;

            try
            {
                if (saveCombat)
                {
                    requestedSaveCount++;
                    AssetDatabase.SaveAssetIfDirty(_combatConfig);
                }

                if (savePresentation)
                {
                    requestedSaveCount++;
                    AssetDatabase.SaveAssetIfDirty(
                        _presentationConfig);
                }

                bool combatSaved =
                    !saveCombat ||
                    !EditorUtility.IsDirty(_combatConfig);

                bool presentationSaved =
                    !savePresentation ||
                    !EditorUtility.IsDirty(_presentationConfig);

                if (combatSaved && presentationSaved)
                {
                    _saveStatusMessage =
                        $"已保存 {requestedSaveCount} 个配置资产。";

                    _saveStatusMessageType =
                        MessageType.Info;
                }
                else
                {
                    _saveStatusMessage =
                        "部分配置资产保存失败，仍处于 Dirty 状态。";

                    _saveStatusMessageType =
                        MessageType.Error;
                }
            }
            catch (System.Exception exception)
            {
                _saveStatusMessage =
                    $"保存配置资产失败：{exception.Message}";

                _saveStatusMessageType =
                    MessageType.Error;
            }

            Repaint();
        }

    }
}
