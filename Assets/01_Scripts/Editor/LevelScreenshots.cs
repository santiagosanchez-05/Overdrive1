using System;
using System.IO;
using System.Linq;
using Overdrive.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Capturas del nivel con la misma cámara que el juego, para revisar cómo se ve sin entrar en Play.
    /// Batch: Unity.exe -batchmode -quit -projectPath ... -executeMethod Overdrive.EditorTools.LevelScreenshots.Capture -shots "C:/ruta"
    /// No modifica la escena (se reabre sin guardar).
    /// </summary>
    public static class LevelScreenshots
    {
        private const int Width = 1600;
        private const int Height = 900;

        [MenuItem("TopDown/Capturas del nivel")]
        public static void CaptureMenu() => Capture();

        public static void Capture()
        {
            string dir = ArgValue("-shots") ?? Path.Combine(Application.temporaryCachePath, "OverdriveShots");
            Directory.CreateDirectory(dir);

            Scene scene = EditorSceneManager.OpenScene(SceneFactory.ScenePath, OpenSceneMode.Single);
            Camera gameCam = BuilderUtil.FindRoot(scene, SceneFactory.CameraRigName).GetComponentInChildren<Camera>(true);
            GameObject level = BuilderUtil.FindRoot(scene, SceneFactory.LevelRoot);
            GameObject floor1 = level.transform.Find(SceneFactory.FloorRootName(1))?.gameObject;
            var exit = level.transform.Find(SceneFactory.ExitName).GetComponent<ExitZone>();

            var go = new GameObject("ScreenshotCamera");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(gameCam);
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;

            try
            {
                // Planta baja (piso de arriba oculto, como en el juego cuando estás abajo).
                SetFloorVisible(floor1, false);
                SetUpperObjectsVisible(scene, false);
                // Calentamiento: en batch, el primer render puede usar texturas a medio enlazar.
                cam.Render();
                PoseAnimators();
                Transform player = BuilderUtil.FindRoot(scene, SceneFactory.PlayerName).transform;
                Shot(cam, rt, dir, "00_jugador", player.position, 4.5f);
                GameObject nina = BuilderUtil.FindRoot(scene, StoryFactory.NinaName);
                if (nina != null) Shot(cam, rt, dir, "00_nina", nina.transform.position + new Vector3(0f, 0f, 1.5f), 7f);
                // Un enemigo de cada arma, de cerca.
                foreach (var group in UnityEngine.Object.FindObjectsByType<Overdrive.Enemies.Enemy>()
                             .Where(e => e.Weapon != null && e.gameObject.activeInHierarchy)
                             .GroupBy(e => e.Weapon.Definition.Type))
                {
                    Transform enemy = group.OrderBy(e => e.name).First().transform;
                    Shot(cam, rt, dir, "00_enemigo_" + group.Key, enemy.position, 4.5f);
                }
                Shot(cam, rt, dir, "01_planta_baja_inicio", new Vector3(8f, 0f, -6f), 20f);
                Shot(cam, rt, dir, "02_planta_baja_norte", new Vector3(32f, 0f, -6f), 20f);
                Shot(cam, rt, dir, "03_planta_baja_sur", new Vector3(14f, 0f, -24f), 20f);
                exit.SetOpen(true);
                Shot(cam, rt, dir, "04_salida_abierta", new Vector3(42f, 0f, -25f), 20f);
                exit.SetOpen(false);
                Shot(cam, rt, dir, "05_puerta_refuerzos", new Vector3(43f, 0f, -14f), 14f);

                // Piso de arriba.
                SetFloorVisible(floor1, true);
                SetUpperObjectsVisible(scene, true);
                Shot(cam, rt, dir, "06_piso_arriba_oeste", new Vector3(20f, 4f, -6f), 20f);
                Shot(cam, rt, dir, "07_piso_arriba_este", new Vector3(40f, 4f, -6f), 20f);

                // Vista general alta.
                Shot(cam, rt, dir, "08_general", new Vector3(25f, 0f, -15f), 55f);
                Debug.Log($"[Screenshots] Capturas guardadas en {dir}");
            }
            finally
            {
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(go);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                EditorSceneManager.OpenScene(SceneFactory.ScenePath, OpenSceneMode.Single);
            }
            CaptureLevel02(dir);
        }

        /// <summary>
        /// Pose real de los modelos animados: en modo edición el Animator no corre solo, así que se evalúa a mano,
        /// y en batch la malla con huesos no se recalcula sola antes de renderizar: se le obliga en cada render.
        /// </summary>
        private static void PoseAnimators()
        {
            foreach (Animator animator in UnityEngine.Object.FindObjectsByType<Animator>())
            {
                animator.Rebind();
                animator.Update(0.5f);
                foreach (SkinnedMeshRenderer skinned in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                    skinned.forceMatrixRecalculationPerRender = true;
            }
        }

        /// <summary>Para las capturas del nivel 2 de Rommel: pone los modelos en su pose real (no deja objetos que limpiar).</summary>
        public static System.Collections.Generic.List<GameObject> BakePoses(Scene scene)
        {
            PoseAnimators();
            return new System.Collections.Generic.List<GameObject>();
        }

        public static void CleanupBakedPoses(System.Collections.Generic.List<GameObject> baked)
        {
            if (baked == null) return;
            foreach (GameObject go in baked)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>Nivel 2 (escena de Rommel): Nina y un enemigo de cada arma, de cerca.</summary>
        private static void CaptureLevel02(string dir)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(StoryFactory.Level02Path) == null) return;
            Scene scene = EditorSceneManager.OpenScene(StoryFactory.Level02Path, OpenSceneMode.Single);
            Camera gameCam = UnityEngine.Object.FindObjectsByType<Camera>().FirstOrDefault(c => c.CompareTag("MainCamera")) ??
                             UnityEngine.Object.FindObjectsByType<Camera>().FirstOrDefault();
            var go = new GameObject("ScreenshotCamera");
            var cam = go.AddComponent<Camera>();
            if (gameCam != null) cam.CopyFrom(gameCam);
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            try
            {
                cam.Render();
                PoseAnimators();
                GameObject nina = BuilderUtil.FindRoot(scene, StoryFactory.NinaName);
                if (nina != null) Shot(cam, rt, dir, "10_nivel2_nina", nina.transform.position, 6f);
                foreach (var group in UnityEngine.Object.FindObjectsByType<Overdrive.Enemies.Enemy>()
                             .Where(e => e.Weapon != null && e.gameObject.activeInHierarchy)
                             .GroupBy(e => e.Weapon.Definition.Type))
                {
                    Transform enemy = group.OrderBy(e => e.name).First().transform;
                    Shot(cam, rt, dir, "11_nivel2_enemigo_" + group.Key, enemy.position, 5f);
                }
            }
            finally
            {
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(go);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                EditorSceneManager.OpenScene(SceneFactory.ScenePath, OpenSceneMode.Single);
            }
        }

        private static void Shot(Camera cam, RenderTexture rt, string dir, string name, Vector3 focus, float distance)
        {
            // Mismo ángulo que la cámara del juego (70°).
            Quaternion rotation = Quaternion.Euler(70f, 0f, 0f);
            cam.transform.SetPositionAndRotation(focus + rotation * new Vector3(0f, 0f, -distance), rotation);
            cam.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        /// <summary>Lo que FloorVisibility hace en el juego: oculta todo lo que esté en el piso de arriba (enemigos, armas, muebles...).</summary>
        private static void SetUpperObjectsVisible(Scene scene, bool visible)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == SceneFactory.LevelRoot || root.name == SceneFactory.CameraRigName) continue;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    if (Core.Floors.IndexOf(r.bounds.min.y) >= 1) r.enabled = visible;
            }
        }

        private static void SetFloorVisible(GameObject floor, bool visible)
        {
            if (floor == null) return;
            foreach (Renderer r in floor.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            foreach (Light l in floor.GetComponentsInChildren<Light>(true)) l.enabled = visible;
        }

        private static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
