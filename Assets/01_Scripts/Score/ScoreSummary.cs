using System.Collections.Generic;
using UnityEngine;

namespace Overdrive.Scoring
{
    /// <summary>
    /// Resumen al completar el nivel: puntos de combate, bonus, total y nota.
    /// Lógica pura para probarla en el editor.
    /// </summary>
    public sealed class ScoreSummary
    {
        public readonly struct Line
        {
            public readonly string Label;
            public readonly int Points;

            public Line(string label, int points)
            {
                Label = label;
                Points = points;
            }
        }

        public int CombatPoints { get; }
        public IReadOnlyList<Line> Bonuses { get; }
        public int Total { get; }
        public string Grade { get; }
        public float Seconds { get; }

        public const string LabelTime = "Tiempo";
        public const string LabelVariety = "Variedad";
        public const string LabelCombo = "Mejor combo";
        public const string LabelNoRewind = "Sin rebobinar";

        public ScoreSummary(ScoreState score, float seconds, int rewindsUsed, int enemyCount)
        {
            CombatPoints = score.Points;
            Seconds = seconds;

            var bonuses = new List<Line>();
            float par = enemyCount * ScoreRules.ParSecondsPerEnemy;
            int timeBonus = Mathf.Max(0, Mathf.FloorToInt(par - seconds)) * ScoreRules.PointsPerSecondUnderPar;
            if (timeBonus > 0) bonuses.Add(new Line(LabelTime, timeBonus));

            int variety = score.DistinctCauses * ScoreRules.VarietyBonusPerCause;
            if (variety > 0) bonuses.Add(new Line(LabelVariety, variety));

            int combo = Mathf.Max(0, score.BestCombo - 1) * ScoreRules.BestComboBonusPerKill;
            if (combo > 0) bonuses.Add(new Line($"{LabelCombo} x{score.BestCombo}", combo));

            if (rewindsUsed == 0) bonuses.Add(new Line(LabelNoRewind, ScoreRules.NoRewindBonus));
            Bonuses = bonuses;

            int total = CombatPoints;
            foreach (Line line in bonuses) total += line.Points;
            Total = total;
            Grade = ScoreRules.GradeFor(total, enemyCount);
        }
    }
}
