using System.Collections.Generic;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Scoring;
using Overdrive.Weapons;
using UnityEngine;

namespace Overdrive.UI
{
    /// <summary>HUD con IMGUI: arma y munición, enemigos restantes y mensajes de muerte o victoria.</summary>
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private LevelManager level;
        [SerializeField] private TimeRewinder rewinder;
        [SerializeField] private ScoreKeeper score;

        private GUIStyle cornerLeft;
        private GUIStyle cornerRight;
        private GUIStyle center;
        private GUIStyle sub;
        private GUIStyle sneak;
        private GUIStyle suspicious;
        private GUIStyle alerted;
        private GUIStyle scoreStyle;
        private GUIStyle comboStyle;
        private GUIStyle popupStyle;
        private GUIStyle popupLabelStyle;
        private GUIStyle summaryTitle;
        private GUIStyle summaryLabel;
        private GUIStyle summaryValue;
        private GUIStyle summaryTotalLabel;
        private GUIStyle summaryTotalValue;
        private GUIStyle summaryCaption;
        private GUIStyle summaryRecordNew;
        private GUIStyle gradeStyle;
        private float scale = -1f;
        private PlayerController controller;

        public PlayerWeapons Weapons => weapons;
        public LevelManager Level => level;
        public TimeRewinder Rewinder => rewinder;
        public ScoreKeeper Score => score;

        private void OnGUI()
        {
            if (level == null || GameInput.Blocked) return; // en pausa manda el menú
            EnsureStyles();

            WeaponItem current = weapons != null ? weapons.Current : null;
            string weaponText = HudText.Weapon(current != null ? current.Definition : null, current != null ? current.State.Ammo : 0, current != null ? current.Capacity : 0);
            float margin = 20f * scale;
            float lineHeight = 44f * scale;

            DrawShadowed(new Rect(margin, margin, Screen.width * 0.5f, lineHeight), weaponText, cornerLeft);
            DrawShadowed(new Rect(Screen.width * 0.5f - margin, margin, Screen.width * 0.5f, lineHeight),
                HudText.Enemies(level.Progress.Remaining), cornerRight);

            bool carrying = level.Briefcase != null && level.Briefcase.IsCollected;
            string briefcaseText = HudText.Briefcase(carrying);
            if (briefcaseText != null)
                DrawShadowed(new Rect(margin, margin + lineHeight, Screen.width * 0.5f, lineHeight), briefcaseText, cornerLeft);

            if (controller == null && weapons != null) controller = weapons.GetComponent<PlayerController>();
            if (controller != null && controller.IsSneaking)
                DrawShadowed(new Rect(margin, Screen.height - margin - lineHeight, Screen.width * 0.5f, lineHeight),
                    HudText.Sneaking, sneak);

            DrawAwarenessMarks();

            if (rewinder != null)
                DrawShadowed(new Rect(Screen.width * 0.5f - margin, margin + lineHeight, Screen.width * 0.5f, lineHeight),
                    HudText.Rewinds(rewinder.UsesLeft), cornerRight);

            DrawScore(margin);
            if (level.Progress.State == LevelState.Complete && score != null && score.Summary != null)
            {
                DrawSummary(score.Summary);
                return;
            }

            string message = HudText.CenterMessage(level.Progress.State);
            string hint = HudText.SubMessage(level.Progress.State, !level.Progress.HasObjective);
            if (level.Progress.State == LevelState.Failed && rewinder != null) hint = HudText.RewindHint(rewinder.UsesLeft);
            float middle = Screen.height * 0.5f;
            if (message != null)
                DrawShadowed(new Rect(0f, middle - 70f * scale, Screen.width, 80f * scale), message, center);
            if (hint != null)
            {
                // Sin mensaje central, va arriba, debajo de los puntos y el combo.
                float y = message != null ? middle + 10f * scale : margin + lineHeight * 3f;
                DrawShadowed(new Rect(0f, y, Screen.width, 50f * scale), hint, sub);
            }
        }

        /// <summary>Dibuja "?" o "!" sobre la cabeza de cada enemigo visible que sospecha o te ha detectado.</summary>
        private void DrawAwarenessMarks()
        {
            Camera cam = Camera.main;
            if (cam == null || weapons == null) return;
            float playerY = weapons.transform.position.y;
            float size = 60f * scale;

            foreach (Enemy enemy in Enemy.All)
            {
                if (!FloorVisibility.IsVisible(enemy.transform.position.y, playerY)) continue;
                var ai = enemy.GetComponent<EnemyAI>();
                if (ai == null) continue;
                Awareness awareness = ai.Awareness;
                string mark = HudText.AwarenessMark(awareness);
                if (mark == null) continue;

                Vector3 screen = cam.WorldToScreenPoint(enemy.transform.position + Vector3.up * 2.4f);
                if (screen.z <= 0f) continue;
                var rect = new Rect(screen.x - size * 0.5f, Screen.height - screen.y - size * 0.5f, size, size);
                DrawShadowed(rect, mark, awareness == Awareness.Alerted ? alerted : suspicious);
            }
        }

