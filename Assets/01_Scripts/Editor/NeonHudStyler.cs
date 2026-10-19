using Overdrive.Driving;
using Overdrive.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    public static partial class NeonPolishBuilder
    {
        private static void ApplyHudAndTypography(Scene scene)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/horrendo.ttf");
            var readableFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            string fontPath = DataPath + "/WorldHorrendo.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath) == null)
                AssetDatabase.CopyAsset("Assets/TextMesh Pro/Fonts/horrendo SDF.asset", fontPath);
            var sdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            foreach (var text in Find<TextMeshPro>(scene))
            {
                text.font = sdf;
                // A font swap changes glyph widths: preserve the sign bounds and fit its wording.
                text.enableAutoSizing = true; text.fontSizeMax = text.fontSize; text.fontSizeMin = Mathf.Min(text.fontSize, .8f);
                EditorUtility.SetDirty(text);
            }
            foreach (var text in Find<TextMesh>(scene)) { text.font = font; text.GetComponent<Renderer>().sharedMaterial = font.material; EditorUtility.SetDirty(text); }
            foreach (var hud in Find<GameHUD>(scene))
            {
                SetFont(hud, readableFont);
                var serialized = new SerializedObject(hud);
                serialized.FindProperty("objectiveHint").stringValue = scene.name == "Level02" ? "RECUPERA EL MALETÍN EN LOS CAMERINOS" :
                    scene.name == "Level03" ? "LLEGA AL ÁTICO Y ENFRENTA AL JEFE" : "RECUPERA EL MALETÍN DEL PISO DE ARRIBA";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var hud in Find<ChaseHUD>(scene)) SetFont(hud, readableFont);
        }

        private static void SetFont(Component component, Font font)
        {
            var serialized = new SerializedObject(component); serialized.FindProperty("hudFont").objectReferenceValue=font; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
