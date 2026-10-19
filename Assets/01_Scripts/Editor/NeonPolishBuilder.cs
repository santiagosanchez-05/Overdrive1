using System;
using System.IO;
using System.Linq;
using System.Text;
using Overdrive.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    public static partial class NeonPolishBuilder
    {
        public const string DataPath = "Assets/04_Materials/NeonFinish";
        private static readonly string[] Paths = { "Menu", "Level01", "Level02", "Level03", "Level04" };

        public static void Verify()
        {
            var scene = SceneManager.GetSceneByPath("Assets/00_Scenes/Menu.unity");
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/00_Scenes/Menu.unity", OpenSceneMode.Additive);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MainMenuController>(true)).Single();
                var c = menu.playButton.neon;
                if (Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b) < .2f)
                    throw new InvalidOperationException("El botón principal todavía utiliza neón monocromático.");
                if (!menu.levelButtons[0].label.text.Contains("LA CASA"))
                    throw new InvalidOperationException("El nivel 1 todavía no tiene nombre en la selección.");
                File.WriteAllText("Temp/NeonPolish.validation.txt", "PASS Menú con neón cromático y nombre de nivel.");
            }
            catch (Exception e) { File.WriteAllText("Temp/NeonPolish.validation.txt", "FAIL " + e.Message); throw; }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        public static void Inspect()
        {
            var text = new StringBuilder();
            foreach (string name in Paths)
            {
                string path = "Assets/00_Scenes/" + name + ".unity";
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    T[] Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
                    text.AppendLine(name + " dirty=" + scene.isDirty + " playing=" + EditorApplication.isPlaying);
                    text.AppendLine("Lights=" + Find<Light>().Length + " probes=" + Find<ReflectionProbe>().Length + " volumes=" + Find<Volume>().Length);
                    foreach (var material in Find<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Take(55))
                    {
                        string props = string.Join(" ", new[] { "_Smoothness", "_Metallic", "_BumpScale" }.Where(material.HasProperty).Select(p => p + "=" + material.GetFloat(p).ToString("F2")));
                        text.AppendLine("MAT " + material.name + " shader=" + material.shader.name + " " + props);
                    }
                    foreach (var renderer in Find<MeshRenderer>().Where(r => r.name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0 || r.name.IndexOf("asphalt", StringComparison.OrdinalIgnoreCase) >= 0).Take(12))
                        text.AppendLine("FLOOR " + renderer.name + " center=" + renderer.bounds.center.ToString("F1") + " size=" + renderer.bounds.size.ToString("F1"));
                    foreach (var camera in Find<Camera>()) text.AppendLine("CAM " + camera.name + " pos=" + camera.transform.position.ToString("F1"));
                    foreach (var pause in Find<PauseMenuController>()) text.AppendLine("PAUSE " + pause.panel.name);
                    foreach (var volume in Find<Volume>()) text.AppendLine("VOLUME " + volume.name + " profile=" + (volume.sharedProfile != null ? AssetDatabase.GetAssetPath(volume.sharedProfile) : "none"));
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText("Temp/NeonPolish.inspect.txt", text.ToString());
        }
    }
}
