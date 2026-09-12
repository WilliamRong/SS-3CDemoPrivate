#if UNITY_EDITOR

using Character.Config;
using Character.Combat;
using UnityEditor;
using UnityEngine;

namespace Character.Diagnostics
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ExecutionSpatialGizmo : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private CharacterCombatConfig _config;

        [Header("Preview")]
        [SerializeField] private Transform _target;
        [SerializeField] private bool _autoResolveRuntimeTarget = true;
        [SerializeField] private bool _drawWhenNotSelected;

        private Transform _runtimeTarget;

        private void OnDrawGizmosSelected()
        {
            DrawExecutionGizmo();
        }

        private void OnDrawGizmos()
        {
            if (_drawWhenNotSelected)
                DrawExecutionGizmo();
        }

        private void DrawExecutionGizmo()
        {
            if (_config == null)
                return;

            Vector3 origin = transform.position;
            Vector3 up = Vector3.up;

            Vector3 forward =
                Vector3.ProjectOnPlane(transform.forward, up);

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            float radius =
                Mathf.Max(0f, _config.executionMaxDistance);

            float halfAngle =
                Mathf.Clamp(
                    _config.executionFrontHalfAngle,
                    0f,
                    180f);

            Vector3 left =
                Quaternion.AngleAxis(-halfAngle, up) * forward;
            Vector3 right =
                Quaternion.AngleAxis(halfAngle, up) * forward;

            Handles.color = new Color(0.1f, 1f, 0.2f, 0.9f);

            Handles.DrawWireDisc(
                origin,
                up,
                radius);

            Handles.DrawWireArc(
                origin,
                up,
                left,
                halfAngle * 2f,
                radius);

            Handles.DrawLine(
                origin,
                origin + left * radius);

            Handles.DrawLine(
                origin,
                origin + right * radius);

            Handles.Label(
                origin + Vector3.up * 0.15f,
                $"Execution range: {radius:F2}m\n" +
                $"Front half angle: {halfAngle:F1} deg");

            Transform target = ResolvePreviewTarget();
            if (target == null)
                return;

            Vector3 anchor =
                target.position +
                target.rotation * _config.executorAnchorOffset;

            Handles.color =
                new Color(1f, 0.7f, 0.1f, 0.95f);

            Handles.DrawLine(
                _target.position,
                anchor);

            Handles.DrawWireDisc(
                anchor,
                up,
                Mathf.Max(
                    0f,
                    _config.executionMaxWarpTranslation));

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(anchor, 0.08f);

            float translationError =
                Vector3.Distance(transform.position, anchor);

            float yawError =
                Mathf.Abs(
                    Mathf.DeltaAngle(
                        transform.eulerAngles.y,
                        target.eulerAngles.y + 180f));

            Handles.Label(
                anchor + Vector3.up * 0.15f,
                $"Anchor error: {translationError:F3}m\n" +
                $"Yaw error: {yawError:F1} deg");
        }

        private Transform ResolvePreviewTarget()
        {
            if (_target != null &&
                (!Application.isPlaying ||
                 !EditorUtility.IsPersistent(_target)))
            {
                return _target;
            }

            if (!Application.isPlaying ||
                !_autoResolveRuntimeTarget)
            {
                return _target;
            }

            if (_runtimeTarget != null &&
                _runtimeTarget != transform &&
                _runtimeTarget.gameObject.activeInHierarchy)
            {
                return _runtimeTarget;
            }

            CombatActor[] actors =
                FindObjectsByType<CombatActor>(
                    FindObjectsSortMode.None);

            for (int i = 0; i < actors.Length; i++)
            {
                CombatActor actor = actors[i];
                if (actor == null || actor.transform == transform)
                    continue;

                _runtimeTarget = actor.transform;
                return _runtimeTarget;
            }

            return null;
        }
    }
}

#endif
