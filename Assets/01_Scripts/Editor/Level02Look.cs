using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Overdrive.Level;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Da a Level02 el look del Elysium original (ámbar cálido arriba, verde en los pisos, emisivos vivos)
    /// sin reconstruir la escena: solo toca luces, materiales y post-proceso.
    /// </summary>
    internal static class Level02Look
    {
        private const string DataPath = "Assets/00_Scenes/Level02_Data";
        internal static readonly Color Amber = new Color(1f, 0.62f, 0.28f);
        internal static readonly Color Green = new Color(0.25f, 1f, 0.55f);
        internal static readonly Color Teal = new Color(0.1f, 0.8f, 0.9f);

        [MenuItem("TopDown/Nivel 02/Aplicar look Elysium e iluminación interior")]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Level02")
            {
                Debug.LogError("[Level02Look] Abre Level02 primero.");
                return;
            }

            ApplyAmbient();
            RecolorLights(scene);
            AddInteriorLights(scene);
            TuneMaterials();
            TuneVolume(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Level02Look] Look aplicado. Guarda la escena (Ctrl+S).");
        }

        internal static void ApplyAmbient()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.42f, 0.24f);     // techo dorado
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.42f, 0.30f); // paredes verdosas
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.20f, 0.20f);  // piso teal oscuro
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.6f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.10f, 0.12f, 0.09f);
            RenderSettings.fogDensity = 0.006f;
        }

        private static void RecolorLights(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var light in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)))
            {
                if (light.type == LightType.Directional)
                {
                    light.color = Amber;
                    light.intensity = 1.1f;
                    continue;
                }
                // Magentas y violetas pasan a la paleta ámbar/verde del Elysium.
                Color.RGBToHSV(light.color, out float h, out _, out _);
                bool pinkOrPurple = h > 0.7f || h < 0.02f;
                if (pinkOrPurple) light.color = light.name.GetHashCode() % 2 == 0 ? Amber : Green;
                light.intensity *= 1.4f;
            }

            foreach (var club in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ClubLights>(true)))
            {
                var so = new SerializedObject(club);
                var palette = so.FindProperty("palette");
                Color[] colors = { Amber, Green, Teal, new Color(1f, 0.85f, 0.45f) };
                palette.arraySize = colors.Length;
                for (int i = 0; i < colors.Length; i++) palette.GetArrayElementAtIndex(i).colorValue = colors[i];
                so.FindProperty("pulseAmount").floatValue = 0.15f;
                so.ApplyModifiedProperties();
            }
        }

        private static void AddInteriorLights(UnityEngine.SceneManagement.Scene scene)
        {
            var old = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Interior Lighting");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Interior Lighting").transform;

            void Add(string name, Vector3 pos, Color color, float intensity, float range)
            {
                var l = new GameObject(name).AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.position = pos;
                l.type = LightType.Point;
                l.color = color;
                l.intensity = intensity;
                l.range = range;
                l.shadows = LightShadows.None;
            }

            // Sala principal: rejilla de rellenos ámbar bajo el techo y verdes a la altura de los balcones.
            for (float x = -20f; x <= 20f; x += 10f)
                for (float z = -28f; z <= 28f; z += 11f)
                {
                    Add($"Ceiling_Amber_{x}_{z}", new Vector3(x, 3.2f, z), Amber, 2.6f, 12f);
                    Add($"Balcony_Green_{x}_{z}", new Vector3(x + 5f, 1.2f, z + 5f), Green, 2f, 9f);
                }

            // Entrada y pasillo hacia el escenario.
            foreach (float z in new[] { 30f, 24f, 18f, -16f, -24f, -32f })
                Add($"Hall_{z}", new Vector3(0f, 1.6f, z), z > 0f ? Teal : Amber, 3f, 9f);

            // Escenario, backstage y camerinos (más abajo).
            foreach (float z in new[] { -42f, -52f, -62f, -72f, -82f })
            {
                Add($"Stage_Amber_{z}", new Vector3(-6.7f, 0.6f, z), Amber, 3f, 11f);
                Add($"Stage_Green_{z}", new Vector3(-12f, -0.4f, z + 4f), Green, 2.2f, 9f);
            }
        }

        internal static void TuneMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { DataPath }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat.name == "L02_NeonPink")
                {
                    SetNeon(mat, Amber * 4f);
                    continue;
                }
                if (mat.name == "L02_NeonCyan")
                {
                    SetNeon(mat, Green * 4f);
                    continue;
                }
                if (!mat.name.StartsWith("L02_M_Elysium")) continue;

                // Pisos y muros brillantes como en el render original.
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
                if (mat.HasProperty("_EmissionMap") && mat.GetTexture("_EmissionMap") != null)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.white * 3f);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                EditorUtility.SetDirty(mat);
            }
        }

        private static void SetNeon(Material mat, Color emission)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", emission / 4f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(mat);
        }

        private static void TuneVolume(UnityEngine.SceneManagement.Scene scene)
        {
            var volume = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Volume>(true)).FirstOrDefault();
            if (volume == null || volume.sharedProfile == null) return;
            TuneProfile(volume.sharedProfile);
        }

        internal static void TuneProfile(VolumeProfile profile)
        {
            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(1f);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.75f));
            if (!profile.TryGet(out ColorAdjustments grading)) grading = profile.Add<ColorAdjustments>(true);
            grading.postExposure.Override(0.7f);
            grading.contrast.Override(12f);
            grading.saturation.Override(12f);
            grading.colorFilter.Override(new Color(1f, 0.95f, 0.85f));
            if (!profile.TryGet(out WhiteBalance wb)) wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(12f);
            wb.tint.Override(-8f); // ligero empuje hacia el verde
            if (AssetDatabase.Contains(profile))
                foreach (var component in profile.components)
                    if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
        }
    }
}
