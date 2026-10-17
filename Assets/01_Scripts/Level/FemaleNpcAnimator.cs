using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Personaje femenino de ambiente (no ataca): alterna entre estar quieta (Idle) y hablar (Talking).
    /// </summary>
    [DisallowMultipleComponent]
    public class FemaleNpcAnimator : MonoBehaviour
    {
        public const string TalkingParam = "Talking";
        public const string IdleState = "Idle";
        public const string TalkingState = "Talking";

        [SerializeField] private Animator animator;
        [SerializeField] private float switchInterval = 4f;

        private float timer;
        private bool talking;

        public Animator Animator => animator;

        private void OnEnable()
        {
            timer = switchInterval;
        }

        private void Update()
        {
            if (animator == null) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = switchInterval;
            talking = !talking;
            animator.SetBool(TalkingParam, talking);
        }
    }
}
