using Overdrive.Core;
using Overdrive.Player;
using UnityEngine;

namespace Overdrive.Story
{
    /// <summary>Avance del diálogo (lógica pura, comprobable en el editor).</summary>
    public static class DialogueLogic
    {
        /// <summary>Caracteres visibles del efecto máquina de escribir.</summary>
        public static int VisibleChars(string line, float elapsed, float charsPerSecond) =>
            string.IsNullOrEmpty(line) ? 0 : Mathf.Clamp(Mathf.FloorToInt(elapsed * charsPerSecond), 0, line.Length);

        /// <summary>Tiempo que se queda cada línea: lo que tarda en escribirse más un rato para leerla.</summary>
        public static float LineDuration(string line, float charsPerSecond, float readTime) =>
            (string.IsNullOrEmpty(line) ? 0 : line.Length) / Mathf.Max(charsPerSecond, 1f) + readTime;
    }

    /// <summary>
    /// Personaje que habla al acercarse: mira al jugador, pone la animación de hablar y muestra sus líneas
    /// abajo en pantalla con efecto de máquina de escribir. E pasa a la siguiente línea; al alejarse se calla
    /// y al volver sigue donde lo dejó. Cuando termina vuelve a la animación de reposo.
    /// </summary>
    [DisallowMultipleComponent]
    public class NpcDialogue : MonoBehaviour
    {
        public const string TalkingParam = "Talking";

        [SerializeField] private string speaker = "Nina";
        [TextArea] [SerializeField] private string[] lines = new string[0];
        [SerializeField] private Animator animator;
        [SerializeField] private float talkRadius = 3.5f;
        [SerializeField] private float charsPerSecond = 40f;
        [SerializeField] private float readTime = 2.2f;
        [SerializeField] private float turnSpeed = 240f;

        private Transform player;
        private int index;
        private float elapsed;
        private bool talking;
        private GUIStyle boxStyle, nameStyle, textStyle, hintStyle;

        public string Speaker => speaker;
        public string[] Lines => lines;
        public Animator Animator => animator;
        public float TalkRadius => talkRadius;
        public bool Talking => talking;
        public bool Finished => index >= lines.Length;
        public int LineIndex => index;

        private void Update() => Tick(Time.deltaTime, FindPlayer(), GameInput.PickupPressed);

        /// <summary>Avanza el diálogo. Público para simularlo en el editor.</summary>
        public void Tick(float dt, Transform target, bool skipPressed)
        {
            bool near = target != null && !Finished && Vector3.Distance(Flat(target.position), Flat(transform.position)) <= talkRadius;
            var life = target != null ? target.GetComponent<PlayerLife>() : null;
            if (life != null && life.IsDead) near = false;
            talking = near;
            if (animator != null) animator.SetBool(TalkingParam, talking);
            if (!talking) return;

            Vector3 to = Flat(target.position) - Flat(transform.position);
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), turnSpeed * dt);

            string line = lines[index];
            elapsed += dt;
            bool written = DialogueLogic.VisibleChars(line, elapsed, charsPerSecond) >= line.Length;
            if (skipPressed && !written) elapsed = line.Length / Mathf.Max(charsPerSecond, 1f); // E: termina de escribirla
            else if (skipPressed || elapsed >= DialogueLogic.LineDuration(line, charsPerSecond, readTime)) NextLine();
        }

        private void NextLine()
        {
            index++;
            elapsed = 0f;
            if (Finished)
            {
                talking = false;
                if (animator != null) animator.SetBool(TalkingParam, false);
            }
        }

        private Transform FindPlayer()
        {
            if (player == null)
            {
                var found = GameObject.FindGameObjectWithTag("Player");
                player = found != null ? found.transform : null;
            }
            return player;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private void OnGUI()
        {
            if (!talking || Finished || GameInput.Blocked) return;
            EnsureStyles();
            string line = lines[index];
            string shown = line.Substring(0, DialogueLogic.VisibleChars(line, elapsed, charsPerSecond));

            float width = Mathf.Min(Screen.width - 40f, 900f);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 170f, width, 130f);
            GUI.Box(rect, GUIContent.none, boxStyle);
            GUI.Label(new Rect(rect.x + 22f, rect.y + 12f, rect.width - 44f, 30f), speaker.ToUpperInvariant(), nameStyle);
            GUI.Label(new Rect(rect.x + 22f, rect.y + 44f, rect.width - 44f, 70f), shown, textStyle);
            GUI.Label(new Rect(rect.x, rect.yMax - 26f, rect.width - 18f, 22f), "E  siguiente", hintStyle);
        }

        private void EnsureStyles()
        {
            if (boxStyle != null) return;
            var background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0.05f, 0.02f, 0.08f, 0.85f));
            background.Apply();
            boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = background } };
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.35f, 0.75f) } };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true, normal = { textColor = Color.white } };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(1f, 1f, 1f, 0.5f) } };
            // Sin cambio de color ni de fondo al pasar el ratón por encima.
            boxStyle.hover.background = background;
            foreach (GUIStyle style in new[] { nameStyle, textStyle, hintStyle })
                UI.GameHUD.SetTextColor(style, style.normal.textColor);
        }
    }
}
