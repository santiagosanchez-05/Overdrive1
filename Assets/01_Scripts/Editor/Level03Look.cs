using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Overdrive.Level;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Da a Level03 el ambiente de los niveles anteriores (interior oscuro, lámparas cálidas, acentos neón y bloom)
    /// sin tocar el modelo del loft: no cambia materiales, texturas ni objetos existentes, solo añade luces y post-proceso.
    /// </summary>
    internal static class Level03Look
    {
        private const string DataPath = "Assets/00_Scenes/Level03_Data";
        private const string LightingRoot = "Level03 Lighting";
        private const string VolumeName = "Level03_GlobalVolume";

        private static readonly Color Warm = new Color(1f, 0.82f, 0.58f);
        private static readonly Color Yellow = new Color(1f, 0.72f, 0.36f);
        private static readonly Color Magenta = new Color(1f, 0.15f, 0.6f);
        private static readonly Color Cyan = new Color(0.1f, 0.75f, 1f);
        private static readonly Color Violet = new Color(0.55f, 0.2f, 1f);

        [MenuItem("TopDown/Nivel 03/Aplicar ambiente e iluminación")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Level03")
            {
                Debug.LogError("[Level03Look] Abre Level03 primero.");
                return;
            }

            Bounds bounds = SceneBounds(scene);
            if (bounds.size == Vector3.zero)
            {
                Debug.LogError("[Level03Look] No hay renderers en la escena.");
                return;
            }

            ApplyAmbient();
            TuneSunAndCamera(scene);
            BuildLights(scene, bounds);
            BuildVolume(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Level03Look] Ambiente aplicado sobre {bounds.size.x:0.#} x {bounds.size.z:0.#} m. Guarda la escena (Ctrl+S).");
        }

        private static Bounds SceneBounds(Scene scene)
        {
            var renderers = scene.GetRootGameObjects()
                .Where(r => r.name != LightingRoot)
                .SelectMany(r => r.GetComponentsInChildren<Renderer>(true))
                .Where(r => !(r is ParticleSystemRenderer))
                .ToList();
            if (renderers.Count == 0) return new Bounds();
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static void ApplyAmbient()
        {
            // Sin cielo: interior nocturno como Level01/02, con un degradado cálido que deja leer las texturas.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.30f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.26f, 0.22f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.10f, 0.13f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.5f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.05f, 0.04f, 0.07f);
            RenderSettings.fogDensity = 0.008f;
        }

        private static void TuneSunAndCamera(Scene scene)
        {
            var all = scene.GetRootGameObjects();
            foreach (var light in all.SelectMany(r => r.GetComponentsInChildren<Light>(true)))
            {
                if (light.type != LightType.Directional) continue;
                // Luz de luna fría y baja que entra por las ventanas.
                light.color = new Color(0.62f, 0.68f, 1f);
                light.intensity = 0.35f;
                light.shadows = LightShadows.Soft;
            }
            foreach (var cam in all.SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }
        }

        private static void BuildLights(Scene scene, Bounds b)
        {
            var old = scene.GetRootGameObjects().FirstOrDefault(r => r.name == LightingRoot);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(LightingRoot).transform;

            float height = Mathf.Clamp(b.size.y, 2.5f, 6f);
            float ceiling = b.min.y + height * 0.8f;
            float spacing = 5f;
            int nx = Mathf.Max(1, Mathf.RoundToInt(b.size.x / spacing));
            int nz = Mathf.Max(1, Mathf.RoundToInt(b.size.z / spacing));

            // 1. Lámparas de techo cálidas (blanco cálido / amarillo) en rejilla.
            int i = 0;
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                {
                    var pos = new Vector3(
                        Mathf.Lerp(b.min.x, b.max.x, (x + 0.5f) / nx),
                        ceiling,
                        Mathf.Lerp(b.min.z, b.max.z, (z + 0.5f) / nz));
                    AddPoint(root, $"Lamp_{x}_{z}", pos, (x + z) % 2 == 0 ? Warm : Yellow, 2.4f, spacing * 1.5f);
                    i++;
                }

            // 2. Acentos neón animados a media altura en las cuatro paredes (como los niveles anteriores).
            var neon = new System.Collections.Generic.List<Light>();
            Color[] palette = { Magenta, Cyan, Violet };
            float y = b.min.y + height * 0.35f;
            float inset = 1.2f;
            Vector3[] corners =
            {
                new Vector3(b.min.x + inset, y, b.min.z + inset), new Vector3(b.max.x - inset, y, b.min.z + inset),
                new Vector3(b.max.x - inset, y, b.max.z - inset), new Vector3(b.min.x + inset, y, b.max.z - inset),
            };
            int n = 0;
            for (int side = 0; side < 4; side++)
            {
                Vector3 from = corners[side], to = corners[(side + 1) % 4];
                int count = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(from, to) / 7f));
                for (int k = 0; k < count; k++)
                {
                    Vector3 pos = Vector3.Lerp(from, to, (k + 0.5f) / count);
                    neon.Add(AddPoint(root, $"Neon_{n}", pos, palette[n % palette.Length], 2.2f, 6f));
                    n++;
                }
            }
            root.gameObject.AddComponent<ClubLights>().Setup(neon.ToArray());

            // 3. Sonda de reflejos para que los suelos y metales no reflejen negro.
            var probe = new GameObject("Reflection Probe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(root, false);
            probe.transform.position = new Vector3(b.center.x, b.min.y + height * 0.5f, b.center.z);
            probe.size = b.size + Vector3.one;
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.intensity = 0.8f;

            Debug.Log($"[Level03Look] {i} lámparas cálidas y {n} acentos neón.");
        }

        private static Light AddPoint(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = pos;
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        private static void BuildVolume(Scene scene)
        {
            Directory.CreateDirectory(DataPath);
            string path = DataPath + "/Level03_Lighting.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.8f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.65f);
            if (!profile.TryGet(out Tonemapping tone)) tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            if (!profile.TryGet(out ColorAdjustments grading)) grading = profile.Add<ColorAdjustments>(true);
            grading.postExposure.Override(0.5f);
            grading.contrast.Override(10f);
            grading.saturation.Override(8f);
            if (!profile.TryGet(out Vignette vignette)) vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            foreach (var component in profile.components)
                if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);

            var go = scene.GetRootGameObjects().FirstOrDefault(r => r.name == VolumeName) ?? new GameObject(VolumeName);
            var volume = go.GetComponent<Volume>();
            if (volume == null) volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }
    }
}
