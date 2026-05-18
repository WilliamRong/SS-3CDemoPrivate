using UnityEngine;

namespace Character.Config
{
    [CreateAssetMenu(fileName = "CharacterPresentationConfig", menuName = "SS3C/Character/Presentation Config")]
    public sealed class CharacterPresentationConfig : ScriptableObject
    {
        [Header("Locomotion Animator")]
        public float locomotionCrossFadeDuration = 0.15f;
        public float locomotionEnterCrossFadeDuration = 0.08f;
        [Tooltip("World speed mapped to full run on blend tree forward axis.")]
        public float locomotionBlendReferenceSpeed = 5f;
        public float velocityEpsilon = 0.05f;

        [Header("Blend Tree")]
        public float freeMoveBlendScale = 1f;
        public float blendAxisMax = 2f;
        public float runForwardBlendZ = 2f;

        [Header("Sprint Animator")]
        public float sprintCrossFadeDuration = 0.15f;
    }
}
