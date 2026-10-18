using Overdrive.Core;
using Overdrive.Level;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overdrive.Story
{
    public sealed class EndingScreen : MonoBehaviour
    {
        [SerializeField] private EndingContent content;
        [SerializeField] private CanvasGroup screen, epilogue, credits, thanks;
        [SerializeField] private TMP_Text storyText, creditText, chapterText, hintText, skipText;
        [SerializeField] private RectTransform creditViewport;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private MusicVolume musicVolume;
        private float aliveTime, creditHeight;
        private bool loadedMenu;

        public EndingTimeline Timeline { get; private set; }
        public EndingContent Content => content;

        private void Awake()
        {
            Time.timeScale = 1f;
            GameInput.Blocked = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            creditText.text = content.credits;
            Canvas.ForceUpdateCanvases();
            creditHeight = creditText.preferredHeight + 120f;
            creditText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, creditHeight);
            float seconds = Mathf.Max(content.minimumCreditsSeconds, (creditHeight + creditViewport.rect.height) / 65f);
            Timeline = new EndingTimeline(content.epilogue?.Length ?? 0, content.cardSeconds, seconds);
            Present();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            aliveTime += dt;
            Timeline.Tick(dt);
            // Ignore the key which opened this scene and rapid repeats across the final card.
            if (aliveTime > .6f && Timeline.Elapsed > .35f)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Skip();
                else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Timeline.Advance();
            }
            Present();
            if (Timeline.Phase == EndingPhase.Leaving && Timeline.Elapsed >= .7f && !loadedMenu)
            {
                loadedMenu = true;
                SaveSystem.SaveCheckpoint("Menu");
                Time.timeScale = 1f;
                GameInput.Blocked = false;
                SceneManager.LoadScene("Menu");
            }
        }

        public void Skip()
        {
            if (Timeline.Phase == EndingPhase.Epilogue) Timeline.SkipEpilogue();
            else Timeline.Advance();
        }

        public void ReturnToMenu() => Timeline.RequestReturn();

        private void Present()
        {
            var phase = Timeline.Phase;
            float fade = Mathf.Clamp01(Timeline.Elapsed / .7f);
            screen.alpha = phase == EndingPhase.Leaving ? 1f - fade : Mathf.Clamp01(aliveTime / 1.1f);
            if (musicVolume != null) musicVolume.baseVolume = .5f * screen.alpha;
            if (phase == EndingPhase.Leaving) return;
            SetVisible(epilogue, phase == EndingPhase.Epilogue);
            SetVisible(credits, phase == EndingPhase.Credits);
            SetVisible(thanks, phase == EndingPhase.Thanks);
            chapterText.text = phase == EndingPhase.Epilogue ? "EPÍLOGO / PISO FRANCO" : "FIN DE LA HUIDA / OVERDRIVE";
            if (phase == EndingPhase.Epilogue)
            {
                storyText.text = content.epilogue[Timeline.Card];
                epilogue.alpha = Mathf.Min(fade, Mathf.Clamp01((content.cardSeconds - Timeline.Elapsed) / .7f));
            }
            if (phase == EndingPhase.Credits)
            {
                credits.alpha = fade;
                creditText.rectTransform.anchoredPosition = new Vector2(0f,
                    Mathf.Lerp(-creditViewport.rect.height, creditHeight, Timeline.CreditsProgress));
            }
            hintText.text = phase == EndingPhase.Thanks ? "ESPACIO / ENTER · VOLVER AL MENÚ" : "ESPACIO / ENTER · AVANZAR";
            skipButton.gameObject.SetActive(phase == EndingPhase.Epilogue || phase == EndingPhase.Credits);
            skipText.text = phase == EndingPhase.Epilogue ? "VER CRÉDITOS" : "FINALIZAR CRÉDITOS";
            if (menuButton != null)
                ((RectTransform)menuButton.transform).anchoredPosition = new Vector2(phase == EndingPhase.Thanks ? 0f : 285f, -423f);
        }

        private static void SetVisible(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
