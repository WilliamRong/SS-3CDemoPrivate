using Cinemachine;
using UnityEngine;

namespace Core
{
    [CreateAssetMenu(fileName = "PlayerCameraRigConfig", menuName = "SS3C/Camera/Player Camera Rig Config")]
    public sealed class PlayerCameraRigConfig : ScriptableObject
    {
        [Header("Scene Paths")]
        public string freeLookPath = "Cameras/TP";
        public string lockOnPath = "Cameras/LockOn";

        [Header("Priority")]
        public int freeLookPriority = 10;
        public int lockOnActivePriority = 20;
        public int lockOnInactivePriority = 0;

        [Header("Lock-On Body (Transposer)")]
        public Vector3 bodyFollowOffset = new Vector3(0f, 3f, -8f);
        public float bodyDamping = 1f;
        public float pivotYawDamping = 0.35f;

        [Header("Lock-On Aim (GroupComposer)")]
        public float playerFramingWeight = 0.8f;
        public float targetFramingWeight = 1.4f;
        public float playerFramingRadius = 1f;
        public float targetFramingRadius = 0.85f;
        public float groupFramingSize = 0.62f;
        public float lockScreenX = 0.5f;
        public float lockScreenY = 0.52f;
        public float aimHorizontalDamping = 0.6f;
        public float aimVerticalDamping = 0.6f;
        public float deadZoneWidth = 0.1f;
        public float deadZoneHeight = 0.1f;
        public float softZoneWidth = 0.8f;
        public float softZoneHeight = 0.8f;
        public bool centerOnActivate;
        public float lockMinDistance = 4f;
        public float lockMaxDistance = 16f;
        public float maxDollyIn = 3f;
        public float maxDollyOut = 10f;
        public float frameDamping = 1.5f;

        public void ApplyToTransposer(CinemachineTransposer transposer)
        {
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
            transposer.m_FollowOffset = bodyFollowOffset;
            transposer.m_XDamping = bodyDamping;
            transposer.m_YDamping = bodyDamping;
            transposer.m_ZDamping = bodyDamping;
            transposer.m_AngularDampingMode = CinemachineTransposer.AngularDampingMode.Euler;
            transposer.m_PitchDamping = 0f;
            transposer.m_YawDamping = 0f;
            transposer.m_RollDamping = 0f;
        }

        public void ApplyToGroupComposer(CinemachineGroupComposer composer)
        {
            composer.m_TrackedObjectOffset = Vector3.zero;
            composer.m_LookaheadTime = 0f;
            composer.m_HorizontalDamping = aimHorizontalDamping;
            composer.m_VerticalDamping = aimVerticalDamping;
            composer.m_ScreenX = lockScreenX;
            composer.m_ScreenY = lockScreenY;
            composer.m_DeadZoneWidth = deadZoneWidth;
            composer.m_DeadZoneHeight = deadZoneHeight;
            composer.m_SoftZoneWidth = softZoneWidth;
            composer.m_SoftZoneHeight = softZoneHeight;
            composer.m_CenterOnActivate = centerOnActivate;
            composer.m_GroupFramingSize = groupFramingSize;
            composer.m_FramingMode = CinemachineGroupComposer.FramingMode.HorizontalAndVertical;
            composer.m_FrameDamping = frameDamping;
            composer.m_AdjustmentMode = CinemachineGroupComposer.AdjustmentMode.DollyOnly;
            composer.m_MaxDollyIn = maxDollyIn;
            composer.m_MaxDollyOut = maxDollyOut;
            composer.m_MinimumDistance = lockMinDistance;
            composer.m_MaximumDistance = lockMaxDistance;
        }
    }
}
