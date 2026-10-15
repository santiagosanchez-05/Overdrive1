using System.Collections.Generic;
using System.IO;
using System.Linq;
using Overdrive.CameraRig;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Construye la escena Level01 a partir del mapa ASCII.</summary>
    internal static class SceneFactory
    {
        public const string ScenesPath = AssetFactory.Root + "/Scenes";
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

        public static Scene EnsureScene()
        {
            BuilderUtil.EnsureFolder(ScenesPath);
            if (File.Exists(ScenePath))
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        public static void Build(LevelMap map)
        {
            Scene scene = EnsureScene();

            EnsureLighting(scene);
            GameObject level = BuilderUtil.EnsureRoot(scene, LevelRoot);
            BuildFloor(level.transform, map);
            BuildWalls(level.transform, map);
            BuildExit(level.transform, map);

            GameObject player = EnsurePlayer(scene, map);
            EnsureEnemies(scene, map);
            EnsureFloorWeapons(scene, map);
            EnsureCamera(scene, player);

            GameObject systems = BuilderUtil.EnsureRoot(scene, SystemsName);
            BuilderUtil.EnsureComponent<LevelRestart>(systems);

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
            BuilderUtil.SetMaterial(exit, AssetFactory.Mat(AssetFactory.MatExit));
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

        public static string EnemyName(char code, int col, int row) => $"Enemy_{code}_c{col}_r{row}";

        private static void EnsureEnemies(Scene scene, LevelMap map)
        {
            Transform container = BuilderUtil.EnsureRoot(scene, EnemiesName).transform;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            var expected = new HashSet<string>();

            foreach (var (col, row, c) in map.Cells())
            {
                if (!LevelMap.IsEnemy(c)) continue;
                string name = EnemyName(c, col, row);
                expected.Add(name);
                Transform existing = container.Find(name);
                GameObject enemy = existing != null
                    ? existing.gameObject
                    : (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                enemy.name = name;
                // Al empezar miran hacia el sur (hacia abajo en pantalla).
                enemy.transform.SetPositionAndRotation(map.CellToWorld(col, row), Quaternion.Euler(0f, 180f, 0f));
                EnsureEnemyWeapon(enemy.GetComponent<Enemy>(), LevelMap.WeaponFor(c));
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

        public static string FloorWeaponName(char code, int col, int row) => $"Pickup_{code}_c{col}_r{row}";

        private static void EnsureFloorWeapons(Scene scene, LevelMap map)
        {
            Transform container = BuilderUtil.EnsureRoot(scene, WeaponsName).transform;
            var expected = new HashSet<string>();

            foreach (var (col, row, c) in map.Cells())
            {
                if (!LevelMap.IsFloorWeapon(c)) continue;
                WeaponType type = LevelMap.WeaponFor(c);
                string name = FloorWeaponName(c, col, row);
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
