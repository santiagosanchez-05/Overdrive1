using System.Collections.Generic;
using System.IO;
using System.Linq;
using Overdrive.CameraRig;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Scoring;
using Overdrive.UI;
using Overdrive.Weapons;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Construye la escena Level01 a partir del mapa ASCII.</summary>
    internal static class SceneFactory
    {
        public const string ScenesPath = "Assets/00_Scenes";
        public const string ScenePath = ScenesPath + "/Level01.unity";
        public const float WallHeight = 2.5f;

        public const string LevelRoot = "Level";
        public const string FloorName = "Floor";
        public const string WallsName = "Walls";
        public const string ExitName = "Exit";
        public const string PlayerName = "Player";
        public const string EnemiesName = "Enemies";
        public const string WeaponsName = "Weapons";
        public const string CameraRigName = "CameraRig";
        public const string ShakeName = "Shake";
        public const string CameraName = "Main Camera";
        public const string SystemsName = "Systems";
        public const string LightName = "Directional Light";
        public const string BloodName = "Blood";
        public const string SlabsName = "Slabs";
        public const string DoorsName = "Doors";
        public const string DoorName = "Door";
        public const string ObjectiveName = "Objective";
        public const string BriefcaseName = "Briefcase";
        public const string ReinforcementsName = "Reinforcements";
        public const float SlabThickness = 0.3f;
        public const string NavMeshPath = ScenesPath + "/Level01_NavMesh.asset";
        public const int PatrolCells = 4;

        public static Scene EnsureScene()
        {
            BuilderUtil.EnsureFolder(ScenesPath);
            if (File.Exists(ScenePath))
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        public static void Build(LevelLayout layout)
        {
            Scene scene = EnsureScene();
            LevelMap ground = layout.Ground;

            EnsureLighting(scene);
            GameObject level = BuilderUtil.EnsureRoot(scene, LevelRoot);
            BuildFloor(level.transform, ground);
            BuildWalls(level.transform, ground);
            for (int i = 1; i < layout.Floors.Length; i++) BuildUpperFloor(level.transform, layout.Floors[i]);
            BuildStairs(level.transform, layout);
            BuildExit(level.transform, ground);
            BakeNavMesh(level);

            GameObject door = EnsureDoor(scene, ground);
            GameObject player = EnsurePlayer(scene, ground);
            var life = player.GetComponent<PlayerLife>();
            EnsureEnemies(scene, layout, life);
            EnsureFloorWeapons(scene, layout);
            BriefcasePickup briefcase = EnsureBriefcase(scene, layout, life);
            ReinforcementWave wave = EnsureReinforcements(scene, ground, door, life);
            EnsureCamera(scene, player);

            EnsureSystems(scene, level, player, briefcase, wave);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EnsureBuildSettings();
        }

        private static void EnsureLighting(Scene scene)
        {
            GameObject lightGo = BuilderUtil.EnsureRoot(scene, LightName);
            var light = BuilderUtil.EnsureComponent<Light>(lightGo);
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.color = new Color(1f, 0.9f, 1f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.25f, 0.35f);
        }

        private static void BuildFloor(Transform level, LevelMap map)
        {
            GameObject floor = BuilderUtil.EnsurePrimitiveChild(level, FloorName, PrimitiveType.Cube, true);
            BuilderUtil.SetLocal(floor,
                map.Center + new Vector3(0f, -0.1f, 0f),
                new Vector3(map.Width * map.CellSize, 0.2f, map.Height * map.CellSize),
                Quaternion.identity);
            BuilderUtil.SetMaterial(floor, AssetFactory.Mat(AssetFactory.MatFloor));
        }

        private static void BuildWalls(Transform level, LevelMap map)
        {
            Transform walls = BuilderUtil.EnsureChild(level, WallsName).transform;
            List<LevelMap.Block> blocks = map.WallBlocks();
            var expected = new HashSet<string>(blocks.Select(b => b.Name));

            // Borra solo paredes generadas que ya no están en el mapa.
            for (int i = walls.childCount - 1; i >= 0; i--)
            {
                GameObject child = walls.GetChild(i).gameObject;
                if (!expected.Contains(child.name)) Object.DestroyImmediate(child);
            }

            Material wallMat = AssetFactory.Mat(AssetFactory.MatWall);
            foreach (LevelMap.Block block in blocks)
            {
                GameObject wall = BuilderUtil.EnsurePrimitiveChild(walls, block.Name, PrimitiveType.Cube, true);
                BuilderUtil.SetLocal(wall,
                    map.BlockCenter(block) + new Vector3(0f, WallHeight * 0.5f, 0f),
                    new Vector3(block.Width * map.CellSize, WallHeight, block.Height * map.CellSize),
                    Quaternion.identity);
                BuilderUtil.SetMaterial(wall, wallMat);
            }
        }

        public static string FloorRootName(int floorIndex) => $"Floor{floorIndex}";

        /// <summary>Piso superior: losas de suelo (con huecos) y paredes, bajo Level/FloorN.</summary>
        private static void BuildUpperFloor(Transform level, LevelMap map)
        {
            Transform root = BuilderUtil.EnsureChild(level, FloorRootName(map.FloorIndex)).transform;
            Transform slabs = BuilderUtil.EnsureChild(root, SlabsName).transform;
            List<LevelMap.Block> blocks = map.SlabBlocks();
            var expected = new HashSet<string>(blocks.Select(b => b.NameWith("Slab")));
            for (int i = slabs.childCount - 1; i >= 0; i--)
            {
                GameObject child = slabs.GetChild(i).gameObject;
                if (!expected.Contains(child.name)) Object.DestroyImmediate(child);
            }

            Material mat = AssetFactory.Mat(AssetFactory.MatFloorUpper);
            foreach (LevelMap.Block block in blocks)
            {
                GameObject slab = BuilderUtil.EnsurePrimitiveChild(slabs, block.NameWith("Slab"), PrimitiveType.Cube, true);
                BuilderUtil.SetLocal(slab,
                    map.BlockCenter(block) + new Vector3(0f, -SlabThickness * 0.5f, 0f),
                    new Vector3(block.Width * map.CellSize, SlabThickness, block.Height * map.CellSize),
                    Quaternion.identity);
                BuilderUtil.SetMaterial(slab, mat);
            }

            BuildWalls(root, map);
        }

        /// <summary>
        /// Escaleras: una rampa por cada zona '/' de un piso, que sube hacia el este hasta el suelo del piso siguiente.
        /// </summary>
        private static void BuildStairs(Transform level, LevelLayout layout)
        {
            for (int i = 0; i < layout.Floors.Length - 1; i++)
            {
                LevelMap map = layout.Floors[i];
                LevelMap.Block? bounds = map.BoundsOf(LevelMap.Stairs);
                if (bounds == null) continue;
                RampGeometry ramp = RampFor(map, bounds.Value, layout.Floors[i + 1].BaseY);

                GameObject go = BuilderUtil.EnsurePrimitiveChild(level, StairsName(i), PrimitiveType.Cube, true);
                go.transform.localPosition = ramp.Center;
                go.transform.localRotation = Quaternion.Euler(0f, 0f, ramp.AngleDegrees);
                go.transform.localScale = ramp.Scale;
                BuilderUtil.SetMaterial(go, AssetFactory.Mat(AssetFactory.MatStairs));
            }
        }

        public static string StairsName(int floorIndex) => $"Stairs{floorIndex}";

        public readonly struct RampGeometry
        {
            public readonly Vector3 Bottom, Top, Center, Scale;
            public readonly float AngleDegrees;

            public RampGeometry(Vector3 bottom, Vector3 top, Vector3 center, Vector3 scale, float angle)
            {
                Bottom = bottom; Top = top; Center = center; Scale = scale; AngleDegrees = angle;
            }
        }

        /// <summary>
        /// Rampa cuya cara superior va del borde oeste de la zona (a la altura de este piso)
        /// al borde este (a la altura del piso siguiente).
        /// </summary>
        public static RampGeometry RampFor(LevelMap map, LevelMap.Block stairs, float topY)
        {
            float cell = map.CellSize;
            float x0 = (stairs.Col - 0.5f) * cell;
            float x1 = (stairs.Col + stairs.Width - 0.5f) * cell;
            float z = map.BlockCenter(stairs).z;
            float rise = topY - map.BaseY;
            float run = x1 - x0;
            float angle = Mathf.Atan2(rise, run);
            var up = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f);
            var bottom = new Vector3(x0, map.BaseY, z);
            var top = new Vector3(x1, topY, z);
            Vector3 center = (bottom + top) * 0.5f - up * (SlabThickness * 0.5f);
            var scale = new Vector3(Mathf.Sqrt(run * run + rise * rise), SlabThickness, stairs.Height * cell);
            return new RampGeometry(bottom, top, center, scale, angle * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Puerta de los refuerzos: bloque sólido sobre las celdas 'D', fuera de Level para que no entre
        /// en el NavMesh horneado; mientras está cerrada, su NavMeshObstacle corta el paso a los agentes.
        /// </summary>
        private static GameObject EnsureDoor(Scene scene, LevelMap map)
        {
            Transform doors = BuilderUtil.EnsureRoot(scene, DoorsName).transform;
            LevelMap.Block? bounds = map.BoundsOf(LevelMap.Door);
            if (bounds == null) return null;
            LevelMap.Block b = bounds.Value;

            GameObject door = BuilderUtil.EnsurePrimitiveChild(doors, DoorName, PrimitiveType.Cube, true);
            door.SetActive(true);
            BuilderUtil.SetLocal(door,
                map.BlockCenter(b) + new Vector3(0f, WallHeight * 0.5f, 0f),
                new Vector3(b.Width * map.CellSize, WallHeight, b.Height * map.CellSize),
                Quaternion.identity);
            BuilderUtil.SetMaterial(door, AssetFactory.Mat(AssetFactory.MatDoor));

            var obstacle = BuilderUtil.EnsureComponent<NavMeshObstacle>(door);
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = Vector3.zero;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            return door;
        }

        private static BriefcasePickup EnsureBriefcase(Scene scene, LevelLayout layout, PlayerLife player)
        {
            Transform root = BuilderUtil.EnsureRoot(scene, ObjectiveName).transform;
            var cells = layout.FindAll(LevelMap.Briefcase);
            if (cells.Count == 0) return null;
            var (map, col, row) = cells[0];

            Transform existing = root.Find(BriefcaseName);
            GameObject go = existing != null
                ? existing.gameObject
                : (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BriefcasePrefabPath), root);
            go.name = BriefcaseName;
            go.transform.SetPositionAndRotation(map.CellToWorld(col, row), Quaternion.Euler(0f, 20f, 0f));
            var pickup = go.GetComponent<BriefcasePickup>();
            BuilderUtil.SetRef(pickup, "player", player);
            return pickup;
        }

        /// <summary>Armas de los refuerzos, por orden de entrada.</summary>
        public static readonly WeaponType[] ReinforcementWeapons =
        {
            WeaponType.Smg, WeaponType.Shotgun, WeaponType.Smg, WeaponType.Bat,
        };

        public static string ReinforcementName(int index, int col, int row) => $"Reinforcement_{index}_c{col}_r{row}";

        /// <summary>Refuerzos desactivados tras la puerta, en las celdas 'R'. Se activan al coger el maletín.</summary>
        private static ReinforcementWave EnsureReinforcements(Scene scene, LevelMap map, GameObject door, PlayerLife player)
        {
            GameObject root = BuilderUtil.EnsureRoot(scene, ReinforcementsName);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            List<(int col, int row)> cells = map.FindAll(LevelMap.Reinforcement);
            var expected = new HashSet<string>();
            var squad = new List<EnemyAI>();

            for (int i = 0; i < cells.Count; i++)
            {
                var (col, row) = cells[i];
                string name = ReinforcementName(i, col, row);
                expected.Add(name);
                Transform existing = root.transform.Find(name);
                GameObject enemy = existing != null
                    ? existing.gameObject
                    : (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                enemy.name = name;
                // Miran hacia la puerta (oeste).
                enemy.transform.SetPositionAndRotation(map.CellToWorld(col, row), Quaternion.Euler(0f, -90f, 0f));
                EnsureEnemyWeapon(enemy.GetComponent<Enemy>(), ReinforcementWeapons[i % ReinforcementWeapons.Length]);

                var ai = enemy.GetComponent<EnemyAI>();
                BuilderUtil.SetRef(ai, "player", player);
                var so = new SerializedObject(ai);
                so.FindProperty("patrolPoints").arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                enemy.SetActive(false);
                squad.Add(ai);
            }

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.transform.GetChild(i).gameObject;
                if (!expected.Contains(child.name) || child.GetComponent<EnemyAI>() == null) Object.DestroyImmediate(child);
            }

            var wave = BuilderUtil.EnsureComponent<ReinforcementWave>(root);
            var waveSo = new SerializedObject(wave);
            SerializedProperty list = waveSo.FindProperty("squad");
            list.arraySize = squad.Count;
            for (int i = 0; i < squad.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = squad[i];
            waveSo.FindProperty("door").objectReferenceValue = door;
            waveSo.ApplyModifiedPropertiesWithoutUndo();
            return wave;
        }

        private static void BuildExit(Transform level, LevelMap map)
        {
            List<(int col, int row)> cells = map.FindAll(LevelMap.Exit);
            if (cells.Count == 0) return;
            int minC = cells.Min(c => c.col), maxC = cells.Max(c => c.col);
            int minR = cells.Min(c => c.row), maxR = cells.Max(c => c.row);
            var block = new LevelMap.Block(minC, minR, maxC - minC + 1, maxR - minR + 1);

            GameObject exit = BuilderUtil.EnsurePrimitiveChild(level, ExitName, PrimitiveType.Cube, false);
            BuilderUtil.SetLocal(exit,
                map.BlockCenter(block) + new Vector3(0f, 0.03f, 0f),
                new Vector3(block.Width * map.CellSize, 0.05f, block.Height * map.CellSize),
                Quaternion.identity);
            // Empieza apagada; LevelManager la enciende cuando no quedan enemigos.
            BuilderUtil.SetMaterial(exit, AssetFactory.Mat(AssetFactory.MatExitLocked));

            var zone = BuilderUtil.EnsureComponent<ExitZone>(exit);
            var so = new SerializedObject(zone);
            so.FindProperty("size").vector2Value = new Vector2(block.Width * map.CellSize, block.Height * map.CellSize);
            so.ApplyModifiedPropertiesWithoutUndo();
            BuilderUtil.SetRef(zone, "zoneRenderer", exit.GetComponent<Renderer>());
            BuilderUtil.SetRef(zone, "lockedMaterial", AssetFactory.Mat(AssetFactory.MatExitLocked));
            BuilderUtil.SetRef(zone, "openMaterial", AssetFactory.Mat(AssetFactory.MatExit));
        }

        /// <summary>Sistemas del nivel: reinicio, progreso/victoria, sangre, rebobinado, puntuación y HUD.</summary>
        private static void EnsureSystems(Scene scene, GameObject level, GameObject player,
            BriefcasePickup briefcase, ReinforcementWave wave)
        {
            GameObject systems = BuilderUtil.EnsureRoot(scene, SystemsName);
            BuilderUtil.EnsureComponent<LevelRestart>(systems);

            var manager = BuilderUtil.EnsureComponent<LevelManager>(systems);
            BuilderUtil.SetRef(manager, "player", player.GetComponent<PlayerLife>());
            BuilderUtil.SetRef(manager, "exit", level.transform.Find(ExitName).GetComponent<ExitZone>());
            BuilderUtil.SetRef(manager, "briefcase", briefcase);
            BuilderUtil.SetRef(manager, "reinforcements", wave);

            var visibility = BuilderUtil.EnsureComponent<FloorVisibility>(systems);
            BuilderUtil.SetRef(visibility, "player", player.transform);
            BuilderUtil.SetRef(visibility, "briefcase", briefcase);
            var floorsSo = new SerializedObject(visibility);
            SerializedProperty floors = floorsSo.FindProperty("upperFloors");
            var upper = new List<GameObject>();
            for (int i = 1; level.transform.Find(FloorRootName(i)) != null; i++) upper.Add(level.transform.Find(FloorRootName(i)).gameObject);
            floors.arraySize = upper.Count;
            for (int i = 0; i < upper.Count; i++) floors.GetArrayElementAtIndex(i).objectReferenceValue = upper[i];
            floorsSo.ApplyModifiedPropertiesWithoutUndo();
            Enemy[] enemies = BuilderUtil.FindRoot(scene, EnemiesName).GetComponentsInChildren<Enemy>(true);
            var so = new SerializedObject(manager);
            SerializedProperty list = so.FindProperty("enemies");
            list.arraySize = enemies.Length;
            for (int i = 0; i < enemies.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Las manchas de sangre se crean como hijas de este objeto.
            GameObject bloodRoot = BuilderUtil.EnsureChild(systems.transform, BloodName);
            var blood = BuilderUtil.EnsureComponent<BloodSpawner>(bloodRoot);
            BuilderUtil.SetRef(blood, "bloodPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BloodPrefabPath));

            var rewinder = BuilderUtil.EnsureComponent<TimeRewinder>(systems);
            BuilderUtil.SetRef(rewinder, "player", player.GetComponent<PlayerLife>());
            BuilderUtil.SetRef(rewinder, "level", manager);
            BuilderUtil.SetRef(rewinder, "cameraRig", BuilderUtil.FindRoot(scene, CameraRigName).GetComponent<TopDownCamera>());

            var score = BuilderUtil.EnsureComponent<ScoreKeeper>(systems);
            BuilderUtil.SetRef(score, "level", manager);
            BuilderUtil.SetRef(score, "rewinder", rewinder);

            var hud = BuilderUtil.EnsureComponent<GameHUD>(systems);
            BuilderUtil.SetRef(hud, "weapons", player.GetComponent<PlayerWeapons>());
            BuilderUtil.SetRef(hud, "level", manager);
            BuilderUtil.SetRef(hud, "rewinder", rewinder);
            BuilderUtil.SetRef(hud, "score", score);
        }

        private static GameObject EnsurePlayer(Scene scene, LevelMap map)
        {
            GameObject player = BuilderUtil.FindRoot(scene, PlayerName);
            if (player == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
                player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                player.name = PlayerName;
            }
            var (col, row) = map.FindAll(LevelMap.PlayerSpawn)[0];
            player.transform.SetPositionAndRotation(map.CellToWorld(col, row), Quaternion.identity);
            return player;
        }

        /// <summary>Nombre de un enemigo del mapa. Los de pisos superiores llevan el sufijo _fN.</summary>
        public static string EnemyName(char code, int col, int row, int floor = 0) =>
            floor == 0 ? $"Enemy_{code}_c{col}_r{row}" : $"Enemy_{code}_c{col}_r{row}_f{floor}";

        /// <summary>
        /// Hornea el NavMesh con las paredes y el suelo (hijos de Level) y lo guarda como asset.
        /// Si el asset ya existe se sobrescribe su contenido para conservar la misma referencia.
        /// </summary>
        private static void BakeNavMesh(GameObject level)
        {
            var surface = BuilderUtil.EnsureComponent<NavMeshSurface>(level);
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            Physics.SyncTransforms();
            surface.BuildNavMesh();

            NavMeshData built = surface.navMeshData;
            var stored = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
            if (stored == null)
            {
                AssetDatabase.CreateAsset(built, NavMeshPath);
                return;
            }
            if (stored == built) return;
            EditorUtility.CopySerialized(built, stored);
            surface.RemoveData();
            surface.navMeshData = stored;
            surface.AddData();
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(stored);
        }

        private static void EnsureEnemies(Scene scene, LevelLayout layout, PlayerLife player)
        {
            Transform container = BuilderUtil.EnsureRoot(scene, EnemiesName).transform;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            var expected = new HashSet<string>();
            int index = 0;

            foreach (var (map, col, row, c) in layout.AllCells())
            {
                if (!LevelMap.IsEnemy(c)) continue;
                string name = EnemyName(c, col, row, map.FloorIndex);
                expected.Add(name);
                Transform existing = container.Find(name);
                GameObject enemy = existing != null
                    ? existing.gameObject
                    : (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                enemy.name = name;
                // Al empezar miran hacia el sur (hacia abajo en pantalla).
                enemy.transform.SetPositionAndRotation(map.CellToWorld(col, row), Quaternion.Euler(0f, 180f, 0f));
                EnsureEnemyWeapon(enemy.GetComponent<Enemy>(), LevelMap.WeaponFor(c));

                // La mitad de los enemigos patrulla; la otra mitad espera quieta en su puesto.
                Vector3[] route = index % 2 == 0 ? map.PatrolRoute(col, row, PatrolCells) : new Vector3[0];
                index++;
                var ai = enemy.GetComponent<EnemyAI>();
                BuilderUtil.SetRef(ai, "player", player);
                var so = new SerializedObject(ai);
                SerializedProperty points = so.FindProperty("patrolPoints");
                points.arraySize = route.Length;
                for (int i = 0; i < route.Length; i++) points.GetArrayElementAtIndex(i).vector3Value = route[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Borra solo enemigos generados que ya no están en el mapa.
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                GameObject child = container.GetChild(i).gameObject;
                if (!expected.Contains(child.name) || child.GetComponent<Enemy>() == null) Object.DestroyImmediate(child);
            }
        }

        private static void EnsureEnemyWeapon(Enemy enemy, WeaponType type)
        {
            Transform hand = enemy.Hand;
            WeaponItem item = hand.GetComponentInChildren<WeaponItem>(true);
            if (item != null && item.Type != type)
            {
                Object.DestroyImmediate(item.gameObject);
                item = null;
            }
            if (item == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.WeaponPrefabPath(type));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hand);
                item = go.GetComponent<WeaponItem>();
            }
            item.gameObject.name = $"Weapon_{type}";
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;

            var so = new SerializedObject(item);
            so.FindProperty("mode").enumValueIndex = (int)WeaponItem.Mode.Held;
            so.ApplyModifiedPropertiesWithoutUndo();
            BuilderUtil.SetRef(enemy, "weapon", item);
        }

        public static string FloorWeaponName(char code, int col, int row, int floor = 0) =>
            floor == 0 ? $"Pickup_{code}_c{col}_r{row}" : $"Pickup_{code}_c{col}_r{row}_f{floor}";

        private static void EnsureFloorWeapons(Scene scene, LevelLayout layout)
        {
            Transform container = BuilderUtil.EnsureRoot(scene, WeaponsName).transform;
            var expected = new HashSet<string>();

            foreach (var (map, col, row, c) in layout.AllCells())
            {
                if (!LevelMap.IsFloorWeapon(c)) continue;
                WeaponType type = LevelMap.WeaponFor(c);
                string name = FloorWeaponName(c, col, row, map.FloorIndex);
                expected.Add(name);
                Transform existing = container.Find(name);
                GameObject go = existing != null
                    ? existing.gameObject
                    : (GameObject)PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.WeaponPrefabPath(type)), container);
                go.name = name;
                go.transform.SetPositionAndRotation(map.CellToWorld(col, row) + new Vector3(0f, 0.08f, 0f),
                    Quaternion.Euler(0f, 30f, 0f));
            }

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                GameObject child = container.GetChild(i).gameObject;
                if (!expected.Contains(child.name) || child.GetComponent<WeaponItem>() == null) Object.DestroyImmediate(child);
            }
        }

        private static void EnsureCamera(Scene scene, GameObject player)
        {
            GameObject rig = BuilderUtil.EnsureRoot(scene, CameraRigName);
            GameObject shake = BuilderUtil.EnsureChild(rig.transform, ShakeName);
            BuilderUtil.EnsureComponent<CameraShake>(shake);
            GameObject camGo = BuilderUtil.EnsureChild(shake.transform, CameraName);
            camGo.tag = "MainCamera";

            var cam = BuilderUtil.EnsureComponent<Camera>(camGo);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0f, 0.05f);
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            BuilderUtil.EnsureComponent<AudioListener>(camGo);

            var topDown = BuilderUtil.EnsureComponent<TopDownCamera>(rig);
            var aim = player.GetComponent<PlayerAim>();
            BuilderUtil.SetRef(topDown, "target", player.transform);
            BuilderUtil.SetRef(topDown, "aim", aim);
            BuilderUtil.SetRef(topDown, "cam", cam);
            BuilderUtil.SetRef(aim, "aimCamera", cam);

            topDown.ApplyCameraOffset();
            topDown.SnapToTarget();
        }

        private static void EnsureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Count > 0 && scenes[0].path == ScenePath && scenes[0].enabled) return;
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
