using Overdrive.Level;
using Overdrive.Weapons;
using UnityEngine;

namespace Overdrive.UI
{
    /// <summary>HUD con IMGUI: arma y munición, enemigos restantes y mensajes de muerte o victoria.</summary>
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private LevelManager level;

        private GUIStyle cornerLeft;
        private GUIStyle cornerRight;
        private GUIStyle center;
        private GUIStyle sub;
        private float scale = -1f;

        public PlayerWeapons Weapons => weapons;
        public LevelManager Level => level;

        private void OnGUI()
        {
            if (level == null) return;
            EnsureStyles();

            WeaponItem current = weapons != null ? weapons.Current : null;
            string weaponText = HudText.Weapon(current != null ? current.Definition : null, current != null ? current.State.Ammo : 0);
            float margin = 20f * scale;
            float lineHeight = 44f * scale;

            DrawShadowed(new Rect(margin, margin, Screen.width * 0.5f, lineHeight), weaponText, cornerLeft);
            DrawShadowed(new Rect(Screen.width * 0.5f - margin, margin, Screen.width * 0.5f, lineHeight),
                HudText.Enemies(level.Progress.Remaining), cornerRight);

            bool carrying = level.Briefcase != null && level.Briefcase.IsCollected;
            string briefcaseText = HudText.Briefcase(carrying);
            if (briefcaseText != null)
                DrawShadowed(new Rect(margin, margin + lineHeight, Screen.width * 0.5f, lineHeight), briefcaseText, cornerLeft);

            string message = HudText.CenterMessage(level.Progress.State);
            string hint = HudText.SubMessage(level.Progress.State, !level.Progress.HasObjective);
            float middle = Screen.height * 0.5f;
            if (message != null)
                DrawShadowed(new Rect(0f, middle - 70f * scale, Screen.width, 80f * scale), message, center);
            if (hint != null)
            {
                float y = message != null ? middle + 10f * scale : margin + lineHeight * 2f + 10f * scale;
                DrawShadowed(new Rect(0f, y, Screen.width, 50f * scale), hint, sub);
            }
        }

        private void EnsureStyles()
        {
            // Escala el texto según la altura de la pantalla (referencia 1080p).
            float newScale = Mathf.Max(0.5f, Screen.height / 1080f);
            if (cornerLeft != null && Mathf.Approximately(newScale, scale)) return;
            scale = newScale;

            cornerLeft = MakeStyle(30, TextAnchor.UpperLeft, new Color(1f, 0.95f, 0.3f));
            cornerRight = MakeStyle(30, TextAnchor.UpperRight, new Color(1f, 0.3f, 0.5f));
            center = MakeStyle(64, TextAnchor.MiddleCenter, Color.white);
            sub = MakeStyle(30, TextAnchor.MiddleCenter, new Color(0.3f, 1f, 0.6f));
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
            style.normal.textColor = color;
            return style;
        }

        private static void DrawShadowed(Rect rect, string text, GUIStyle style)
        {
            Color color = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }
    }
}
