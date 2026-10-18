using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.Level
{
    /// <summary>
    /// Cuenta los enemigos vivos, vigila el maletín, lanza los refuerzos, abre la salida
    /// y detecta la victoria o la derrota. Permite avanzar a la siguiente escena al completar el nivel.
    /// </summary>
    public class LevelManager : MonoBehaviour, IRewindable
    {
        [SerializeField] private PlayerLife player;
        [SerializeField] private ExitZone exit;
        [SerializeField] private Enemy[] enemies = new Enemy[0];
        [SerializeField] private BriefcasePickup briefcase;
        [SerializeField] private ReinforcementWave reinforcements;
        [SerializeField] private string nextScene;
        [Tooltip("Segundos tras completar el nivel para pasar solo al siguiente. 0 = esperar a que el jugador pulse.")]
        [SerializeField] private float autoAdvanceDelay;

        private readonly LevelProgress progress = new LevelProgress();
        private float completedTime;

        public LevelProgress Progress => progress;
        public string NextScene => nextScene;
        public PlayerLife Player => player;
        public ExitZone Exit => exit;
        public Enemy[] Enemies => enemies;
        public BriefcasePickup Briefcase => briefcase;
        public ReinforcementWave Reinforcements => reinforcements;

        public object Capture() => progress.State;

        /// <summary>Rebobinar: vuelve al estado guardado (de "perdido" a "jugando").</summary>
        public void Restore(object snapshot)
        {
            completedTime = 0f;
            progress.Restore((LevelState)snapshot);
        }

        private void Start() => Evaluate();
        private void Update()
        {
            Evaluate();

            if (progress.State == LevelState.Complete)
            {
                completedTime += Time.unscaledDeltaTime;
                bool pressed = completedTime >= 0.5f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0));
                bool timedOut = autoAdvanceDelay > 0f && completedTime >= autoAdvanceDelay;
                if (pressed || timedOut)
                {
                    string target = string.IsNullOrEmpty(nextScene) ? "Menu" : nextScene;
                    Time.timeScale = 1f;
                    GameInput.Blocked = false;
                    SceneManager.LoadScene(target);
                }
            }
            else
            {
                completedTime = 0f;
            }
        }

        /// <summary>Recalcula el estado del nivel. Público para simularlo en el editor.</summary>
        public LevelState Evaluate()
        {
            bool hasBriefcase = briefcase == null || briefcase.IsCollected;
            if (hasBriefcase && reinforcements != null && !reinforcements.Triggered && player != null && !player.IsDead)
                reinforcements.Trigger(player);

            int total = enemies.Length;
            int dead = CountDead(enemies);
            // Los refuerzos solo cuentan cuando ya han entrado.
            if (reinforcements != null && reinforcements.Triggered)
            {
                total += reinforcements.Squad.Length;
                foreach (EnemyAI ai in reinforcements.Squad)
                    if (ai == null || ai.Enemy.IsDead) dead++;
            }

            bool playerDead = player == null || player.IsDead;
            bool inExit = player != null && exit != null && exit.Contains(player.transform.position);

            LevelState before = progress.State;
            LevelState state = progress.Evaluate(total, dead, playerDead, inExit, hasBriefcase);
            if (exit != null) exit.SetOpen(state == LevelState.ExitOpen || state == LevelState.Complete);
            if (state == LevelState.Complete && before != LevelState.Complete) OnComplete();
            return state;
        }

        private static int CountDead(Enemy[] list)
        {
            int dead = 0;
            foreach (Enemy enemy in list)
                if (enemy == null || enemy.IsDead) dead++;
            return dead;
        }

        private void OnComplete()
        {
            // Nivel completado: el jugador se queda quieto y deja de atacar.
            var controller = player.GetComponent<PlayerController>();
            if (controller != null) controller.MovementLocked = true;
            var weapons = player.GetComponent<PlayerWeapons>();
            if (weapons != null) weapons.enabled = false;
        }
    }
}
