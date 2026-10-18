using System.Collections;
using Overdrive.Core;
using Overdrive.Level;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overdrive.UI
{
    /// <summary>
    /// Menú de pausa (Esc o P): reanudar, guardar la partida, volumen de la música y volver al menú.
    /// Mientras está abierto el tiempo se detiene y los controles de juego se bloquean.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Partes")]
        public CanvasGroup panel;
        public NeonMenuButton resumeButton;
        public NeonMenuButton saveButton;
        public NeonMenuButton menuButton;
        public Slider musicSlider;
        public Slider sfxSlider;
        public TMP_Text status;

        [Header("Escena")]
        public string menuScene = "Menu";

        [Header("Animación")]
        public float fade = 0.2f;
        public float statusTime = 2.5f;

        private LevelManager level;
        private bool open;
        private bool leaving;
        private Coroutine fadeRoutine;
        private Coroutine statusRoutine;
        private bool completeHandled;

        public bool IsOpen => open;

        private void Awake()
        {
            GameInput.Blocked = false;
            Time.timeScale = 1f;
            Hide();
        }

        private void Start()
        {
            level = FindAnyObjectByType<LevelManager>();
            resumeButton.Clicked += _ => Resume();
            saveButton.Clicked += _ => SaveGame();
            menuButton.Clicked += _ => GoToMenu();
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
        }

        private void OnDestroy()
        {
            GameInput.Blocked = false;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            // Nivel superado: la partida guardada ya no tiene sentido.
            if (!completeHandled && level != null && level.Progress.State == LevelState.Complete)
            {
                completeHandled = true;
                SaveSystem.Delete();
            }

            if (leaving || !GameInput.PausePressed) return;
            if (open) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (open || leaving) return;
            open = true;
            Time.timeScale = 0f;
            GameInput.Blocked = true;
            if (status) status.text = "";
            if (musicSlider) musicSlider.SetValueWithoutNotify(AudioPrefs.Music);
            if (sfxSlider) sfxSlider.SetValueWithoutNotify(AudioPrefs.Sfx);
            panel.gameObject.SetActive(true);
            Fade(1f);
            NeonMenuButton.SelectQuietly(resumeButton.gameObject);
        }

        public void Resume()
        {
            if (!open) return;
            open = false;
            Time.timeScale = 1f;
            GameInput.Blocked = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            Fade(0f);
        }

        private void SaveGame()
        {
            bool canSave = level != null && level.Player != null && !level.Player.IsDead
                           && level.Progress.State != LevelState.Complete && level.Progress.State != LevelState.Failed;
            string msg = !canSave ? "NO SE PUEDE GUARDAR AHORA"
                : SaveSystem.Save() ? "PARTIDA GUARDADA · " + SaveSystem.SavedAt
                : "ERROR AL GUARDAR";
            if (statusRoutine != null) StopCoroutine(statusRoutine);
            statusRoutine = StartCoroutine(ShowStatus(msg));
        }

        private IEnumerator ShowStatus(string msg)
        {
            if (!status) yield break;
            status.text = msg;
            yield return new WaitForSecondsRealtime(statusTime);
            status.text = "";
        }

        private void GoToMenu()
        {
            leaving = true;
            Time.timeScale = 1f;
            GameInput.Blocked = false;
            SceneManager.LoadScene(menuScene);
        }

        private void Hide()
        {
            if (!panel) return;
            panel.alpha = 0f;
            panel.interactable = false;
            panel.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
        }

        private void Fade(float target)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeTo(target));
        }

        private IEnumerator FadeTo(float target)
        {
            panel.interactable = target > 0f;
            panel.blocksRaycasts = target > 0f;
            float start = panel.alpha;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                panel.alpha = Mathf.Lerp(start, target, t / fade);
                yield return null;
            }
            panel.alpha = target;
            if (target <= 0f) panel.gameObject.SetActive(false);
        }
    }
}
