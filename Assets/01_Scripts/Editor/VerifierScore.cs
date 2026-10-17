using System.Linq;
using Overdrive.Combat;
using Overdrive.Level;
using Overdrive.Scoring;
using Overdrive.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación de la puntuación: puntos por tipo de muerte, combo, bonus, notas y HUD.</summary>
    internal static partial class Verifier
    {
        private static void VerifyScoreLogic(Checker c)
        {
            // --- Puntos por tipo de muerte ---
            c.Check(ScoreRules.KillPoints(KillCause.Backstab) > ScoreRules.KillPoints(KillCause.Finisher) &&
                    ScoreRules.KillPoints(KillCause.Finisher) > ScoreRules.KillPoints(KillCause.Melee) &&
                    ScoreRules.KillPoints(KillCause.Melee) > ScoreRules.KillPoints(KillCause.Shot),
                "Por la espalda > remate > arma cuerpo a cuerpo > disparo");
            c.Check(ScoreRules.KnockdownPoints(KnockdownCause.Punch) < ScoreRules.KillPoints(KillCause.Shot),
                "Un derribo da menos que cualquier muerte");

            // --- Combo ---
            var s = new ScoreState();
            ScoreGain first = s.RegisterKill(KillCause.Melee);
            c.Check(first.Multiplier == 1 && first.Points == ScoreRules.KillPoints(KillCause.Melee), "La primera muerte vale x1");
            s.Tick(ScoreRules.ComboWindow * 0.5f);
            ScoreGain second = s.RegisterKill(KillCause.Shot);
            c.Check(s.Combo == 2 && second.Multiplier == 2 && second.Points == ScoreRules.KillPoints(KillCause.Shot) * 2,
                "Matar a tiempo sube el combo y duplica los puntos");
            s.Tick(ScoreRules.ComboWindow * 0.9f);
            s.RegisterKnockdown(KnockdownCause.Punch);
            c.Check(s.Combo == 2 && Mathf.Approximately(s.ComboTimeLeft, ScoreRules.ComboWindow),
                "Un derribo mantiene vivo el combo sin subirlo");
            c.Check(s.Tick(ScoreRules.ComboWindow + 0.1f) && s.Combo == 0, "Sin matar a tiempo, el combo se acaba");
            ScoreGain fresh = s.RegisterKill(KillCause.Finisher);
            c.Check(fresh.Multiplier == 1 && s.BestCombo == 2 && s.Kills == 3, "Tras acabarse, el combo empieza de cero y se guarda el mejor");
            c.Check(s.DistinctCauses == 3, "Se cuentan las formas distintas de matar");

            var capped = new ScoreState();
            for (int i = 0; i < ScoreRules.MaxMultiplier + 4; i++) capped.RegisterKill(KillCause.Shot);
            c.Check(capped.Multiplier == ScoreRules.MaxMultiplier, $"El multiplicador no pasa de x{ScoreRules.MaxMultiplier}");

            // --- Rebobinar ---
            var r = new ScoreState();
            r.RegisterKill(KillCause.Melee);
            ScoreState.Snapshot snap = r.Capture();
            r.RegisterKill(KillCause.Backstab);
            r.RegisterKill(KillCause.Shot);
            r.Restore(snap);
            c.Check(r.Points == ScoreRules.KillPoints(KillCause.Melee) && r.Combo == 1 && r.Kills == 1 && r.DistinctCauses == 1,
                "Rebobinar devuelve los puntos y el combo guardados");

            // --- Resumen y nota ---
            var clean = new ScoreState();
            clean.RegisterKill(KillCause.Backstab);
            clean.RegisterKill(KillCause.Melee);
            var fast = new ScoreSummary(clean, 5f, 0, 2);
            c.Check(fast.Bonuses.Any(b => b.Label == ScoreSummary.LabelTime) && fast.Bonuses.Any(b => b.Label == ScoreSummary.LabelNoRewind),
                "Acabar rápido y sin rebobinar da bonus");
            c.Check(fast.Total == fast.CombatPoints + fast.Bonuses.Sum(b => b.Points), "El total suma combate y bonus");
            var slow = new ScoreSummary(clean, 999f, 2, 2);
            c.Check(!slow.Bonuses.Any(b => b.Label == ScoreSummary.LabelTime || b.Label == ScoreSummary.LabelNoRewind) && slow.Total < fast.Total,
                "Lento y rebobinando no hay esos bonus");
            c.Check(ScoreRules.GradeFor(0, 10) == "D" && ScoreRules.GradeFor(100000, 10) == "S" &&
                    ScoreRules.GradeFor(12000, 10) == "B", "Las notas van de D a S según los puntos por enemigo");

            // --- Textos ---
            c.Check(HudText.Points(12400) == "12.400" && HudText.Points(999) == "999" && HudText.Points(1234567) == "1.234.567",
                "Los puntos se muestran con separador de miles");
            c.Check(HudText.Combo(1, 1) == null && HudText.Combo(3, 3) == "x3 COMBO", "El combo se muestra desde 2 muertes seguidas");
            c.Check(HudText.Gain(800, 1) == "+800" && HudText.Gain(1600, 2) == "+1.600 x2", "Texto flotante de los puntos ganados");
            c.Check(HudText.KillLabel(KillCause.Backstab) != null && HudText.KillLabel(KillCause.Shot) == null,
                "Solo las muertes especiales llevan etiqueta");
            c.Check(HudText.Time(95f) == "1:35", "El tiempo se muestra como minutos:segundos");
        }

        private static void VerifyScoreScene(Checker c)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject systems = BuilderUtil.FindRoot(scene, SceneFactory.SystemsName);
            var score = systems != null ? systems.GetComponent<ScoreKeeper>() : null;
            c.NotNull(score, "Systems: falta ScoreKeeper");
            if (score == null) return;
            c.Check(score.Level == systems.GetComponent<LevelManager>(), "ScoreKeeper.level sin asignar");
            c.Check(score.Rewinder == systems.GetComponent<TimeRewinder>(), "ScoreKeeper.rewinder sin asignar");
            c.Check(systems.GetComponent<GameHUD>()?.Score == score, "GameHUD.score sin asignar");
        }
    }
}
