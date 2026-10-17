using System.Collections.Generic;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.Scoring
{
    /// <summary>
    /// Suma puntos por cada muerte y derribo, lleva el combo y, al completar el nivel,
    /// calcula los bonus, la nota y guarda el récord. Al rebobinar se pierden los puntos posteriores.
    /// </summary>
    public class ScoreKeeper : MonoBehaviour, IRewindable
    {
        [SerializeField] private LevelManager level;
        [SerializeField] private TimeRewinder rewinder;
        [Tooltip("Segundos que se ve el \"+500\" sobre un enemigo.")]
        [SerializeField] private float popupDuration = 1.1f;

        /// <summary>Puntos ganados en un sitio, para el texto flotante del HUD.</summary>
        public sealed class Popup
        {
            public Vector3 Position;
            public ScoreGain Gain;
            public KillCause? Cause;
            public float Age;
        }

        private readonly ScoreState state = new ScoreState();
        private readonly List<Popup> popups = new List<Popup>();
        private float elapsed;

        public ScoreState State => state;
        public LevelManager Level => level;
        public TimeRewinder Rewinder => rewinder;
        public IReadOnlyList<Popup> Popups => popups;
        public float PopupDuration => popupDuration;
        public float Elapsed => elapsed;

        /// <summary>Resumen final; null hasta completar el nivel.</summary>
        public ScoreSummary Summary { get; private set; }
        public int BestTotal { get; private set; }
        public bool IsNewRecord { get; private set; }

        private string RecordKey => $"Overdrive.Record.{SceneManager.GetActiveScene().name}";

        private void Awake() => BestTotal = PlayerPrefs.GetInt(RecordKey, 0);

        private void OnEnable()
        {
            Enemy.AnyKilled += OnKilled;
            Enemy.AnyKnockedDown += OnKnockedDown;
        }

        private void OnDisable()
        {
            Enemy.AnyKilled -= OnKilled;
            Enemy.AnyKnockedDown -= OnKnockedDown;
        }

        private void OnKilled(Enemy enemy, KillCause cause) => AddKill(cause, enemy.transform.position, IsStealthKill(enemy));

        private PlayerController playerController;

        /// <summary>
        /// ¿Muerte en sigilo? El jugador va agachado (Ctrl) y el enemigo no lo había detectado
        /// (no tenía "!"). La IA conserva su estado al morir, así que se puede mirar ahora.
        /// </summary>
        public bool IsStealthKill(Enemy enemy)
        {
            if (playerController == null && level != null && level.Player != null)
                playerController = level.Player.GetComponent<PlayerController>();
            if (playerController == null || !playerController.IsSneaking) return false;
            var ai = enemy.GetComponent<EnemyAI>();
            return ai != null && !ai.Brain.IsInCombat;
        }
        private void OnKnockedDown(Enemy enemy, KnockdownCause cause) => AddKnockdown(cause, enemy.transform.position);

        /// <summary>Suma una muerte. Público para simularlo en el editor.</summary>
        public ScoreGain AddKill(KillCause cause, Vector3 position, bool stealth = false)
        {
            if (Summary != null) return default;
            ScoreGain gain = state.RegisterKill(cause, stealth);
            popups.Add(new Popup { Position = position, Gain = gain, Cause = cause });
            return gain;
        }

        public ScoreGain AddKnockdown(KnockdownCause cause, Vector3 position)
        {
            if (Summary != null) return default;
            ScoreGain gain = state.RegisterKnockdown(cause);
            popups.Add(new Popup { Position = position, Gain = gain });
            return gain;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                popups[i].Age += dt;
                if (popups[i].Age >= popupDuration) popups.RemoveAt(i);
            }
            if (Summary != null || level == null) return;

            LevelState levelState = level.Progress.State;
            if (levelState == LevelState.Complete)
            {
                Complete();
                return;
            }
            // Muerto, el reloj y el combo se paran mientras decides si rebobinar.
            if (levelState == LevelState.Failed) return;
            elapsed += dt;
            state.Tick(dt);
        }

        /// <summary>Cierra la puntuación del nivel y guarda el récord. Público para simularlo en el editor.</summary>
        public ScoreSummary Complete()
        {
            if (Summary != null) return Summary;
            int rewindsUsed = rewinder != null ? rewinder.MaxUses - rewinder.UsesLeft : 0;
            int enemies = level != null ? level.Progress.Total : 0;
            Summary = new ScoreSummary(state, elapsed, rewindsUsed, enemies);

            IsNewRecord = Summary.Total > BestTotal;
            if (IsNewRecord && Application.isPlaying)
            {
                BestTotal = Summary.Total;
                PlayerPrefs.SetInt(RecordKey, BestTotal);
                PlayerPrefs.Save();
            }
            return Summary;
        }

        // ---------- Rebobinar ----------

        public object Capture() => state.Capture();

        /// <summary>El reloj no se rebobina: el tiempo perdido cuenta.</summary>
        public void Restore(object snapshot)
        {
            state.Restore((ScoreState.Snapshot)snapshot);
            popups.Clear();
        }
    }
}
