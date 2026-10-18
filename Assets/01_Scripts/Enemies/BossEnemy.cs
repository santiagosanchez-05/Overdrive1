using Overdrive.CameraRig;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Level;
using Overdrive.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overdrive.Enemies
{
    /// <summary>
    /// El Jefe: un <see cref="Enemy"/> que aguanta varios golpes letales y no se deja derribar.
    /// Cada golpe lo frena un instante; a mitad de vida entra en furia (más rápido) y llama a sus refuerzos.
    /// Muestra su barra de vida cuando el jugador se acerca. Su vida también se rebobina.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class BossEnemy : MonoBehaviour, IHitArmor, IRewindable
    {
        /// <summary>Disparador del baile de victoria en su Animator (Boss.controller).</summary>
        public const string DanceParam = "Dance";

        [SerializeField] private string displayName = "EL JEFE";
        [SerializeField] private int maxHealth = 8;
        [Tooltip("Los disparos de escopeta (varios perdigones a la vez) no deben vaciar la barra de golpe.")]
        [SerializeField] private float hitCooldown = 0.25f;
        [SerializeField] private float staggerTime = 0.35f;
        [SerializeField] private float rageSpeedMultiplier = 1.35f;
        [SerializeField] private ReinforcementWave rageWave;
        [SerializeField] private PlayerLife player;
        [SerializeField] private float showBarDistance = 18f;

        private Enemy enemy;
        private EnemyAI ai;
        private int health;
        private float lastHit = -10f;
        private float staggerUntil;
        private float baseChaseSpeed;
        private bool raging;
        private bool danced;
        private EnemyVisual visual;

        private CanvasGroup barGroup;
        private Image barFill;

        public int Health => health;
        public int MaxHealth => maxHealth;
        public bool Raging => raging;

        private void Awake()
        {
            enemy = GetComponent<Enemy>();
            ai = GetComponent<EnemyAI>();
            visual = GetComponent<EnemyVisual>();
            health = maxHealth;
            if (ai != null) baseChaseSpeed = ai.ChaseSpeed;
        }

        private void Start() => BuildBar();

        private void Update()
        {
            if (ai != null && ai.Agent != null && ai.Agent.isOnNavMesh)
                ai.Agent.isStopped = Time.time < staggerUntil && !enemy.IsDead;
            UpdateBar();
            Celebrate();
        }

        /// <summary>Si el jugador muere, el Jefe baila.</summary>
        private void Celebrate()
        {
            bool playerDead = player != null && player.IsDead;
            if (playerDead && !danced && !enemy.IsDead && visual != null && visual.Animator != null)
            {
                danced = true;
                visual.Animator.SetTrigger(DanceParam);
            }
        }

        // ---- Armadura ----

        public bool AbsorbLethal(KillCause cause)
        {
            if (Time.time - lastHit < hitCooldown) return true; // mismo disparo/golpe: no cuenta dos veces
            lastHit = Time.time;
            health = Mathf.Max(0, health - 1);
            if (health <= 0) return false; // el último golpe lo mata de verdad

            staggerUntil = Time.time + staggerTime;
            BloodSpawner.SpawnAt(transform.position, 0.6f);
            CameraShake.Shake(0.2f);
            GameSounds.Play(Sound.BatHit);
            if (player != null && ai != null) ai.Hunt(player);
            if (!raging && health <= maxHealth / 2) EnterRage();
            return true;
        }

        public bool ResistKnockdown(KnockdownCause cause)
        {
            // Un puñetazo o un arma lanzada solo lo frenan: al jefe hay que matarlo con armas.
            staggerUntil = Time.time + staggerTime;
            CameraShake.Shake(0.1f);
            if (player != null && ai != null) ai.Hunt(player);
            return true;
        }

        private void EnterRage()
        {
            raging = true;
            if (ai != null) ai.ChaseSpeed = baseChaseSpeed * rageSpeedMultiplier;
            CameraShake.Shake(0.45f);
            GameSounds.Play(Sound.EnemyFall);
            if (rageWave != null && player != null) rageWave.Trigger(player);
        }

        // ---- Rebobinado ----

        public object Capture() => health;

        public void Restore(object snapshot)
        {
            health = (int)snapshot;
            raging = health <= maxHealth / 2;
            if (ai != null) ai.ChaseSpeed = raging ? baseChaseSpeed * rageSpeedMultiplier : baseChaseSpeed;
            staggerUntil = 0f;
            if (danced && visual != null && visual.Animator != null)
            {
                visual.Animator.ResetTrigger(DanceParam);
                visual.Animator.Play(EnemyVisual.LocomotionState, 0, 0f);
            }
            danced = false;
        }

        // ---- Barra de vida ----

        private void BuildBar()
        {
            var canvasGo = new GameObject("BossBar", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            barGroup = canvasGo.GetComponent<CanvasGroup>();
            barGroup.alpha = 0f;
            barGroup.blocksRaycasts = false;

            RectTransform back = NewRect("Back", canvasGo.transform, new Color(0f, 0f, 0f, 0.65f));
            // Abajo en el centro: arriba están la puntuación, el combo y el objetivo, y la barra se les encimaba.
            back.anchorMin = back.anchorMax = new Vector2(0.5f, 0f);
            back.pivot = new Vector2(0.5f, 0f);
            back.sizeDelta = new Vector2(760f, 26f);
            back.anchoredPosition = new Vector2(0f, 64f);

            RectTransform fill = NewRect("Fill", back, new Color(1f, 0.12f, 0.35f));
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            barFill = fill.GetComponent<Image>();
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

            var label = new GameObject("Name", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(back, false);
            var lr = label.rectTransform;
            lr.anchorMin = new Vector2(0f, 1f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.pivot = new Vector2(0.5f, 0f);
            lr.sizeDelta = new Vector2(0f, 40f);
            lr.anchoredPosition = new Vector2(0f, 4f);
            label.text = displayName;
            label.fontSize = 30f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
        }

        private static RectTransform NewRect(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        private void UpdateBar()
        {
            if (barGroup == null) return;
            bool near = player != null && Vector3.Distance(player.transform.position, transform.position) < showBarDistance;
            bool show = !enemy.IsDead && (near || health < maxHealth);
            barGroup.alpha = Mathf.MoveTowards(barGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 3f);
            barFill.fillAmount = Mathf.MoveTowards(barFill.fillAmount, (float)health / maxHealth, Time.unscaledDeltaTime * 2f);
            barFill.color = raging ? new Color(1f, 0.45f, 0.1f) : new Color(1f, 0.12f, 0.35f);
        }

        private void OnDestroy()
        {
            if (barGroup != null) Destroy(barGroup.gameObject);
        }
    }
}
