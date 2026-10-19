using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Overdrive.Driving;
using Overdrive.Level;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    public static partial class NeonPolishBuilder
    {
        public static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        [MenuItem("TopDown/Acabado neón/Aplicar a los cuatro niveles y menús")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode antes de aplicar el acabado.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Guarda las escenas antes de aplicar el acabado.");
            EnsureFolder(DataPath); EnsureFolder(DataPath + "/Materials"); EnsureFolder(DataPath + "/Probes");
            var previous = SceneManager.GetActiveScene();
            var report = new System.Text.StringBuilder();
            try
            {
                foreach (string name in Paths)
                {
                    string path = "Assets/00_Scenes/" + name + ".unity";
                    var scene = SceneManager.GetSceneByPath(path);
                    bool opened = !scene.isLoaded;
                    if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        SceneManager.SetActiveScene(scene);
                        int colliders = Find<Collider>(scene).Length;
                        Apply(scene);
                        if (Find<Collider>(scene).Length != colliders) throw new InvalidOperationException("Cambió la física de " + name);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        report.AppendLine(name + ": colliders conservados=" + colliders + ", probes=" + Find<ReflectionProbe>(scene).Length);
                    }
                    finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
                }
                AssetDatabase.SaveAssets();
                File.WriteAllText("Temp/NeonPolish.build.txt", report.ToString());
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); EditorUtility.ClearProgressBar(); }
        }

        public static void ApplyMenusIfInstalled(Scene scene)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(DataPath + "/MenuArtwork.mat") != null) NeonMenuStyler.Apply(scene);
        }

        public static void ApplyIfInstalled(Scene scene)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(DataPath + "/MenuArtwork.mat") != null) Apply(scene);
        }

        private static void Apply(Scene scene)
        {
            NeonMenuStyler.Apply(scene);
            if (scene.name == "Menu") return;
            var old = Find<NeonSceneLook>(scene).SingleOrDefault();
            if (old != null) { old.RestoreOriginals(); Object.DestroyImmediate(old.gameObject); }
            var root = new GameObject("Visual Finish"); SceneManager.MoveGameObjectToScene(root, scene);
            var look = root.AddComponent<NeonSceneLook>();
            var bindings = new List<NeonMaterialBinding>();
            foreach (var renderer in Find<MeshRenderer>(scene))
            {
                if (renderer.GetComponentInParent<Overdrive.Player.PlayerController>() != null ||
                    renderer.GetComponentInParent<Overdrive.Enemies.Enemy>() != null ||
                    renderer.GetComponentInParent<Car>() != null) continue;
                var original = renderer.sharedMaterials;
                var styled = original.Select(m => StyleMaterial(m, renderer, scene.name)).ToArray();
                if (!original.SequenceEqual(styled)) bindings.Add(new NeonMaterialBinding { renderer = renderer, original = original, styled = styled });
                if (renderer.GetComponent<TMP_Text>() == null)
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) | StaticEditorFlags.ReflectionProbeStatic);
            }
            look.Configure(bindings.ToArray());
            foreach (var binding in bindings)
                if (PrefabUtility.IsPartOfPrefabInstance(binding.renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(binding.renderer);
            AddVolume(scene, root.transform);
            AddFills(scene, root.transform);
            BakeProbes(scene, root.transform);
            ApplyHudAndTypography(scene);
            foreach (var camera in Find<Camera>(scene))
            {
                camera.allowHDR = true;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }
            foreach (string car in new[] { "PlayerCar", "Hilux", "HiluxBlindada" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/04_Materials/M_" + car + ".mat");
                if (material == null) continue;
                material.SetFloat("_Smoothness", .7f); material.SetFloat("_Metallic", .35f); EditorUtility.SetDirty(material);
            }
        }

        private static Material StyleMaterial(Material source, MeshRenderer renderer, string scene)
        {
            if (source == null || (source.shader.name != "Universal Render Pipeline/Lit" && source.shader.name != "Overdrive/Wall Surface")) return source;
            string name = source.name.ToLowerInvariant();
            if (new[] { "carpet", "skin", "cartoon", "neon", "glow", "blood", "fabric", "veget", "раст", "aura", "screen" }.Any(name.Contains)) return source;
            string objectName = renderer.name.ToLowerInvariant();
            string kind = name.Contains("metal") || name.Contains("steel") || name.Contains("chrome") ? "Metal" :
                name.Contains("glass") ? "Glass" : name.Contains("wood") ? "Wood" :
                ((objectName.Contains("floor") && !objectName.Contains("lamp") && !objectName.Contains("stair")) || objectName.Contains("asphalt") || name.Contains("roadsground")) ? "Floor" :
                source.shader.name == "Overdrive/Wall Surface" ? "Wall" : null;
            if (kind == null) return source;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
            if (string.IsNullOrEmpty(guid)) return source;
            string path = DataPath + "/Materials/" + guid + "_" + id + "_" + kind + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null) { result = new Material(source); AssetDatabase.CreateAsset(result, path); }
            else EditorUtility.CopySerialized(source, result);
            result.name = source.name + " · Neon " + kind;
            result.SetFloat("_Smoothness", kind == "Floor" ? .68f : kind == "Metal" ? .68f : kind == "Glass" ? .87f : kind == "Wood" ? .36f : .22f);
            if (kind == "Metal") result.SetFloat("_Metallic", Mathf.Max(.55f, source.GetFloat("_Metallic")));
            if (kind == "Floor" && result.HasProperty("_MetallicGlossMap") && result.GetTexture("_MetallicGlossMap") == null)
            {
                result.SetTexture("_MetallicGlossMap", FloorMask());
                result.SetFloat("_SmoothnessTextureChannel", 0f);
                result.EnableKeyword("_METALLICSPECGLOSSMAP"); result.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            }
            if (result.HasProperty("_EnvironmentReflections")) result.SetFloat("_EnvironmentReflections", 1f);
            result.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); result.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            EditorUtility.SetDirty(result);
            return result;
        }

        private static Texture2D FloorMask()
        {
            string path = DataPath + "/FloorRoughness.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) return texture;
            texture = new Texture2D(128, 128, TextureFormat.RGBA32, true, true) { name = "Floor · uneven polish", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                // Periodic coordinates keep the mask seamless without touching the base texture.
                float ax = x * Mathf.PI * 2f / 128f, ay = y * Mathf.PI * 2f / 128f;
                float grain = Mathf.PerlinNoise(3f + Mathf.Cos(ax) * 1.4f + Mathf.Sin(ay) * .6f, 4f + Mathf.Sin(ax) * 1.4f + Mathf.Cos(ay) * .6f);
                pixels[y * 128 + x] = new Color(0f, 0f, 0f, Mathf.Lerp(.55f, .98f, grain));
            }
            texture.SetPixels(pixels); texture.Apply(true, true); AssetDatabase.CreateAsset(texture, path); return texture;
        }

        private static void AddVolume(Scene scene, Transform root)
        {
            string path = DataPath + "/" + scene.name + "_Grade.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            T Effect<T>() where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var effect)) { effect = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(effect, profile); }
                return effect;
            }
            var bloom = Effect<Bloom>(); bloom.intensity.Override(scene.name == "Level02" ? .48f : .38f); bloom.threshold.Override(1.1f); bloom.scatter.Override(.6f);
            Effect<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var color = Effect<ColorAdjustments>(); color.contrast.Override(5f); color.saturation.Override(7f);
            var vignette = Effect<Vignette>(); vignette.intensity.Override(.15f); vignette.smoothness.Override(.45f);
            var volume = root.gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20; volume.sharedProfile = profile;
            EditorUtility.SetDirty(profile);
        }

        private static void AddFills(Scene scene, Transform root)
        {
            Vector3[] positions = scene.name == "Level01" ? new[] { new Vector3(8, 2.5f, -7), new Vector3(41, 6, -22) } :
                scene.name == "Level03" ? Find<Overdrive.Player.PlayerController>(scene).Select(p => p.transform.position + Vector3.up * 2f).ToArray() : Array.Empty<Vector3>();
            for (int i = 0; i < positions.Length; i++)
            {
                var light = new GameObject("Neon bounce " + i).AddComponent<Light>(); light.transform.SetParent(root, false); light.transform.position = positions[i];
                light.type = LightType.Point; light.color = i % 2 == 0 ? new Color(1, .04f, .3f) : new Color(.03f, .7f, 1f);
                light.range = 9; light.intensity = 2.2f; light.shadows = LightShadows.None;
            }
        }

        private static void BakeProbes(Scene scene, Transform root)
        {
            var positions = new List<Vector3>(); Vector3 size;
            switch (scene.name)
            {
                case "Level01":
                    positions.AddRange(new[] { new Vector3(10,2,-8),new Vector3(10,2,-22),new Vector3(32,2,-8),new Vector3(32,2,-22),new Vector3(43,6,-8),new Vector3(43,6,-22) }); size = new Vector3(23,4.5f,18); break;
                case "Level02":
                    positions.AddRange(new[] { new Vector3(0,2,0),new Vector3(0,2,17),new Vector3(-2,0,-25),new Vector3(-7,0,-51),new Vector3(-7,0,-77) }); size = new Vector3(46,8,32); break;
                case "Level03":
                    var actors = Find<Overdrive.Player.PlayerController>(scene).Select(p=>p.transform.position).Concat(Find<Overdrive.Enemies.Enemy>(scene).Select(e=>e.transform.position));
                    foreach (var p in actors) if (!positions.Any(q => Vector3.Distance(q, p + Vector3.up * 1.5f) < 16)) positions.Add(p + Vector3.up * 1.5f);
                    size = new Vector3(34,10,30); break;
                default:
                    var road = Find<RoadPath>(scene).FirstOrDefault();
                    if (road != null) for (float d=30; d<road.Length; d+=600) positions.Add(road.PointAt(d)+Vector3.up*3);
                    size = new Vector3(240,24,720); break;
            }
            for (int i = 0; i < positions.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Acabado neón", scene.name + " · reflejos " + (i+1) + "/" + positions.Count, (float)i/positions.Count);
                var probe = new GameObject("Baked Neon " + i).AddComponent<ReflectionProbe>(); probe.transform.SetParent(root, false); probe.transform.position=positions[i];
                probe.mode=ReflectionProbeMode.Baked; probe.resolution=128; probe.hdr=true; probe.boxProjection=true; probe.size=size;
                probe.blendDistance=1.5f; probe.importance=3; probe.intensity=.9f; probe.nearClipPlane=.15f; probe.farClipPlane=scene.name=="Level04"?180:80;
                string path = DataPath + "/Probes/" + scene.name + "_" + i + ".exr";
                if (!Lightmapping.BakeReflectionProbe(probe, path)) throw new InvalidOperationException("No se pudo capturar " + path);
                AssetDatabase.ImportAsset(path);
                // External baked captures belong in Custom: bakedTexture is owned by LightingData
                // and assigning it directly does not persist when the scene is reopened.
                probe.mode=ReflectionProbeMode.Custom;
                probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (probe.customBakedTexture==null) throw new InvalidOperationException("Captura vacía " + path);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
