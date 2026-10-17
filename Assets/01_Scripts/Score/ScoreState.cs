using Overdrive.Combat;
using UnityEngine;

namespace Overdrive.Scoring
{
    /// <summary>Lo que se ha ganado con una muerte o un derribo, para mostrarlo en pantalla.</summary>
    public readonly struct ScoreGain
    {
        public readonly int Points;
        public readonly int Multiplier;

        public ScoreGain(int points, int multiplier)
        {
            Points = points;
            Multiplier = multiplier;
        }
    }

    /// <summary>
    /// Puntos y combo durante el nivel. Cada muerte dentro de <see cref="ScoreRules.ComboWindow"/> segundos
    /// de la anterior sube el combo, que multiplica los puntos. Los derribos dan pocos puntos y mantienen
    /// vivo el combo, pero no lo suben. Lógica pura para probarla en el editor.
    /// </summary>
    public sealed class ScoreState
    {
        public int Points { get; private set; }
        /// <summary>Muertes seguidas del combo actual (0 = sin combo).</summary>
        public int Combo { get; private set; }
        public float ComboTimeLeft { get; private set; }
        public int BestCombo { get; private set; }
        public int Kills { get; private set; }
        /// <summary>Formas de matar distintas usadas (un bit por <see cref="KillCause"/>).</summary>
        public int CausesMask { get; private set; }

        /// <summary>Multiplicador que tendría la próxima muerte si llega a tiempo.</summary>
        public int Multiplier => ScoreRules.MultiplierFor(Combo);

        public int DistinctCauses
        {
            get
            {
                int count = 0;
                for (int mask = CausesMask; mask != 0; mask &= mask - 1) count++;
                return count;
            }
        }

        public ScoreGain RegisterKill(KillCause cause)
        {
            Combo = ComboTimeLeft > 0f ? Combo + 1 : 1;
            ComboTimeLeft = ScoreRules.ComboWindow;
            BestCombo = Mathf.Max(BestCombo, Combo);
            Kills++;
            if (cause != KillCause.Other) CausesMask |= 1 << (int)cause;

            int multiplier = ScoreRules.MultiplierFor(Combo);
            int points = ScoreRules.KillPoints(cause) * multiplier;
            Points += points;
            return new ScoreGain(points, multiplier);
        }

        public ScoreGain RegisterKnockdown(KnockdownCause cause)
        {
            // Mantiene vivo el combo que hubiera, sin subirlo.
            if (ComboTimeLeft > 0f) ComboTimeLeft = ScoreRules.ComboWindow;
            int points = ScoreRules.KnockdownPoints(cause);
            Points += points;
            return new ScoreGain(points, 1);
        }

        /// <summary>Avanza el tiempo. Devuelve true si en este paso se acabó el combo.</summary>
        public bool Tick(float deltaTime)
        {
            if (ComboTimeLeft <= 0f) return false;
            ComboTimeLeft = Mathf.Max(0f, ComboTimeLeft - deltaTime);
            if (ComboTimeLeft > 0f) return false;
            Combo = 0;
            return true;
        }

        public readonly struct Snapshot
        {
            public readonly int Points, Combo, BestCombo, Kills, CausesMask;
            public readonly float ComboTimeLeft;

            public Snapshot(ScoreState s)
            {
                Points = s.Points; Combo = s.Combo; BestCombo = s.BestCombo; Kills = s.Kills;
                CausesMask = s.CausesMask; ComboTimeLeft = s.ComboTimeLeft;
            }
        }

        public Snapshot Capture() => new Snapshot(this);

        /// <summary>Rebobinar: los puntos ganados después de la instantánea se pierden.</summary>
        public void Restore(Snapshot s)
        {
            Points = s.Points;
            Combo = s.Combo;
            BestCombo = s.BestCombo;
            Kills = s.Kills;
            CausesMask = s.CausesMask;
            ComboTimeLeft = s.ComboTimeLeft;
        }
    }
}
