using System.Collections.Generic;
using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Story
{
    /// <summary>
    /// Diálogo sin acercarse a nadie (Nina va de copiloto): las líneas se encolan y se muestran abajo, una tras otra,
    /// con efecto de máquina de escribir. E pasa a la siguiente. Mismo aspecto que <see cref="NpcDialogue"/>.
    /// </summary>
    public class RadioDialogue : MonoBehaviour
    {
        [SerializeField] private string speaker = StoryLines.NinaName;
        [SerializeField] private float charsPerSecond = 45f;
        [SerializeField] private float readTime = 2f;

        private readonly Queue<string> queue = new Queue<string>();
        private string current;
        private float elapsed;
        private GUIStyle boxStyle, nameStyle, textStyle;

        public string Current => current;
        public int Pending => queue.Count + (current != null ? 1 : 0);

        public void Say(params string[] lines)
        {
            foreach (string line in lines)
                if (!string.IsNullOrEmpty(line)) queue.Enqueue(line);
            if (current == null) Next();
        }

        /// <summary>Corta lo que diga y dice esto ya (para lo urgente).</summary>
        public void Interrupt(params string[] lines)
        {
            queue.Clear();
            current = null;
            Say(lines);
        }

        private void Update() => Tick(Time.unscaledDeltaTime, GameInput.PickupPressed);

        /// <summary>Avanza el diálogo. Público para simularlo en el editor.</summary>
        public void Tick(float dt, bool skip)
        {
            if (current == null) return;
            elapsed += dt;
            float duration = DialogueLogic.LineDuration(current, charsPerSecond, readTime);
            if (skip || elapsed >= duration) Next();
        }

        private void Next()
        {
            current = queue.Count > 0 ? queue.Dequeue() : null;
            elapsed = 0f;
        }

        private void OnGUI()
        {
            if (current == null || GameInput.Blocked) return;
            EnsureStyles();
            string shown = current.Substring(0, DialogueLogic.VisibleChars(current, elapsed, charsPerSecond));
            float width = Mathf.Min(Screen.width - 40f, 900f);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 160f, width, 120f);
            GUI.Box(rect, GUIContent.none, boxStyle);
            GUI.Label(new Rect(rect.x + 22f, rect.y + 12f, rect.width - 44f, 30f), speaker.ToUpperInvariant(), nameStyle);
            GUI.Label(new Rect(rect.x + 22f, rect.y + 44f, rect.width - 44f, 70f), shown, textStyle);
        }

        private void EnsureStyles()
        {
            if (boxStyle != null) return;
            var background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0.05f, 0.02f, 0.08f, 0.85f));
            background.Apply();
            boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = background }, hover = { background = background } };
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
            UI.GameHUD.SetTextColor(nameStyle, new Color(1f, 0.35f, 0.75f));
            UI.GameHUD.SetTextColor(textStyle, Color.white);
        }
    }
}
