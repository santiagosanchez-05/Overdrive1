using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Alien de ambiente en la pista: baila uno de los bailes de su Animator (estados Dance0..DanceN) elegido al azar,
    /// y cada cierto tiempo cambia a otro. Cada alien empieza en un momento distinto para que no vayan sincronizados.
    /// </summary>
    public class AlienDancer : MonoBehaviour
    {
        public const string StatePrefix = "Dance";

        [SerializeField] private Animator animator;
        [SerializeField] private int danceCount = 5;
        [SerializeField] private Vector2 changeEvery = new Vector2(6f, 14f);
        [SerializeField] private float crossFade = 0.35f;

        private int current = -1;
        private float timer;

        public void Setup(Animator target, int count)
        {
            animator = target;
            danceCount = count;
        }

        private void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null) { enabled = false; return; }
            animator.speed = Random.Range(0.9f, 1.1f);
            Next(Random.value);
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f) Next(0f);
        }

        private void Next(float startAt)
        {
            if (animator == null || danceCount <= 0) return;
            int pick = Random.Range(0, danceCount);
            if (danceCount > 1 && pick == current) pick = (pick + 1) % danceCount;
            current = pick;
            if (startAt > 0f) animator.Play(StatePrefix + pick, 0, startAt);
            else animator.CrossFadeInFixedTime(StatePrefix + pick, crossFade);
            timer = Random.Range(changeEvery.x, changeEvery.y);
        }
    }
}
