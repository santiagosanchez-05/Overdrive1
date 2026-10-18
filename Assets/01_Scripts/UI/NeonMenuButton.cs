using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overdrive.UI
{
    /// <summary>
    /// Botón de menú con neón blanco: hover, selección (ratón, teclado o mando), clic,
    /// barrido de luz por el borde, respiración del brillo y sonido.
    /// Todas las partes visuales son Images/TMP hijas asignadas desde el Inspector.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class NeonMenuButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [Header("Partes")]
        public Image fill;
        public Image highlight;
        public Image border;
        public Image glow;
        public Image stripes;
        public Image icon;
        public TMP_Text label;
        public RectTransform sweepTop;
        public RectTransform sweepBottom;
        public Image flash;

        [Header("Colores")]
        public Color fillColor = new Color32(0x08, 0x09, 0x0D, 0xC8);
        public Color borderNormal = new Color32(0x9A, 0x9D, 0xA3, 0xFF);
        public Color neon = Color.white;
        public Color textNormal = new Color(1f, 1f, 1f, 0.82f);
        [Tooltip("Sin uso: el texto ya no cambia de color al pasar el ratón (se conserva para no perder el valor guardado).")]
        public Color textActive = Color.white;
        [Range(0f, 1f)] public float stripesNormalAlpha = 0.25f;
        [Range(0f, 1f)] public float highlightMaxAlpha = 0.18f;

        [Header("Brillo")]
        [Range(0f, 1f)] public float glowHoverAlpha = 0.7f;
        [Range(0f, 1f)] public float glowSelectedAlpha = 1f;
        [Range(0f, 0.5f)] public float breathAmount = 0.15f;
        public float breathSpeed = 1.6f;

        [Header("Animación")]
        [Range(0.05f, 0.5f)] public float transitionTime = 0.25f;
        public float hoverScale = 1.03f;
        public float pressScale = 0.97f;
        public float pressTime = 0.08f;
        public float sweepDuration = 0.45f;
        public Vector2 sweepInterval = new Vector2(2.5f, 4.5f);

        [Header("Sonido")]
        public AudioSource audioSource;
        public AudioClip hoverClip;
        public AudioClip clickClip;
        [Range(0f, 1f)] public float volume = 0.6f;

        /// <summary>Se lanza tras la animación de pulsado.</summary>
        public event Action<NeonMenuButton> Clicked;

        public bool IsSelected { get; private set; }

        private Button _button;
        private RectTransform _rt;
        private bool _hovered;
        private float _intensity;      // 0 normal, 1 encendido
        private float _pressTimer = -1f;
        private float _flashAlpha;
        private float _sweepT = -1f;
        private float _nextSweep;
        private bool _clickPending;
        private static bool _quiet;

        /// <summary>
        /// Selecciona un objeto desde código (al abrir un panel, la pausa...) sin sonido de hover:
        /// el hover solo suena cuando el jugador se mueve por los botones.
        /// </summary>
        public static void SelectQuietly(GameObject go)
        {
            if (!EventSystem.current) return;
            _quiet = true;
            try { EventSystem.current.SetSelectedGameObject(go); }
            finally { _quiet = false; }
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            _rt = (RectTransform)transform;
            _button.transition = Selectable.Transition.None;
            _button.onClick.AddListener(OnButtonClick);
            Apply(0f);
        }

        private void OnEnable()
        {
            _hovered = false;
            _pressTimer = -1f;
            _clickPending = false;
            transform.localScale = Vector3.one;
        }

        private float Target => IsSelected ? 1f : (_hovered ? 0.85f : 0f);

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _intensity = Mathf.MoveTowards(_intensity, Target, dt / transitionTime);

            // Escala: hover/selección -> 1.03, pulsado -> 0.97 y vuelta suave.
            float scale = Mathf.Lerp(1f, hoverScale, Ease(_intensity));
            if (_pressTimer >= 0f)
            {
                _pressTimer += dt;
                float down = Mathf.Clamp01(_pressTimer / pressTime);
                float up = Mathf.Clamp01((_pressTimer - pressTime) / (transitionTime * 0.8f));
                float pressed = Mathf.Lerp(scale, pressScale, down);
                scale = Mathf.Lerp(pressed, scale, Ease(up));
                if (_clickPending && _pressTimer >= pressTime)
                {
                    _clickPending = false;
                    Clicked?.Invoke(this);
                }
                if (up >= 1f) _pressTimer = -1f;
            }
            _rt.localScale = new Vector3(scale, scale, 1f);

            // Línea de luz que recorre el borde: al entrar y, si está seleccionado, de vez en cuando.
            if (IsSelected && _sweepT < 0f && Time.unscaledTime >= _nextSweep) StartSweep();
            if (_sweepT >= 0f)
            {
                _sweepT += dt / sweepDuration;
                if (_sweepT >= 1f) _sweepT = -1f;
            }

            _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, dt / 0.25f);
            Apply(dt);
        }

        private void Apply(float dt)
        {
            float k = Ease(_intensity);
            float breath = IsSelected ? 1f - breathAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * breathSpeed * Mathf.PI)) : 1f;

            if (fill) fill.color = fillColor;
            if (highlight) highlight.color = WithAlpha(neon, highlightMaxAlpha * k * breath);
            if (border) border.color = Color.Lerp(borderNormal, neon, k);
            if (glow)
            {
                float target = IsSelected ? glowSelectedAlpha : glowHoverAlpha;
                glow.color = WithAlpha(neon, target * k * breath);
            }
            if (stripes) stripes.color = WithAlpha(neon, Mathf.Lerp(stripesNormalAlpha, 1f, k));
            // El texto y el icono no cambian de color con el ratón: el hover se nota en el borde y el brillo.
            if (icon) icon.color = textNormal;
            if (label) label.color = textNormal;
            if (flash) flash.color = WithAlpha(neon, _flashAlpha);

            UpdateSweep(sweepTop);
            UpdateSweep(sweepBottom);
        }

        private void UpdateSweep(RectTransform sweep)
        {
            if (!sweep) return;
            bool on = _sweepT >= 0f;
            sweep.gameObject.SetActive(on);
            if (!on) return;
            float w = _rt.rect.width;
            float x = Mathf.Lerp(-sweep.rect.width, w + sweep.rect.width, Ease(_sweepT));
            sweep.anchoredPosition = new Vector2(x, sweep.anchoredPosition.y);
        }

        private void StartSweep()
        {
            _sweepT = 0f;
            _nextSweep = Time.unscaledTime + sweepDuration + UnityEngine.Random.Range(sweepInterval.x, sweepInterval.y);
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        private void Play(AudioClip clip)
        {
            if (audioSource && clip) audioSource.PlayOneShot(clip, volume);
        }

        // --- Eventos ---

        public void OnPointerEnter(PointerEventData e)
        {
            if (!_hovered && _button.IsInteractable()) Play(hoverClip);
            _hovered = true;
            if (_sweepT < 0f) StartSweep();
            // Pasar el ratón también selecciona: solo hay un botón activo a la vez.
            if (_button.IsInteractable() && EventSystem.current) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnPointerExit(PointerEventData e) => _hovered = false;

        public void OnPointerDown(PointerEventData e) { }

        public void OnSelect(BaseEventData e)
        {
            // Con teclado o mando también suena; con el ratón ya sonó al entrar (y _hovered está activo).
            if (!IsSelected && !_hovered && !_quiet) Play(hoverClip);
            IsSelected = true;
            if (_sweepT < 0f) StartSweep();
        }

        public void OnDeselect(BaseEventData e) => IsSelected = false;

        public void OnSubmit(BaseEventData e) { } // Button.onClick se encarga.

        private void OnButtonClick()
        {
            if (_clickPending) return;
            _pressTimer = 0f;
            _flashAlpha = 1f;
            _clickPending = true;
            Play(clickClip);
        }
    }
}
