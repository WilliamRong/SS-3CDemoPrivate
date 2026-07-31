using UnityEngine;

namespace Character.Config
{
    /// <summary>
    /// 让移动状态与 Motor 共用同一套速度和垂直运动参数，避免表现调参改变玩法结果。
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterLocomotionConfig", menuName = "SS3C/Character/Locomotion Config")]
    public sealed class CharacterLocomotionConfig : ScriptableObject
    {
        [Header("Horizontal")]
        public float moveSpeed = 5f;
        public float sprintSpeed = 10f;
        public float rotationSlerpSpeed = 30f;
        public float smoothTime = 0.1f;

        [Header("Vertical")]
        public float gravity = -9.81f;
        public float jumpHeight = 1f;
    }
}