        /// <summary>Puntos arriba en el centro, combo debajo con su barra de tiempo, y "+puntos" flotantes.</summary>
        private void DrawScore(float margin)
        {
            if (score == null) return;
            ScoreState s = score.State;
            float width = 500f * scale;
            float x = (Screen.width - width) * 0.5f;
            DrawShadowed(new Rect(x, margin, width, 56f * scale), HudText.Points(s.Points), scoreStyle);

            string combo = HudText.Combo(s.Combo, s.Multiplier);
            if (combo != null)
            {
                float y = margin + 56f * scale;
                DrawShadowed(new Rect(x, y, width, 40f * scale), combo, comboStyle);
                float barWidth = 180f * scale * (s.ComboTimeLeft / ScoreRules.ComboWindow);
                var bar = new Rect((Screen.width - barWidth) * 0.5f, y + 42f * scale, barWidth, 5f * scale);
                Color before = GUI.color;
                GUI.color = comboStyle.normal.textColor;
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = before;
            }

            Camera cam = Camera.main;
            if (cam == null) return;
            float playerY = weapons != null ? weapons.transform.position.y : 0f;
            foreach (ScoreKeeper.Popup popup in score.Popups)
            {
                if (!FloorVisibility.IsVisible(popup.Position.y, playerY)) continue;
                float t = popup.Age / score.PopupDuration;
                Vector3 screen = cam.WorldToScreenPoint(popup.Position + Vector3.up * (2f + t * 1.2f));
                if (screen.z <= 0f) continue;
                float alpha = 1f - t * t;
                var rect = new Rect(screen.x - 150f * scale, Screen.height - screen.y - 20f * scale, 300f * scale, 40f * scale);
                DrawShadowed(rect, HudText.Gain(popup.Gain.Points, popup.Gain.Multiplier), popupStyle, alpha);
                string label = HudText.PopupLabel(popup.Cause, popup.Gain.Stealth);
                if (label != null)
                    DrawShadowed(new Rect(rect.x, rect.y + 34f * scale, rect.width, 30f * scale), label, popupLabelStyle, alpha);
            }
        }

        // ---------- Pantalla final ----------

        // Mismos tonos que los paneles del menú principal.
        private static readonly Color PanelFill = new Color32(0x08, 0x09, 0x0D, 0xC8);
        private static readonly Color PanelBorder = new Color32(0x9A, 0x9D, 0xA3, 0xFF);
        private static readonly Color Pink = new Color(1f, 0.3f, 0.85f);

        /// <summary>Momento en que apareció la pantalla final, para animar su entrada.</summary>
        private float summaryStart = -1f;

