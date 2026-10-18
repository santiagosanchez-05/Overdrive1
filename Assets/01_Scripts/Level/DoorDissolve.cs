using UnityEngine;
using UnityEngine.AI;

namespace Overdrive.Level
{
    /// <summary>
    /// Puerta de los refuerzos que se abre disolviéndose (shader Overdrive/NoteDissolve) en vez de desaparecer de golpe.
    /// El paso se libera al instante; lo visual tarda <see cref="Duration"/> segundos y luego la puerta se desactiva.
    /// </summary>
    public class DoorDissolve : MonoBehaviour
    {
        private static readonly int DissolveAmount = Shader.PropertyToID("_DissolveAmount");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>
        /// Valor de disolución con la puerta cerrada. Negativo para que el borde brillante no salpique
        /// la puerta entera de puntos (el borde aparece donde el ruido queda cerca del nivel de disolución).
        /// </summary>
        public const float ClosedAmount = -0.15f;

        [SerializeField] private Renderer doorRenderer;
        [SerializeField] private Collider doorCollider;
        [SerializeField] private NavMeshObstacle obstacle;
        [SerializeField] private float duration = 0.7f;
        [Tooltip("Color de la puerta cerrada. Con alfa 0 se usa el del material (naranja brillante). El brillo naranja del borde solo se ve al disolverse.")]
        [SerializeField] private Color restTint = new Color(0f, 0f, 0f, 0f);

        private MaterialPropertyBlock block;
        private float elapsed;

        public Renderer DoorRenderer => doorRenderer;
        public Collider DoorCollider => doorCollider;
        public NavMeshObstacle Obstacle => obstacle;
        public float Duration => duration;
        public bool IsDissolving { get; private set; }

        private void OnEnable()
        {
            // Cerrada, se pinta con su color de reposo ya desde el primer fotograma (si no, saldría el naranja del material).
            if (!IsDissolving && restTint.a > 0f) SetAmount(ClosedAmount);
        }

        private void Update()
        {
            if (IsDissolving) Tick(Time.deltaTime);
        }

        /// <summary>Abre la puerta: deja pasar ya y empieza a disolverse.</summary>
        public void Play()
        {
            gameObject.SetActive(true);
            SetBlocking(false);
            elapsed = 0f;
            IsDissolving = true;
            SetAmount(ClosedAmount);
        }

        /// <summary>Avanza la disolución. Público para simularlo en el editor.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsDissolving) return;
            elapsed += deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            SetAmount(Mathf.Lerp(ClosedAmount, 1f, t));
            if (t < 1f) return;
            IsDissolving = false;
            gameObject.SetActive(false);
        }

        /// <summary>Puerta cerrada, entera y sólida (estado inicial y al rebobinar).</summary>
        public void ResetClosed()
        {
            IsDissolving = false;
            elapsed = 0f;
            gameObject.SetActive(true);
            SetBlocking(true);
            SetAmount(ClosedAmount);
        }

        /// <summary>Puerta ya abierta (al rebobinar a un momento en que los refuerzos ya habían entrado).</summary>
        public void SetOpenImmediate()
        {
            IsDissolving = false;
            SetBlocking(false);
            gameObject.SetActive(false);
        }

        private void SetBlocking(bool blocking)
        {
            if (doorCollider != null) doorCollider.enabled = blocking;
            if (obstacle != null) obstacle.enabled = blocking;
        }

        private void SetAmount(float amount)
        {
            if (doorRenderer == null) return;
            block ??= new MaterialPropertyBlock();
            doorRenderer.GetPropertyBlock(block);
            block.SetFloat(DissolveAmount, amount);
            if (restTint.a > 0f) block.SetColor(ColorId, restTint);
            doorRenderer.SetPropertyBlock(block);
        }

        public float CurrentAmount
        {
            get
            {
                if (doorRenderer == null) return 0f;
                block ??= new MaterialPropertyBlock();
                doorRenderer.GetPropertyBlock(block);
                return block.GetFloat(DissolveAmount);
            }
        }
    }
}
