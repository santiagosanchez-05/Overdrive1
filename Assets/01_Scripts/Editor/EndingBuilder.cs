using System;
using System.IO;
using System.Linq;
using Overdrive.Driving;
using Overdrive.Core;
using Overdrive.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    public static class EndingBuilder
    {
        public const string ScenePath = "Assets/00_Scenes/Ending.unity";
        public const string ContentPath = "Assets/00_Scenes/Ending_Data/EndingContent.asset";
        private static readonly Color Cyan = new Color32(39, 216, 235, 255);
        private static readonly Color Pink = new Color32(255, 43, 125, 255);
        public static void RunPlayTest() => EndingPlayTest.Run();

        [MenuItem("TopDown/Final/Crear o actualizar cierre y créditos")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode antes de crear el final.");
            var level = SceneManager.GetSceneByPath("Assets/00_Scenes/Level04.unity");
            var previous = SceneManager.GetSceneByPath(ScenePath);
            if (level.isLoaded && level.isDirty || previous.isLoaded && previous.isDirty)
                throw new InvalidOperationException("Guarda los cambios de Level04 y Ending antes de actualizar el cierre.");
            var content = EnsureContent();
            var scene = previous.isLoaded ? previous : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            var camera = new GameObject("Ending Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            var events = new GameObject("Ending EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var canvasObject = new GameObject("Ending Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            var canvas = canvasObject.transform;
            var background = Panel("Night", canvas, new Color32(6, 8, 17, 255));
            Stretch(background);
            var screen = Group("Closing Sequence", canvas);
            DrawBackdrop(screen.transform);
            var top = Text("Chapter", screen.transform, "EPÍLOGO / PISO FRANCO", 23f, Cyan, new Vector2(0f, 432f), new Vector2(1200f, 44f));
            top.characterSpacing = 4f;
            var logo = Text("Overdrive", screen.transform, "OVERDRIVE", 88f, Pink, new Vector2(0f, 330f), new Vector2(1280f, 135f));
            logo.font = EnsureTitleFont() ?? logo.font;
            var shadow = logo.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, .65f);
            shadow.effectDistance = new Vector2(3f, -3f);
            var epilogue = Group("Epilogue", screen.transform);
            var story = Text("Story", epilogue.transform, content.epilogue[0], 43f, Color.white, new Vector2(0f, 10f), new Vector2(1220f, 390f));
            story.lineSpacing = 18f;
            var credits = Group("Credits", screen.transform);
            var viewport = Rect("Credit Viewport", credits.transform, Vector2.zero, new Vector2(1280f, 500f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var creditText = Text("Credit Roll", viewport, content.credits, 37f, Color.white, Vector2.zero, new Vector2(1200f, 1500f));
            creditText.alignment = TextAlignmentOptions.Top;
            creditText.lineSpacing = 12f;
            creditText.rectTransform.anchorMin = creditText.rectTransform.anchorMax = new Vector2(.5f, 1f);
            creditText.rectTransform.pivot = new Vector2(.5f, 1f);
            creditText.rectTransform.anchoredPosition = Vector2.zero;
            credits.alpha = 0f;
            var thanks = Group("Thanks", screen.transform);
            Text("Thank You", thanks.transform, "GRACIAS POR JUGAR", 68f, Color.white, new Vector2(0f, 52f), new Vector2(1280f, 130f));
            Text("Last Line", thanks.transform, "La noche termina. La historia se queda contigo.", 28f, Cyan, new Vector2(0f, -70f), new Vector2(1250f, 70f));
            thanks.alpha = 0f;
            var hint = Text("Keyboard Hint", screen.transform, "ESPACIO / ENTER · AVANZAR", 20f, new Color(.62f, .66f, .76f), new Vector2(0f, -330f), new Vector2(1200f, 44f));
            var skip = Button("Skip", screen.transform, "VER CRÉDITOS", new Vector2(-285f, -423f), Cyan);
            var menu = Button("Menu", screen.transform, "VOLVER AL MENÚ", new Vector2(285f, -423f), Pink);
            var musicObject = new GameObject("Ending Music", typeof(AudioSource), typeof(MusicVolume));
            var audio = musicObject.GetComponent<AudioSource>();
            audio.clip = content.music;
            audio.loop = true;
            audio.spatialBlend = 0f;
            audio.volume = .5f;
            var ending = canvasObject.AddComponent<EndingScreen>();
            foreach (var reference in new (string field, Object value)[]
            {
                ("content", content), ("screen", screen), ("epilogue", epilogue), ("credits", credits), ("thanks", thanks),
                ("storyText", story), ("creditText", creditText), ("chapterText", top), ("hintText", hint),
                ("skipButton", skip), ("menuButton", menu), ("skipText", skip.GetComponentInChildren<TMP_Text>()), ("creditViewport", viewport),
                ("musicVolume", musicObject.GetComponent<MusicVolume>())
            }) BuilderUtil.SetRef(ending, reference.field, reference.value);
            UnityEventTools.AddPersistentListener(skip.onClick, ending.Skip);
            UnityEventTools.AddPersistentListener(menu.onClick, ending.ReturnToMenu);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            int index = scenes.FindIndex(s => s.path == ScenePath);
            if (index < 0) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else scenes[index].enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
            bool openedLevel = !level.isLoaded;
            if (openedLevel) level = EditorSceneManager.OpenScene("Assets/00_Scenes/Level04.unity", OpenSceneMode.Additive);
            var chase = level.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChaseLevel>(true)).Single();
            var chaseData = new SerializedObject(chase);
            chaseData.FindProperty("nextScene").stringValue = "Ending";
            chaseData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(level);
            EditorSceneManager.SaveScene(level);
            if (openedLevel) EditorSceneManager.CloseScene(level, true);
            SceneManager.SetActiveScene(scene);
            AssetDatabase.SaveAssets();
            Verify();
        }

        private static EndingContent EnsureContent()
        {
            var content = AssetDatabase.LoadAssetAtPath<EndingContent>(ContentPath);
            if (content != null) return content;
            if (!AssetDatabase.IsValidFolder("Assets/00_Scenes/Ending_Data")) AssetDatabase.CreateFolder("Assets/00_Scenes", "Ending_Data");
            content = ScriptableObject.CreateInstance<EndingContent>();
            content.epilogue = new[]
            {
                "El motor se apaga.\nLa ciudad queda al otro lado de la puerta.",
                "<color=#27D8EB><size=28>NINA</size></color>\n\nHasta la próxima.",
                "Afuera empieza a amanecer.\nEsta noche termina aquí."
            };
            content.credits = "<color=#27D8EB><size=26>EQUIPO DE DESARROLLO</size></color>\n\n"
                + "Antonio Santiago Sanchez Angulo\nJhael Elisa Guevara Silva\nJohnny Suarez Abasto\nMateo Alejandro Valeriano Sanchez\nRommel Rodrigo Gutierrez Herrera\n\n\n"
                + "<color=#FF2B7D><size=26>AGRADECIMIENTOS ESPECIALES</size></color>\n\nA sus chatcitos, Claudes\ny Antigravity.\n\n\n"
                + "<color=#27D8EB><size=26>INSPIRACIÓN</size></color>\n\nHotline Miami\n\n\n"
                + "<color=#FF2B7D><size=26>Y A TI</size></color>\n\nPor jugar hasta el final.\n\nOVERDRIVE";
            content.music = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/07_Audio/birds-flowers.mp3");
            AssetDatabase.CreateAsset(content, ContentPath);
            return content;
        }

        private static TMP_FontAsset EnsureTitleFont()
        {
            const string path = "Assets/00_Scenes/Ending_Data/EndingTitle.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null) return font;
            // TMP can populate its atlas while previewing text. Keep those additions local to Ending.
            if (!AssetDatabase.CopyAsset("Assets/TextMesh Pro/Fonts/horrendo SDF.asset", path)) return TMP_Settings.defaultFontAsset;
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static RectTransform Panel(string name, Transform parent, Color color)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return rect;
        }

        private static CanvasGroup Group(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            Stretch(rect);
            return rect.gameObject.AddComponent<CanvasGroup>();
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color, Vector2 position, Vector2 dimensions)
        {
            var text = Rect(name, parent, position, dimensions).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset") ?? TMP_Settings.defaultFontAsset;
            text.text = value; text.fontSize = size; text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, string label, Vector2 position, Color accent)
        {
            var rect = Rect(name, parent, position, new Vector2(470f, 66f));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.04f, .055f, .09f);
            var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = accent * .75f;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(.7f, .85f, 1f); colors.pressedColor = new Color(.4f, .5f, .7f);
            button.colors = colors;
            Text("Label", rect, label, 22f, accent, Vector2.zero, new Vector2(450f, 58f));
            return button;
        }

        private static void DrawBackdrop(Transform parent)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var line = Panel("Neon Edge", parent, side < 0 ? Cyan * .5f : Pink * .5f);
                line.anchorMin = line.anchorMax = new Vector2(side < 0 ? 0f : 1f, .5f);
                line.anchoredPosition = new Vector2(-side * 36f, 0f);
                line.sizeDelta = new Vector2(3f, 850f);
            }
            for (int i = 0; i < 22; i++)
            {
                float height = 55f + (i * 43 % 91);
                var building = Panel("City " + i, parent, new Color(.025f, .034f, .065f));
                building.sizeDelta = new Vector2(64f, height);
                building.anchoredPosition = new Vector2(-935f + i * 89f, -540f + height * .5f);
                var window = Panel("Window", building, i % 2 == 0 ? new Color(.02f, .15f, .2f) : new Color(.2f, .025f, .09f));
                window.sizeDelta = new Vector2(20f, 3f);
            }
            var divider = Panel("Horizon", parent, new Color(.13f, .08f, .19f));
            divider.sizeDelta = new Vector2(1500f, 1f); divider.anchoredPosition = new Vector2(0f, -295f);
        }

        public static void Verify()
        {
            const string levelPath = "Assets/00_Scenes/Level04.unity";
            var level = SceneManager.GetSceneByPath(levelPath);
            bool opened = !level.isLoaded;
            if (opened) level = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Additive);
            try
            {
                var chase = level.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChaseLevel>(true)).Single();
                if (chase.NextScene != "Ending") throw new InvalidOperationException("La victoria del nivel 4 todavía no conduce al cierre: " + chase.NextScene);
                if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath))
                    throw new InvalidOperationException("Falta la escena Ending habilitada en Build Settings.");
                var timeline = new EndingTimeline(2, 3f, 5f);
                timeline.Tick(3.25f);
                Require(timeline.Card == 1 && timeline.Phase == EndingPhase.Epilogue, "El epílogo debe avanzar automáticamente.");
                timeline.Advance();
                Require(timeline.Phase == EndingPhase.Credits && timeline.Elapsed == 0f, "Avanzar la última tarjeta debe iniciar los créditos.");
                timeline.Tick(4f);
                Require(timeline.Phase == EndingPhase.Credits, "Los créditos no deben finalizar antes de tiempo.");
                timeline.Tick(1.1f);
                Require(timeline.Phase == EndingPhase.Thanks, "Los créditos deben terminar en el agradecimiento.");
                timeline.Advance(); timeline.Tick(.3f); timeline.RequestReturn();
                Require(timeline.Phase == EndingPhase.Leaving && timeline.Elapsed > .29f, "Volver al menú debe ser idempotente durante el fundido.");
                var skipped = new EndingTimeline(3, 6f, 28f);
                skipped.Tick(2f); skipped.SkipEpilogue();
                Require(skipped.Phase == EndingPhase.Credits && skipped.Elapsed == 0f, "Saltar el epílogo no debe saltar también los créditos.");
                var empty = new EndingTimeline(0, 6f, 28f);
                Require(empty.Phase == EndingPhase.Credits, "Un epílogo vacío debe conducir a los créditos.");
                empty.Tick(-1f);
                Require(empty.Elapsed == 0f, "El tiempo no puede retroceder.");
                var content = AssetDatabase.LoadAssetAtPath<EndingContent>(ContentPath);
                Require(content != null && content.music != null && !string.IsNullOrWhiteSpace(content.credits), "Falta contenido o música del final.");
                File.WriteAllText("Temp/Ending.validation.txt", "PASS Level04 -> Ending habilitado.");
            }
            catch (Exception e)
            {
                File.WriteAllText("Temp/Ending.validation.txt", "FAIL " + e.Message);
                throw;
            }
            finally { if (opened) EditorSceneManager.CloseScene(level, true); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
