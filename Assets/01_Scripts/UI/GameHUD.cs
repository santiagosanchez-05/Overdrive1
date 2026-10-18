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
        private GUIStyle summaryLeft;
        private GUIStyle summaryRight;
        private GUIStyle summaryCenter;
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

        /// <summary>Pantalla final: puntos de combate, bonus, total, nota y récord.</summary>
        private void DrawSummary(ScoreSummary summary)
        {
            float width = 640f * scale;
            float x = (Screen.width - width) * 0.5f;
            float row = 40f * scale;
            int rows = summary.Bonuses.Count + 2;
            float height = 90f * scale + rows * row + 8f * scale + 130f * scale + row + 60f * scale;
            float y = Mathf.Max(10f * scale, (Screen.height - height) * 0.5f);

            Color before = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(x - 30f * scale, y - 20f * scale, width + 60f * scale, height + 40f * scale), Texture2D.whiteTexture);
            GUI.color = before;

            DrawShadowed(new Rect(0f, y, Screen.width, 80f * scale), HudText.Complete, center);
            y += 90f * scale;

            void Line(string label, string value)
            {
                DrawShadowed(new Rect(x, y, width, row), label, summaryLeft);
                DrawShadowed(new Rect(x, y, width, row), value, summaryRight);
                y += row;
            }

            Line("Combate", HudText.Points(summary.CombatPoints));
            foreach (ScoreSummary.Line bonus in summary.Bonuses)
            {
                string label = bonus.Label == ScoreSummary.LabelTime ? $"{bonus.Label} {HudText.Time(summary.Seconds)}" : bonus.Label;
                Line(label, "+" + HudText.Points(bonus.Points));
            }
            y += 8f * scale;
            Line("TOTAL", HudText.Points(summary.Total));

            DrawShadowed(new Rect(0f, y, Screen.width, 130f * scale), summary.Grade, gradeStyle);
            y += 130f * scale;
            string record = score.IsNewRecord ? HudText.NewRecord : HudText.Record(score.BestTotal);
            DrawShadowed(new Rect(0f, y, Screen.width, row), record, score.IsNewRecord ? comboStyle : summaryCenter);
            y += row + 10f * scale;
            string hint = HudText.CompleteActionHint(level != null ? level.NextScene : null);
            DrawShadowed(new Rect(0f, y, Screen.width, 50f * scale), hint, sub);
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
            summaryLeft = MakeStyle(30, TextAnchor.MiddleLeft, Color.white);
            summaryRight = MakeStyle(30, TextAnchor.MiddleRight, new Color(1f, 0.95f, 0.3f));
            summaryCenter = MakeStyle(28, TextAnchor.MiddleCenter, Color.white);
            gradeStyle = MakeStyle(120, TextAnchor.MiddleCenter, new Color(1f, 0.3f, 0.85f));
        }

        private GUIStyle MakeStyle(int size, TextAnchor anchor, Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(size * scale),
                fontStyle = FontStyle.Bold,
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
