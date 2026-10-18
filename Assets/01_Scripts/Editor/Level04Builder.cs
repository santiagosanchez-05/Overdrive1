using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Overdrive.Driving;
using Overdrive.Level;
using Overdrive.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Nivel 4 (escena de Jhael con la ciudad): huida en coche con Nina de copiloto. Junto a la ciudad empieza una
    /// autopista nocturna de ~3.3 km con curvas, quitamiedos de neón, farolas y edificios. Las camionetas de los dealers
    /// llegan por oleadas, van por los carriles laterales y embisten; se las destruye a tiros (ratón y clic izquierdo).
    /// Siempre se puede seguir avanzando. Construcción idempotente por nombres.
    /// </summary>
    internal static class Level04Builder
    {
        public const string ScenePath = "Assets/00_Scenes/Level04.unity";
        public const string Level03Path = "Assets/00_Scenes/Level03.unity";
        public const string RoadName = "Highway";
        public const string SystemsName = "ChaseSystems";
        public const string PlayerName = "PlayerCar";
        public const float RoadLength = 3300f;
        public const float RoadWidth = 24f;
        public const float PointSpacing = 6f;
        public const float StartAlong = 20f;

        /// <summary>Oleadas: dónde aparecen y cuántas camionetas traen (normales y blindadas).</summary>
        public static readonly ChaseWave[] Waves =
        {
            Wave(150f, 2, 0),
            Wave(560f, 3, 0),
            Wave(1000f, 3, 0),
            Wave(1450f, 4, 0),
            Wave(1900f, 4, 0),
            Wave(2350f, 5, 0),
            Wave(2800f, 3, 1),
        };

        private static ChaseWave Wave(float start, int dealers, int armored) =>
            new ChaseWave { startDistance = start, dealers = dealers, armored = armored };

        [MenuItem("TopDown/Nivel 04/Construir y verificar")]
        public static void BuildAndVerify()
        {
            Build();
            var c = new Checker();
            Verify(c);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (c.Failures.Count > 0)
            {
                string message = $"[Level04] Verificación con {c.Failures.Count} fallo(s) y {c.Passed} correctas:\n - " + string.Join("\n - ", c.Failures);
                Debug.LogError(message);
                if (Application.isBatchMode) throw new Exception(message);
                return;
            }
            Debug.Log($"[Level04] Verificación OK: {c.Passed} comprobaciones correctas.");
        }

        public static void Build()
        {
            VehicleFactory.EnsurePrefabs();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Vector3 start = StartPoint(scene);
            Vector3[] points = Centerline(start);
            GameObject roadRoot = BuilderUtil.EnsureRoot(scene, RoadName);
            var road = BuilderUtil.EnsureComponent<RoadPath>(roadRoot);
            road.Set(points, RoadWidth);
            EditorUtility.SetDirty(road);

            BuildRoadMesh(roadRoot.transform, points);
            BuildRails(roadRoot.transform, points);
            BuildLamps(roadRoot.transform, road);
            BuildCity(roadRoot.transform, road);
            RemoveGates(roadRoot.transform);
            BuildFinish(roadRoot.transform, road);
            Car player = EnsurePlayer(scene, road);
            EnsureSystems(scene, road, player);
            EnsureCamera(scene, player);
            EnsureLighting(scene);
            MenuBuilder.AddPauseMenu(scene);
            MenuBuilder.AddLevelMusic(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ChainLevel03();
            AddBuildSettings();
            AssetDatabase.SaveAssets();
        }

        /// <summary>La autopista empieza a la salida de la ciudad de Jhael (por su lado norte), o en el origen si no está.</summary>
        public static Vector3 StartPoint(Scene scene)
        {
            Renderer[] city = scene.GetRootGameObjects()
                .Where(r => r.name != RoadName && r.name != PlayerName && r.GetComponentInChildren<Car>() == null)
                .SelectMany(r => r.GetComponentsInChildren<Renderer>()).ToArray();
            if (city.Length == 0) return Vector3.zero;
            Bounds b = city[0].bounds;
            foreach (Renderer r in city) b.Encapsulate(r.bounds);
            return new Vector3(b.center.x, b.min.y, b.max.z + 25f);
        }

        /// <summary>Eje de la carretera: recto al principio y luego con curvas suaves, de sur a norte.</summary>
        public static Vector3[] Centerline(Vector3 start)
        {
            var pts = new List<Vector3>();
            for (float z = 0f; z <= RoadLength; z += PointSpacing)
            {
                float ease = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(60f, 260f, z));
                float x = ease * (60f * Mathf.Sin(z / 230f) + 26f * Mathf.Sin(z / 91f + 1.3f));
                pts.Add(start + new Vector3(x, 0f, z));
            }
            return pts.ToArray();
        }

        // ---------- Carretera ----------

        private static void BuildRoadMesh(Transform root, Vector3[] pts)
        {
            GameObject go = BuilderUtil.EnsureChild(root, "Asphalt");
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float along = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                if (i > 0) along += Vector3.Distance(pts[i - 1], pts[i]);
                Vector3 right = Right(pts, i) * (RoadWidth * 0.5f);
                verts.Add(pts[i] - right + Vector3.up * 0.01f);
                verts.Add(pts[i] + right + Vector3.up * 0.01f);
                uvs.Add(new Vector2(0f, along / RoadWidth));
                uvs.Add(new Vector2(1f, along / RoadWidth));
                if (i == 0) continue;
                int a = (i - 1) * 2;
                tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            var mesh = new Mesh { name = "Highway", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string meshPath = $"{VehicleFactory.VehiclesPath}/Highway.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            BuilderUtil.EnsureComponent<MeshFilter>(go).sharedMesh = mesh;
            BuilderUtil.EnsureComponent<MeshRenderer>(go).sharedMaterial = RoadMaterial();

            // Suelo oscuro bajo todo (fuera de la calzada).
            GameObject ground = BuilderUtil.EnsurePrimitiveChild(root, "Ground", PrimitiveType.Plane, false);
            Vector3 mid = (pts[0] + pts[pts.Length - 1]) * 0.5f;
            BuilderUtil.SetLocal(ground, mid - Vector3.up * 0.02f, new Vector3(60f, 1f, RoadLength / 10f + 20f), Quaternion.identity);
            BuilderUtil.SetMaterial(ground, VehicleFactory.EnsureFlatMaterial("HighwayGround", new Color(0.04f, 0.04f, 0.06f), 0f));
        }

        private static Vector3 Right(Vector3[] pts, int i)
        {
            Vector3 d = pts[Mathf.Min(i + 1, pts.Length - 1)] - pts[Mathf.Max(i - 1, 0)];
            d.y = 0f;
            return Vector3.Cross(Vector3.up, d.normalized);
        }

        /// <summary>Asfalto oscuro con líneas discontinuas entre los cuatro carriles y bordes continuos (textura generada).</summary>
        private static Material RoadMaterial()
        {
            string texPath = AssetFactory.NumberedFolder("Textures", "05_Textures") + "/T_Highway.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
            {
                const int w = 256, h = 256;
                var tex = new Texture2D(w, h);
                var rng = new System.Random(4);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n = 0.07f + (float)rng.NextDouble() * 0.03f;
                    Color c = new Color(n, n, n * 1.15f);
                    bool edge = x < 6 || x > w - 7;
                    bool lane = (Mathf.Abs(x - w / 4) < 2 || Mathf.Abs(x - w / 2) < 2 || Mathf.Abs(x - 3 * w / 4) < 2) && (y % 128) < 64;
                    if (edge || lane) c = new Color(0.85f, 0.82f, 0.7f);
                    tex.SetPixel(x, y, c);
                }
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(texPath);
            }
            Material mat = VehicleFactory.EnsureFlatMaterial("Highway", Color.white, 0f);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetFloat("_Smoothness", 0.45f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Quitamiedos a los dos lados: cajas sólidas cada 12 m y una línea de neón encima.</summary>
        private static void BuildRails(Transform root, Vector3[] pts)
        {
            GameObject rails = BuilderUtil.EnsureChild(root, "Rails");
            for (int i = rails.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(rails.transform.GetChild(i).gameObject);
            Material railMat = VehicleFactory.EnsureFlatMaterial("Rail", new Color(0.15f, 0.15f, 0.2f), 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                var line = new List<Vector3>();
                for (int i = 0; i + 2 < pts.Length; i += 2)
                {
                    Vector3 a = pts[i] + Right(pts, i) * (side * (RoadWidth * 0.5f + 0.4f));
                    Vector3 b = pts[i + 2] + Right(pts, i + 2) * (side * (RoadWidth * 0.5f + 0.4f));
                    GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    seg.name = $"Rail_{(side < 0 ? "L" : "R")}_{i}";
                    seg.transform.SetParent(rails.transform, false);
                    seg.transform.position = (a + b) * 0.5f + Vector3.up * 0.6f;
                    seg.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
                    seg.transform.localScale = new Vector3(0.6f, 1.2f, Vector3.Distance(a, b) + 0.3f);
                    seg.GetComponent<Renderer>().sharedMaterial = railMat;
                    seg.isStatic = true;
                    line.Add(a + Vector3.up * 1.25f);
                }
                GameObject neon = new GameObject(side < 0 ? "Neon_L" : "Neon_R");
                neon.transform.SetParent(rails.transform, false);
                var lr = neon.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = line.Count;
                lr.SetPositions(line.ToArray());
                lr.widthMultiplier = 0.35f;
                lr.alignment = LineAlignment.View;
                lr.sharedMaterial = AssetFactory.Mat(AssetFactory.LedMaterial(side < 0 ? LedColor.Magenta : LedColor.Teal, false));
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>Farolas cada 45 m, alternando lado: poste y luz cálida (sin sombras).</summary>
        private static void BuildLamps(Transform root, RoadPath road)
        {
            GameObject lamps = BuilderUtil.EnsureChild(root, "Lamps");
            for (int i = lamps.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(lamps.transform.GetChild(i).gameObject);
            Material pole = VehicleFactory.EnsureFlatMaterial("LampPole", new Color(0.1f, 0.1f, 0.12f), 0f);
            int n = 0;
            for (float d = 30f; d < road.Length - 20f; d += 45f, n++)
            {
                float side = n % 2 == 0 ? 1f : -1f;
                Vector3 basePos = road.PointAt(d) + road.RightAt(d) * side * (RoadWidth * 0.5f + 1.6f);
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                p.name = $"Lamp_{n}";
                p.transform.SetParent(lamps.transform, false);
                p.transform.position = basePos + Vector3.up * 4f;
                p.transform.localScale = new Vector3(0.3f, 4f, 0.3f);
                p.GetComponent<Renderer>().sharedMaterial = pole;
                Object.DestroyImmediate(p.GetComponent<Collider>());
                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(p.transform, false);
                lightGo.transform.position = basePos + Vector3.up * 8f - road.RightAt(d) * side * 3f;
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = n % 4 == 3 ? new Color(1f, 0.35f, 0.75f) : new Color(1f, 0.78f, 0.5f);
                light.range = 22f;
                light.intensity = 3.2f;
                light.shadows = LightShadows.None;
            }
        }

        /// <summary>Edificios oscuros con ventanas encendidas a los dos lados (sin colisión: los quitamiedos ya cierran).</summary>
        private static void BuildCity(Transform root, RoadPath road)
        {
            GameObject city = BuilderUtil.EnsureChild(root, "Buildings");
            for (int i = city.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(city.transform.GetChild(i).gameObject);
            Material dark = VehicleFactory.EnsureFlatMaterial("Building", new Color(0.07f, 0.07f, 0.1f), 0f);
            Material windows = WindowsMaterial();
            var rng = new System.Random(17);
            int n = 0;
            for (float d = 10f; d < road.Length; d += 28f)
            for (int side = -1; side <= 1; side += 2, n++)
            {
                float w = 14f + (float)rng.NextDouble() * 14f, h = 10f + (float)rng.NextDouble() * 45f, depth = 14f + (float)rng.NextDouble() * 12f;
                Vector3 pos = road.PointAt(d) + road.RightAt(d) * side * (RoadWidth * 0.5f + 8f + depth * 0.5f + (float)rng.NextDouble() * 6f);
                GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = $"Building_{n}";
                b.transform.SetParent(city.transform, false);
                b.transform.position = pos + Vector3.up * h * 0.5f;
                b.transform.rotation = Quaternion.LookRotation(road.TangentAt(d), Vector3.up);
                b.transform.localScale = new Vector3(depth, h, w);
                b.GetComponent<Renderer>().sharedMaterial = rng.NextDouble() < 0.6 ? windows : dark;
                Object.DestroyImmediate(b.GetComponent<Collider>());
                b.isStatic = true;
            }
        }

        private static Material WindowsMaterial()
        {
            string texPath = AssetFactory.NumberedFolder("Textures", "05_Textures") + "/T_Windows.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
            {
                const int size = 128;
                var tex = new Texture2D(size, size);
                var rng = new System.Random(9);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) tex.SetPixel(x, y, new Color(0.03f, 0.03f, 0.05f));
                for (int wy = 4; wy < size - 8; wy += 12)
                for (int wx = 4; wx < size - 8; wx += 10)
                {
                    if (rng.NextDouble() < 0.55) continue;
                    Color lit = rng.NextDouble() < 0.7 ? new Color(1f, 0.8f, 0.5f) : new Color(0.6f, 0.35f, 1f);
                    for (int y = 0; y < 6; y++)
                    for (int x = 0; x < 5; x++) tex.SetPixel(wx + x, wy + y, lit);
                }
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(texPath);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            Material mat = VehicleFactory.EnsureFlatMaterial("BuildingWindows", Color.white, 0f);
            mat.SetTexture("_BaseMap", texture);
            mat.SetTextureScale("_BaseMap", new Vector2(2f, 3f));
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", texture);
            mat.SetColor("_EmissionColor", Color.white * 1.2f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Ya no hay controles que corten la carretera: siempre se puede seguir avanzando.</summary>
        private static void RemoveGates(Transform root)
        {
            Transform gates = root.Find("Gates");
            if (gates != null) Object.DestroyImmediate(gates.gameObject);
        }

        /// <summary>El piso franco al final: un garaje con luz verde y un muro que cierra la carretera.</summary>
        private static void BuildFinish(Transform root, RoadPath road)
        {
            GameObject finish = BuilderUtil.EnsureChild(root, "Safehouse");
            for (int i = finish.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(finish.transform.GetChild(i).gameObject);
            float d = road.Length;
            finish.transform.SetPositionAndRotation(road.PointAt(d), Quaternion.LookRotation(road.TangentAt(d), Vector3.up));
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "EndWall";
            wall.transform.SetParent(finish.transform, false);
            wall.transform.localPosition = new Vector3(0f, 4f, 6f);
            wall.transform.localScale = new Vector3(RoadWidth + 6f, 8f, 3f);
            wall.GetComponent<Renderer>().sharedMaterial = VehicleFactory.EnsureFlatMaterial("Building", new Color(0.07f, 0.07f, 0.1f), 0f);
            var green = new GameObject("SafeLight").AddComponent<Light>();
            green.transform.SetParent(finish.transform, false);
            green.transform.localPosition = new Vector3(0f, 6f, -20f);
            green.color = new Color(0.2f, 1f, 0.55f);
            green.range = 40f;
            green.intensity = 6f;
        }

        // ---------- Coche, sistemas, cámara ----------

        private static Car EnsurePlayer(Scene scene, RoadPath road)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleFactory.PlayerCarPrefabPath);
            GameObject player = BuilderUtil.FindRoot(scene, PlayerName);
            if (player != null && PrefabUtility.GetCorrespondingObjectFromSource(player) != prefab)
            {
                Object.DestroyImmediate(player);
                player = null;
            }
            if (player == null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                player.name = PlayerName;
            }
            player.transform.SetPositionAndRotation(road.PointAt(StartAlong), Quaternion.LookRotation(road.TangentAt(StartAlong), Vector3.up));
            var rail = player.GetComponent<RailCar>();
            if (rail != null)
            {
                var so = new SerializedObject(rail);
                so.FindProperty("road").objectReferenceValue = road;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return player.GetComponent<Car>();
        }

        private static void EnsureSystems(Scene scene, RoadPath road, Car player)
        {
            GameObject systems = BuilderUtil.EnsureRoot(scene, SystemsName);
            var dialogue = BuilderUtil.EnsureComponent<RadioDialogue>(systems);
            var level = BuilderUtil.EnsureComponent<ChaseLevel>(systems);
            var so = new SerializedObject(level);
            so.FindProperty("road").objectReferenceValue = road;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("dialogue").objectReferenceValue = dialogue;
            so.FindProperty("dealerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Car>(VehicleFactory.DealerPrefabPath);
            so.FindProperty("armoredPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Car>(VehicleFactory.ArmoredPrefabPath);
            so.FindProperty("nextScene").stringValue = "Ending";
            SerializedProperty waves = so.FindProperty("waves");
            waves.arraySize = Waves.Length;
            for (int i = 0; i < Waves.Length; i++)
            {
                SerializedProperty w = waves.GetArrayElementAtIndex(i);
                w.FindPropertyRelative("startDistance").floatValue = Waves[i].startDistance;
                w.FindPropertyRelative("dealers").intValue = Waves[i].dealers;
                w.FindPropertyRelative("armored").intValue = Waves[i].armored;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            BuilderUtil.SetRef(BuilderUtil.EnsureComponent<ChaseHUD>(systems), "level", level);
            SoundFactory.EnsureGameSounds(systems);
        }

        private static void EnsureCamera(Scene scene, Car player)
        {
            Camera cam = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                SceneManager.MoveGameObjectToScene(go, scene);
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.tag = "MainCamera";
            cam.fieldOfView = 50f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.01f, 0.04f);
            var follow = BuilderUtil.EnsureComponent<CarCamera>(cam.gameObject);
            BuilderUtil.SetRef(follow, "target", player);
            var gun = player.GetComponent<CarGun>();
            if (gun != null) BuilderUtil.SetRef(gun, "aimCamera", cam);
            follow.Snap();
        }

        /// <summary>Noche: luz direccional tenue y azulada, ambiente muy oscuro (mandan las farolas y el neón).</summary>
        private static void EnsureLighting(Scene scene)
        {
            Light sun = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun != null)
            {
                sun.color = new Color(0.45f, 0.5f, 0.85f);
                sun.intensity = 0.25f;
                sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.07f, 0.12f);
        }

        /// <summary>El nivel 3 (de Jhael) lleva a este al completarse; este, al menú.</summary>
        private static void ChainLevel03()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Level03Path) == null) return;
            Scene scene = EditorSceneManager.OpenScene(Level03Path, OpenSceneMode.Single);
            foreach (LevelManager manager in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelManager>(true)))
            {
                var so = new SerializedObject(manager);
                so.FindProperty("nextScene").stringValue = "Level04";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void AddBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int index = scenes.FindIndex(s => s.path == ScenePath);
            if (index < 0) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else scenes[index].enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------- Verificación ----------

        public static void Verify(Checker c)
        {
            VerifyLogic(c);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            VerifyScene(c, scene);
            VerifySimulation(c, scene);
        }

        private static void VerifyLogic(Checker c)
        {
            c.Check(DrivingLogic.RamDamage(15f, 15f, 0f, 3.5f) > 0f && DrivingLogic.RamDamage(15f, 15f, 0f, 3.5f) < 50f,
                "Un volantazo de un dealer hace daño, pero hacen falta varios para destrozar el coche");
            c.Check(DrivingLogic.RamDamage(30f, 30f, 0f, 0f) == 0f, "Embestir con el coche del jugador no destroza a nadie (se dispara)");
            c.Check(DrivingLogic.Closing(new Vector3(0f, 0f, 30f), new Vector3(0f, 0f, 40f), Vector3.back) == 10f,
                "Si te alcanzan por detrás cuenta la diferencia de velocidad, no la velocidad entera");
            // Sobre la carretera: dentro de la calzada, choques por cercanía, persecución y giro visual.
            c.Check(Mathf.Approximately(RailLogic.MaxLateral(24f, 2.6f), 10.3f), "El coche se mueve de lado sin salirse de la calzada");
            c.Check(RailLogic.Touching(2f, 2f, 6f, 6f, 2.6f, 2.6f) && !RailLogic.Touching(2f, 4f, 6f, 6f, 2.6f, 2.6f) && !RailLogic.Touching(8f, 1f, 6f, 6f, 2.6f, 2.6f),
                "Dos coches se tocan solo si están a la par y pegados de lado");
            c.Check(RailLogic.ChaserSpeed(28f, 50f, 20f) == 48f && Mathf.Abs(RailLogic.ChaserSpeed(28f, 0f, 20f) - 28f) < 0.01f && RailLogic.ChaserSpeed(28f, -20f, 20f) < 28f,
                "Los dealers van muy rápido hasta alcanzar al jugador y luego a su ritmo");
            c.Check(RailLogic.Yaw(0f, 28f) == 0f && Mathf.Abs(RailLogic.Yaw(50f, 28f)) <= 18f, "Al cambiar de carril el coche apenas gira (como mucho 18 grados)");

            // Carriles: los dealers van por fuera, embisten estando a la par y vuelven a su carril.
            float lane = DrivingLogic.LaneOffset(1, RoadWidth);
            c.Check(DrivingLogic.LaneOffset(-1, RoadWidth) < -RoadWidth * 0.3f && lane > RoadWidth * 0.3f && lane < RoadWidth * 0.5f - 2f,
                "Cada dealer tiene su carril exterior (izquierdo o derecho), dentro de la calzada");
            c.Check(DrivingLogic.ShouldAttack(2f, 8f, 0f) && !DrivingLogic.ShouldAttack(20f, 8f, 0f) && !DrivingLogic.ShouldAttack(2f, 8f, 1f),
                "Embiste solo si va a la par del jugador y ya pasó su espera");
            c.Check(Mathf.Approximately(DrivingLogic.TargetLateral(lane, false, 0f), lane) && DrivingLogic.TargetLateral(lane, true, 0f) < 0f,
                "Al embestir va hacia el jugador (y un poco más allá); si no, a su carril");
            c.Check(DrivingLogic.DesiredSpeed(25f, 5f, 40f) > 25f && DrivingLogic.DesiredSpeed(25f, -5f, 40f) < 25f, "Acelera o frena para ponerse en su sitio");
            var placements = Enumerable.Range(0, 6).Select(ChaseLogic.Placement).ToList();
            c.Check(placements.Count(p => p.side < 0) == 3 && placements.Count(p => p.side > 0) == 3 && placements.Select(p => p.slot).Distinct().Count() >= 4,
                "Las camionetas se reparten a los dos lados y en sitios distintos");

            // Disparo: el punto bajo el ratón en el plano del suelo.
            c.Check(DrivingLogic.AimOnPlane(new Ray(new Vector3(0f, 20f, 0f), new Vector3(0f, -1f, 1f).normalized), 0f, out Vector3 aimPoint) &&
                    Vector3.Distance(aimPoint, new Vector3(0f, 0f, 20f)) < 0.01f, "Se apunta al suelo bajo el ratón");
            c.Check(!DrivingLogic.AimOnPlane(new Ray(Vector3.up, Vector3.up), 0f, out _), "Mirando al cielo no hay punto de mira");

            c.Check(ChaseLogic.WaveAt(Waves, 0f) == -1 && ChaseLogic.WaveAt(Waves, Waves[2].startDistance + 1f) == 2, "Cada oleada empieza al pasar por su punto");
            for (int i = 1; i < Waves.Length; i++) c.Check(Waves[i].startDistance > Waves[i - 1].startDistance + 300f, $"Oleada {i + 1}: bien separada de la anterior");
            c.Check(ChaseLogic.Finished(RoadLength - 10f, RoadLength, 50f) && !ChaseLogic.Finished(100f, RoadLength, 50f), "Se gana al llegar al final");
            c.Check(Waves.Sum(ChaseLogic.Total) >= 25 && RoadLength >= 3000f, $"Nivel largo: {Waves.Sum(ChaseLogic.Total)} camionetas en {RoadLength / 1000f:F1} km");
            c.Check(ChaseLines.WaveStart.Length >= Waves.Length && ChaseLines.WaveClear.Length >= Waves.Length && ChaseLines.Intro.Length > 0,
                "Nina tiene frase para el inicio y para cada oleada");
        }

        private static void VerifyScene(Checker c, Scene scene)
        {
            GameObject roadGo = BuilderUtil.FindRoot(scene, RoadName);
            var road = roadGo != null ? roadGo.GetComponent<RoadPath>() : null;
            c.NotNull(road, "Hay carretera");
            if (road == null) return;
            c.Check(road.Length > RoadLength - 10f && Mathf.Approximately(road.Width, RoadWidth), $"La carretera mide {road.Length:F0} m y {road.Width} m de ancho");
            float maxStep = 0f;
            for (int i = 1; i < road.Points.Length; i++) maxStep = Mathf.Max(maxStep, Vector3.Distance(road.Points[i - 1], road.Points[i]));
            c.Check(maxStep < PointSpacing * 1.6f, "La carretera es continua (sin saltos)");
            c.Check(roadGo.transform.Find("Rails").childCount > 400, "Quitamiedos a los dos lados en toda la carretera");
            c.Check(roadGo.transform.Find("Lamps").childCount > 60, "Farolas a lo largo de la autopista");
            c.Check(roadGo.transform.Find("Gates") == null, "No hay controles que corten la carretera: siempre se puede avanzar");
            // Nada sólido cruza la calzada entre la salida y la meta.
            Physics.SyncTransforms();
            bool blocked = false;
            for (float d = StartAlong + 10f; d < road.Length - 20f && !blocked; d += 12f)
            {
                Vector3 p = road.PointAt(d) + Vector3.up;
                foreach (RaycastHit hit in Physics.RaycastAll(p - road.TangentAt(d) * 6f, road.TangentAt(d), 12f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.GetComponentInParent<Car>() == null) blocked = true;
            }
            c.Check(!blocked, "La calzada está libre de principio a fin");

            var level = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChaseLevel>(true)).FirstOrDefault();
            c.NotNull(level, "Hay ChaseLevel");
            if (level == null) return;
            c.Check(level.Waves.Length == Waves.Length, "Todas las oleadas configuradas");
            c.Check(level.Player != null && level.Player.IsPlayer && level.Player.GetComponent<PlayerCarInput>() != null, "El coche del jugador se conduce con el teclado");
            var gun = level.Player != null ? level.Player.GetComponent<CarGun>() : null;
            c.Check(gun != null && level.Player.RamDamagePerMps == 0f, "El jugador dispara (y embestir no destroza a nadie)");
            c.Check(level.Player.Rail != null && level.Player.Rail.Road == road && level.Player.Body.isKinematic,
                "El coche va sobre la carretera, sin física que lo haga girar");
            c.Check(level.Dialogue != null && scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChaseHUD>(true)).Any(), "Hay diálogo de Nina y HUD");
            c.Check(level.NextScene == "Ending", "Al acabar continúa al epílogo y los créditos");
            float startAlong = road.Project(level.Player.transform.position, out float lateral);
            c.Check(Mathf.Abs(startAlong - StartAlong) < 1f && Mathf.Abs(lateral) < 0.5f, "El coche empieza en la carretera, al principio");
            var cam = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CarCamera>(true)).FirstOrDefault();
            c.Check(cam != null && cam.Target == level.Player, "La cámara sigue al coche");
            if (cam != null && level.Player.Rail != null)
            {
                // En cualquier punto de la carretera y de cualquier carril, el coche queda en el centro de la pantalla.
                Camera view = cam.GetComponent<Camera>();
                RailCar rail = level.Player.Rail;
                float worst = 0f;
                foreach (float d in new[] { 100f, 900f, 1700f, 2500f, 3100f })
                foreach (float lat in new[] { -8f, 0f, 8f })
                {
                    rail.Road = road;
                    Place(rail, d, lat);
                    cam.Follow();
                    Vector3 vp = view.WorldToViewportPoint(level.Player.transform.position);
                    worst = Mathf.Max(worst, Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f)));
                }
                c.Check(worst < 0.01f, $"El coche está siempre en el centro de la cámara (desvío máximo {worst:P1})");
                Place(rail, StartAlong, 0f);
                cam.Follow();
            }
            c.Check(scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<Renderer>() != null && r.name.Contains("maya2sketchfab")), "Sigue la ciudad de Jhael");
            c.Check(EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled), "El nivel 4 está en Build Settings");

            foreach (string path in new[] { VehicleFactory.PlayerCarPrefabPath, VehicleFactory.DealerPrefabPath, VehicleFactory.ArmoredPrefabPath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var car = prefab != null ? prefab.GetComponent<Car>() : null;
                var box = prefab != null ? prefab.GetComponent<BoxCollider>() : null;
                c.Check(car != null && box != null && box.size.z > box.size.x * 1.5f && box.size.z > 5f,
                    $"{Path.GetFileNameWithoutExtension(path)}: coche más largo que ancho ({box?.size.x:F1} x {box?.size.z:F1} m)");
                c.Check(prefab != null && prefab.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null)), $"{Path.GetFileNameWithoutExtension(path)}: se ve con su textura");
            }
            var armored = AssetDatabase.LoadAssetAtPath<Car>(VehicleFactory.ArmoredPrefabPath);
            var dealer = AssetDatabase.LoadAssetAtPath<Car>(VehicleFactory.DealerPrefabPath);
            c.Check(armored != null && dealer != null && armored.MaxHealth > dealer.MaxHealth * 3f, "La blindada aguanta muchas más balas");
            c.Check(gun != null && dealer != null && Mathf.CeilToInt(dealer.MaxHealth / gun.Damage) <= 6, $"Una camioneta cae con unos pocos disparos ({Mathf.CeilToInt(dealer.MaxHealth / gun.Damage)})");
        }

        /// <summary>
        /// Simulación sobre la carretera: el coche avanza solo y se mueve de lado sin salirse; las camionetas llegan deprisa
        /// hasta ponerse a la par en su carril; al embestir solo desplazan un poco al jugador, que sigue recto a su
        /// velocidad, y vuelven a su carril; embestirlas no las destroza pero a tiros sí; las oleadas y el final.
        /// </summary>
        private static void VerifySimulation(Checker c, Scene scene)
        {
            var level = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChaseLevel>(true)).FirstOrDefault();
            if (level == null) return;
            Car player = level.Player;
            RoadPath road = level.Road;
            RailCar pr = player.Rail;
            var input = player.GetComponent<PlayerCarInput>();
            c.Check(pr != null && input != null, "El coche del jugador va sobre la carretera");
            if (pr == null || input == null) return;
            try
            {
                player.ResetCar();
                pr.Road = road;
                Place(pr, StartAlong, 0f);
                pr.Speed = input.CruiseSpeed;
                input.Override = Vector2.zero;
                float startAlong = pr.Along;
                for (int i = 0; i < 150; i++) { input.Tick(0.02f); pr.Step(0.02f); }
                c.Check(Mathf.Abs(pr.Along - startAlong - input.CruiseSpeed * 3f) < 2f && Mathf.Abs(pr.Lateral) < 0.01f,
                    $"Simulación: sin tocar nada, el coche avanza solo y recto ({pr.Along - startAlong:F0} m en 3 s)");
                input.Override = new Vector2(1f, 0f);
                for (int i = 0; i < 200; i++) { input.Tick(0.02f); pr.Step(0.02f); }
                float max = RailLogic.MaxLateral(road.Width, pr.Width);
                c.Check(Mathf.Abs(pr.Lateral - max) < 0.01f, $"Simulación: con D se cambia de carril sin salirse de la calzada ({pr.Lateral:F1} m)");
                input.Override = new Vector2(0f, -1f);
                for (int i = 0; i < 200; i++) { input.Tick(0.02f); pr.Step(0.02f); }
                c.Check(pr.Speed >= input.MinSpeed - 0.01f && pr.Speed < input.CruiseSpeed, $"Simulación: frenando sigue avanzando ({pr.Speed:F0} m/s)");
                input.Override = Vector2.zero;
                Place(pr, StartAlong + 200f, 0f);
                pr.Speed = input.CruiseSpeed;

                var dealerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleFactory.DealerPrefabPath);
                Car NewDealer(float along, float lateral, float speed, int side, float slot)
                {
                    var car = ((GameObject)PrefabUtility.InstantiatePrefab(dealerPrefab, scene)).GetComponent<Car>();
                    // A la altura de la carretera, como en el juego (ChaseLevel.Spawn).
                    car.transform.position = new Vector3(0f, player.transform.position.y, 0f);
                    car.ResetCar();
                    car.Rail.Road = road;
                    Place(car.Rail, along, lateral);
                    car.Rail.Speed = speed;
                    car.GetComponent<EnemyCarAI>().Setup(car, player, road, side, slot);
                    return car;
                }
                void Sim(float seconds, params Car[] dealers)
                {
                    for (int i = 0; i < seconds / 0.02f; i++)
                    {
                        input.Tick(0.02f);
                        foreach (Car d in dealers) d.GetComponent<EnemyCarAI>().Think(0.02f);
                        pr.Step(0.02f);
                        foreach (Car d in dealers) d.Rail.Step(0.02f);
                    }
                }

                // Llega deprisa por detrás hasta su sitio, en su carril, y se queda a la par.
                float lane = DrivingLogic.LaneOffset(1, road.Width);
                Car dealer = NewDealer(pr.Along - 55f, lane, pr.Speed + 15f, 1, 2f);
                var ai = dealer.GetComponent<EnemyCarAI>();
                Sim(4f, dealer);
                float gap = dealer.Rail.Along - pr.Along;
                c.Check(Mathf.Abs(gap - ai.Slot) < 4f, $"Simulación: la camioneta llega deprisa hasta ponerse a la par ({gap:F1} m)");
                c.Check(Mathf.Abs(dealer.Rail.Lateral - lane) < 1f, $"Simulación: va por su carril lateral ({dealer.Rail.Lateral:F1} m)");
                Sim(2f, dealer);
                gap = dealer.Rail.Along - pr.Along;
                c.Check(Mathf.Abs(gap - ai.Slot) < 3f && Mathf.Abs(dealer.Rail.Speed - pr.Speed) < 4f, "Simulación: y se queda a su ritmo");

                // Embiste: el jugador se desplaza un poco de lado, pierde vida y sigue recto a su velocidad.
                player.ResetCar();
                float lateralBefore = pr.Lateral, alongBefore = pr.Along, speedBefore = pr.Speed, healthBefore = player.Health;
                float maxShift = 0f, maxYaw = 0f;
                int hitsBefore = ai.Hits; // ya puede haber embestido por su cuenta mientras iba a la par
                ai.ForceAttack();
                for (int i = 0; i < 150 && ai.Hits == hitsBefore; i++)
                {
                    Sim(0.02f, dealer);
                    maxShift = Mathf.Max(maxShift, Mathf.Abs(pr.Lateral - lateralBefore));
                    maxYaw = Mathf.Max(maxYaw, Vector3.Angle(player.transform.forward, road.TangentAt(pr.Along)));
                }
                c.Check(ai.Hits > hitsBefore && player.Health < healthBefore, $"Simulación: la camioneta embiste al jugador y le quita vida ({healthBefore - player.Health:F0})");
                for (int i = 0; i < 60; i++)
                {
                    Sim(0.02f, dealer);
                    maxShift = Mathf.Max(maxShift, Mathf.Abs(pr.Lateral - lateralBefore));
                    maxYaw = Mathf.Max(maxYaw, Vector3.Angle(player.transform.forward, road.TangentAt(pr.Along)));
                }
                c.Check(maxShift > 0.5f && maxShift < 3f, $"Simulación: el golpe solo lo desplaza un poco de lado ({maxShift:F1} m)");
                c.Check(maxYaw < 20f, $"Simulación: no lo hace girar ({maxYaw:F0} grados como mucho)");
                c.Check(Mathf.Abs(pr.Speed - speedBefore) < 0.01f && pr.Along > alongBefore + 20f, "Simulación: el jugador sigue avanzando a su velocidad");
                // Vuelve en cuanto golpea (antes de que acabe su espera mínima de 2 s y pueda embestir otra vez).
                Sim(1.5f, dealer);
                c.Check(!ai.Attacking && Mathf.Abs(dealer.Rail.Lateral - lane) < 1.5f, $"Simulación: después la camioneta vuelve a su carril ({dealer.Rail.Lateral:F1} m)");

                // El jugador echándose encima de ella no la destroza.
                float dealerHealth = dealer.Health;
                input.Override = new Vector2(1f, 0f);
                Sim(1.5f, dealer);
                input.Override = Vector2.zero;
                c.Check(!dealer.Destroyed && Mathf.Approximately(dealer.Health, dealerHealth), "Simulación: embestir con el coche no destroza la camioneta");

                // A tiros sí.
                var gun = player.GetComponent<CarGun>();
                Place(dealer.Rail, pr.Along + 20f, pr.Lateral);
                Physics.SyncTransforms();
                int shots = 0;
                for (; shots < 20 && !dealer.Destroyed; shots++) gun.Fire(dealer.transform.position + Vector3.up);
                c.Check(dealer.Destroyed && shots <= 6, $"Simulación: a tiros la camioneta se destroza ({shots} disparos)");
                float wreckAlong = dealer.Rail.Along;
                for (int i = 0; i < 150; i++) dealer.Rail.Step(0.02f);
                c.Check(dealer.Rail.Speed == 0f && Vector3.Angle(dealer.transform.up, Vector3.up) > 90f, "Simulación: destrozada, frena y vuelca en la carretera");
                c.Check(dealer.Rail.Along > wreckAlong, "Y se queda atrás");
                Object.DestroyImmediate(dealer.gameObject);

                // Oleadas: salen al avanzar, siempre se puede seguir, y al final se gana.
                player.ResetCar();
                for (int w = 0; w < level.Waves.Length; w++)
                {
                    Place(pr, level.Waves[w].startDistance + 5f, 0f);
                    for (int t = 0; t < 40; t++) level.Tick(0.25f);
                    var wave = level.Spawned.Where(car => car != null && car.name.StartsWith($"Dealer_{w + 1}_")).ToList();
                    c.Check(wave.Count == ChaseLogic.Total(level.Waves[w]), $"Oleada {w + 1}: llegan sus {ChaseLogic.Total(level.Waves[w])} camionetas ({wave.Count})");
                    c.Check(wave.All(car => car.Rail != null && car.Rail.Along < pr.Along && Mathf.Abs(car.Rail.Lateral) > 4f),
                        $"Oleada {w + 1}: aparecen por detrás, en los carriles laterales");
                    foreach (Car car in wave) car.Explode();
                    level.Tick(0.1f);
                    c.Check(level.IsCleared(w), $"Oleada {w + 1}: destruidas todas, Nina lo celebra");
                }
                c.Check(level.Kills == Waves.Sum(ChaseLogic.Total), $"Se cuentan todas las bajas ({level.Kills})");
                Place(pr, road.Length - 20f, 0f);
                level.Tick(0.1f);
                c.Check(level.State == ChaseState.Won, "Simulación: al llegar al piso franco, nivel completado");
            }
            finally
            {
                input.Override = null;
                foreach (Car car in Object.FindObjectsByType<Car>(FindObjectsInactive.Include).Where(x => !x.IsPlayer))
                    Object.DestroyImmediate(car.gameObject);
            }
        }

        private static void Place(RailCar rail, float along, float lateral)
        {
            rail.Along = along;
            rail.Lateral = lateral;
            float speed = rail.Speed;
            rail.Speed = 0f;
            rail.Step(1e-4f);
            rail.Speed = speed;
            Physics.SyncTransforms();
        }
    }
}
