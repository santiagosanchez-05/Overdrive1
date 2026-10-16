using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>Cuenta los enemigos vivos, abre la salida y detecta la victoria o la derrota.</summary>
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private PlayerLife player;
        [SerializeField] private ExitZone exit;
        [SerializeField] private Enemy[] enemies = new Enemy[0];

        private readonly LevelProgress progress = new LevelProgress();

        public LevelProgress Progress => progress;
        public PlayerLife Player => player;
        public ExitZone Exit => exit;
        public Enemy[] Enemies => enemies;

        private void Start() => Evaluate();
        private void Update() => Evaluate();

        /// <summary>Recalcula el estado del nivel. Público para simularlo en el editor.</summary>
        public LevelState Evaluate()
        {
            int dead = 0;
            foreach (Enemy enemy in enemies)
                if (enemy == null || enemy.IsDead) dead++;

            bool playerDead = player == null || player.IsDead;
            bool inExit = player != null && exit != null && exit.Contains(player.transform.position);

            LevelState before = progress.State;
            LevelState state = progress.Evaluate(enemies.Length, dead, playerDead, inExit);
            if (exit != null) exit.SetOpen(state == LevelState.ExitOpen || state == LevelState.Complete);
            if (state == LevelState.Complete && before != LevelState.Complete) OnComplete();
            return state;
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
