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
    /// <summary>Decisiones del Jefe (lógica pura, comprobable en el editor).</summary>
    public static class BossLogic
    {
        /// <summary>¿Prepara una embestida? Solo si ya pasó su espera, no está aturdido y el jugador está a media distancia.</summary>
        public static bool ShouldCharge(float cooldownLeft, bool staggered, float distance, float minDistance, float maxDistance) =>
            cooldownLeft <= 0f && !staggered && distance >= minDistance && distance <= maxDistance;

        /// <summary>Fase según la vida: 0 normal, 1 furia (mitad), 2 desesperado (un cuarto).</summary>
        public static int Phase(int health, int maxHealth) =>
            health <= maxHealth / 4 ? 2 : health <= maxHealth / 2 ? 1 : 0;

        /// <summary>Espera entre embestidas: más corta cuanto más herido está.</summary>
        public static float ChargeCooldown(int phase) => phase == 2 ? 3.5f : phase == 1 ? 5f : 7f;
    }

    /// <summary>
    /// El Jefe: un <see cref="Enemy"/> que aguanta varios golpes letales y no se deja derribar.
    /// Cada golpe lo frena un instante; a mitad de vida entra en furia (más rápido, luz roja) y llama a sus refuerzos,
    /// y con un cuarto de vida llama a los últimos. Cada pocos segundos prepara una embestida: se para, se ilumina
    /// en rojo (aviso) y carga a toda velocidad; si alcanza al jugador le quita mucha vida. Las coberturas lo paran.
    /// Muestra su barra de vida cuando el jugador se acerca.
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

        [Header("Embestida")]
        [SerializeField] private float chargeMinDistance = 2.5f;
        [SerializeField] private float chargeMaxDistance = 11f;
        [SerializeField] private float telegraphTime = 0.7f;
        [SerializeField] private float chargeTime = 0.9f;
        [SerializeField] private float chargeSpeedMultiplier = 3f;
        [SerializeField] private float chargeDamage = 40f;
        [SerializeField] private float chargeHitDistance = 1.1f;

        [Header("Fases")]
        [SerializeField] private ReinforcementWave finalWave;
        [SerializeField] private Light rageLight;

        private Enemy enemy;
        private EnemyAI ai;
        private int health;
        private float lastHit = -10f;
        private float staggerUntil;
        private float baseChaseSpeed;
        private bool raging;
        private bool desperate;
        private bool danced;
        private float chargeCooldown = 4f;
        private float telegraphLeft;
        private float chargeLeft;
        private bool chargeHit;
        private EnemyVisual visual;

        private CanvasGroup barGroup;
        private Image barFill;

        public int Health => health;
        public int MaxHealth => maxHealth;
        public bool Raging => raging;
        public bool Desperate => desperate;
        public bool Telegraphing => telegraphLeft > 0f;
        public bool Charging => chargeLeft > 0f;
        public int Charges { get; private set; }
        public float ChargeDamage => chargeDamage;

        private bool initialized;

        /// <summary>Para las pruebas en el editor: adelanta el reloj del Jefe (en el editor Time.time no avanza).</summary>
        public float ClockOffset { get; set; }
        private float Now => Time.time + ClockOffset;

        private void Awake() => Init();

        /// <summary>Prepara referencias y vida (en Awake, o al usarlo desde el editor, donde Awake no se ejecuta).</summary>
        public void Init()
        {
            if (initialized) return;
            initialized = true;
            enemy = GetComponent<Enemy>();
            ai = GetComponent<EnemyAI>();
            visual = GetComponent<EnemyVisual>();
            health = maxHealth;
            if (ai != null) baseChaseSpeed = ai.ChaseSpeed;
        }

        private void Start() => BuildBar();

        private void Update()
        {
            TickCharge(Time.deltaTime);
            if (ai != null && ai.Agent != null && ai.Agent.isOnNavMesh)
                ai.Agent.isStopped = (Time.time < staggerUntil || telegraphLeft > 0f) && !enemy.IsDead;
            if (rageLight != null)
            {
                rageLight.enabled = !enemy.IsDead && (raging || telegraphLeft > 0f);
                rageLight.intensity = telegraphLeft > 0f ? 6f + Mathf.Sin(Time.time * 40f) * 3f : 2.5f;
            }
            UpdateBar();
            Celebrate();
        }

        /// <summary>Embestida: aviso (quieto y en rojo) y carga a toda velocidad. Público para simularla en el editor.</summary>
        public void TickCharge(float dt)
        {
            Init();
            if (enemy == null || enemy.IsDead || player == null || player.IsDead || ai == null) { EndCharge(); return; }
            float distance = Vector3.Distance(player.transform.position, transform.position);
            if (telegraphLeft > 0f)
            {
                telegraphLeft -= dt;
                Vector3 to = player.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
                if (telegraphLeft <= 0f)
                {
                    chargeLeft = chargeTime;
                    chargeHit = false;
                    ai.ChaseSpeed = CurrentBaseSpeed() * chargeSpeedMultiplier;
                    ai.Hunt(player);
                    Charges++;
                    GameSounds.Play(Sound.Swing);
                }
                return;
            }
            if (chargeLeft > 0f)
            {
                chargeLeft -= dt;
                if (!chargeHit && distance < chargeHitDistance * Mathf.Max(transform.lossyScale.x, 0.1f))
                {
                    chargeHit = true;
                    player.TakeHit(chargeDamage, false, false);
                    CameraShake.Shake(0.5f);
                    GameSounds.Play(Sound.Punch);
                    chargeLeft = Mathf.Min(chargeLeft, 0.1f);
                }
                if (chargeLeft <= 0f) EndCharge();
                return;
            }
            chargeCooldown -= dt;
            bool staggered = Now < staggerUntil;
            if (BossLogic.ShouldCharge(chargeCooldown, staggered, distance, chargeMinDistance, chargeMaxDistance) && ai.HuntTarget != null)
                StartTelegraph();
        }

        /// <summary>Para las pruebas: preparar la embestida ya.</summary>
        public void StartTelegraph()
        {
            Init();
            telegraphLeft = telegraphTime;
            chargeCooldown = BossLogic.ChargeCooldown(BossLogic.Phase(health, maxHealth));
            GameSounds.Play(Sound.EnemyFall);
        }

        private void EndCharge()
        {
            if (chargeLeft <= 0f && telegraphLeft <= 0f) return;
            chargeLeft = 0f;
            telegraphLeft = 0f;
            if (ai != null) ai.ChaseSpeed = CurrentBaseSpeed();
        }

        private float CurrentBaseSpeed() => raging ? baseChaseSpeed * rageSpeedMultiplier : baseChaseSpeed;

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
            Init();
            if (Now - lastHit < hitCooldown) return true; // mismo disparo/golpe: no cuenta dos veces
            lastHit = Now;
            health = Mathf.Max(0, health - 1);
            if (health <= 0) return false; // el último golpe lo mata de verdad

            staggerUntil = Now + staggerTime;
            BloodSpawner.SpawnAt(transform.position, 0.6f);
            CameraShake.Shake(0.2f);
            GameSounds.Play(Sound.BatHit);
            if (player != null && ai != null) ai.Hunt(player);
            if (!raging && health <= maxHealth / 2) EnterRage();
            if (!desperate && BossLogic.Phase(health, maxHealth) == 2) EnterDesperation();
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

        private void EnterDesperation()
        {
            desperate = true;
            CameraShake.Shake(0.5f);
            if (finalWave != null && player != null) finalWave.Trigger(player);
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
