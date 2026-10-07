using UnityEngine;

namespace Character.EditorTools.Execution
{
    [CreateAssetMenu(
        fileName = "MotionWarpingPreviewLayout",
        menuName = "SS3C/Motion Warping Preview Layout")]
    public sealed class MotionWarpingPreviewLayout : ScriptableObject
    {
        public Vector3 executorPosition =
            new Vector3(0f, 0f, 0.6f);

        public Vector3 executorEulerAngles =
            new Vector3(0f, 180f, 0f);

        public Vector3 targetPosition = Vector3.zero;

        public Vector3 targetEulerAngles = Vector3.zero;
    }
}
