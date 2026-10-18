using Overdrive.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Overdrive.CameraRig
{
    /// <summary>
    /// Mira blanca: sigue al ratón en la vista cenital y queda fija en el centro en primera persona.
    /// Se oculta en pausa. La crea <see cref="FirstPersonCamera"/>.
    /// </summary>
    public class Crosshair : MonoBehaviour
    {
        public static Crosshair Instance { get; private set; }

        [SerializeField] private float size = 30f;
        [SerializeField] private float thickness = 3f;
        [SerializeField] private float gap = 6f;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private Color outline = new Color(0f, 0f, 0f, 0.8f);

        private RectTransform root;
        private Canvas canvas;

        private void Awake()
        {
            Instance = this;
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40; // por debajo del menú de pausa
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            root = new GameObject("Reticle", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.sizeDelta = Vector2.one * size;

            float arm = (size - gap) / 2f;
            float offset = gap / 2f + arm / 2f;
            Bar(new Vector2(0f, offset), new Vector2(thickness, arm));
            Bar(new Vector2(0f, -offset), new Vector2(thickness, arm));
            Bar(new Vector2(offset, 0f), new Vector2(arm, thickness));
            Bar(new Vector2(-offset, 0f), new Vector2(arm, thickness));
            Bar(Vector2.zero, new Vector2(thickness, thickness));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Bar(Vector2 position, Vector2 barSize)
        {
            var rt = new GameObject("Bar", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(root, false);
            rt.anchoredPosition = position;
            rt.sizeDelta = barSize;
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var shadow = rt.gameObject.AddComponent<Outline>();
            shadow.effectColor = outline;
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void LateUpdate()
        {
            bool show = !GameInput.Blocked;
            root.gameObject.SetActive(show);
            if (!show) return;
            Vector2 screen = ViewMode.FirstPerson
                ? new Vector2(Screen.width / 2f, Screen.height / 2f)
                : (Vector2)GameInput.MousePosition;
            // De píxeles de pantalla a unidades del canvas.
            root.anchoredPosition = screen / canvas.scaleFactor;
        }
    }
}
