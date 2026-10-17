using Overdrive.Combat;
using UnityEngine;

namespace Overdrive.Scoring
{
    /// <summary>Tabla de puntos, combo, bonus y notas. Si hay que equilibrar la puntuación, se toca aquí.</summary>
    public static class ScoreRules
    {
        /// <summary>Segundos que tienes tras una muerte para que la siguiente sume al combo.</summary>
        public const float ComboWindow = 4f;
        public const int MaxMultiplier = 8;

        public static int MultiplierFor(int combo) => Mathf.Clamp(combo, 1, MaxMultiplier);

        public static int KillPoints(KillCause cause)
        {
            switch (cause)
            {
                case KillCause.Melee: return 400;
                case KillCause.Shot: return 300;
                case KillCause.Finisher: return 600;
                case KillCause.Backstab: return 800;
                default: return 300;
            }
        }

        public static int KnockdownPoints(KnockdownCause cause)
        {
            switch (cause)
            {
                case KnockdownCause.Thrown: return 150;
                case KnockdownCause.Stomp: return 300;
                default: return 100;
            }
        }

        // ---------- Bonus al terminar el nivel ----------

        /// <summary>Tiempo de referencia: segundos por enemigo del nivel.</summary>
        public const float ParSecondsPerEnemy = 10f;
        /// <summary>Puntos por cada segundo que te sobra del tiempo de referencia.</summary>
        public const int PointsPerSecondUnderPar = 25;
        /// <summary>Por cada forma distinta de matar usada (arma, disparo, remate, por la espalda).</summary>
        public const int VarietyBonusPerCause = 500;
        public const int NoRewindBonus = 2000;
        /// <summary>Por cada muerte del mejor combo a partir de la segunda.</summary>
        public const int BestComboBonusPerKill = 250;

        // ---------- Notas ----------

        /// <summary>Puntos de referencia por enemigo: la nota compara el total con enemigos × esto.</summary>
        public const int GradeReferencePerEnemy = 1000;

        /// <summary>Proporción mínima de la referencia para cada nota, de mejor a peor.</summary>
        public static readonly (string grade, float ratio)[] Grades =
        {
            ("S", 2.2f),
            ("A", 1.6f),
            ("B", 1.1f),
            ("C", 0.7f),
            ("D", 0f),
        };

        public static string GradeFor(int total, int enemyCount)
        {
            float reference = Mathf.Max(1, enemyCount) * GradeReferencePerEnemy;
            float ratio = total / reference;
            foreach (var (grade, min) in Grades)
                if (ratio >= min) return grade;
            return Grades[Grades.Length - 1].grade;
        }
    }
}