        /// <summary>
        /// Pantalla final con el estilo del menú: panel oscuro con borde y neón, filas que entran una a una,
        /// total que cuenta hacia arriba, nota en un recuadro que aparece de golpe y el récord.
        /// </summary>
        private void DrawSummary(ScoreSummary summary)
        {
            if (summaryStart < 0f) summaryStart = Time.unscaledTime;
            float t = Time.unscaledTime - summaryStart;
            float s = scale;
            Matrix4x4 baseMatrix = GUI.matrix;

            var lines = new List<(string label, string value)> { ("COMBATE", HudText.Points(summary.CombatPoints)) };
            foreach (ScoreSummary.Line bonus in summary.Bonuses)
            {
                string label = bonus.Label == ScoreSummary.LabelTime ? $"{bonus.Label} {HudText.Time(summary.Seconds)}" : bonus.Label;
                lines.Add((label.ToUpperInvariant(), "+" + HudText.Points(bonus.Points)));
            }

            // --- Medidas ---
            float pad = 50f * s, rowH = 46f * s, gap = 6f * s, totalH = 62f * s, gradeSize = 230f * s;
            float rowsH = lines.Count * (rowH + gap) + 10f * s + totalH;
            float contentH = Mathf.Max(rowsH, gradeSize + 80f * s);
            float panelW = Mathf.Min(960f * s, Screen.width - 20f);
            float panelH = 130f * s + contentH + 110f * s;
            float enter = Ease01(t / 0.35f);
            var panel = new Rect((Screen.width - panelW) * 0.5f, (Screen.height - panelH) * 0.5f + (1f - enter) * 40f * s, panelW, panelH);

            // --- Fondo y panel ---
            FillRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f * enter));
            FillRect(panel, WithAlpha(PanelFill, PanelFill.a * enter));
            DrawBorder(panel, 2f * s, WithAlpha(PanelBorder, enter));
            FillRect(new Rect(panel.x, panel.y, panel.width, 4f * s), WithAlpha(Pink, enter));
            DrawStripes(new Rect(panel.xMax - 190f * s, panel.y + 30f * s, 150f * s, 56f * s), enter, s);

            // --- Título ---
            DrawShadowed(new Rect(panel.x + pad, panel.y + 26f * s, panel.width - pad * 2f, 70f * s),
                HudText.Complete.ToUpperInvariant(), summaryTitle, enter);
            FillRect(new Rect(panel.x + pad, panel.y + 108f * s, panel.width - pad * 2f, 1f * s), WithAlpha(PanelBorder, 0.5f * enter));

            // --- Filas ---
            float top = panel.y + 130f * s;
            float rowsW = panel.width - pad * 2f - gradeSize - 40f * s;
            float y = top;
            for (int i = 0; i < lines.Count; i++)
            {
                float a = Ease01((t - 0.35f - i * 0.12f) / 0.25f);
                if (a > 0f)
                {
                    var row = new Rect(panel.x + pad - (1f - a) * 30f * s, y, rowsW, rowH);
                    FillRect(row, new Color(1f, 1f, 1f, 0.05f * a));
                    FillRect(new Rect(row.x, row.y, 4f * s, row.height), new Color(1f, 1f, 1f, 0.6f * a));
                    var inner = new Rect(row.x + 20f * s, row.y, row.width - 36f * s, row.height);
                    DrawShadowed(inner, lines[i].label, summaryLabel, a);
                    DrawShadowed(inner, lines[i].value, summaryValue, a);
                }
                y += rowH + gap;
            }

            // --- Total: cuenta hacia arriba ---
            y += 10f * s;
            float totalAt = 0.35f + lines.Count * 0.12f;
            float ta = Ease01((t - totalAt) / 0.25f);
            if (ta > 0f)
            {
                var totalRect = new Rect(panel.x + pad, y, rowsW, totalH);
                FillRect(totalRect, new Color(1f, 1f, 1f, 0.1f * ta));
                DrawBorder(totalRect, 2f * s, new Color(1f, 1f, 1f, 0.9f * ta));
                float count = Ease01((t - totalAt) / 0.9f);
                var inner = new Rect(totalRect.x + 20f * s, totalRect.y, totalRect.width - 36f * s, totalRect.height);
                DrawShadowed(inner, "TOTAL", summaryTotalLabel, ta);
                DrawShadowed(inner, HudText.Points(Mathf.RoundToInt(summary.Total * count)), summaryTotalValue, ta);
            }

            // --- Nota: aparece de golpe cuando termina de contar el total ---
            float gradeAt = totalAt + 0.9f;
            float ga = Ease01((t - gradeAt) / 0.25f);
            var box = new Rect(panel.xMax - pad - gradeSize, top, gradeSize, gradeSize);
            if (ga > 0f)
            {
                for (int g = 1; g <= 4; g++)
                {
                    float grow = g * 7f * s;
                    FillRect(new Rect(box.x - grow, box.y - grow, box.width + grow * 2f, box.height + grow * 2f), WithAlpha(Pink, 0.06f * ga));
                }
                FillRect(box, WithAlpha(PanelFill, ga));
                DrawBorder(box, 3f * s, WithAlpha(Pink, ga));
                DrawShadowed(new Rect(box.x, box.y + 14f * s, box.width, 30f * s), "NOTA", summaryCaption, ga);

                float stamp = Mathf.Lerp(1.8f, 1f, ga);
                GUIUtility.ScaleAroundPivot(new Vector2(stamp, stamp), box.center);
                DrawShadowed(new Rect(box.x, box.y + 20f * s, box.width, box.height - 20f * s), summary.Grade, gradeStyle, ga);
                GUI.matrix = baseMatrix;

                // Récord bajo la nota.
                float ra = Ease01((t - gradeAt - 0.2f) / 0.3f);
                var recordRect = new Rect(box.x - 40f * s, box.yMax + 16f * s, box.width + 80f * s, 40f * s);
                if (score.IsNewRecord)
                {
                    float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
                    DrawShadowed(recordRect, HudText.NewRecord.ToUpperInvariant(), summaryRecordNew, ra * pulse);
                }
                else
                {
                    DrawShadowed(recordRect, HudText.Record(score.BestTotal).ToUpperInvariant(), summaryCaption, ra);
                }
            }

            // --- Instrucciones ---
            float ha = Ease01((t - gradeAt - 0.4f) / 0.3f);
            FillRect(new Rect(panel.x + pad, panel.yMax - 92f * s, panel.width - pad * 2f, 1f * s), WithAlpha(PanelBorder, 0.5f * enter));
            string hint = HudText.CompleteActionHint(level != null ? level.NextScene : null);
            DrawShadowed(new Rect(panel.x, panel.yMax - 80f * s, panel.width, 56f * s), hint, sub, ha);
        }

        /// <summary>Rayas inclinadas como las de los botones del menú.</summary>
        private static void DrawStripes(Rect area, float alpha, float s)
        {
            Matrix4x4 before = GUI.matrix;
            float w = 14f * s, step = 30f * s;
            for (int i = 0; i < 5; i++)
            {
                var stripe = new Rect(area.x + i * step, area.y, w, area.height);
                GUI.matrix = before;
                GUIUtility.RotateAroundPivot(20f, stripe.center);
                FillRect(stripe, new Color(1f, 1f, 1f, (0.12f + i * 0.06f) * alpha));
            }
            GUI.matrix = before;
        }

        private static void FillRect(Rect rect, Color color)
        {
            Color before = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = before;
        }

        private static void DrawBorder(Rect r, float w, Color color)
        {
            FillRect(new Rect(r.x, r.y, r.width, w), color);
            FillRect(new Rect(r.x, r.yMax - w, r.width, w), color);
            FillRect(new Rect(r.x, r.y, w, r.height), color);
            FillRect(new Rect(r.xMax - w, r.y, w, r.height), color);
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>Curva de entrada suave (rápida al principio, frena al final), de 0 a 1.</summary>
        private static float Ease01(float x)
        {
            x = Mathf.Clamp01(x);
            return 1f - (1f - x) * (1f - x) * (1f - x);
        }

        private void EnsureStyles()
        {
            // Escala el texto según la altura de la pantalla (referencia 1080p).
            float newScale = Mathf.Max(0.5f, Screen.height / 1080f);
            if (cornerLeft != null && Mathf.Approximately(newScale, scale)) return;
            scale = newScale;

            cornerLeft = MakeStyle(30, TextAnchor.UpperLeft, new Color(1f, 0.95f, 0.3f));
            sneak = MakeStyle(26, TextAnchor.LowerLeft, new Color(0.5f, 0.8f, 1f));
            suspicious = MakeStyle(48, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.1f));
            alerted = MakeStyle(48, TextAnchor.MiddleCenter, new Color(1f, 0.15f, 0.2f));
            cornerRight = MakeStyle(30, TextAnchor.UpperRight, new Color(1f, 0.3f, 0.5f));
            center = MakeStyle(64, TextAnchor.MiddleCenter, Color.white);
            sub = MakeStyle(30, TextAnchor.MiddleCenter, new Color(0.3f, 1f, 0.6f));
            scoreStyle = MakeStyle(46, TextAnchor.UpperCenter, Color.white);
            comboStyle = MakeStyle(32, TextAnchor.UpperCenter, new Color(1f, 0.3f, 0.85f));
            popupStyle = MakeStyle(30, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.3f));
            popupLabelStyle = MakeStyle(22, TextAnchor.MiddleCenter, new Color(1f, 0.3f, 0.85f));
            summaryTitle = MakeStyle(54, TextAnchor.MiddleLeft, Color.white, FontStyle.BoldAndItalic);
            summaryLabel = MakeStyle(26, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.82f), FontStyle.BoldAndItalic);
            summaryValue = MakeStyle(28, TextAnchor.MiddleRight, new Color(1f, 0.95f, 0.3f));
            summaryTotalLabel = MakeStyle(34, TextAnchor.MiddleLeft, Color.white, FontStyle.BoldAndItalic);
            summaryTotalValue = MakeStyle(38, TextAnchor.MiddleRight, new Color(1f, 0.95f, 0.3f));
            summaryCaption = MakeStyle(22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f), FontStyle.BoldAndItalic);
            summaryRecordNew = MakeStyle(26, TextAnchor.MiddleCenter, Pink, FontStyle.BoldAndItalic);
            gradeStyle = MakeStyle(150, TextAnchor.MiddleCenter, Pink, FontStyle.BoldAndItalic);
        }

        private GUIStyle MakeStyle(int size, TextAnchor anchor, Color color, FontStyle fontStyle = FontStyle.Bold)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(size * scale),
                fontStyle = fontStyle,
                alignment = anchor,
                wordWrap = false,
            };
            SetTextColor(style, color);
            return style;
        }

        /// <summary>
        /// Color del texto en todos los estados. La etiqueta por defecto de IMGUI tiene su propio color
        /// "hover": sin esto, el texto se vuelve blanco al pasar el ratón por encima.
        /// </summary>
        public static void SetTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
        }

        private static void DrawShadowed(Rect rect, string text, GUIStyle style, float alpha = 1f)
        {
            Color color = style.normal.textColor;
            SetTextColor(style, new Color(0f, 0f, 0f, 0.85f * alpha));
            GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), text, style);
            SetTextColor(style, new Color(color.r, color.g, color.b, color.a * alpha));
            GUI.Label(rect, text, style);
            SetTextColor(style, color);
        }
    }
}
