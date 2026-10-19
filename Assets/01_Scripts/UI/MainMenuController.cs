using System.Collections;
using System.Collections.Generic;
using Overdrive.Core;
using Overdrive.Level;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overdrive.UI
{
    /// <summary>
    /// Menú principal: animación de entrada, acciones de los botones y paneles
    /// (Ajustes / Selección de niveles) con fundido y vuelta al menú.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Botones principales (en orden)")]
        public NeonMenuButton playButton;
        [Tooltip("Solo aparece si hay una partida guardada.")]
        public NeonMenuButton continueButton;
        public NeonMenuButton settingsButton;
        public NeonMenuButton levelsButton;
        public NeonMenuButton quitButton;

        [Header("Paneles")]
        public CanvasGroup mainGroup;
        public CanvasGroup settingsPanel;
        public CanvasGroup levelsPanel;
        public NeonMenuButton[] backButtons;
        [Tooltip("Botones de niveles; cada uno carga levelScenes[i].")]
        public NeonMenuButton[] levelButtons;
        public string[] levelScenes = { "Level01" };

        [Header("Ajustes")]
        public Slider volumeSlider;
        public Slider musicSlider;
        public Slider sfxSlider;
        public Toggle fullscreenToggle;

        [Header("Escena")]
        public string gameScene = "Level01";

        [Header("Entrada")]
        public float entranceOffset = 420f;
        public float entranceDuration = 0.4f;
        public float entranceStagger = 0.08f;
        public float panelFade = 0.25f;
        public float buttonSpacing = 118f;

        private CanvasGroup _current;
        private NeonMenuButton _returnTo;
        private bool _busy;

        private NeonMenuButton[] main;

        private void Start()
        {
            Time.timeScale = 1f;
            GameInput.Blocked = false;
            LayoutMainButtons(continueButton != null && SaveSystem.HasSave);

            playButton.Clicked += _ => LoadScene(gameScene);
            if (continueButton) continueButton.Clicked += _ => Continue();
            settingsButton.Clicked += b => OpenPanel(settingsPanel, b);
            levelsButton.Clicked += b => OpenPanel(levelsPanel, b);
            quitButton.Clicked += _ => Quit();
            foreach (NeonMenuButton b in backButtons) b.Clicked += _ => ClosePanel();
            for (int i = 0; i < levelButtons.Length; i++)
            {
                string scene = i < levelScenes.Length ? levelScenes[i] : null;
                levelButtons[i].Clicked += _ => LoadScene(scene);
            }

            if (volumeSlider)
            {
                volumeSlider.SetValueWithoutNotify(AudioPrefs.Master);
                volumeSlider.onValueChanged.AddListener(v => AudioPrefs.Master = v);
            }
            if (musicSlider)
            {
                musicSlider.SetValueWithoutNotify(AudioPrefs.Music);
                musicSlider.onValueChanged.AddListener(v => AudioPrefs.Music = v);
            }
            if (sfxSlider)
            {
                sfxSlider.SetValueWithoutNotify(AudioPrefs.Sfx);
                sfxSlider.onValueChanged.AddListener(v => AudioPrefs.Sfx = v);
            }
            if (fullscreenToggle)
            {
                fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
                fullscreenToggle.onValueChanged.AddListener(v => Screen.fullScreen = v);
            }

            HideInstant(settingsPanel);
            HideInstant(levelsPanel);
            _current = mainGroup;
            StartCoroutine(Entrance());
        }

        private void Update()
        {
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back && _current != mainGroup && !_busy) ClosePanel();

            // Si se pierde la selección (clic en vacío), recuperarla para teclado/mando.
            EventSystem es = EventSystem.current;
            if (es && !_busy && (es.currentSelectedGameObject == null || !es.currentSelectedGameObject.activeInHierarchy))
            {
                Selectable first = _current ? _current.GetComponentInChildren<Selectable>() : null;
                if (first) NeonMenuButton.SelectQuietly(first.gameObject);
            }
        }

        private IEnumerator Entrance()
        {
            _busy = true;
            NeonMenuButton[] buttons = main;
            var groups = new CanvasGroup[buttons.Length];
            var basePos = new Vector2[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                groups[i] = Group(buttons[i]);
                groups[i].alpha = 0f;
                basePos[i] = ((RectTransform)buttons[i].transform).anchoredPosition;
            }
            mainGroup.interactable = false;

            float total = entranceDuration + entranceStagger * (buttons.Length - 1);
            for (float t = 0f; t < total + Time.unscaledDeltaTime; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    float k = Mathf.Clamp01((t - i * entranceStagger) / entranceDuration);
                    float e = 1f - Mathf.Pow(1f - k, 3f); // Ease Out
                    groups[i].alpha = e;
                    ((RectTransform)buttons[i].transform).anchoredPosition = basePos[i] + Vector2.left * entranceOffset * (1f - e);
                }
                yield return null;
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                groups[i].alpha = 1f;
                ((RectTransform)buttons[i].transform).anchoredPosition = basePos[i];
            }
            mainGroup.interactable = true;
            _busy = false;
            Select(playButton);
        }

        /// <summary>Coloca en columna los botones visibles (CONTINUAR va bajo JUGAR) y enlaza la navegación.</summary>
        private void LayoutMainButtons(bool showContinue)
        {
            var list = new List<NeonMenuButton> { playButton };
            if (continueButton)
            {
                continueButton.gameObject.SetActive(showContinue);
                if (showContinue) list.Add(continueButton);
            }
            list.Add(settingsButton);
            list.Add(levelsButton);
            list.Add(quitButton);
            main = list.ToArray();

            Vector2 top = ((RectTransform)playButton.transform).anchoredPosition;
            for (int i = 0; i < main.Length; i++)
            {
                var rt = (RectTransform)main[i].transform;
                rt.anchoredPosition = new Vector2(top.x, top.y - i * buttonSpacing);
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = main[(i - 1 + main.Length) % main.Length].GetComponent<Button>(),
                    selectOnDown = main[(i + 1) % main.Length].GetComponent<Button>(),
                };
                main[i].GetComponent<Button>().navigation = nav;
            }
        }

        private void Continue()
        {
            _busy = true;
            if (!SaveSystem.Continue())
            {
                _busy = false;
                Debug.LogWarning("No se pudo cargar la partida guardada.");
            }
        }

        private static CanvasGroup Group(Component c)
        {
            CanvasGroup g = c.GetComponent<CanvasGroup>();
            return g ? g : c.gameObject.AddComponent<CanvasGroup>();
        }

        private void OpenPanel(CanvasGroup panel, NeonMenuButton from)
        {
            if (_busy || !panel) return;
            _returnTo = from;
            StartCoroutine(Switch(mainGroup, panel, null));
        }

        private void ClosePanel()
        {
            if (_busy || _current == mainGroup) return;
            StartCoroutine(Switch(_current, mainGroup, _returnTo ? _returnTo : playButton));
        }

        private IEnumerator Switch(CanvasGroup from, CanvasGroup to, NeonMenuButton select)
        {
            _busy = true;
            from.interactable = false;
            from.blocksRaycasts = false;
            to.gameObject.SetActive(true);
            to.interactable = false;
            for (float t = 0f; t < panelFade; t += Time.unscaledDeltaTime)
            {
                float k = t / panelFade;
                from.alpha = 1f - k;
                to.alpha = k;
                yield return null;
            }
            from.alpha = 0f;
            if (from != mainGroup) from.gameObject.SetActive(false);
            to.alpha = 1f;
            to.interactable = true;
            to.blocksRaycasts = true;
            _current = to;
            _busy = false;

            if (select) Select(select);
            else
            {
                Selectable first = to.GetComponentInChildren<Selectable>();
                if (first) NeonMenuButton.SelectQuietly(first.gameObject);
            }
        }

        private static void HideInstant(CanvasGroup g)
        {
            if (!g) return;
            g.alpha = 0f;
            g.interactable = false;
            g.blocksRaycasts = false;
            g.gameObject.SetActive(false);
        }

        private static void Select(NeonMenuButton b)
        {
            if (b) NeonMenuButton.SelectQuietly(b.gameObject);
        }

        private void LoadScene(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogWarning($"La escena '{scene}' no está en Build Settings.");
                return;
            }
            _busy = true;
            // JUGAR and level selection start a fresh level checkpoint. Otherwise
            // CONTINUAR could reopen an older campaign after returning to the menu.
            SaveSystem.SaveCheckpoint(scene);
            SceneManager.LoadScene(scene);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
