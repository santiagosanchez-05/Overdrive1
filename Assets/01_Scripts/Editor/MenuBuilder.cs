using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Overdrive.Core;
using Overdrive.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Construye la escena Menu: fondo (06_Images/MenuImage), cuatro botones de neón blanco,
    /// paneles de Ajustes y Selección de niveles, EventSystem y Build Settings.
    /// Genera los sprites de los botones (paralelogramo, borde, glow, rayas, iconos) y los sonidos.
    /// Es idempotente: se puede ejecutar varias veces.
    /// </summary>
    public static class MenuBuilder
    {
        public const string ScenePath = "Assets/00_Scenes/Menu.unity";
        private const string BackgroundPath = "Assets/06_Images/MenuImage.png";
        private const string UiDir = "Assets/06_Images/UI";
        private const string AudioDir = "Assets/07_Audio";
        private const string LevelScenePath = "Assets/00_Scenes/Level01.unity";

        // Geometría del sprite del botón (px). El rect del botón mide lo mismo que la textura.
        private const int W = 720, H = 136, Pad = 24, Slant = 16, StripesW = 84;
        private static readonly Vector2 ButtonSize = new Vector2(W, H);
        private const float Spacing = 118f;

        private static readonly Color Fill = new Color32(0x08, 0x09, 0x0D, 0xC8);

        [MenuItem("TopDown/Construir menú principal")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            if (!EnsureTmpResources()) return;

            Sprites s = EnsureSprites();
            AudioClip hover = EnsureHoverClip();
            AudioClip click = EnsureClip("UI_Click.wav", 1250f, 0.09f, 0.5f);
            Sprite background = EnsureBackground();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "MenuCanvas" || root.GetComponent<EventSystem>()) UnityEngine.Object.DestroyImmediate(root);

            // EventSystem con el Input System (teclado, ratón y mando).
            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            esGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var audioGo = new GameObject("UIAudio", typeof(AudioSource));
            var audio = audioGo.GetComponent<AudioSource>();
            audio.playOnAwake = false;

            // Canvas que escala con la resolución (1920x1080 de referencia).
            var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // escala por altura: en ultrapanorámico los botones no crecen
            audioGo.transform.SetParent(canvasGo.transform, false);

            // Fondo: cubre toda la pantalla sin deformarse (Envelope).
            var backdrop = NewUI("Backdrop", canvasGo.transform);
            Stretch(backdrop);
            backdrop.gameObject.AddComponent<Image>().color = Color.black;
            var bg = NewUI("Background", canvasGo.transform);
            Stretch(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = background;
            bgImg.raycastTarget = false;
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = background.rect.width / background.rect.height;

            // Menú principal: columna a la izquierda.
            var main = NewUI("MainMenu", canvasGo.transform);
            Stretch(main);
            var mainGroup = main.gameObject.AddComponent<CanvasGroup>();
            var column = NewUI("Buttons", main);
            column.anchorMin = column.anchorMax = new Vector2(0f, 0.5f);
            column.pivot = new Vector2(0f, 0.5f);
            column.sizeDelta = new Vector2(W, Spacing * 4);
            column.anchoredPosition = new Vector2(48f, 110f);

            // CONTINUAR va bajo JUGAR; el controlador lo oculta (y sube el resto) si no hay partida guardada.
            string[] labels = { "JUGAR", "CONTINUAR", "AJUSTES", "SELECCIÓN DE NIVELES", "SALIR DEL JUEGO" };
            Sprite[] icons = { s.play, s.resume, s.gear, s.bars, s.exit };
            var mainButtons = new NeonMenuButton[labels.Length];
            for (int i = 0; i < labels.Length; i++)
                mainButtons[i] = CreateButton(column, labels[i], icons[i], new Vector2(W / 2f, (1.5f - i) * Spacing), s, audio, hover, click);
            WireVertical(mainButtons.Select(b => (Selectable)b.GetComponent<Button>()).ToArray());

            // Panel de ajustes.
            var settings = CreatePanel(canvasGo.transform, "SettingsPanel", "AJUSTES", 700f);
            var volume = CreateSlider(settings, "VOLUMEN", "VolumeSlider", new Vector2(0f, 150f), 1f);
            var music = CreateSlider(settings, "MÚSICA", "MusicSlider", new Vector2(0f, 75f));
            var sfx = CreateSlider(settings, "EFECTOS", "SfxSlider", new Vector2(0f, 0f));
            var fullscreen = CreateToggle(settings, "PANTALLA COMPLETA", new Vector2(0f, -75f));
            var backSettings = CreateButton(settings, "VOLVER", s.back, new Vector2(0f, -210f), s, audio, hover, click, true);
            WireVertical(new Selectable[] { volume, music, sfx, fullscreen, backSettings.GetComponent<Button>() });

            // Panel de selección de niveles.
            var levels = CreatePanel(canvasGo.transform, "LevelsPanel", "SELECCIÓN DE NIVELES");
            var level01 = CreateButton(levels, "NIVEL 01", s.play, new Vector2(0f, 90f), s, audio, hover, click, true);
            var level02 = CreateButton(levels, "NIVEL 02 · ELYSIUM", s.play, new Vector2(0f, -25f), s, audio, hover, click, true);
            var backLevels = CreateButton(levels, "VOLVER", s.back, new Vector2(0f, -150f), s, audio, hover, click, true);
            WireVertical(new Selectable[] { level01.GetComponent<Button>(), level02.GetComponent<Button>(), backLevels.GetComponent<Button>() });

            var controller = canvasGo.AddComponent<MainMenuController>();
            controller.playButton = mainButtons[0];
            controller.continueButton = mainButtons[1];
            controller.settingsButton = mainButtons[2];
            controller.levelsButton = mainButtons[3];
            controller.quitButton = mainButtons[4];
            controller.buttonSpacing = Spacing;
            controller.mainGroup = mainGroup;
            controller.settingsPanel = settings.GetComponent<CanvasGroup>();
            controller.levelsPanel = levels.GetComponent<CanvasGroup>();
            controller.backButtons = new[] { backSettings, backLevels };
            controller.levelButtons = new[] { level01, level02 };
            controller.levelScenes = new[] { "Level01", "Level02" };
            controller.volumeSlider = volume;
            controller.musicSlider = music;
            controller.sfxSlider = sfx;
            controller.fullscreenToggle = fullscreen;
            controller.gameScene = "Level01";

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EnsureBuildSettings();

            // El menú de pausa vive en Level01.
            var level = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
            AddPauseMenu(level);
            AddLevelMusic(level);
            EditorSceneManager.SaveScene(level);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            Debug.Log("[MenuBuilder] Escena Menu y menú de pausa construidos.");
        }

        /// <summary>
        /// Añade (o rehace) el menú de pausa en una escena de juego: REANUDAR, GUARDAR PARTIDA,
        /// volumen de la música y MENÚ PRINCIPAL. No guarda la escena.
        /// </summary>
        public static void AddPauseMenu(UnityEngine.SceneManagement.Scene scene)
        {
            Sprites s = EnsureSprites();
            AudioClip hover = EnsureHoverClip();
            AudioClip click = EnsureClip("UI_Click.wav", 1250f, 0.09f, 0.5f);

            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "PauseMenu") UnityEngine.Object.DestroyImmediate(root);

            bool hasEventSystem = scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<EventSystem>(true) != null);
            if (!hasEventSystem)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                EditorSceneManager.MoveGameObjectToScene(es, scene);
            }

            GameObject canvasGo = CreateCanvas("PauseMenu", 50);
            EditorSceneManager.MoveGameObjectToScene(canvasGo, scene);
            var audio = new GameObject("UIAudio", typeof(AudioSource)).GetComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.ignoreListenerPause = true;
            audio.transform.SetParent(canvasGo.transform, false);

            var panel = CreatePanel(canvasGo.transform, "PausePanel", "PAUSA", 740f);
            var status = SmallLabel(panel, "", new Vector2(0f, 215f));
            status.rectTransform.sizeDelta = new Vector2(820, 40);
            status.alignment = TextAlignmentOptions.Center;
            status.fontSize = 24;
            status.name = "Status";
            var resume = CreateButton(panel, "REANUDAR", s.play, new Vector2(0f, 135f), s, audio, hover, click, true);
            var save = CreateButton(panel, "GUARDAR PARTIDA", s.save, new Vector2(0f, 25f), s, audio, hover, click, true);
            var music = CreateSlider(panel, "MÚSICA", "MusicSlider", new Vector2(0f, -70f));
            var sfx = CreateSlider(panel, "EFECTOS", "SfxSlider", new Vector2(0f, -140f));
            var menu = CreateButton(panel, "MENÚ PRINCIPAL", s.exit, new Vector2(0f, -245f), s, audio, hover, click, true);
            WireVertical(new Selectable[] { resume.GetComponent<Button>(), save.GetComponent<Button>(), music, sfx, menu.GetComponent<Button>() });

            var controller = canvasGo.AddComponent<PauseMenuController>();
            controller.panel = panel.GetComponent<CanvasGroup>();
            controller.resumeButton = resume;
            controller.saveButton = save;
            controller.menuButton = menu;
            controller.musicSlider = music;
            controller.sfxSlider = sfx;
            controller.status = status;
            controller.menuScene = "Menu";
            panel.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("TopDown/Añadir menú de pausa a Level01")]
        public static void BuildPauseMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!EnsureTmpResources()) return;
            var level = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
            AddPauseMenu(level);
            AddLevelMusic(level);
            EditorSceneManager.SaveScene(level);
        }

        /// <summary>Pista de música de cada nivel (nombre de escena → archivo en 07_Audio).</summary>
        private static readonly Dictionary<string, string> LevelMusic = new Dictionary<string, string>
        {
            { "Level01", "DurantePartida.mp3" },
            { "Level02", "DurantePartida_Nvl2.mp3" },
        };

        /// <summary>
        /// Añade (o rehace) el objeto "Music" del nivel: AudioSource en bucle con la pista del nivel y
        /// <see cref="MusicVolume"/>, que aplica el ajuste de volumen de música. No guarda la escena.
        /// </summary>
        public static void AddLevelMusic(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "Music") UnityEngine.Object.DestroyImmediate(root);
            if (!LevelMusic.TryGetValue(scene.name, out string file)) return;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{file}");
            if (clip == null)
            {
                Debug.LogWarning($"[MenuBuilder] No se encontró la música {AudioDir}/{file}.");
                return;
            }

            var go = new GameObject("Music", typeof(AudioSource), typeof(MusicVolume));
            EditorSceneManager.MoveGameObjectToScene(go, scene);
            var source = go.GetComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.volume = 0.7f;
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static GameObject CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            return go;
        }

        // ---------------------------------------------------------------- UI

        private static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Image AddImage(RectTransform parent, string name, Sprite sprite, Color color, bool stretch = true)
        {
            var rt = NewUI(name, parent);
            if (stretch) Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static NeonMenuButton CreateButton(Transform parent, string text, Sprite iconSprite, Vector2 pos,
            Sprites s, AudioSource audio, AudioClip hover, AudioClip click, bool centered = false)
        {
            var rt = NewUI("Btn_" + text.Replace(" ", ""), parent);
            rt.anchorMin = rt.anchorMax = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
            rt.sizeDelta = ButtonSize;
            rt.anchoredPosition = pos;

            var glow = AddImage(rt, "Glow", s.glow, new Color(1, 1, 1, 0));
            var fill = AddImage(rt, "Fill", s.fill, Fill);
            fill.raycastTarget = true;
            var highlight = AddImage(rt, "Highlight", s.highlight, new Color(1, 1, 1, 0));
            var border = AddImage(rt, "Border", s.border, new Color32(0x9A, 0x9D, 0xA3, 0xFF));

            // Barrido de luz por el borde, recortado al área del botón.
            var mask = NewUI("SweepMask", rt);
            Stretch(mask);
            mask.offsetMin = new Vector2(Pad, Pad - 4);
            mask.offsetMax = new Vector2(-Pad - StripesW, -Pad + 4);
            mask.gameObject.AddComponent<RectMask2D>();
            RectTransform sweepTop = Sweep(mask, "SweepTop", s.sweep, H - 2 * Pad + 4);
            RectTransform sweepBottom = Sweep(mask, "SweepBottom", s.sweep, 4);

            var flash = AddImage(rt, "Flash", s.flash, new Color(1, 1, 1, 0));

            var icon = AddImage(rt, "Icon", iconSprite, Color.white, false);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(44, 44);
            icon.rectTransform.anchoredPosition = new Vector2(Pad + 62, 0);

            var labelRt = NewUI("Label", rt);
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(Pad + 118, Pad);
            labelRt.offsetMax = new Vector2(-Pad - StripesW - Slant * 2 - 14, -Pad); // antes del corte inclinado
            var label = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = text.Length > 14 ? 34 : 38;
            label.fontStyle = FontStyles.Bold | FontStyles.Italic | FontStyles.UpperCase;
            label.characterSpacing = 2f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true; // reduce la letra si no cabe
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = 22;
            label.raycastTarget = false;
            label.color = Color.white;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            rt.gameObject.AddComponent<CanvasGroup>();

            var neon = rt.gameObject.AddComponent<NeonMenuButton>();
            neon.fill = fill;
            neon.highlight = highlight;
            neon.border = border;
            neon.glow = glow;
            neon.icon = icon;
            neon.label = label;
            neon.sweepTop = sweepTop;
            neon.sweepBottom = sweepBottom;
            neon.flash = flash;
            neon.audioSource = audio;
            neon.hoverClip = hover;
            neon.clickClip = click;
            return neon;
        }

        private static RectTransform Sweep(RectTransform parent, string name, Sprite sprite, float y)
        {
            var img = AddImage(parent, name, sprite, Color.white, false);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(180, 10);
            rt.anchoredPosition = new Vector2(-200, y);
            rt.gameObject.SetActive(false);
            return rt;
        }

        private static RectTransform CreatePanel(Transform parent, string name, string title, float height = 560f)
        {
            var rt = NewUI(name, parent);
            Stretch(rt);
            rt.gameObject.AddComponent<CanvasGroup>();
            AddImage(rt, "Dim", null, new Color(0, 0, 0, 0.55f)).raycastTarget = true;

            var box = NewUI("Box", rt);
            box.sizeDelta = new Vector2(900, height);
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.color = Fill;
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(0x9A, 0x9D, 0xA3, 0xFF);
            outline.effectDistance = new Vector2(2, -2);

            var titleRt = NewUI("Title", rt);
            titleRt.sizeDelta = new Vector2(820, 80);
            titleRt.anchoredPosition = new Vector2(0, height / 2f - 80f);
            var t = titleRt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = title;
            t.fontSize = 46;
            t.fontStyle = FontStyles.Bold | FontStyles.Italic;
            t.characterSpacing = 3f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;
            return rt;
        }

        private static TextMeshProUGUI SmallLabel(RectTransform parent, string text, Vector2 pos)
        {
            var rt = NewUI("Label_" + text.Replace(" ", ""), parent);
            rt.sizeDelta = new Vector2(320, 50);
            rt.anchoredPosition = pos;
            var l = rt.gameObject.AddComponent<TextMeshProUGUI>();
            l.text = text;
            l.fontSize = 28;
            l.fontStyle = FontStyles.Bold | FontStyles.Italic;
            l.alignment = TextAlignmentOptions.MidlineLeft;
            l.color = Color.white;
            l.raycastTarget = false;
            return l;
        }

        /// <param name="value">Valor inicial en el editor; al jugar, el controlador pone el guardado.</param>
        private static Slider CreateSlider(RectTransform panel, string text, string name, Vector2 pos, float value = 0.5f)
        {
            SmallLabel(panel, text, pos + new Vector2(-230, 0));
            var res = StandardResources();
            var go = DefaultControls.CreateSlider(res);
            go.transform.SetParent(panel, false);
            go.name = name;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(340, 24);
            rt.anchoredPosition = pos + new Vector2(170, 0);
            var slider = go.GetComponent<Slider>();
            slider.value = value;
            foreach (Image img in go.GetComponentsInChildren<Image>())
                img.color = img.name == "Fill" || img.name == "Handle" ? Color.white : new Color(1, 1, 1, 0.15f);
            NeonColors(slider);
            return slider;
        }

        private static Toggle CreateToggle(RectTransform panel, string text, Vector2 pos)
        {
            SmallLabel(panel, text, pos + new Vector2(-230, 0));
            var go = DefaultControls.CreateToggle(StandardResources());
            go.transform.SetParent(panel, false);
            go.name = "FullscreenToggle";
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(40, 40);
            rt.anchoredPosition = pos + new Vector2(60, 0);
            UnityEngine.Object.DestroyImmediate(go.transform.Find("Label").gameObject);
            var bgRt = (RectTransform)go.transform.Find("Background");
            bgRt.sizeDelta = new Vector2(36, 36);
            bgRt.anchoredPosition = new Vector2(18, -18);
            bgRt.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            go.transform.Find("Background/Checkmark").GetComponent<Image>().color = Color.white;
            var toggle = go.GetComponent<Toggle>();
            NeonColors(toggle);
            return toggle;
        }

        private static void NeonColors(Selectable sel)
        {
            ColorBlock c = sel.colors;
            c.normalColor = new Color(0.75f, 0.75f, 0.78f);
            c.highlightedColor = Color.white;
            c.selectedColor = Color.white;
            c.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            sel.colors = c;
        }

        private static DefaultControls.Resources StandardResources() => new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
        };

        private static void WireVertical(Selectable[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = items[(i - 1 + items.Length) % items.Length],
                    selectOnDown = items[(i + 1) % items.Length],
                };
                items[i].navigation = nav;
            }
        }

        // ---------------------------------------------------------------- Recursos

        private static bool EnsureTmpResources()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;
            string pkg = Directory.GetDirectories("Library/PackageCache", "com.unity.ugui@*")
                .Select(d => Path.Combine(d, "Package Resources", "TMP Essential Resources.unitypackage"))
                .FirstOrDefault(File.Exists);
            if (pkg == null)
            {
                Debug.LogError("[MenuBuilder] Importa 'TMP Essential Resources' (Window > TextMeshPro) y vuelve a ejecutar.");
                return false;
            }
            AssetDatabase.importPackageCompleted += OnTmpImported;
            AssetDatabase.ImportPackage(pkg, false);
            Debug.Log("[MenuBuilder] Importando TMP Essential Resources; el menú se construirá al terminar.");
            return false;
        }

        private static void OnTmpImported(string name)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            EditorApplication.delayCall += Build;
        }

        private static Sprite EnsureBackground()
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(BackgroundPath);
            if (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single || imp.mipmapEnabled)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.maxTextureSize = 4096;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        }

        private class Sprites
        {
            public Sprite fill, highlight, border, glow, stripes, flash, sweep;
            public Sprite play, gear, bars, exit, back, resume, save;
        }

        private static Sprites EnsureSprites()
        {
            Directory.CreateDirectory(UiDir);
            Vector2[] shape =
            {
                new Vector2(Pad, Pad),
                new Vector2(Pad + Slant, H - Pad),
                new Vector2(W - Pad - StripesW, H - Pad),
                new Vector2(W - Pad - StripesW - Slant * 2, Pad),
            };
            float ShapeD(float x, float y) => PolyDistance(shape, x, y);

            float StripesA(float x, float y)
            {
                if (y < Pad + 6 || y > H - Pad - 6) return 0f;
                float a = 0f;
                for (int i = 0; i < 3; i++)
                {
                    float x0 = W - Pad - StripesW + 14 + i * 22;
                    // Banda inclinada paralela al corte derecho (2*Slant por altura del botón).
                    float t = (y - Pad) / (H - 2 * Pad);
                    float cx = x0 - Slant * 2 * (1 - t) + 4;
                    float d = Mathf.Abs(x - cx) - (i == 0 ? 3f : 2f);
                    a = Mathf.Max(a, Mathf.Clamp01(0.5f - d) * (1f - i * 0.25f));
                }
                return a;
            }

            var s = new Sprites
            {
                fill = Gen("Btn_Fill", W, H, (x, y) => Mathf.Clamp01(0.5f - ShapeD(x, y))),
                highlight = Gen("Btn_Highlight", W, H, (x, y) =>
                {
                    float inside = Mathf.Clamp01(0.5f - ShapeD(x, y));
                    float h = 1f - Mathf.Clamp01((x - Pad) / (W * 0.75f));
                    float v = 0.4f + 0.6f * Mathf.Clamp01((y - Pad) / (H - 2f * Pad));
                    return inside * h * v;
                }),
                border = Gen("Btn_Border", W, H, (x, y) => Mathf.Clamp01(1.75f - Mathf.Abs(ShapeD(x, y) + 0.5f))),
                glow = Gen("Btn_Glow", W, H, (x, y) =>
                {
                    float d = ShapeD(x, y);
                    float outer = Mathf.Exp(-Mathf.Max(d, 0f) / 7f) * 0.85f;
                    float inner = Mathf.Exp(Mathf.Min(d, 0f) / 5f) * 0.35f;
                    return d > 0 ? outer : inner;
                }),
                stripes = Gen("Btn_Stripes", W, H, StripesA),
                flash = Gen("Btn_Flash", W, H, (x, y) =>
                {
                    float d = ShapeD(x, y);
                    return Mathf.Clamp01(3.5f - Mathf.Abs(d)) + Mathf.Exp(-Mathf.Abs(d) / 10f) * 0.6f;
                }),
                sweep = Gen("Btn_Sweep", 180, 10, (x, y) =>
                {
                    float u = x / 179f;
                    float v = Mathf.Abs(y - 4.5f) / 4.5f;
                    return Mathf.Pow(Mathf.Sin(u * Mathf.PI), 2f) * Mathf.Pow(u, 0.6f) * Mathf.Clamp01(1f - v * v);
                }),
                play = Icon("Icon_Play", (x, y) => Tri(x, y, new Vector2(16, 10), new Vector2(16, 54), new Vector2(52, 32))),
                gear = Icon("Icon_Gear", (x, y) =>
                {
                    float dx = x - 32, dy = y - 32;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float tooth = Mathf.Cos(ang * 8f) > 0.15f ? 27f : 21f;
                    return r <= tooth && r >= 9f;
                }),
                bars = Icon("Icon_Bars", (x, y) =>
                    (x >= 10 && x <= 20 && y >= 8 && y <= 34) ||
                    (x >= 27 && x <= 37 && y >= 8 && y <= 56) ||
                    (x >= 44 && x <= 54 && y >= 8 && y <= 44)),
                exit = Icon("Icon_Exit", (x, y) =>
                {
                    bool frame = x >= 10 && x <= 42 && y >= 6 && y <= 58 && !(x > 15 && x < 37 && y > 11 && y < 53);
                    bool gap = x > 36 && y > 20 && y < 44;
                    bool shaft = x >= 22 && x <= 46 && y >= 29 && y <= 35;
                    bool head = Tri(x, y, new Vector2(44, 20), new Vector2(44, 44), new Vector2(58, 32));
                    return (frame && !gap) || shaft || head;
                }),
                resume = Icon("Icon_Resume", (x, y) =>
                    (x >= 10 && x <= 18 && y >= 10 && y <= 54) || Tri(x, y, new Vector2(26, 10), new Vector2(26, 54), new Vector2(56, 32))),
                save = Icon("Icon_Save", (x, y) =>
                {
                    // Disquete: cuerpo con esquina cortada, etiqueta hueca arriba con una muesca.
                    bool body = x >= 8 && x <= 56 && y >= 8 && y <= 56 && (x - 48) + (y - 48) <= 8;
                    bool label = x >= 18 && x <= 46 && y >= 36 && y <= 52;
                    bool notch = x >= 36 && x <= 42 && y >= 40 && y <= 50;
                    bool slot = x >= 18 && x <= 46 && y >= 12 && y <= 28 && !(x >= 21 && x <= 43 && y >= 15 && y <= 25);
                    return (body && !label && !(x >= 21 && x <= 43 && y >= 15 && y <= 25)) || notch || (body && slot);
                }),
                back = Icon("Icon_Back", (x, y) => Tri(x, y, new Vector2(48, 10), new Vector2(48, 54), new Vector2(12, 32))),
            };
            return s;
        }

        private static float PolyDistance(Vector2[] poly, float x, float y)
        {
            // Polígono convexo en sentido horario: distancia firmada (negativa dentro).
            float d = float.MinValue;
            var p = new Vector2(x, y);
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                Vector2 e = (b - a).normalized;
                Vector2 n = new Vector2(-e.y, e.x); // normal exterior para orden horario
                d = Mathf.Max(d, Vector2.Dot(p - a, n));
            }
            return d;
        }

        private static bool Tri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            float S(Vector2 p1, Vector2 p2) => (x - p2.x) * (p1.y - p2.y) - (p1.x - p2.x) * (y - p2.y);
            float d1 = S(a, b), d2 = S(b, c), d3 = S(c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        private static Sprite Icon(string name, Func<float, float, bool> inside)
        {
            const int n = 4;
            return Gen(name, 64, 64, (x, y) =>
            {
                int hits = 0;
                for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (inside(x - 0.5f + (i + 0.5f) / n, y - 0.5f + (j + 0.5f) / n)) hits++;
                return hits / (float)(n * n);
            });
        }

        private static Sprite Gen(string name, int w, int h, Func<float, float, float> alpha)
        {
            string path = $"{UiDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f)));
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Hover "neón": clic suave + tono brillante que sube + golpe grave corto (110 ms).</summary>
        private static AudioClip EnsureHoverClip()
        {
            Directory.CreateDirectory(AudioDir);
            string path = $"{AudioDir}/UI_Hover.wav";
            if (!File.Exists(path))
            {
                const int rate = 44100;
                const float length = 0.11f;
                var rng = new System.Random(7);
                int n = (int)(rate * length);
                var samples = new short[n];
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)rate;
                    float attack = 1f - Mathf.Exp(-t / 0.003f);
                    float phase = 2f * Mathf.PI * (1500f * t + 450f * t * t / length);
                    float tone = (0.55f * Mathf.Sin(phase) + 0.25f * Mathf.Sin(2f * phase) * Mathf.Exp(-t / 0.015f)) * attack * Mathf.Exp(-t / 0.028f);
                    float low = 0.35f * Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t / 0.04f) * attack;
                    float click = 0.15f * ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t / 0.002f);
                    samples[i] = (short)(Mathf.Clamp((tone + low + click) * 0.5f, -1f, 1f) * short.MaxValue);
                }
                WriteWav(path, samples, rate);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void WriteWav(string path, short[] samples, int rate)
        {
            int n = samples.Length;
            using (var bw = new BinaryWriter(File.Create(path)))
            {
                bw.Write("RIFF".ToCharArray()); bw.Write(36 + n * 2); bw.Write("WAVE".ToCharArray());
                bw.Write("fmt ".ToCharArray()); bw.Write(16); bw.Write((short)1); bw.Write((short)1);
                bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
                bw.Write("data".ToCharArray()); bw.Write(n * 2);
                foreach (short sm in samples) bw.Write(sm);
            }
        }

        private static AudioClip EnsureClip(string file, float freq, float length, float gain)
        {
            Directory.CreateDirectory(AudioDir);
            string path = $"{AudioDir}/{file}";
            if (!File.Exists(path))
            {
                const int rate = 44100;
                int n = (int)(rate * length);
                var samples = new short[n];
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)rate;
                    float env = Mathf.Exp(-t / (length * 0.25f)) * Mathf.Clamp01(t * 2000f);
                    float f = freq * (1f + 0.6f * (1f - t / length));
                    float v = Mathf.Sin(2f * Mathf.PI * f * t) * 0.7f + Mathf.Sin(4f * Mathf.PI * f * t) * 0.3f;
                    samples[i] = (short)(Mathf.Clamp(v * env * gain, -1f, 1f) * short.MaxValue);
                }
                using (var bw = new BinaryWriter(File.Create(path)))
                {
                    bw.Write("RIFF".ToCharArray()); bw.Write(36 + n * 2); bw.Write("WAVE".ToCharArray());
                    bw.Write("fmt ".ToCharArray()); bw.Write(16); bw.Write((short)1); bw.Write((short)1);
                    bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
                    bw.Write("data".ToCharArray()); bw.Write(n * 2);
                    foreach (short sm in samples) bw.Write(sm);
                }
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void EnsureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Count > 0 && scenes[0].path == ScenePath && scenes[0].enabled) return;
            scenes.RemoveAll(sc => sc.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
