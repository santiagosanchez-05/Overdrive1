using System.Linq;
using Overdrive.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    internal static class NeonMenuStyler
    {
        private static readonly Color Pink = new Color32(255, 38, 127, 255);
        private static readonly Color Cyan = new Color32(35, 216, 239, 255);
        private static readonly Color Violet = new Color32(148, 103, 255, 255);

        public static void Apply(Scene scene)
        {
            T[] Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            var buttons = Find<NeonMenuButton>();
            for (int i = 0; i < buttons.Length; i++) Style(buttons[i], i);
            foreach (var slider in Find<Slider>())
            {
                if (slider.fillRect != null) slider.fillRect.GetComponent<Image>().color = Cyan;
                if (slider.handleRect != null) slider.handleRect.GetComponent<Image>().color = Pink;
            }
            foreach (var toggle in Find<Toggle>()) if (toggle.graphic != null) toggle.graphic.color = Pink;
            foreach (var menu in Find<MainMenuController>())
            {
                if (menu.levelButtons.Length > 0) menu.levelButtons[0].label.text = "NIVEL 01 · LA CASA";
                var background = menu.transform.Find("Background")?.GetComponent<Image>();
                if (background != null)
                {
                    background.material = MenuMaterial(true);
                    EnsureMotion(background.gameObject);
                }
                if (menu.mainGroup.transform.Find("Neon Title") == null) AddTitle(menu.mainGroup.transform);
                foreach (var panel in new[] { menu.settingsPanel, menu.levelsPanel }) StylePanel(panel.transform);
                EditorUtility.SetDirty(menu);
            }
            foreach (var pause in Find<PauseMenuController>()) StylePanel(pause.panel.transform);
        }

        public static void Style(NeonMenuButton button, int index)
        {
            Color accent = index % 3 == 0 ? Pink : index % 3 == 1 ? Cyan : Violet;
            button.neon = accent;
            button.borderNormal = new Color(accent.r * .65f, accent.g * .65f, accent.b * .65f, .95f);
            button.fillColor = new Color(.025f, .022f, .065f, .94f);
            button.textNormal = new Color(.94f, .97f, 1f, 1f);
            button.stripesNormalAlpha = .55f;
            button.highlightMaxAlpha = .14f;
            button.glowSelectedAlpha = 1f;
            button.breathAmount = .07f;
            button.breathSpeed = .65f;
            if (button.stripes == null) button.stripes = button.transform.Find("Stripes")?.GetComponent<Image>();
            if (button.fill != null) button.fill.color = button.fillColor;
            if (button.border != null) button.border.color = button.borderNormal;
            if (button.stripes != null) button.stripes.color = new Color(accent.r, accent.g, accent.b, .55f);
            if (button.label != null) button.label.color = button.textNormal;
            if (button.icon != null) button.icon.color = button.textNormal;
            EditorUtility.SetDirty(button);
        }

        private static Material MenuMaterial(bool artwork)
        {
            string path = NeonPolishBuilder.DataPath + (artwork ? "/MenuArtwork.mat" : "/MenuAtmosphere.mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Overdrive/UI/Neon Atmosphere");
            if (shader == null) throw new System.InvalidOperationException("Falta el shader de ambiente neón.");
            material = new Material(shader) { name = artwork ? "Neon Artwork" : "Neon Atmosphere" };
            material.SetFloat("_Artwork", artwork ? 1f : 0f);
            material.SetFloat("_Strength", artwork ? .9f : .7f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void StylePanel(Transform panel)
        {
            var dim = panel.Find("Dim")?.GetComponent<Image>();
            if (dim != null) dim.color = new Color(.008f, .005f, .025f, .74f);
            var box = panel.Find("Box")?.GetComponent<Image>();
            if (box != null)
            {
                box.color = new Color(.025f, .015f, .065f, .96f);
                var outline = box.GetComponent<Outline>();
                if (outline != null) { outline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, .72f); outline.effectDistance = new Vector2(1.5f, -1.5f); }
            }
            var title = panel.Find("Title")?.GetComponent<TMP_Text>();
            if (title != null) title.color = Pink;
            if (panel.Find("Neon Ambience") == null)
            {
                var image = new GameObject("Neon Ambience", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(panel, false);
                image.transform.SetSiblingIndex(dim != null ? dim.transform.GetSiblingIndex() + 1 : 0);
                Stretch(image.rectTransform);
                image.raycastTarget = false;
                image.material = MenuMaterial(false);
                EnsureMotion(image.gameObject);
            }
        }

        private static void EnsureMotion(GameObject target)
        {
            if (target.GetComponent<NeonMenuAtmosphere>() == null) target.AddComponent<NeonMenuAtmosphere>();
        }

        private static void AddTitle(Transform parent)
        {
            var title = new GameObject("Neon Title", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            title.transform.SetParent(parent, false);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(72f, -50f);
            title.rectTransform.sizeDelta = new Vector2(690f, 110f);
            const string fontPath = NeonPolishBuilder.DataPath + "/MenuTitle.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath) == null)
            {
                string source = "Assets/00_Scenes/Ending_Data/EndingTitle.asset";
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(source) == null) source = "Assets/TextMesh Pro/Fonts/horrendo SDF.asset";
                AssetDatabase.CopyAsset(source, fontPath);
            }
            title.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath) ?? TMP_Settings.defaultFontAsset;
            title.text = "OVERDRIVE";
            title.fontSize = 88f;
            title.color = Pink;
            title.raycastTarget = false;
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, .8f);
            shadow.effectDistance = new Vector2(3f, -2f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
