using System.Text;
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

        private const string DefaultPreviewLayoutDirectory =
            "Assets/Editor/MotionWarpingPreviewLayouts";

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

        private MotionWarpingRootMotionSample
            _targetRootMotionSample;

        private AnimationClip _sampledExecuteClip;

        private GameObject _sampledExecutorPrefab;

        private string _executeSamplingMessage;

        private AnimationClip _sampledTargetRootMotionClip;

        private GameObject _sampledTargetRootMotionPrefab;

        private TargetPreviewBranch _sampledTargetRootMotionBranch;

        private string _targetRootMotionSamplingMessage;

        //========================Trajectory==============================
        private ExecutionWarpTrajectory _executeTrajectory;

        private ExecutionWarpTrajectory _targetTrajectory;

        private bool _showTrajectoryDiagnostics = true;

        private string _calibrationReportMessage = string.Empty;

        private MessageType _calibrationReportMessageType =
            MessageType.Info;

        //==========================Timeline==============================

        private float _previewElapsedTime = 0f;

        // Warp 轨迹始终使用处决者自己的 normalized time。
        private float _scrubNormalizedTime = 0f;

        private bool _isPreviewPlaying;

        private double _previewPlaybackStartedAt;

        private string _previewPlaybackMessage = string.Empty;

        //==========================PreivewScene==========================

        private const string DefaultPreviewScenePath = "Assets/Scenes/MotionWarpingVisualToolPreview.unity";

        private SceneAsset _previewSceneAsset;

        private Scene _previewScene;

        private bool _previewSceneOpenedByWindow;

        private bool _previewSceneIsEditorPreviewScene;

        private GameObject _previewRoot;
        private GameObject _previewExecutorInstance;

        private GameObject _previewTargetInstance;

        private GameObject _previewExecutorSource;
        private GameObject _previewTargetSource;
        private string _previewScenePath;

        private string _sceneNavigationMessage = string.Empty;

        private Vector3 _previewExecutorInitialPosition =
            new Vector3(0f, 0f, 0.6f);

        private float _previewExecutorInitialYaw = 180f;

        private Vector3 _previewExecutorInitialEulerAngles =
            new Vector3(0f, 180f, 0f);

        private Vector3 _previewTargetInitialPosition = Vector3.zero;

        private float _previewTargetInitialYaw;

        private Vector3 _previewTargetInitialEulerAngles = Vector3.zero;

        [SerializeField]
        private MotionWarpingPreviewLayout _previewLayout;

        [SerializeField]
        private Vector3 _configuredExecutorPosition =
            new Vector3(0f, 0f, 0.6f);

        [SerializeField]
        private Vector3 _configuredExecutorEulerAngles =
            new Vector3(0f, 180f, 0f);

        [SerializeField]
        private Vector3 _configuredTargetPosition = Vector3.zero;

        [SerializeField]
        private Vector3 _configuredTargetEulerAngles = Vector3.zero;

        [SerializeField]
        private bool _useCombatDefaultPreviewLayout = true;

        private string _previewLayoutMessage = string.Empty;

        private string _previewSpatialMessage = string.Empty;

        private MotionWarpingAnimationPreviewPlayer
            _executorAnimationPreviewPlayer;

        private MotionWarpingAnimationPreviewPlayer
            _targetAnimationPreviewPlayer;


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
            DrawHeader();
            DrawDefaultResourceToolBar();

            SyncPreviewScene();
            DrawPreviewWorkspaceToolbar();
            DrawTimelineSection();

            EditorGUILayout.Space(4f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawAssetSection();

            SyncPreviewScene();
            DrawPreviewSceneStatus();
            DrawInitialPoseConfigurationSection();


            DrawCombatConfigSection();
            DrawPresentationConfigSection();
            DrawConfigSaveSection();
            DrawClipSection();
            DrawExecuteRootMotionSamplingSection();

            DrawTargetPosePreviewSection();
            DrawTrajectoryDiagnosticsSection();
            DrawCalibrationReportSection();
            DrawDiagnosticsSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "进入专用编辑器场景后采样 Root Motion，并用 Scene View 轨迹和 Handle 校准 Motion Warping。",
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
            ClearTargetRootMotionSample();
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

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "被处决者 Warp",
                EditorStyles.boldLabel);
            DrawProperty(serialized, "executedAnchorOffset");
            DrawProperty(serialized, "executedAnchorYawOffset");
            DrawProperty(serialized, "executedDeathAnchorOffset");
            DrawProperty(serialized, "executedDeathAnchorYawOffset");
            DrawProperty(serialized, "executedMaxWarpTranslation");
            DrawProperty(serialized, "executedMaxWarpYaw");
            DrawProperty(serialized, "executedWarpWindowStartNormalized");
            DrawProperty(serialized, "executedWarpWindowEndNormalized");
            DrawProperty(serialized, "executedWarpCurve");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "处决时长",
                EditorStyles.boldLabel);
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

            EditorApplication.update +=
                HandleEditorUpdate;

            EditorSceneManager.activeSceneChangedInEditMode +=
                HandleActiveSceneChangedInEditMode;

            Undo.undoRedoPerformed += HandleUndoRedo;
            SceneView.duringSceneGui += HandleSceneGUI;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -=
                HandleBeforeAssemblyReload;

            EditorApplication.quitting -=
                HandleEditorQuitting;

            EditorApplication.update -=
                HandleEditorUpdate;

            EditorSceneManager.activeSceneChangedInEditMode -=
                HandleActiveSceneChangedInEditMode;

            Undo.undoRedoPerformed -= HandleUndoRedo;
            SceneView.duringSceneGui -= HandleSceneGUI;

            StopPreviewPlayback(false);
            DisposePreviewScene();
        }

        private void HandleUndoRedo()
        {
            _executeTrajectory = null;
            _targetTrajectory = null;

            if (_previewExecutorInstance != null &&
                _previewTargetInstance != null)
            {
                ApplyConfiguredPreviewPoses();
            }

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
            _targetTrajectory = null;

            if (_previewExecutorInstance != null &&
                _previewTargetInstance != null)
            {
                SetScrubElapsedTime(
                    _previewElapsedTime,
                    true);
            }

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

        private void HandleActiveSceneChangedInEditMode(
            Scene previousScene,
            Scene nextScene)
        {
            if (IsDedicatedPreviewSceneActive(
                    GetConfiguredPreviewScenePath()))
            {
                SyncPreviewScene();
            }
            else
            {
                StopPreviewPlayback(false);
                DisposePreviewScene();
            }

            Repaint();
            SceneView.RepaintAll();
        }

        private string GetConfiguredPreviewScenePath()
        {
            if (_previewSceneAsset != null)
            {
                string selectedPath =
                    AssetDatabase.GetAssetPath(_previewSceneAsset);

                if (!string.IsNullOrEmpty(selectedPath))
                {
                    return selectedPath;
                }
            }

            return DefaultPreviewScenePath;
        }

        private static bool IsDedicatedPreviewSceneActive(
            string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();

            return activeScene.IsValid() &&
                   activeScene.isLoaded &&
                   string.Equals(
                       activeScene.path,
                       scenePath,
                       System.StringComparison.OrdinalIgnoreCase);
        }

        private void DrawPreviewWorkspaceToolbar()
        {
            string scenePath = GetConfiguredPreviewScenePath();
            bool dedicatedSceneActive =
                IsDedicatedPreviewSceneActive(scenePath);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "预览工作区",
                EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           dedicatedSceneActive))
                {
                    if (GUILayout.Button(
                            dedicatedSceneActive
                                ? "已在预览场景"
                                : "进入预览场景",
                            GUILayout.Height(26f)))
                    {
                        OpenDedicatedPreviewScene();
                    }
                }

                bool canRun =
                    dedicatedSceneActive &&
                    _previewExecutorInstance != null &&
                    _previewTargetInstance != null &&
                    _executeClip != null;

                using (new EditorGUI.DisabledScope(!canRun))
                {
                    if (GUILayout.Button(
                            _isPreviewPlaying
                                ? "重新运行动作"
                                : "运行动作",
                            GUILayout.Height(26f)))
                    {
                        StartPreviewPlayback();
                    }
                }

                using (new EditorGUI.DisabledScope(
                           !_isPreviewPlaying))
                {
                    if (GUILayout.Button(
                            "停止",
                            GUILayout.Width(64f),
                            GUILayout.Height(26f)))
                    {
                        StopPreviewPlayback(false);
                    }
                }

                using (new EditorGUI.DisabledScope(!canRun))
                {
                    if (GUILayout.Button(
                            "复位",
                            GUILayout.Width(64f),
                            GUILayout.Height(26f)))
                    {
                        StopPreviewPlayback(false);
                        SetScrubElapsedTime(0f, true);
                        _previewPlaybackMessage =
                            "Edit Mode 预览已复位。";
                    }
                }
            }

            if (!dedicatedSceneActive)
            {
                EditorGUILayout.HelpBox(
                    $"当前不在专用预览场景。点击“进入预览场景”将以 Single 模式打开：\n{scenePath}",
                    MessageType.Info);
            }

            if (!string.IsNullOrEmpty(_sceneNavigationMessage))
            {
                EditorGUILayout.HelpBox(
                    _sceneNavigationMessage,
                    dedicatedSceneActive
                        ? MessageType.Info
                        : MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(_previewPlaybackMessage))
            {
                EditorGUILayout.HelpBox(
                    _previewPlaybackMessage,
                    _previewPlaybackMessage.StartsWith(
                        "无法")
                        ? MessageType.Warning
                        : MessageType.Info);
            }
        }

        private void OpenDedicatedPreviewScene()
        {
            string scenePath = GetConfiguredPreviewScenePath();
            SceneAsset sceneAsset =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    scenePath);

            if (sceneAsset == null)
            {
                _sceneNavigationMessage =
                    $"无法进入预览场景：资源不存在。\n{scenePath}";
                return;
            }

            if (IsDedicatedPreviewSceneActive(scenePath))
            {
                _previewSceneAsset = sceneAsset;
                _sceneNavigationMessage =
                    "专用预览场景已经处于活动状态。";
                SyncPreviewScene();
                return;
            }

            if (!EditorSceneManager
                    .SaveCurrentModifiedScenesIfUserWantsTo())
            {
                _sceneNavigationMessage =
                    "已取消进入预览场景。";
                return;
            }

            StopPreviewPlayback(false);
            DisposePreviewScene();

            try
            {
                Scene openedScene =
                    EditorSceneManager.OpenScene(
                        scenePath,
                        OpenSceneMode.Single);

                if (!openedScene.IsValid() ||
                    !openedScene.isLoaded)
                {
                    _sceneNavigationMessage =
                        "无法进入预览场景：Unity 未能加载场景。";
                    return;
                }

                _previewSceneAsset = sceneAsset;
                _sceneNavigationMessage =
                    $"已进入专用预览场景：{scenePath}";

                SyncPreviewScene();
                Repaint();
                SceneView.RepaintAll();
            }
            catch (System.Exception exception)
            {
                _sceneNavigationMessage =
                    $"无法进入预览场景：{exception.Message}";
            }
        }


        private void SyncPreviewScene()
        {
            string scenePath = GetConfiguredPreviewScenePath();

            bool hasRequiredResources =
                !string.IsNullOrEmpty(scenePath) &&
                _executorPreviewPrefab != null &&
                _targetPreviewPrefab != null;

            if (!hasRequiredResources ||
                !IsDedicatedPreviewSceneActive(scenePath))
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

            bool previewHierarchyIntact =
                _previewRoot != null &&
                _previewExecutorInstance != null &&
                _previewTargetInstance != null &&
                _previewExecutorInstance.transform.parent ==
                    _previewRoot.transform &&
                _previewTargetInstance.transform.parent ==
                    _previewRoot.transform &&
                _previewExecutorInstance.scene == _previewScene &&
                _previewTargetInstance.scene == _previewScene;

            if (_previewScene.IsValid() &&
                _previewScene.isLoaded &&
                previewHierarchyIntact &&
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
            Scene activeScene =
                SceneManager.GetActiveScene();

            if (!activeScene.IsValid() ||
                !activeScene.isLoaded ||
                !string.Equals(
                    activeScene.path,
                    scenePath,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _previewScene = activeScene;
            _previewSceneOpenedByWindow = false;
            _previewSceneIsEditorPreviewScene = false;
            _previewScenePath = scenePath;
            return true;
        }

        private void CreatePreviewInstances(string scenePath)
        {
            bool sceneWasDirty = _previewScene.isDirty;

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

            if (_useCombatDefaultPreviewLayout)
            {
                ConfigureDefaultPreviewPoses();
            }
            else
            {
                ApplyConfiguredPreviewPoses();
            }

            _previewExecutorSource =
                _executorPreviewPrefab;

            _previewTargetSource =
                _targetPreviewPrefab;

            _previewCombatSource = _combatConfig;

            _previewPresentationSource = _presentationConfig;

            if (!sceneWasDirty &&
                _previewScene.IsValid() &&
                _previewScene.isLoaded)
            {
                ClearPreviewSceneDirtiness(
                    _previewScene);
            }
        }

        private void ConfigureDefaultPreviewPoses()
        {
            Vector3 anchorOffset =
                _combatConfig == null
                    ? new Vector3(0f, 0f, 0.6f)
                    : _combatConfig.executorAnchorOffset;

            Quaternion targetYawRotation =
                Quaternion.identity;

            Vector3 executorPosition =
                Vector3.zero +
                targetYawRotation * anchorOffset;

            Vector3 executorToTarget =
                Vector3.zero - executorPosition;

            executorToTarget.y = 0f;

            float executorYaw =
                executorToTarget.sqrMagnitude > 0.000001f
                    ? Mathf.Atan2(
                          executorToTarget.x,
                          executorToTarget.z) *
                      Mathf.Rad2Deg
                    : 180f;

            _configuredTargetPosition = Vector3.zero;
            _configuredTargetEulerAngles = Vector3.zero;
            _configuredExecutorPosition = executorPosition;
            _configuredExecutorEulerAngles =
                new Vector3(0f, executorYaw, 0f);
            _useCombatDefaultPreviewLayout = true;

            ApplyConfiguredPreviewPoses();
        }



        private void DisposePreviewScene()
        {
            _previewElapsedTime = 0f;
            _scrubNormalizedTime = 0f;
            _targetPreviewNormalizedTime = 0f;

            bool hasPreviewState =
                _previewScene.IsValid() ||
                _previewRoot != null ||
                _previewExecutorInstance != null ||
                _previewTargetInstance != null ||
                _previewExecutorSource != null ||
                _previewTargetSource != null ||
                _executorAnimationPreviewPlayer != null ||
                _targetAnimationPreviewPlayer != null;

            if (!hasPreviewState)
            {
                _isPreviewPlaying = false;
                return;
            }

            Scene sceneToClose = _previewScene;
            bool sceneWasDirty =
                sceneToClose.IsValid() &&
                sceneToClose.isLoaded &&
                sceneToClose.isDirty;
            bool closeScene =
                _previewSceneOpenedByWindow;
            bool closeEditorPreviewScene =
                _previewSceneIsEditorPreviewScene;

            DisposeAnimationPreviewPlayers();
            ClearSelectionIfItBelongsToPreview();

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
                if (closeEditorPreviewScene)
                {
                    EditorSceneManager.ClosePreviewScene(
                        sceneToClose);
                }
                else
                {
                    EditorSceneManager.CloseScene(
                        sceneToClose,
                        true);
                }
            }
            else if (!sceneWasDirty &&
                     sceneToClose.IsValid() &&
                     sceneToClose.isLoaded)
            {
                ClearPreviewSceneDirtiness(
                    sceneToClose);
            }

            _previewScene = default;
            _previewScenePath = string.Empty;
            _previewSceneOpenedByWindow = false;
            _previewSceneIsEditorPreviewScene = false;

            _previewRoot = null;
            _previewExecutorInstance = null;
            _previewTargetInstance = null;
            _previewExecutorSource = null;
            _previewTargetSource = null;
            _previewCombatSource = null;
            _previewPresentationSource = null;
            _previewExecutorInitialPosition =
                new Vector3(0f, 0f, 0.6f);
            _previewExecutorInitialYaw = 180f;
            _previewExecutorInitialEulerAngles =
                new Vector3(0f, 180f, 0f);
            _previewTargetInitialPosition = Vector3.zero;
            _previewTargetInitialYaw = 0f;
            _previewTargetInitialEulerAngles = Vector3.zero;
            _executeTrajectory = null;
            _targetTrajectory = null;
            _previewSpatialMessage = string.Empty;
            _isPreviewPlaying = false;

            _targetPreviewMessage = string.Empty;
            _targetPoseSamplingAttempted = false;
            _lastTargetPoseClip = null;
            _lastTargetPoseBranch = TargetPreviewBranch.Executed;
            _lastTargetPoseNormalizedTime = -1f;
            ClearTargetPosePreviewState();
        }

        private void ClearSelectionIfItBelongsToPreview()
        {
            if (_previewRoot == null ||
                Selection.activeObject == null)
            {
                return;
            }

            GameObject selectedObject =
                Selection.activeObject as GameObject;

            if (selectedObject == null &&
                Selection.activeObject is Component component)
            {
                selectedObject = component.gameObject;
            }

            if (selectedObject != null &&
                selectedObject.transform.IsChildOf(
                    _previewRoot.transform))
            {
                Selection.activeObject = null;
            }
        }

        private void DrawPreviewSceneStatus()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "专用预览场景",
                EditorStyles.boldLabel);

            string scenePath = GetConfiguredPreviewScenePath();

            if (!IsDedicatedPreviewSceneActive(scenePath))
            {
                EditorGUILayout.HelpBox(
                    "当前场景不是专用预览场景；工具不会在这里创建预览对象。",
                    MessageType.Info);

                return;
            }

            if (!_previewScene.IsValid())
            {
                EditorGUILayout.HelpBox(
                    "专用场景已打开，但临时预览环境尚未就绪。",
                    MessageType.Info);

                return;
            }

            bool valid =
                _previewExecutorInstance != null &&
                _previewTargetInstance != null;

            EditorGUILayout.HelpBox(
                valid
                    ? $"已进入：{_previewScenePath}\n预览对象仅临时存在，不会写入场景资产。"
                    : "专用场景已打开，但预览对象创建失败。",
                valid ? MessageType.Info : MessageType.Error);

            if (!string.IsNullOrEmpty(_previewSpatialMessage))
            {
                EditorGUILayout.HelpBox(
                    _previewSpatialMessage,
                    _previewSpatialMessage.Contains(
                        "空间条件有效")
                        ? MessageType.Info
                        : MessageType.Warning);
            }
        }

        private void DrawInitialPoseConfigurationSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "预览初始站位",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "这里的世界位置/旋转只控制专用场景中的临时预览角色，不会改写运行时出生点或 Combat 配置。",
                MessageType.Info);

            MotionWarpingPreviewLayout selectedLayout =
                (MotionWarpingPreviewLayout)EditorGUILayout.ObjectField(
                    "站位配置",
                    _previewLayout,
                    typeof(MotionWarpingPreviewLayout),
                    false);

            if (selectedLayout != _previewLayout)
            {
                Undo.RecordObject(
                    this,
                    "Select Motion Warping Preview Layout");

                _previewLayout = selectedLayout;
                _previewLayoutMessage = selectedLayout == null
                    ? "已取消选择站位配置；当前工作副本仍保留。"
                    : $"已选择 {selectedLayout.name}；点击“加载配置”应用其内容。";
            }

            EditorGUI.BeginChangeCheck();

            Vector3 executorPosition =
                EditorGUILayout.Vector3Field(
                    "处决者 Position",
                    _configuredExecutorPosition);

            Vector3 executorEulerAngles =
                EditorGUILayout.Vector3Field(
                    "处决者 Rotation",
                    _configuredExecutorEulerAngles);

            Vector3 targetPosition =
                EditorGUILayout.Vector3Field(
                    "被处决者 Position",
                    _configuredTargetPosition);

            Vector3 targetEulerAngles =
                EditorGUILayout.Vector3Field(
                    "被处决者 Rotation",
                    _configuredTargetEulerAngles);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(
                    this,
                    "Edit Motion Warping Preview Layout");

                _configuredExecutorPosition = executorPosition;
                _configuredExecutorEulerAngles =
                    executorEulerAngles;
                _configuredTargetPosition = targetPosition;
                _configuredTargetEulerAngles = targetEulerAngles;
                _useCombatDefaultPreviewLayout = false;
                _previewLayoutMessage =
                    "初始站位工作副本已更新；如需复用，请新建或保存配置。";

                ApplyConfiguredPreviewPoses();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("新建配置"))
                {
                    CreatePreviewLayoutAsset();
                }

                using (new EditorGUI.DisabledScope(
                           _previewLayout == null))
                {
                    if (GUILayout.Button("加载配置"))
                    {
                        LoadSelectedPreviewLayout();
                    }

                    if (GUILayout.Button("保存配置"))
                    {
                        SaveSelectedPreviewLayout();
                    }
                }

                if (GUILayout.Button("按 Combat 锚点重置"))
                {
                    Undo.RecordObject(
                        this,
                        "Reset Motion Warping Preview Layout");

                    ConfigureDefaultPreviewPoses();
                    _previewLayoutMessage = _combatConfig == null
                        ? "已按后备锚点重置工作副本。"
                        : "已按当前 Combat 锚点重置工作副本；尚未覆盖站位配置资产。";
                }
            }

            if (!string.IsNullOrEmpty(_previewLayoutMessage))
            {
                bool isWarning =
                    _previewLayoutMessage.StartsWith("无法") ||
                    _previewLayoutMessage.StartsWith("站位数据无效");

                EditorGUILayout.HelpBox(
                    _previewLayoutMessage,
                    isWarning
                        ? MessageType.Warning
                        : MessageType.Info);
            }
        }

        private void ApplyConfiguredPreviewPoses()
        {
            if (!IsFiniteVector3(_configuredExecutorPosition) ||
                !IsFiniteVector3(
                    _configuredExecutorEulerAngles) ||
                !IsFiniteVector3(_configuredTargetPosition) ||
                !IsFiniteVector3(_configuredTargetEulerAngles))
            {
                _previewLayoutMessage =
                    "站位数据无效：Position/Rotation 必须全部是有限数值。";
                return;
            }

            if (_previewExecutorInstance == null ||
                _previewTargetInstance == null)
            {
                _previewSpatialMessage =
                    "初始站位已保留；进入专用预览场景后应用。";
                return;
            }

            StopPreviewPlayback(false);

            bool sceneWasDirty =
                _previewScene.IsValid() &&
                _previewScene.isLoaded &&
                _previewScene.isDirty;

            Transform targetTransform =
                _previewTargetInstance.transform;

            targetTransform.SetPositionAndRotation(
                _configuredTargetPosition,
                Quaternion.Euler(
                    _configuredTargetEulerAngles));

            _previewExecutorInstance.transform
                .SetPositionAndRotation(
                    _configuredExecutorPosition,
                    Quaternion.Euler(
                        _configuredExecutorEulerAngles));

            _previewExecutorInitialPosition =
                _configuredExecutorPosition;
            _previewExecutorInitialEulerAngles =
                _configuredExecutorEulerAngles;
            _previewExecutorInitialYaw =
                Mathf.DeltaAngle(
                    0f,
                    _configuredExecutorEulerAngles.y);

            _previewTargetInitialPosition =
                _configuredTargetPosition;
            _previewTargetInitialEulerAngles =
                _configuredTargetEulerAngles;
            _previewTargetInitialYaw =
                Mathf.DeltaAngle(
                    0f,
                    _configuredTargetEulerAngles.y);

            _scrubNormalizedTime = 0f;
            _targetPreviewNormalizedTime = 0f;
            _previewElapsedTime = 0f;
            _executeTrajectory = null;
            _targetTrajectory = null;

            if (_combatConfig == null)
            {
                _previewSpatialMessage =
                    "初始站位已应用；选择 Combat Config 后可验证空间条件。";
            }
            else
            {
                ExecutionEligibilityResult eligibility =
                    ExecutionSpatialValidator.Evaluate(
                        _previewExecutorInstance.transform,
                        targetTransform,
                        _combatConfig);

                string layoutKind =
                    _useCombatDefaultPreviewLayout
                        ? "默认"
                        : "当前";

                _previewSpatialMessage = eligibility.IsEligible
                    ? $"{layoutKind}空间条件有效：距离 {eligibility.HorizontalDistance:F3}m，" +
                      $"目标正面角 {eligibility.TargetFrontAngle:F2}°，" +
                      $"初始 Warp 误差 {eligibility.WarpTranslationError:F3}m / " +
                      $"{eligibility.WarpYawError:F2}°。"
                    : $"{layoutKind}站位未通过空间判定：{eligibility.RejectionReason}。";
            }

            RestorePreviewSceneDirtiness(sceneWasDirty);
            SceneView.RepaintAll();
        }

        private void LoadSelectedPreviewLayout()
        {
            if (_previewLayout == null)
            {
                _previewLayoutMessage =
                    "无法加载：请先选择站位配置。";
                return;
            }

            if (!IsPreviewLayoutFinite(_previewLayout))
            {
                _previewLayoutMessage =
                    "站位数据无效：所选配置包含 NaN 或 Infinity。";
                return;
            }

            Undo.RecordObject(
                this,
                "Load Motion Warping Preview Layout");

            _configuredExecutorPosition =
                _previewLayout.executorPosition;
            _configuredExecutorEulerAngles =
                _previewLayout.executorEulerAngles;
            _configuredTargetPosition =
                _previewLayout.targetPosition;
            _configuredTargetEulerAngles =
                _previewLayout.targetEulerAngles;
            _useCombatDefaultPreviewLayout = false;

            ApplyConfiguredPreviewPoses();
            _previewLayoutMessage =
                $"已加载站位配置：{_previewLayout.name}。";
        }

        private void SaveSelectedPreviewLayout()
        {
            if (_previewLayout == null)
            {
                _previewLayoutMessage =
                    "无法保存：请先选择或新建站位配置。";
                return;
            }

            if (!AreConfiguredPreviewPosesFinite())
            {
                _previewLayoutMessage =
                    "站位数据无效：Position/Rotation 必须全部是有限数值。";
                return;
            }

            Undo.RecordObject(
                _previewLayout,
                "Save Motion Warping Preview Layout");

            using var serialized =
                new SerializedObject(_previewLayout);

            serialized.Update();
            serialized.FindProperty("executorPosition")
                .vector3Value = _configuredExecutorPosition;
            serialized.FindProperty("executorEulerAngles")
                .vector3Value = _configuredExecutorEulerAngles;
            serialized.FindProperty("targetPosition")
                .vector3Value = _configuredTargetPosition;
            serialized.FindProperty("targetEulerAngles")
                .vector3Value = _configuredTargetEulerAngles;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(_previewLayout);
            AssetDatabase.SaveAssetIfDirty(_previewLayout);

            _previewLayoutMessage =
                $"已保存站位配置：{AssetDatabase.GetAssetPath(_previewLayout)}";
        }

        private void CreatePreviewLayoutAsset()
        {
            if (!AreConfiguredPreviewPosesFinite())
            {
                _previewLayoutMessage =
                    "站位数据无效：Position/Rotation 必须全部是有限数值。";
                return;
            }

            if (!EnsurePreviewLayoutDirectory())
            {
                _previewLayoutMessage =
                    $"无法创建默认目录：{DefaultPreviewLayoutDirectory}";
                return;
            }

            string assetPath =
                EditorUtility.SaveFilePanelInProject(
                    "新建 Motion Warping 预览站位配置",
                    "MotionWarpingPreviewLayout",
                    "asset",
                    "站位配置仅用于 Editor 预览。",
                    DefaultPreviewLayoutDirectory);

            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            string normalizedPath =
                assetPath.Replace('\\', '/');

            if (!normalizedPath.StartsWith(
                    "Assets/Editor/",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                _previewLayoutMessage =
                    "无法创建：站位配置必须保存在 Assets/Editor/ 下，以保持 Editor-only。";
                return;
            }

            var layout =
                CreateInstance<MotionWarpingPreviewLayout>();

            layout.executorPosition =
                _configuredExecutorPosition;
            layout.executorEulerAngles =
                _configuredExecutorEulerAngles;
            layout.targetPosition =
                _configuredTargetPosition;
            layout.targetEulerAngles =
                _configuredTargetEulerAngles;

            AssetDatabase.CreateAsset(layout, normalizedPath);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssetIfDirty(layout);

            Undo.RecordObject(
                this,
                "Select New Motion Warping Preview Layout");

            _previewLayout = layout;
            _useCombatDefaultPreviewLayout = false;
            _previewLayoutMessage =
                $"已新建站位配置：{normalizedPath}";

            Selection.activeObject = layout;
            EditorGUIUtility.PingObject(layout);
        }

        private bool AreConfiguredPreviewPosesFinite()
        {
            return IsFiniteVector3(_configuredExecutorPosition) &&
                   IsFiniteVector3(
                       _configuredExecutorEulerAngles) &&
                   IsFiniteVector3(_configuredTargetPosition) &&
                   IsFiniteVector3(
                       _configuredTargetEulerAngles);
        }

        private static bool IsPreviewLayoutFinite(
            MotionWarpingPreviewLayout layout)
        {
            return layout != null &&
                   IsFiniteVector3(layout.executorPosition) &&
                   IsFiniteVector3(
                       layout.executorEulerAngles) &&
                   IsFiniteVector3(layout.targetPosition) &&
                   IsFiniteVector3(layout.targetEulerAngles);
        }

        private static bool EnsurePreviewLayoutDirectory()
        {
            const string editorDirectory = "Assets/Editor";

            if (!AssetDatabase.IsValidFolder(editorDirectory))
            {
                string editorGuid =
                    AssetDatabase.CreateFolder(
                        "Assets",
                        "Editor");

                if (string.IsNullOrEmpty(editorGuid))
                {
                    return false;
                }
            }

            if (!AssetDatabase.IsValidFolder(
                    DefaultPreviewLayoutDirectory))
            {
                string layoutGuid =
                    AssetDatabase.CreateFolder(
                        editorDirectory,
                        "MotionWarpingPreviewLayouts");

                if (string.IsNullOrEmpty(layoutGuid))
                {
                    return false;
                }
            }

            return true;
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
            _isPreviewPlaying = false;
            DisposeExecutorAnimationPreviewPlayer();
            RestorePreviewExecutorInitialPose();
            _executeSamplingAttempted = true;
            ClearExecuteRootMotionSample();

            bool sceneWasDirty =
                _previewScene.IsValid() &&
                _previewScene.isLoaded &&
                _previewScene.isDirty;

            bool success =
                MotionWarpingRootMotionSampler.TrySample(
                    _previewExecutorInstance,
                    _executeClip,
                    ExecuteSampleRate,
                    out MotionWarpingRootMotionSample sample,
                    out string error);

            RestorePreviewSceneDirtiness(sceneWasDirty);

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

        private void StartPreviewPlayback()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _previewPlaybackMessage =
                    "无法运行动作：该按钮只用于 Edit Mode 预览。";
                return;
            }

            string scenePath = GetConfiguredPreviewScenePath();

            if (!IsDedicatedPreviewSceneActive(scenePath) ||
                _previewExecutorInstance == null ||
                _previewTargetInstance == null)
            {
                _previewPlaybackMessage =
                    "无法运行动作：请先进入专用预览场景并准备预览对象。";
                return;
            }

            InvalidateStaleExecuteSample();
            InvalidateStaleTargetRootMotionSample();

            if (_executeRootMotionSample == null)
            {
                SampleExecuteRootMotion();
            }

            if (_targetRootMotionSample == null)
            {
                SampleTargetRootMotion();
            }

            SetScrubElapsedTime(0f, true);

            if (!TryEvaluatePreviewState(
                    out ExecutionWarpTrajectory trajectory,
                    out string previewMessage))
            {
                _previewPlaybackMessage =
                    $"无法运行动作：{previewMessage}";
                return;
            }

            _executeTrajectory = trajectory;
            _previewPlaybackStartedAt =
                EditorApplication.timeSinceStartup;
            _isPreviewPlaying = true;
            _previewPlaybackMessage =
                "正在播放 Edit Mode 预览；该结果不等同于 Runtime Verified。";

            Repaint();
            SceneView.RepaintAll();
        }

        private void StopPreviewPlayback(bool resetToStart)
        {
            bool wasPlaying = _isPreviewPlaying;
            _isPreviewPlaying = false;

            if (resetToStart &&
                _previewExecutorInstance != null)
            {
                SetScrubElapsedTime(0f, true);
            }

            if (wasPlaying)
            {
                _previewPlaybackMessage =
                    "Edit Mode 动作预览已停止。";
                Repaint();
                SceneView.RepaintAll();
            }
        }

        private void HandleEditorUpdate()
        {
            if (!_isPreviewPlaying)
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                StopPreviewPlayback(false);
                _previewPlaybackMessage =
                    "Edit Mode 动作预览已停止：Unity 正在进入 Play Mode。";
                return;
            }

            if (!IsDedicatedPreviewSceneActive(
                    GetConfiguredPreviewScenePath()))
            {
                StopPreviewPlayback(false);
                DisposePreviewScene();
                _previewPlaybackMessage =
                    "Edit Mode 动作预览已停止：已离开专用预览场景。";
                return;
            }

            float duration = GetPreviewAnimationDuration();

            if (duration <= 0f)
            {
                StopPreviewPlayback(false);
                _previewPlaybackMessage =
                    "无法继续运行动作：动画时长无效。";
                return;
            }

            float elapsedTime = Mathf.Max(
                0f,
                (float)(EditorApplication.timeSinceStartup -
                        _previewPlaybackStartedAt));

            SetScrubElapsedTime(elapsedTime, true);

            if (elapsedTime >= duration)
            {
                _isPreviewPlaying = false;
                _previewPlaybackMessage =
                    "Edit Mode 动作预览播放完成；Runtime 仍未验证。";
            }
        }

        private void SetScrubElapsedTime(
            float elapsedTime,
            bool applyPose)
        {
            if (TryGetPreviewTimeSample(
                    elapsedTime,
                    out MotionWarpingPreviewTimeSample timing))
            {
                _previewElapsedTime = timing.ElapsedTime;
                _scrubNormalizedTime =
                    timing.ExecutorNormalizedTime;
                _targetPreviewNormalizedTime =
                    timing.TargetNormalizedTime;
            }
            else
            {
                _previewElapsedTime = 0f;
                _scrubNormalizedTime = 0f;
                _targetPreviewNormalizedTime = 0f;
            }

            if (applyPose)
            {
                ApplyPreviewPoseAtScrub();
            }

            Repaint();
            SceneView.RepaintAll();
        }

        private void ApplyPreviewPoseAtScrub()
        {
            ApplyTargetPosePreview();

            if (_previewExecutorInstance == null ||
                _executeClip == null ||
                !TryBuildExecuteTrajectory(
                    out ExecutionWarpTrajectory trajectory) ||
                trajectory.Frames == null ||
                trajectory.Frames.Count == 0)
            {
                RestorePreviewExecutorInitialPose();
                return;
            }

            float normalizedTime =
                Mathf.Clamp01(_scrubNormalizedTime);

            ExecutionPose executorPose =
                trajectory.InitialPose;

            for (int index = 0;
                 index < trajectory.Frames.Count;
                 index++)
            {
                ExecutionWarpTrajectoryFrame frame =
                    trajectory.Frames[index];

                if (frame.NormalizedTime >
                    normalizedTime + 0.0001f)
                {
                    break;
                }

                executorPose = frame.PoseAfter;
            }

            bool sceneWasDirty =
                _previewScene.IsValid() &&
                _previewScene.isLoaded &&
                _previewScene.isDirty;

            try
            {
                MotionWarpingAnimationPreviewPlayer player =
                    GetExecutorAnimationPreviewPlayer();

                if (!player.TrySample(
                        _previewExecutorInstance,
                        _executeClip,
                        normalizedTime,
                        out string animationError))
                {
                    _previewPlaybackMessage =
                        $"无法更新处决者动画：{animationError}";
                    _isPreviewPlaying = false;
                    return;
                }

                _previewExecutorInstance.transform
                    .SetPositionAndRotation(
                        executorPose.Position,
                        Quaternion.Euler(
                            _previewExecutorInitialEulerAngles.x,
                            executorPose.Yaw,
                            _previewExecutorInitialEulerAngles.z));

                _executeTrajectory = trajectory;
            }
            catch (System.Exception exception)
            {
                _previewPlaybackMessage =
                    $"无法更新动作姿态：{exception.Message}";
                _isPreviewPlaying = false;
            }
            finally
            {
                RestorePreviewSceneDirtiness(sceneWasDirty);
            }
        }

        private void RestorePreviewExecutorInitialPose()
        {
            if (_previewExecutorInstance == null)
            {
                return;
            }

            bool sceneWasDirty =
                _previewScene.IsValid() &&
                _previewScene.isLoaded &&
                _previewScene.isDirty;

            _previewExecutorInstance.transform
                .SetPositionAndRotation(
                    _previewExecutorInitialPosition,
                    Quaternion.Euler(
                        _previewExecutorInitialEulerAngles.x,
                        _previewExecutorInitialYaw,
                        _previewExecutorInitialEulerAngles.z));

            RestorePreviewSceneDirtiness(sceneWasDirty);
        }

        private void RestorePreviewTargetInitialPose()
        {
            if (_previewTargetInstance == null)
            {
                return;
            }

            bool sceneWasDirty =
                _previewScene.IsValid() &&
                _previewScene.isLoaded &&
                _previewScene.isDirty;

            _previewTargetInstance.transform
                .SetPositionAndRotation(
                    _previewTargetInitialPosition,
                    Quaternion.Euler(
                        _previewTargetInitialEulerAngles.x,
                        _previewTargetInitialYaw,
                        _previewTargetInitialEulerAngles.z));

            RestorePreviewSceneDirtiness(sceneWasDirty);
        }

        private void RestorePreviewSceneDirtiness(
            bool sceneWasDirty)
        {
            if (!sceneWasDirty &&
                _previewScene.IsValid() &&
                _previewScene.isLoaded)
            {
                ClearPreviewSceneDirtiness(
                    _previewScene);
            }
        }

        private static void ClearPreviewSceneDirtiness(
            Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            System.Reflection.MethodInfo clearMethod =
                typeof(EditorSceneManager).GetMethod(
                    "ClearSceneDirtiness",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);

            if (clearMethod == null)
            {
                return;
            }

            try
            {
                clearMethod.Invoke(
                    null,
                    new object[] { scene });
            }
            catch (System.Exception)
            {
                // 保留 Dirty 状态比自动保存或覆盖用户场景更安全。
            }
        }

        private MotionWarpingAnimationPreviewPlayer
            GetExecutorAnimationPreviewPlayer()
        {
            if (_executorAnimationPreviewPlayer == null)
            {
                _executorAnimationPreviewPlayer =
                    new MotionWarpingAnimationPreviewPlayer();
            }

            return _executorAnimationPreviewPlayer;
        }

        private MotionWarpingAnimationPreviewPlayer
            GetTargetAnimationPreviewPlayer()
        {
            if (_targetAnimationPreviewPlayer == null)
            {
                _targetAnimationPreviewPlayer =
                    new MotionWarpingAnimationPreviewPlayer();
            }

            return _targetAnimationPreviewPlayer;
        }

        private void DisposeExecutorAnimationPreviewPlayer()
        {
            if (_executorAnimationPreviewPlayer == null)
            {
                return;
            }

            _executorAnimationPreviewPlayer.Dispose();
            _executorAnimationPreviewPlayer = null;
        }

        private void DisposeTargetAnimationPreviewPlayer()
        {
            if (_targetAnimationPreviewPlayer == null)
            {
                return;
            }

            _targetAnimationPreviewPlayer.Dispose();
            _targetAnimationPreviewPlayer = null;
        }

        private void DisposeAnimationPreviewPlayers()
        {
            DisposeExecutorAnimationPreviewPlayer();
            DisposeTargetAnimationPreviewPlayer();
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

        private void InvalidateStaleTargetRootMotionSample()
        {
            if (_targetRootMotionSample == null)
            {
                return;
            }

            bool sourceChanged =
                _sampledTargetRootMotionClip !=
                    GetSelectedTargetPreviewClip() ||
                _sampledTargetRootMotionPrefab !=
                    _targetPreviewPrefab ||
                _sampledTargetRootMotionBranch !=
                    _targetPreviewBranch;

            if (!sourceChanged)
            {
                return;
            }

            ClearTargetRootMotionSample();
            _targetRootMotionSamplingMessage =
                "目标 Root Motion 采样来源已经变化，请重新采样当前分支。";
        }

        private void ClearTargetRootMotionSample()
        {
            _targetRootMotionSample = null;
            _sampledTargetRootMotionClip = null;
            _sampledTargetRootMotionPrefab = null;
            _sampledTargetRootMotionBranch =
                TargetPreviewBranch.Executed;
            _targetRootMotionSamplingMessage = string.Empty;
            _targetTrajectory = null;
        }

        private void SampleTargetRootMotion()
        {
            _isPreviewPlaying = false;
            DisposeTargetAnimationPreviewPlayer();
            RestorePreviewTargetInitialPose();
            ClearTargetRootMotionSample();

            AnimationClip clip = GetSelectedTargetPreviewClip();

            if (_previewTargetInstance == null || clip == null)
            {
                _targetRootMotionSamplingMessage =
                    "目标 Root Motion 采样失败：预览实例或动画不存在。";
                return;
            }

            bool sceneWasDirty =
                _previewScene.IsValid() &&
                _previewScene.isLoaded &&
                _previewScene.isDirty;

            bool success = MotionWarpingRootMotionSampler.TrySample(
                _previewTargetInstance,
                clip,
                ExecuteSampleRate,
                out MotionWarpingRootMotionSample sample,
                out string error);

            RestorePreviewTargetInitialPose();
            RestorePreviewSceneDirtiness(sceneWasDirty);

            if (!success || sample == null ||
                sample.InputSamples == null ||
                sample.InputSamples.Count == 0)
            {
                _targetRootMotionSamplingMessage =
                    $"目标 Root Motion 采样失败：" +
                    $"{(string.IsNullOrEmpty(error) ? "没有生成帧样本。" : error)}";
                SceneView.RepaintAll();
                Repaint();
                return;
            }

            _targetRootMotionSample = sample;
            _sampledTargetRootMotionClip = clip;
            _sampledTargetRootMotionPrefab = _targetPreviewPrefab;
            _sampledTargetRootMotionBranch = _targetPreviewBranch;
            _targetRootMotionSamplingMessage = sample.HasRootMotion
                ? $"采样完成：{sample.InputSamples.Count} 帧，" +
                  $"水平位移 " +
                  $"{ExecutionWarpSolver.ProjectToHorizontalPlane(sample.TotalPosition).magnitude:F4}m，" +
                  $"Yaw {sample.TotalYaw:F3}°。"
                : "采样完成，但没有检测到有效目标 Root Motion。";

            SceneView.RepaintAll();
            Repaint();
        }

        private void DrawTargetPosePreviewSection()
        {
            InvalidateStaleTargetRootMotionSample();

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

                bool branchChanged =
                    EditorGUI.EndChangeCheck();

                if (branchChanged)
                {
                    StopPreviewPlayback(false);
                    _targetPreviewBranch = nextBranch;
                    ClearTargetRootMotionSample();

                    if (GetSelectedTargetPreviewClip() != null)
                    {
                        SampleTargetRootMotion();
                    }

                    SetScrubElapsedTime(
                        _previewElapsedTime,
                        true);
                }

                AnimationClip selectedClip =
                    GetSelectedTargetPreviewClip();

                float targetDuration =
                    GetTargetPreviewDuration();

                EditorGUILayout.LabelField(
                    "Target Time",
                    $"{Mathf.Min(_previewElapsedTime, targetDuration):F3}s / " +
                    $"{targetDuration:F3}s");

                EditorGUILayout.LabelField(
                    "Target Normalized",
                    _targetPreviewNormalizedTime.ToString("F3"));

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
                    "目标会使用当前分支的 Root Motion，并通过与处决者相同的 " +
                    "ExecutionWarpSolver 收敛到目标锚点。",
                    MessageType.Info);

                bool canSampleRootMotion =
                    selectedClip != null &&
                    _previewTargetInstance != null;

                using (new EditorGUI.DisabledScope(!canSampleRootMotion))
                {
                    if (GUILayout.Button(
                            "采样当前目标 Root Motion",
                            GUILayout.Height(24f)))
                    {
                        SampleTargetRootMotion();
                        SetScrubElapsedTime(
                            _previewElapsedTime,
                            true);
                    }
                }

                if (_targetRootMotionSample != null)
                {
                    EditorGUILayout.LabelField(
                        "Root Motion 位移",
                        _targetRootMotionSample.TotalPosition.ToString("F4"));
                    EditorGUILayout.LabelField(
                        "Root Motion Yaw",
                        $"{_targetRootMotionSample.TotalYaw:F3}°");
                }

                if (!string.IsNullOrEmpty(
                        _targetRootMotionSamplingMessage))
                {
                    EditorGUILayout.HelpBox(
                        _targetRootMotionSamplingMessage,
                        _targetRootMotionSample != null &&
                        _targetRootMotionSample.HasRootMotion
                            ? MessageType.Info
                            : MessageType.Warning);
                }

                if (!string.IsNullOrEmpty(_targetPreviewMessage))
                {
                    MessageType messageType =
                        _targetPreviewMessage.StartsWith("姿态与 Root Motion 预览完成")
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

            ExecutionPose targetPose = new ExecutionPose(
                _previewTargetInitialPosition,
                _previewTargetInitialYaw);

            if (TryBuildTargetTrajectory(
                    out ExecutionWarpTrajectory trajectory) &&
                trajectory.Frames != null)
            {
                float normalizedTime =
                    Mathf.Clamp01(_targetPreviewNormalizedTime);

                for (int index = 0;
                     index < trajectory.Frames.Count;
                     index++)
                {
                    ExecutionWarpTrajectoryFrame frame =
                        trajectory.Frames[index];

                    if (frame.NormalizedTime >
                        normalizedTime + 0.0001f)
                    {
                        break;
                    }

                    targetPose = frame.PoseAfter;
                }

                _targetTrajectory = trajectory;
            }

            bool sceneWasDirty =
                _previewScene.IsValid() &&
                _previewScene.isLoaded &&
                _previewScene.isDirty;

            try
            {
                MotionWarpingAnimationPreviewPlayer player =
                    GetTargetAnimationPreviewPlayer();

                if (!player.TrySample(
                        _previewTargetInstance,
                        clip,
                        _targetPreviewNormalizedTime,
                        out string animationError))
                {
                    _targetPreviewMessage =
                        $"姿态采样失败：{animationError}";
                    ClearTargetPosePreviewState();
                    return;
                }

                // Playable 负责骨骼姿态，通用 Warp 轨迹负责参与者根节点。
                targetRoot.SetPositionAndRotation(
                    targetPose.Position,
                    Quaternion.Euler(
                        _previewTargetInitialEulerAngles.x,
                        targetPose.Yaw,
                        _previewTargetInitialEulerAngles.z));

                _sampledTargetPreviewClip = clip;
                _sampledTargetPreviewBranch =
                    _targetPreviewBranch;
                _sampledTargetPreviewNormalizedTime =
                    _targetPreviewNormalizedTime;

                _targetPreviewMessage =
                    $"姿态与 Root Motion 预览完成：{clip.name}，" +
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
                RestorePreviewSceneDirtiness(sceneWasDirty);
            }
        }

        private void ClearTargetPosePreviewState()
        {
            DisposeTargetAnimationPreviewPlayer();
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
                _targetPreviewMessage.StartsWith(
                    "姿态与 Root Motion 预览完成");

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
                "目标姿态和 Warp 根轨迹预览有效。" +
                "不等同于运行时处决验证通过。",
                MessageType.Info);
        }

        private float GetExecutorPreviewDuration()
        {
            if (_combatConfig != null &&
                IsFiniteScalar(
                    _combatConfig.executingDuration) &&
                _combatConfig.executingDuration > 0f)
            {
                return _combatConfig.executingDuration;
            }

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

        private float GetTargetPreviewDuration()
        {
            if (_combatConfig != null)
            {
                float configuredDuration =
                    _targetPreviewBranch ==
                    TargetPreviewBranch.ExecutedDeath
                        ? _combatConfig.executedDeathDuration
                        : _combatConfig.executedDuration;

                if (IsFiniteScalar(configuredDuration) &&
                    configuredDuration > 0f)
                {
                    return configuredDuration;
                }
            }

            AnimationClip targetClip =
                GetSelectedTargetPreviewClip();

            if (targetClip != null &&
                IsFiniteScalar(targetClip.length) &&
                targetClip.length > 0f)
            {
                return targetClip.length;
            }

            if (_presentationConfig != null)
            {
                float calibratedDuration =
                    _targetPreviewBranch ==
                    TargetPreviewBranch.ExecutedDeath
                        ? _presentationConfig
                            .executedDeathClipDuration
                        : _presentationConfig
                            .executedClipDuration;

                if (IsFiniteScalar(calibratedDuration) &&
                    calibratedDuration > 0f)
                {
                    return calibratedDuration;
                }
            }

            return 0f;
        }

        private bool TryGetPreviewTimeSample(
            float elapsedTime,
            out MotionWarpingPreviewTimeSample timing)
        {
            return MotionWarpingPreviewTiming.TryEvaluate(
                elapsedTime,
                GetExecutorPreviewDuration(),
                GetTargetPreviewDuration(),
                out timing);
        }

        private float GetPreviewAnimationDuration()
        {
            return TryGetPreviewTimeSample(
                    0f,
                    out MotionWarpingPreviewTimeSample timing)
                ? timing.PreviewDuration
                : 0f;
        }

        private void DrawTimelineSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Motion Warping Timeline",
                EditorStyles.boldLabel);

            float executorDuration =
                GetExecutorPreviewDuration();

            float targetDuration =
                GetTargetPreviewDuration();

            float duration = GetPreviewAnimationDuration();

            if (duration <= 0f)
            {
                EditorGUILayout.HelpBox(
                    "Timeline unavailable: assign valid configs and execution clips.",
                    MessageType.Info);
                return;
            }

            if (TryGetPreviewTimeSample(
                    _previewElapsedTime,
                    out MotionWarpingPreviewTimeSample currentTiming))
            {
                _previewElapsedTime = currentTiming.ElapsedTime;
                _scrubNormalizedTime =
                    currentTiming.ExecutorNormalizedTime;
                _targetPreviewNormalizedTime =
                    currentTiming.TargetNormalizedTime;
            }

            float warpStart = _combatConfig == null
                ? 0f
                : Mathf.Clamp01(
                    _combatConfig.executionWarpWindowStartNormalized);

            float warpEnd = _combatConfig == null
                ? 0f
                : Mathf.Clamp01(
                    _combatConfig.executionWarpWindowEndNormalized);

            float targetWarpStart = _combatConfig == null
                ? 0f
                : Mathf.Clamp01(
                    _combatConfig.executedWarpWindowStartNormalized);

            float targetWarpEnd = _combatConfig == null
                ? 0f
                : Mathf.Clamp01(
                    _combatConfig.executedWarpWindowEndNormalized);

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
                    "Executor Duration",
                    $"{executorDuration:F3}s");

                EditorGUILayout.LabelField(
                    "Target Duration",
                    $"{targetDuration:F3}s " +
                    $"({_targetPreviewBranch})");

                EditorGUILayout.LabelField(
                    "Preview Duration",
                    $"{duration:F3}s");

                EditorGUILayout.LabelField(
                    "Executor Warp Window",
                    $"{warpStart:F3} - {warpEnd:F3} " +
                    $"({warpStart * executorDuration:F3}s - " +
                    $"{warpEnd * executorDuration:F3}s)");

                EditorGUILayout.LabelField(
                    "Target Warp Window",
                    $"{targetWarpStart:F3} - {targetWarpEnd:F3} " +
                    $"({targetWarpStart * targetDuration:F3}s - " +
                    $"{targetWarpEnd * targetDuration:F3}s)");

                EditorGUILayout.LabelField(
                    "Execution Result",
                    $"{resultTime:F3}s " +
                    $"(Normalized {resultNormalized:F3})");

                DrawTimelineEditingControls(
                    executorDuration);

                EditorGUI.BeginChangeCheck();

                float currentScrub =
                    float.IsNaN(_previewElapsedTime) ||
                    float.IsInfinity(_previewElapsedTime)
                        ? 0f
                        : Mathf.Clamp(
                            _previewElapsedTime,
                            0f,
                            duration);

                float nextScrub = EditorGUILayout.Slider(
                    "Scrub Time",
                    currentScrub,
                    0f,
                    duration);

                if (EditorGUI.EndChangeCheck())
                {
                    StopPreviewPlayback(false);
                    SetScrubElapsedTime(nextScrub, true);
                }

                EditorGUILayout.LabelField(
                    "Current Scrub",
                    $"{_previewElapsedTime:F3}s / " +
                    $"{duration:F3}s");

                EditorGUILayout.LabelField(
                    "Executor Normalized",
                    _scrubNormalizedTime.ToString("F3"));

                EditorGUILayout.LabelField(
                    "Target Normalized",
                    _targetPreviewNormalizedTime.ToString("F3"));

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

            SerializedProperty targetWarpStartProperty =
                serialized.FindProperty(
                    "executedWarpWindowStartNormalized");

            SerializedProperty targetWarpEndProperty =
                serialized.FindProperty(
                    "executedWarpWindowEndNormalized");

            if (warpStartProperty == null ||
                warpEndProperty == null ||
                targetWarpStartProperty == null ||
                targetWarpEndProperty == null ||
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

            float nextTargetWarpStart =
                IsFiniteScalar(targetWarpStartProperty.floatValue)
                    ? Mathf.Clamp01(targetWarpStartProperty.floatValue)
                    : 0f;

            float nextTargetWarpEnd =
                IsFiniteScalar(targetWarpEndProperty.floatValue)
                    ? Mathf.Clamp01(targetWarpEndProperty.floatValue)
                    : 1f;

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.MinMaxSlider(
                "Edit Executor Warp Window",
                ref nextWarpStart,
                ref nextWarpEnd,
                0f,
                1f);

            EditorGUILayout.MinMaxSlider(
                "Edit Target Warp Window",
                ref nextTargetWarpStart,
                ref nextTargetWarpEnd,
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

            targetWarpStartProperty.floatValue =
                Mathf.Min(nextTargetWarpStart, nextTargetWarpEnd);

            targetWarpEndProperty.floatValue =
                Mathf.Max(nextTargetWarpStart, nextTargetWarpEnd);

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

                DrawTargetTrajectoryDiagnostics();
            }
        }

        private void DrawTargetTrajectoryDiagnostics()
        {
            if (_targetTrajectory == null ||
                _targetTrajectory.Frames == null ||
                _targetTrajectory.Frames.Count == 0 ||
                _combatConfig == null)
            {
                return;
            }

            int frameCount = _targetTrajectory.Frames.Count;
            int frameIndex = Mathf.Clamp(
                Mathf.RoundToInt(
                    Mathf.Clamp01(_targetPreviewNormalizedTime) *
                    (frameCount - 1)),
                0,
                frameCount - 1);

            ExecutionWarpTrajectoryFrame frame =
                _targetTrajectory.Frames[frameIndex];
            ExecutionWarpTrajectoryFrame finalFrame =
                _targetTrajectory.Frames[frameCount - 1];

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                $"目标轨迹诊断 ({_targetPreviewBranch})",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "当前目标帧",
                $"{frame.Index + 1}/{frameCount}, " +
                $"Normalized {frame.NormalizedTime:F4}");
            EditorGUILayout.LabelField(
                "目标修正 Delta Position",
                frame.CorrectedDeltaPosition.ToString("F5"));
            EditorGUILayout.LabelField(
                "目标累计位置修正",
                frame.RequestedTranslationCorrection.ToString("F5"));
            EditorGUILayout.LabelField(
                "目标位置预算占用",
                FormatBudgetUsage(
                    frame.RequestedTranslationCorrection.magnitude,
                    _combatConfig.executedMaxWarpTranslation));
            EditorGUILayout.LabelField(
                "目标累计 Yaw 修正",
                $"{frame.RequestedYawCorrection:F4}°");
            EditorGUILayout.LabelField(
                "目标 Yaw 预算占用",
                FormatBudgetUsage(
                    Mathf.Abs(frame.RequestedYawCorrection),
                    _combatConfig.executedMaxWarpYaw));
            EditorGUILayout.LabelField(
                "目标最终姿态",
                $"Position {_targetTrajectory.FinalPose.Position:F4}, " +
                $"Yaw {_targetTrajectory.FinalPose.Yaw:F3}°");
            EditorGUILayout.LabelField(
                "目标最终位置残差",
                _targetTrajectory.FinalPositionError.ToString("F5"));
            EditorGUILayout.LabelField(
                "目标最终 Yaw 残差",
                $"{_targetTrajectory.FinalYawError:F4}°");
            EditorGUILayout.LabelField(
                "目标 Warp Window 完成",
                finalFrame.IsWarpWindowComplete ? "是" : "否");
        }

        private void DrawCalibrationReportSection()
        {
            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField(
                "校准记录",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "完成参数调整并确认 Preview Valid 后，" +
                "复制当前配置和预测残差，用于 6.3/6.4 验收记录。",
                MessageType.Info);

            if (GUILayout.Button(
                    "复制当前校准记录",
                    GUILayout.Height(24f)))
            {
                if (TryBuildCalibrationReport(
                        out string report,
                        out string message))
                {
                    EditorGUIUtility.systemCopyBuffer = report;

                    _calibrationReportMessage = message;
                    _calibrationReportMessageType =
                        MessageType.Info;
                }
                else
                {
                    _calibrationReportMessage = message;
                    _calibrationReportMessageType =
                        MessageType.Warning;
                }
            }

            if (!string.IsNullOrEmpty(
                    _calibrationReportMessage))
            {
                EditorGUILayout.HelpBox(
                    _calibrationReportMessage,
                    _calibrationReportMessageType);
            }
        }

        private bool TryBuildCalibrationReport(
            out string report,
            out string message)
        {
            report = string.Empty;

            bool previewValid = TryEvaluatePreviewState(
                out ExecutionWarpTrajectory trajectory,
                out string previewMessage);

            _executeTrajectory = trajectory;

            if (!previewValid)
            {
                message =
                    "无法生成校准记录：" +
                    previewMessage;

                return false;
            }

            if (trajectory == null ||
                trajectory.Frames == null ||
                trajectory.Frames.Count == 0)
            {
                message =
                    "无法生成校准记录：当前没有有效轨迹帧。";

                return false;
            }

            ExecutionWarpTrajectoryFrame finalFrame =
                trajectory.Frames[
                    trajectory.Frames.Count - 1];

            string combatConfigPath =
                AssetDatabase.GetAssetPath(
                    _combatConfig);

            string executeClipPath =
                AssetDatabase.GetAssetPath(
                    _executeClip);

            var builder = new StringBuilder(1024);

            builder.AppendLine(
                "# Motion Warping Calibration Snapshot");

            builder.AppendLine(
                $"- Captured At: " +
                $"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            builder.AppendLine(
                $"- Combat Config: {combatConfigPath}");

            builder.AppendLine(
                $"- Execute Clip: {executeClipPath}");

            builder.AppendLine(
                $"- Execute Clip Duration: " +
                $"{_executeClip.length:F4}s");

            builder.AppendLine(
                $"- Executor Runtime Duration: " +
                $"{GetExecutorPreviewDuration():F4}s");

            builder.AppendLine(
                $"- Target Runtime Duration " +
                $"({_targetPreviewBranch}): " +
                $"{GetTargetPreviewDuration():F4}s");

            builder.AppendLine(
                $"- Synchronized Preview Duration: " +
                $"{GetPreviewAnimationDuration():F4}s");

            builder.AppendLine(
                $"- executionResultTime: " +
                $"{_combatConfig.executionResultTime:F4}s");

            builder.AppendLine(
                $"- Warp Window: " +
                $"{_combatConfig.executionWarpWindowStartNormalized:F4}" +
                " -> " +
                $"{_combatConfig.executionWarpWindowEndNormalized:F4}");

            builder.AppendLine(
                $"- executorAnchorOffset: " +
                $"{_combatConfig.executorAnchorOffset:F5}");

            builder.AppendLine(
                $"- Max Warp Translation: " +
                $"{_combatConfig.executionMaxWarpTranslation:F5}m");

            builder.AppendLine(
                $"- Max Warp Yaw: " +
                $"{_combatConfig.executionMaxWarpYaw:F4}deg");

            builder.AppendLine(
                $"- Initial Executor Pose: " +
                $"Position {trajectory.InitialPose.Position:F5}, " +
                $"Yaw {trajectory.InitialPose.Yaw:F4}deg");

            builder.AppendLine(
                $"- Anchor Pose: " +
                $"Position {trajectory.AnchorPose.Position:F5}, " +
                $"Yaw {trajectory.AnchorPose.Yaw:F4}deg");

            builder.AppendLine(
                $"- Final Executor Pose: " +
                $"Position {trajectory.FinalPose.Position:F5}, " +
                $"Yaw {trajectory.FinalPose.Yaw:F4}deg");

            builder.AppendLine(
                $"- Final Position Error Vector: " +
                $"{trajectory.FinalPositionError:F5}");

            builder.AppendLine(
                $"- Final Position Error Magnitude: " +
                $"{trajectory.FinalPositionError.magnitude:F5}m");

            builder.AppendLine(
                $"- Final Yaw Error: " +
                $"{trajectory.FinalYawError:F4}deg");

            builder.AppendLine(
                $"- Requested Translation Correction: " +
                $"{finalFrame.RequestedTranslationCorrection:F5}");

            builder.AppendLine(
                $"- Requested Yaw Correction: " +
                $"{finalFrame.RequestedYawCorrection:F4}deg");

            builder.AppendLine(
                $"- Warp Window Complete: " +
                $"{finalFrame.IsWarpWindowComplete}");

            builder.AppendLine(
                $"- Sample Count: " +
                $"{trajectory.Frames.Count}");

            builder.AppendLine(
                "- Config Valid: true");

            builder.AppendLine(
                "- Preview Valid: true");

            builder.AppendLine(
                "- Runtime Verified: false");

            builder.AppendLine(
                $"- Preview Diagnostic: {previewMessage}");

            report = builder.ToString();

            message =
                "校准记录已复制到剪贴板。" +
                "Runtime Verified 仍需在 6.4 的 Offline 双 Player 验收中确认。";

            return true;
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

            if (!ExecutionWarpSettings.TryFromTarget(
                    _combatConfig,
                    out _))
            {
                message =
                    "Config Invalid：被处决者 Motion Warping 配置非法。Preview Blocked。";
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

            if (_targetRootMotionSample == null ||
                _targetRootMotionSample.InputSamples == null ||
                _targetRootMotionSample.InputSamples.Count == 0)
            {
                message =
                    "Preview Blocked：尚未取得当前目标分支的 Root Motion 样本。";
                return false;
            }

            if (_sampledTargetRootMotionClip != selectedTargetClip ||
                _sampledTargetRootMotionPrefab != _targetPreviewPrefab ||
                _sampledTargetRootMotionBranch != _targetPreviewBranch)
            {
                message =
                    "Preview Blocked：目标 Root Motion 采样来源已经变化，请重新采样。";
                return false;
            }

            if (!_targetRootMotionSample.HasRootMotion)
            {
                message =
                    "Preview Blocked：当前目标动画未产生可信 Root Motion。";
                return false;
            }

            if (!TryBuildTargetTrajectory(
                    out ExecutionWarpTrajectory targetTrajectory))
            {
                message =
                    "Preview Blocked：共享 ExecutionWarpSolver 无法生成目标轨迹。";
                return false;
            }

            _targetTrajectory = targetTrajectory;

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

            var initialExecutorPose =
                new ExecutionPose(
                    _previewExecutorInitialPosition,
                    _previewExecutorInitialYaw);

            var targetPose =
                new ExecutionPose(
                    targetTransform.position,
                    targetTransform.eulerAngles.y);

            ExecutionEligibilityResult eligibility =
                ExecutionSpatialValidator.Evaluate(
                    initialExecutorPose,
                    targetPose,
                    _combatConfig);

            if (!eligibility.HasSpatialSolution)
            {
                return false;
            }

            positionError = eligibility.WarpTranslationError;
            yawError = eligibility.WarpYawError;

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
            DrawTargetAnchorHandle();

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (_executeRootMotionSample != null &&
                _executeRootMotionSample.HasRootMotion &&
                TryBuildExecuteTrajectory(
                    out ExecutionWarpTrajectory trajectory))
            {
                _executeTrajectory = trajectory;
                DrawTrajectoryScene(trajectory);
            }

            if (_targetRootMotionSample != null &&
                _targetRootMotionSample.HasRootMotion &&
                TryBuildTargetTrajectory(
                    out ExecutionWarpTrajectory targetTrajectory))
            {
                _targetTrajectory = targetTrajectory;
                DrawTargetTrajectoryScene(targetTrajectory);
            }
        }

        private void DrawAnchorHandle()
        {
            if (_combatConfig == null ||
                _previewTargetInstance == null)
            {
                return;
            }

            float targetYaw =
                _previewTargetInitialYaw;

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
                _previewTargetInitialPosition +
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
                (nextWorldAnchor - _previewTargetInitialPosition);

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

        private void DrawTargetAnchorHandle()
        {
            if (_combatConfig == null ||
                _previewTargetInstance == null)
            {
                return;
            }

            Quaternion referenceRotation = Quaternion.Euler(
                0f,
                _previewTargetInitialYaw,
                0f);

            string propertyName = _targetPreviewBranch ==
                TargetPreviewBranch.ExecutedDeath
                    ? "executedDeathAnchorOffset"
                    : "executedAnchorOffset";

            using var serialized =
                new SerializedObject(_combatConfig);
            serialized.Update();

            SerializedProperty offsetProperty =
                serialized.FindProperty(propertyName);

            if (offsetProperty == null ||
                offsetProperty.propertyType !=
                SerializedPropertyType.Vector3)
            {
                return;
            }

            Vector3 worldAnchor =
                _previewTargetInitialPosition +
                referenceRotation * offsetProperty.vector3Value;

            Handles.color = new Color(0.25f, 0.75f, 1f);
            Handles.Label(
                worldAnchor + Vector3.up * 0.16f,
                "Target Warp Anchor");

            EditorGUI.BeginChangeCheck();
            Vector3 nextWorldAnchor = Handles.PositionHandle(
                worldAnchor,
                referenceRotation);

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Vector3 nextOffset =
                Quaternion.Inverse(referenceRotation) *
                (nextWorldAnchor - _previewTargetInitialPosition);

            if (!IsFiniteVector3(nextOffset))
            {
                return;
            }

            Undo.RecordObject(
                _combatConfig,
                "Move Target Motion Warping Anchor");
            serialized.Update();
            offsetProperty.vector3Value = nextOffset;

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

            var initialExecutorPose =
                new ExecutionPose(
                    _previewExecutorInitialPosition,
                    _previewExecutorInitialYaw);

            var targetPose =
                new ExecutionPose(
                    _previewTargetInitialPosition,
                    _previewTargetInitialYaw);

            ExecutionEligibilityResult eligibility =
                ExecutionSpatialValidator.Evaluate(
                    initialExecutorPose,
                    targetPose,
                    _combatConfig);

            if (!eligibility.HasSpatialSolution)
            {
                return false;
            }

            return ExecutionWarpTrajectorySampler.TrySample(
                eligibility.ExecutorAnchorPose,
                initialExecutorPose,
                _combatConfig,
                _executeRootMotionSample.InputSamples,
                out trajectory);
        }

        private bool TryBuildTargetTrajectory(
            out ExecutionWarpTrajectory trajectory)
        {
            trajectory = null;

            if (_combatConfig == null ||
                _previewTargetInstance == null ||
                _targetRootMotionSample == null ||
                _targetRootMotionSample.InputSamples == null ||
                _targetRootMotionSample.InputSamples.Count == 0 ||
                !ExecutionWarpSettings.TryFromTarget(
                    _combatConfig,
                    out ExecutionWarpSettings settings))
            {
                return false;
            }

            var initialTargetPose = new ExecutionPose(
                _previewTargetInitialPosition,
                _previewTargetInitialYaw);

            bool lethal = _targetPreviewBranch ==
                TargetPreviewBranch.ExecutedDeath;

            if (!ExecutionWarpAnchorResolver.TryResolveTargetAnchor(
                    initialTargetPose,
                    _combatConfig,
                    lethal,
                    out ExecutionPose anchorPose))
            {
                return false;
            }

            return ExecutionWarpTrajectorySampler.TrySample(
                anchorPose,
                initialTargetPose,
                settings,
                _targetRootMotionSample.InputSamples,
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
                "Target Start",
                _previewTargetInitialPosition,
                _previewTargetInitialYaw,
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

        private void DrawTargetTrajectoryScene(
            ExecutionWarpTrajectory trajectory)
        {
            if (trajectory == null ||
                _targetRootMotionSample == null)
            {
                return;
            }

            DrawPoseMarker(
                "Target Start",
                trajectory.InitialPose.Position,
                trajectory.InitialPose.Yaw,
                Color.cyan);

            DrawPoseMarker(
                "Target Anchor",
                trajectory.AnchorPose.Position,
                trajectory.AnchorPose.Yaw,
                new Color(0.15f, 0.65f, 1f));

            Handles.color = new Color(0.35f, 0.55f, 0.75f);

            if (_targetRootMotionSample.AccumulatedPositions != null)
            {
                for (int index = 0;
                     index + 1 <
                     _targetRootMotionSample.AccumulatedPositions.Count;
                     index++)
                {
                    Handles.DrawLine(
                        trajectory.InitialPose.Position +
                        _targetRootMotionSample.AccumulatedPositions[index],
                        trajectory.InitialPose.Position +
                        _targetRootMotionSample.AccumulatedPositions[index + 1]);
                }
            }

            Handles.color = Color.cyan;

            if (trajectory.Frames != null)
            {
                for (int index = 0;
                     index < trajectory.Frames.Count;
                     index++)
                {
                    Vector3 start = index == 0
                        ? trajectory.InitialPose.Position
                        : trajectory.Frames[index - 1].PoseAfter.Position;

                    Handles.DrawLine(
                        start,
                        trajectory.Frames[index].PoseAfter.Position);
                }
            }

            DrawPoseMarker(
                "Target Warp Final",
                trajectory.FinalPose.Position,
                trajectory.FinalPose.Yaw,
                Color.blue);

            int frameCount = trajectory.Frames == null
                ? 0
                : trajectory.Frames.Count;

            if (frameCount > 0)
            {
                int scrubIndex = Mathf.Clamp(
                    Mathf.RoundToInt(
                        Mathf.Clamp01(_targetPreviewNormalizedTime) *
                        (frameCount - 1)),
                    0,
                    frameCount - 1);

                ExecutionWarpTrajectoryFrame frame =
                    trajectory.Frames[scrubIndex];

                DrawPoseMarker(
                    "Target Scrub",
                    frame.PoseAfter.Position,
                    frame.PoseAfter.Yaw,
                    new Color(0.45f, 0.9f, 1f));

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
