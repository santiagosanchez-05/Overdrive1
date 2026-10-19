using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Driving
{
    /// <summary>Textos del HUD de la persecución (lógica pura, comprobable en el editor).</summary>
    public static class ChaseText
    {
        public const string Controls = "· A/D girar · Ratón apuntar · Clic izquierdo disparar";
        public const string Won = "Llegaste al refugio";
        public const string WonHint = "ESPACIO / ENTER · Ver el final";
        public const string Lost = "El coche ha quedado destrozado";
        public const string LostHint = "Presiona R para reiniciar";

        public static string Wave(int started, int total) => started < 0 ? "Oleada -" : $"Oleada {started + 1}/{total}";
        public static string Kills(int kills) => $"Dealers fuera: {kills}";
        public static string Health(float health01) => $"Coche {Mathf.CeilToInt(Mathf.Clamp01(health01) * 100f)}%";
        public static string Distance(float meters) => $"Piso franco: {Mathf.Max(0, Mathf.CeilToInt(meters))} m";
    }

    /// <summary>HUD de la persecución: estado del coche, oleada, bajas, distancia a la meta y mensajes.</summary>
    public class ChaseHUD : MonoBehaviour
    {
        [SerializeField] private ChaseLevel level;
        [SerializeField] private Font hudFont;
        private GUIStyle controlsStyle;
        private float previousScale = -1f;
        private GUIStyle label, big, sub;
        private Texture2D white;

        private void Update()
        {
            // Mientras se conduce, la mira del HUD sustituye al cursor (en pausa o al acabar vuelve el normal).
            Cursor.visible = level == null || GameInput.Blocked || level.State != ChaseState.Driving;
        }

        private void OnDisable() => Cursor.visible = true;

        private void OnGUI()
        {
            if (level == null || level.Player == null || GameInput.Blocked) return;
            Ensure();
            float s = UI.NeonHudGraphics.Scale;
            Car car = level.Player;

            // Barra del coche.
            var bar = new Rect(30f * s, 30f * s, 360f * s, 22f * s);
            UI.NeonHudGraphics.Panel(new Rect(20f * s, 20f * s, 410f * s, 175f * s), UI.NeonHudGraphics.Cyan, s);
            UI.NeonHudGraphics.Meter(bar, car.Health01, car.Health01 < .25f ? UI.NeonHudGraphics.Pink : UI.NeonHudGraphics.Cyan);
            GUI.Label(new Rect(bar.x, bar.yMax + 4f * s, 500f * s, 34f * s), ChaseText.Health(car.Health01), label);
            GUI.Label(new Rect(bar.x, bar.yMax + 38f * s, 500f * s, 34f * s), ChaseText.Wave(level.WaveStarted, level.Waves.Length), label);
            GUI.Label(new Rect(bar.x, bar.yMax + 72f * s, 500f * s, 34f * s), ChaseText.Kills(level.Kills), label);

            // Progreso hasta el piso franco.
            var road = new Rect(Screen.width - 420f * s, 30f * s, 390f * s, 14f * s);
            UI.NeonHudGraphics.Panel(new Rect(road.x - 12f * s, road.y - 10f * s, road.width + 24f * s, 94f * s), UI.NeonHudGraphics.Pink, s);
            Fill(road, new Color(0f, 0f, 0f, 0.6f));
            Fill(new Rect(road.x, road.y, road.width * level.Progress01, road.height), new Color(1f, 0.35f, 0.75f));
            GUI.Label(new Rect(road.x, road.yMax + 4f * s, road.width, 34f * s),
                ChaseText.Distance(level.Road.Length - level.FinishDistance - level.Along), label);

            if (level.State == ChaseState.Won)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.35f, Screen.width, 80f * s), ChaseText.Won, big);
                GUI.Label(new Rect(0f, Screen.height * 0.35f + 90f * s, Screen.width, 40f * s), ChaseText.WonHint, sub);
            }
            else if (level.State == ChaseState.Lost)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.35f, Screen.width, 80f * s), ChaseText.Lost, big);
                GUI.Label(new Rect(0f, Screen.height * 0.35f + 90f * s, Screen.width, 40f * s), ChaseText.LostHint, sub);
            }
            UI.NeonHudGraphics.Controls(s, controlsStyle, true);

            // Mira bajo el ratón.
            if (level.State == ChaseState.Driving)
            {
                Vector2 m = Event.current != null ? Event.current.mousePosition : Vector2.zero;
                float r = 14f * s, t = 3f * s;
                Color aim = new Color(1f, 0.35f, 0.75f);
                Fill(new Rect(m.x - r, m.y - t * 0.5f, r * 0.7f, t), aim);
                Fill(new Rect(m.x + r * 0.3f, m.y - t * 0.5f, r * 0.7f, t), aim);
                Fill(new Rect(m.x - t * 0.5f, m.y - r, t, r * 0.7f), aim);
                Fill(new Rect(m.x - t * 0.5f, m.y + r * 0.3f, t, r * 0.7f), aim);
            }
        }

        private void Fill(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = prev;
        }

        private void Ensure()
        {
            float s = UI.NeonHudGraphics.Scale;
            if (label != null && Mathf.Approximately(s, previousScale)) return;
            previousScale = s;
            white = Texture2D.whiteTexture;
            label = new GUIStyle(GUI.skin.label) { font = hudFont, fontSize = Mathf.RoundToInt(26 * s), fontStyle = FontStyle.Bold };
            controlsStyle = new GUIStyle(label) { fontSize = Mathf.RoundToInt(22 * s), alignment = TextAnchor.MiddleCenter };
            controlsStyle.font = GUI.skin.font;
            big = new GUIStyle(label) { fontSize = Mathf.RoundToInt(56 * s), alignment = TextAnchor.MiddleCenter };
            sub = new GUIStyle(label) { fontSize = Mathf.RoundToInt(26 * s), alignment = TextAnchor.MiddleCenter };
            UI.GameHUD.SetTextColor(label, Color.white);
            UI.GameHUD.SetTextColor(big, new Color(1f, 0.35f, 0.75f));
            UI.GameHUD.SetTextColor(sub, Color.white);
        }
    }
}
