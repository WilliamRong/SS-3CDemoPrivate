using UnityEngine;

namespace Character.Config
{
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
