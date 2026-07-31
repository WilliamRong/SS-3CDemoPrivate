#if UNITY_EDITOR
using AI;
using UnityEditor;
using UnityEngine;

namespace SS3C.Editor
{
    /// <summary>
    /// 把服务器限定和快捷键约束直接展示在 Inspector，避免调试按钮被误认为客户端指令。
    /// </summary>
    [CustomEditor(typeof(NpcCombatDebug))]
    public sealed class NpcCombatDebugEditor : UnityEditor.Editor
    {
        /// <summary>
        /// 按服务器调试入口的分组顺序排列按钮，使 Inspector 操作与运行时小键盘触发保持一致。
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var debug = (NpcCombatDebug)target;
            if (debug == null)
                return;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Server Combat Triggers", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "仅在 Host/Server 上生效。小键盘 1-8 对应下列动作；Client 端观察远端 NPC 表现。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Attack")) debug.TriggerAttack();
                if (GUILayout.Button("Dodge F")) debug.TriggerDodgeForward();
                if (GUILayout.Button("Dodge B")) debug.TriggerDodgeBackward();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Guard")) debug.TriggerGuard();
                if (GUILayout.Button("Hit Light")) debug.TriggerHitLight();
                if (GUILayout.Button("Hit Heavy")) debug.TriggerHitHeavy();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sprint")) debug.TriggerSprint();
                if (GUILayout.Button("Dead")) debug.TriggerDead();
                if (GUILayout.Button("Revive")) debug.TriggerRevive();
            }
        }
    }
}
#endif
