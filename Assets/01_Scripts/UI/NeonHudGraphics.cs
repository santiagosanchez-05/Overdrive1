using UnityEngine;

namespace Overdrive.UI
{
    /// <summary>Vocabulario visual compartido por el combate y la persecución.</summary>
    public static class NeonHudGraphics
    {
        public static readonly Color Cyan = new Color32(35, 216, 239, 255);
        public static readonly Color Pink = new Color32(255, 38, 127, 255);
        public static readonly Color Ink = new Color(.025f, .018f, .065f, .9f);
        public const string CombatControls = "WASD  MOVER   ·   RATÓN  APUNTAR   ·   CLIC IZQ  ATACAR   ·   E  RECOGER";
        public const string CombatSecondary = "CLIC DER  LANZAR   ·   ESPACIO  REMATAR   ·   CTRL  SIGILO   ·   SHIFT  MIRAR   ·   V  CÁMARA   ·   ESC  PAUSA";
        public const string DriveControls = "A / D  CONDUCIR   ·   RATÓN  APUNTAR   ·   CLIC IZQ  DISPARAR   ·   ESC  PAUSA";

        public static float Scale => Mathf.Min(Screen.width / 1280f, Screen.height / 720f) * (2f / 3f);

        public static Rect DialogueRect(float height, bool driving)
        {
            float width = Mathf.Min(Screen.width - 40f, 900f);
            float bottom = Mathf.Max(40f, (driving ? 82f : 110f) * Scale);
            return new Rect((Screen.width - width) * .5f, Screen.height - height - bottom, width, height);
        }

        public static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }

        public static void Panel(Rect rect, Color accent, float scale)
        {
            Fill(rect, Ink);
            Fill(new Rect(rect.x, rect.y, rect.width, 2f * scale), accent);
            Fill(new Rect(rect.x, rect.y, 3f * scale, rect.height), accent);
            for (int i = 0; i < 3; i++)
                Fill(new Rect(rect.xMax - (18f + i * 12f) * scale, rect.y + 9f * scale, 6f * scale, 15f * scale), new Color(accent.r, accent.g, accent.b, .4f));
        }

        public static void Meter(Rect rect, float value, Color accent, int segments = 16)
        {
            value = Mathf.Clamp01(value);
            float cell = rect.width / segments;
            for (int i = 0; i < segments; i++)
            {
                var segment = new Rect(rect.x + cell * i, rect.y, cell * .82f, rect.height);
                Fill(segment, new Color(accent.r, accent.g, accent.b, .16f));
                float fill = Mathf.Clamp01(value * segments - i);
                if (fill > 0f) Fill(new Rect(segment.x, segment.y, segment.width * fill, segment.height), accent);
            }
        }

        public static void Controls(float scale, GUIStyle style, bool driving)
        {
            float width = Mathf.Min(Screen.width - 40f * scale, 1280f * scale);
            float height = (driving ? 48f : 76f) * scale;
            var rect = new Rect((Screen.width - width) * .5f, Screen.height - height - 18f * scale, width, height);
            Panel(rect, Cyan, scale);
            GUI.Label(new Rect(rect.x + 14f * scale, rect.y + 7f * scale, rect.width - 28f * scale, 30f * scale), driving ? DriveControls : CombatControls, style);
            if (!driving) GUI.Label(new Rect(rect.x + 14f * scale, rect.y + 37f * scale, rect.width - 28f * scale, 30f * scale), CombatSecondary, style);
        }
    }
}
