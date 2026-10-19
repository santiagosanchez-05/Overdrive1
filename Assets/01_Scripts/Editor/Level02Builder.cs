using System;
using System.IO;
using System.Linq;
using System.Text;
using Overdrive.Level;
using Overdrive.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    public static class Level02Builder
    {
        public const string ScenePath = "Assets/00_Scenes/Level02.unity";
        public const string GeneratedPath = "Assets/00_Scenes/Level02_Data";

        public static void RunPlayTest() => Level02PlayTest.Run();
        public static void CheckBloodSurfaces() => BloodSurfaceChecks.Run();
        public static void CheckRemovedStairs()
        {
            foreach (float side in new[] { -1f, 1f })
                foreach (var sample in new[] { new Vector3(side * 22f, 3f, 2f), new Vector3(side * 21f, 3f, -9f) })
                    if (!Overdrive.Core.GroundProbe.TrySurface(sample, out var floor) || Mathf.Abs(floor.point.y - 1.6f) > .04f || floor.normal.y < .99f)
                        throw new InvalidOperationException("El antiguo tramo de escalera debe conservar un piso plano con collider: " + sample);
            File.WriteAllText("Temp/Level02.stair-validation.txt", "PASS Los cuatro tramos retirados conservan suelo plano y collider a y=1.6.");
            var rig = Object.FindAnyObjectByType<Overdrive.CameraRig.TopDownCamera>();
            var clone = Object.Instantiate(rig.Cam.gameObject).GetComponent<Camera>();
            if (clone.GetComponent<AudioListener>() != null) Object.DestroyImmediate(clone.GetComponent<AudioListener>());
            RenderShot(clone, "Temp/Level02-stairs-removed.png", new Vector3(-22f, 1.6f, 2f), 9f);
            Object.DestroyImmediate(clone.gameObject);
        }
        [MenuItem("TopDown/Nivel 02/Retirar escaleras superiores sin uso")]
        public static void RemoveUnusedStairs()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode primero.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded || scene.isDirty) throw new InvalidOperationException("Abre y guarda Level02 antes de retirar sus escaleras.");
            SceneManager.SetActiveScene(scene);
            var level = BuilderUtil.FindRoot(scene, "Level");
            Level02Environment.RemoveUnusedUpperStairs(level.transform);
            Level02Environment.BakeNavigation(level);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Validate();
            CaptureGameplay();
        }
        [MenuItem("TopDown/Nivel 02/Separar refuerzos de backstage y salida")]
        public static void SeparateReinforcements()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode primero.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded || scene.isDirty) throw new InvalidOperationException("Abre y guarda Level02 antes de mover sus refuerzos.");
            SceneManager.SetActiveScene(scene);
            var wave = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ReinforcementWave>(true)).Single();
            var guards = Enumerable.Range(1, 4).Select(i => wave.Squad.Single(ai => ai.name == "Response_" + i)).ToArray();
            var positions = Level02Gameplay.BackstageReinforcementPositions.Select(p => Level02Gameplay.OnNavMesh(p)).ToArray();
            var backstage = wave.transform.Find("Backstage Squad");
            if (backstage == null)
            {
                backstage = new GameObject("Backstage Squad").transform;
                backstage.SetParent(wave.transform, false);
            }
            for (int i = 0; i < guards.Length; i++)
            {
                guards[i].transform.SetParent(backstage, true);
                guards[i].transform.position = positions[i];
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Validate();
        }

        [MenuItem("TopDown/Nivel 02/Instalar paredes perimetrales y barandillas")]
        public static void InstallPerimeterWalls()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode primero.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            var level = BuilderUtil.FindRoot(scene, "Level");
            Level02Environment.AddPerimeterWalls(level.transform);
            Level02Environment.BakeNavigation(level);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Validate();
            CaptureGameplay();
            Debug.Log("[Level02] Paredes perimetrales y barandillas instaladas correctamente.");
        }

        [MenuItem("TopDown/Nivel 02/Agregar luces de backstage")]
        public static void AddBackstageLights()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode primero.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            var clubLighting = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Club Lighting");
            Transform parent = clubLighting != null ? clubLighting.transform : BuilderUtil.FindRoot(scene, "Level").transform;
            Level02Environment.AddBackstageLights(parent);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Validate();
            CaptureGameplay();
            Debug.Log("[Level02] Luces de backstage agregadas correctamente.");
        }

        [MenuItem("TopDown/Nivel 02/Ajustar anclajes de neón")]
        public static void FixHangingNeon()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode primero.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded || scene.isDirty) throw new InvalidOperationException("Abre y guarda Level02 antes de ajustar sus luces.");
            SceneManager.SetActiveScene(scene);
            Level02Environment.AddClubCables(BuilderUtil.FindRoot(scene, "Level").transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Validate();
            CaptureGameplay();
        }

        public static void UpdateClubDetails()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode primero.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded || scene.isDirty) throw new InvalidOperationException("Abre y guarda Level02 antes de aplicar los detalles.");
            SceneManager.SetActiveScene(scene);
            var manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelManager>(true)).Single();
            var wave = manager.Reinforcements;
            var oldGroup = wave.transform.Find("Exit Squad");
            if (oldGroup == null || wave.Squad.Length != 7)
            {
                var squad = wave.Squad.ToList();
                if (oldGroup != null)
                {
                    squad.RemoveAll(ai => ai == null || ai.transform.IsChildOf(oldGroup));
                    Object.DestroyImmediate(oldGroup.gameObject);
                }
                Level02Gameplay.AddExitSquad(wave.transform, manager.Player, squad);
                var serialized = new SerializedObject(wave);
                var list = serialized.FindProperty("squad");
                list.arraySize = squad.Count;
                for (int i = 0; i < squad.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = squad[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Level02Environment.AddClubCables(BuilderUtil.FindRoot(scene, "Level").transform);
            var clubLighting = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Club Lighting");
            Level02Environment.AddBackstageLights(clubLighting != null ? clubLighting.transform : BuilderUtil.FindRoot(scene, "Level").transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            CheckBloodSurfaces();
            Validate();
            CaptureGameplay();
        }

        [MenuItem("TopDown/Nivel 02/Añadir menú de pausa y música")]
        public static void AddPauseAndMusic()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MenuBuilder.AddPauseMenu(scene);
            MenuBuilder.AddLevelMusic(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("TopDown/Nivel 02/Construir y verificar")]
        public static void Build()
        {
            PrepareEnvironment();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            Level02Gameplay.Build(scene, BuilderUtil.FindRoot(scene, "Level"));
            RoofFactory.BuildLevel02(scene); // techos del club que solo aparecen en primera persona
            Level02Fixes.Apply(scene); // puertas sin travesaño bajo y paredes que paran las balas
            MenuBuilder.AddPauseMenu(scene);
            MenuBuilder.AddLevelMusic(scene);
            EditorSceneManager.SaveScene(scene);
            StoryFactory.EnsureLevel02(); // modelos de los enemigos según su arma y Nina en la entrada
            scene = SceneManager.GetSceneByPath(ScenePath);
            NeonPolishBuilder.ApplyIfInstalled(scene);
            EditorSceneManager.SaveScene(scene);
            AddBuildSettings();
            ConnectMenu();
            SceneManager.SetActiveScene(scene);
            AssetDatabase.SaveAssets();
            Validate();
            CaptureGameplay();
            Debug.Log("[Level02] Nivel construido y verificado.");
        }

        private static void AddBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (string path in new[] { "Assets/00_Scenes/Menu.unity", "Assets/00_Scenes/Level01.unity", ScenePath })
            {
                int index = scenes.FindIndex(s => s.path == path);
                if (index < 0) scenes.Add(new EditorBuildSettingsScene(path, true));
                else scenes[index].enabled = true;
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ConnectMenu()
        {
            const string path = "Assets/00_Scenes/Menu.unity";
            Scene menu = SceneManager.GetSceneByPath(path);
            bool opened = !menu.isLoaded;
            if (opened) menu = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (menu.isDirty) throw new InvalidOperationException("El menú tiene cambios sin guardar. Guarda la escena Menu antes de conectarla.");
            var controller = menu.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MainMenuController>(true)).Single();
            var first = controller.levelButtons[0];
            var second = controller.levelButtons.Length > 1 ? controller.levelButtons[1] : Object.Instantiate(first, first.transform.parent);
            second.name = "Btn_NIVEL_02";
            second.GetComponentInChildren<TMP_Text>().text = "NIVEL 02 · ELYSIUM";
            ((RectTransform)first.transform).anchoredPosition = new Vector2(0f, 90f);
            ((RectTransform)second.transform).anchoredPosition = new Vector2(0f, -25f);
            controller.levelButtons = new[] { first, second };
            controller.levelScenes = new[] { "Level01", "Level02" };
            var back = first.transform.parent.GetComponentsInChildren<NeonMenuButton>(true).First(b => b != first && b != second);
            var buttons = new[] { first.GetComponent<Button>(), second.GetComponent<Button>(), back.GetComponent<Button>() };
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length], selectOnDown = buttons[(i + 1) % buttons.Length] };
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            if (opened) EditorSceneManager.CloseScene(menu, true);
        }

        [MenuItem("TopDown/Nivel 02/Integrar sonidos y personajes")]
        public static void IntegrateSoundsAndCharacters()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode primero.");
            TopDownBuilder.BuildAndVerify();
            LevelScreenshots.Capture();

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            GameObject systems = BuilderUtil.FindRoot(scene, "Systems");
            if (systems != null) SoundFactory.EnsureGameSounds(systems);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            StoryFactory.EnsureLevel02(); // enemigos con el modelo de su arma y Nina en una zona abierta
            scene = SceneManager.GetSceneByPath(ScenePath);
            AssetDatabase.SaveAssets();

            Validate();
            CaptureGameplay();
            if (File.Exists("Temp/Level02-start.png"))
                File.Copy("Temp/Level02-start.png", "docs/level02-preview.png", true);
            File.WriteAllText("Temp/Integration.done.txt", "PASS: sonidos, modelos 3D y animaciones integrados en Level01 y Level02.");
            Debug.Log("[Level02] Sonidos y personajes integrados y verificados.");
        }

        [MenuItem("TopDown/Nivel 02/Capturas")]
        public static void CaptureGameplay()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasDirty = scene.isDirty;
            var rig = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Overdrive.CameraRig.TopDownCamera>(true)).Single();
            var clone = Object.Instantiate(rig.Cam.gameObject).GetComponent<Camera>();
            if (clone.GetComponent<AudioListener>() != null) Object.DestroyImmediate(clone.GetComponent<AudioListener>());
            var baked = LevelScreenshots.BakePoses(scene);
            try
            {
                // Warm up URP before the first saved image.
                RenderShot(clone, "Temp/Level02-warmup.png", Level02Gameplay.Start, 20f);
                RenderShot(clone, "Temp/Level02-start.png", Level02Gameplay.Start, 20f);
                RenderShot(clone, "Temp/Level02-combat.png", new Vector3(-10f, 0f, 0f), 20f);
                RenderShot(clone, "Temp/Level02-backstage.png", new Vector3(-6f, -2f, -51f), 20f);
                RenderShot(clone, "Temp/Level02-objective.png", new Vector3(-7f, -1.5f, -77f), 20f);
            }
            finally
            {
                LevelScreenshots.CleanupBakedPoses(baked);
                Object.DestroyImmediate(clone.gameObject);
            }
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
        }

        public static void PrepareEnvironment()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode antes de construir.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Level02 tiene cambios sin guardar. Guarda la escena antes de reconstruir.");
            SceneManager.SetActiveScene(scene);
            foreach (GameObject root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            var level = new GameObject("Level");
            Level02Environment.Build(level.transform);
            ScaleEnvironment(level.transform);
            ConfigureLighting();
            Level02Environment.BakeNavigation(level);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            InspectNavigation();
            CaptureOverview();
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Los personajes están a escala 1,3 y el club se modeló para gente de tamaño normal: paredes y puertas
        /// se quedaban bajas. Solo las paredes, marcos y puertas crecen en vertical (desde su base); suelos,
        /// escaleras y escalones no cambian, así que se camina igual. Si una pared tiene otro piso encima, crece
        /// solo hasta tocarlo.
        /// </summary>
        public const float WallScaleY = 1.3f;

        private static void ScaleEnvironment(Transform level)
        {
            Physics.SyncTransforms();
            var done = new System.Collections.Generic.HashSet<Transform>();
            var renderers = level.GetComponentsInChildren<MeshRenderer>(true)
                .OrderBy(r => Depth(r.transform)).ToList();
            int raised = 0;
            foreach (MeshRenderer r in renderers)
            {
                Transform t = r.transform;
                if (!IsWall(r) || HasAncestorIn(t, done)) continue;
                if (Vector3.Dot(t.up, Vector3.up) < 0.99f) continue; // solo piezas verticales
                Bounds b = r.bounds;
                float extra = b.size.y * (WallScaleY - 1f);
                // Sin atravesar el piso de arriba: lo que haya encima de la pared (que no sea otra pared).
                float room = SpaceAbove(b, r);
                extra = Mathf.Min(extra, room);
                if (extra < 0.05f) continue;
                float k = (b.size.y + extra) / b.size.y;
                Vector3 s = t.localScale;
                t.localScale = new Vector3(s.x, s.y * k, s.z);
                Physics.SyncTransforms();
                float shift = b.min.y - r.bounds.min.y;
                t.position += Vector3.up * shift; // la base se queda donde estaba
                done.Add(t);
                raised++;
            }
            Physics.SyncTransforms();
            Debug.Log($"[Level02] Paredes, marcos y puertas elevados: {raised}.");
        }

        private static bool IsWall(Renderer r)
        {
            string n = r.name.ToLowerInvariant();
            bool named = n.Contains("wall") || n.Contains("frame") || n.Contains("door") || n.Contains("pared");
            Bounds b = r.bounds;
            bool thinTall = b.size.y > 1.6f && Mathf.Min(b.size.x, b.size.z) < 0.8f && Mathf.Max(b.size.x, b.size.z) > 0.3f;
            bool notFloor = !(n.Contains("floor") || n.Contains("stair") || n.Contains("step") || n.Contains("ramp") || n.Contains("rail"));
            // Las mallas de una sala entera (paredes + suelo + escalones juntos) no: estirarlas subiría los escalones.
            bool thin = Mathf.Min(b.size.x, b.size.z) < 1.2f;
            return notFloor && thin && b.size.y > 1.6f && (named || thinTall);
        }

        /// <summary>Hueco libre por encima de la pared hasta el siguiente suelo o techo (rayos desde su parte de arriba).</summary>
        private static float SpaceAbove(Bounds b, Renderer self)
        {
            float best = float.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                float u = i == 0 ? 0.5f : (i % 2 == 0 ? 0.15f : 0.85f);
                float v = i < 3 ? 0.5f : (i == 3 ? 0.15f : 0.85f);
                Vector3 from = new Vector3(Mathf.Lerp(b.min.x, b.max.x, u), b.max.y + 0.01f, Mathf.Lerp(b.min.z, b.max.z, v));
                foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.up, 10f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(self.transform)) continue;
                    best = Mathf.Min(best, hit.distance - 0.02f);
                }
            }
            return best;
        }

        private static int Depth(Transform t)
        {
            int d = 0;
            for (; t.parent != null; t = t.parent) d++;
            return d;
        }

        private static bool HasAncestorIn(Transform t, System.Collections.Generic.HashSet<Transform> set)
        {
            for (Transform p = t.parent; p != null; p = p.parent) if (set.Contains(p)) return true;
            return false;
        }

        private static void ConfigureLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.25f, 0.34f);
            RenderSettings.fog = false;
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(65f, -25f, 0f);
            sun.color = new Color(0.68f, 0.76f, 1f);
            sun.intensity = 0.85f;
            sun.shadows = LightShadows.Soft;
            var lighting = new GameObject("Club Lighting");
            var animated = new System.Collections.Generic.List<Light>();
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                var position = new Vector3(Mathf.Cos(angle) * 14f, 2.6f, Mathf.Sin(angle) * 14f);
                animated.Add(AddLight(lighting.transform, "DanceWash_" + i, position, i % 2 == 0 ? new Color(1f, 0.08f, 0.4f) : new Color(0.06f, 0.7f, 1f), 4f, 13f));
            }
            foreach (float z in new[] { 27f, 19f, -18f, -28f, -40f, -52f, -64f, -77f })
                AddLight(lighting.transform, "RouteLight_" + z, new Vector3(z < -35f ? -6.7f : 0f, z < -35f ? 0.2f : 2f, z), new Color(0.5f, 0.12f, 1f), 3f, 12f);
            Level02Environment.AddBackstageLights(lighting.transform);
            lighting.AddComponent<ClubLights>().Setup(animated.ToArray());
            var volume = new GameObject("Elysium_GlobalVolume").AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Bloom>().threshold.Override(1f);
            profile.TryGet(out Bloom bloom);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.65f);
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var grading = profile.Add<ColorAdjustments>();
            grading.postExposure.Override(0.25f);
            grading.contrast.Override(8f);
            grading.saturation.Override(5f);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.18f);
            // Volume components are subassets, so all overrides survive closing/reopening Unity.
            string path = GeneratedPath + "/Level02_Lighting.asset";
            var stored = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (stored == null) { AssetDatabase.CreateAsset(profile, path); stored = profile; }
            else
            {
                foreach (var component in stored.components.ToArray()) Object.DestroyImmediate(component, true);
                stored.components.Clear();
                stored.components.AddRange(profile.components);
                Object.DestroyImmediate(profile);
            }
            foreach (var component in stored.components) if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, stored);
            EditorUtility.SetDirty(stored);
            volume.sharedProfile = stored;
            // Paleta ámbar/verde del Elysium original.
            Level02Look.ApplyAmbient();
            Level02Look.TuneMaterials();
            Level02Look.TuneProfile(stored);
        }

        private static Light AddLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        public static void InspectNavigation()
        {
            var report = new StringBuilder();
            Vector3[] anchors = { new Vector3(0,0,24), new Vector3(0,0,12), new Vector3(0,0,-12), new Vector3(0,-1,-20), new Vector3(0,-1.5f,-26), new Vector3(0,-2,-33), new Vector3(0,-2,-41), new Vector3(-7,-2,-53), new Vector3(-7,-2,-61), new Vector3(-7,-1.5f,-70), new Vector3(-7,-1.5f,-77) };
            for (int i = 0; i + 1 < anchors.Length; i++)
            {
                NavMesh.SamplePosition(anchors[i], out var from, 3f, NavMesh.AllAreas);
                NavMesh.SamplePosition(anchors[i+1], out var to, 3f, NavMesh.AllAreas);
                var route = new NavMeshPath();
                NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, route);
                report.AppendLine(from.position.ToString("F2") + " -> " + to.position.ToString("F2") + ": " + route.status + " end=" + (route.corners.Length > 0 ? route.corners.Last().ToString("F2") : "NONE"));
            }
            for (int z = 32; z >= -84; z -= 2)
            {
                report.Append(z.ToString().PadLeft(4)).Append(' ');
                for (int x = -30; x <= 30; x += 2)
                {
                    bool found = NavMesh.SamplePosition(new Vector3(x, 0f, z), out var hit, 3f, NavMesh.AllAreas);
                    report.Append(!found || Mathf.Abs(hit.position.x - x) > 1f || Mathf.Abs(hit.position.z - z) > 1f ? ' ' : hit.position.y > 0.8f ? '+' : '.');
                }
                report.AppendLine();
            }
            File.WriteAllText("Temp/Level02.navigation.txt", report.ToString());
        }

        public static void CaptureOverview()
        {
            var camera = new GameObject("CaptureCamera").AddComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.backgroundColor = new Color(0.015f, 0.018f, 0.035f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.fieldOfView = 50f;
            camera.farClipPlane = 250f;
            RenderShot(camera, "Temp/Level02-overview.png", new Vector3(0f, 0f, -24f), 155f);
            RenderShot(camera, "Temp/Level02-dancefloor.png", Vector3.zero, 45f);
            Object.DestroyImmediate(camera.gameObject);
        }

        internal static void RenderShot(Camera camera, string path, Vector3 focus, float distance)
        {
            Quaternion rotation = Quaternion.Euler(70f, 0f, 0f);
            camera.transform.SetPositionAndRotation(focus + rotation * Vector3.back * distance, rotation);
            var render = new RenderTexture(1600, 1000, 24);
            var previous = RenderTexture.active;
            var oldTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                var texture = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, render.width, render.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                render.Release();
                Object.DestroyImmediate(render);
            }
        }

        [MenuItem("TopDown/Nivel 02/Verificar escena")]
        public static void Validate()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Physics.SyncTransforms();
            var failures = Level02Validation.Check(scene);
            if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
            Debug.Log("[Level02] Validación correcta.");
        }
    }
}
