using System;
using System.Collections.Generic;
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
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    internal static class Level02Gameplay
    {
        internal static readonly Vector3 Start = new Vector3(0f, 0f, 24f);
        internal static readonly Vector3[] BackstageReinforcementPositions =
        {
            new Vector3(-15f, -2.25f, -56f), new Vector3(1f, -2.25f, -56f),
            new Vector3(-15f, -2.25f, -60f), new Vector3(1f, -2.25f, -60f)
        };

        public static void Build(Scene scene, GameObject level)
        {
            var player = Instantiate(AssetFactory.PlayerPrefabPath, "Player", null).GetComponent<PlayerController>();
            player.transform.position = OnNavMesh(Start);
            SetFloat(player, "moveSpeed", 10f);
            var camera = CreateCamera(player);
            var enemies = CreateEnemies(player.Life);
            var briefcase = Instantiate(AssetFactory.BriefcasePrefabPath, "Briefcase", null).GetComponent<BriefcasePickup>();
            briefcase.transform.position = OnNavMesh(new Vector3(-7f, -1.5f, -77f)) + Vector3.up * 0.12f;
            BuilderUtil.SetRef(briefcase, "player", player.Life);
            var exit = CreateExit(level.transform);
            var wave = CreateReinforcements(player.Life);
            CreateSystems(player, camera, enemies, briefcase, exit, wave);
            CreateFloorWeapons();
            AddWayfinding(level.transform);
            MenuBuilder.AddPauseMenu(scene);
            MenuBuilder.AddLevelMusic(scene);
        }

        private static TopDownCamera CreateCamera(PlayerController player)
        {
            var rig = new GameObject("CameraRig").AddComponent<TopDownCamera>();
            var shake = new GameObject("Shake", typeof(CameraShake));
            shake.transform.SetParent(rig.transform, false);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(shake.transform, false);
            camera.fieldOfView = 50f;
            camera.farClipPlane = 120f;
            camera.nearClipPlane = 0.3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.018f, 0.035f);
            camera.allowHDR = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var aim = player.GetComponent<PlayerAim>();
            BuilderUtil.SetRef(rig, "target", player.transform);
            BuilderUtil.SetRef(rig, "aim", aim);
            BuilderUtil.SetRef(rig, "cam", camera);
            BuilderUtil.SetRef(aim, "aimCamera", camera);
            rig.ApplyCameraOffset();
            rig.SnapToTarget();
            return rig;
        }

        private static Enemy[] CreateEnemies(PlayerLife player)
        {
            var root = new GameObject("Enemies").transform;
            var positions = new[]
            {
                new Vector3(0f, 0f, 14f), new Vector3(-11f, 1.6f, 12f),
                new Vector3(11f, 1.6f, 12f), new Vector3(-13f, 0f, -2f),
                new Vector3(13f, 0f, -2f), new Vector3(-8f, 1.6f, -13f),
                new Vector3(8f, 1.6f, -13f), new Vector3(0f, -1f, -20f),
                new Vector3(-13f, -1.5f, -26f), new Vector3(11f, -1.5f, -26f),
                new Vector3(0f, -2.2f, -42f), new Vector3(-13f, -2.2f, -53f),
                new Vector3(-7f, -1.5f, -61f), new Vector3(-6f, -1.5f, -74f),
            };
            var types = new[] { WeaponType.Bat, WeaponType.Knife, WeaponType.Shotgun, WeaponType.Bat, WeaponType.Smg, WeaponType.Knife, WeaponType.Shotgun };
            var enemies = new List<Enemy>();
            for (int i = 0; i < positions.Length; i++)
            {
                var enemy = CreateEnemy("Guard_" + (i + 1).ToString("D2"), root, player, positions[i], types[i % types.Length]);
                enemy.transform.rotation = Quaternion.Euler(0f, i % 2 == 0 ? 180f : 90f, 0f);
                var ai = enemy.GetComponent<EnemyAI>();
                if (i % 3 != 0) ConfigurePatrol(ai, i % 2 == 0 ? Vector3.right : Vector3.forward);
                enemies.Add(enemy);
            }
            return enemies.ToArray();
        }

        private static Enemy CreateEnemy(string name, Transform parent, PlayerLife player, Vector3 position, WeaponType type, bool behindGate = false)
        {
            Vector3 spawn = OnNavMesh(position, behindGate);
            var enemy = Instantiate(AssetFactory.EnemyPrefabPath, name, parent).GetComponent<Enemy>();
            enemy.transform.position = spawn;
            BuilderUtil.SetRef(enemy.GetComponent<EnemyAI>(), "player", player);
            var weapon = Instantiate(AssetFactory.WeaponPrefabPath(type), "Weapon_" + type, enemy.Hand).GetComponent<WeaponItem>();
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            var serialized = new SerializedObject(weapon);
            serialized.FindProperty("mode").enumValueIndex = (int)WeaponItem.Mode.Held;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            BuilderUtil.SetRef(enemy, "weapon", weapon);
            return enemy;
        }

        private static void ConfigurePatrol(EnemyAI ai, Vector3 direction)
        {
            Vector3 start = ai.transform.position;
            if (!NavMesh.SamplePosition(start + direction * 3f, out var end, 1.2f, NavMesh.AllAreas)) return;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return;
            if (path.corners.Zip(path.corners.Skip(1), Vector3.Distance).Sum() > 6f) return;
            var serialized = new SerializedObject(ai);
            var points = serialized.FindProperty("patrolPoints");
            points.arraySize = 2;
            points.GetArrayElementAtIndex(0).vector3Value = start;
            points.GetArrayElementAtIndex(1).vector3Value = end.position;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ReinforcementWave CreateReinforcements(PlayerLife player)
        {
            var root = new GameObject("Reinforcements");
            var wave = root.AddComponent<ReinforcementWave>();
            var squad = new List<EnemyAI>();
            var backstage = new GameObject("Backstage Squad").transform;
            backstage.SetParent(root.transform, false);
            for (int i = 0; i < 4; i++)
            {
                var enemy = CreateEnemy("Response_" + (i + 1), backstage, player,
                    BackstageReinforcementPositions[i], i < 2 ? WeaponType.Smg : WeaponType.Bat);
                squad.Add(enemy.GetComponent<EnemyAI>());
                enemy.gameObject.SetActive(false);
            }
            AddExitSquad(root.transform, player, squad);
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "SecurityGate";
            door.transform.SetParent(root.transform, false);
            door.transform.position = new Vector3(0f, 1.2f, 26.4f);
            door.transform.localScale = new Vector3(4f, 2.4f, 0.22f);
            door.GetComponent<Renderer>().sharedMaterial = AssetFactory.Mat(AssetFactory.MatDoorDissolve);
            var obstacle = door.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            var dissolve = door.AddComponent<DoorDissolve>();
            BuilderUtil.SetRef(dissolve, "doorRenderer", door.GetComponent<Renderer>());
            BuilderUtil.SetRef(dissolve, "doorCollider", door.GetComponent<Collider>());
            BuilderUtil.SetRef(dissolve, "obstacle", obstacle);
            var serialized = new SerializedObject(wave);
            var list = serialized.FindProperty("squad");
            list.arraySize = squad.Count;
            for (int i = 0; i < squad.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = squad[i];
            serialized.FindProperty("door").objectReferenceValue = door;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return wave;
        }

        internal static void AddExitSquad(Transform root, PlayerLife player, List<EnemyAI> squad)
        {
            var group = new GameObject("Exit Squad").transform;
            group.SetParent(root, false);
            var positions = new[] { new Vector3(-.8f, 0f, 32.2f), new Vector3(.8f, 0f, 32.2f), new Vector3(0f, 0f, 31.4f) };
            for (int i = 0; i < positions.Length; i++)
            {
                var enemy = CreateEnemy("ExitGuard_" + (i + 1), group, player, positions[i], i == 0 ? WeaponType.Shotgun : WeaponType.Bat, true);
                squad.Add(enemy.GetComponent<EnemyAI>());
                enemy.gameObject.SetActive(false);
            }
        }

        private static ExitZone CreateExit(Transform level)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Exit";
            floor.transform.SetParent(level, false);
            floor.transform.position = OnNavMesh(new Vector3(0f, 0f, 31.7f)) + Vector3.up * 0.025f;
            floor.transform.localScale = new Vector3(3f, 0.04f, 2.5f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            var zone = floor.AddComponent<ExitZone>();
            var serialized = new SerializedObject(zone);
            serialized.FindProperty("size").vector2Value = new Vector2(3f, 2.5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            BuilderUtil.SetRef(zone, "zoneRenderer", floor.GetComponent<Renderer>());
            BuilderUtil.SetRef(zone, "lockedMaterial", AssetFactory.Mat(AssetFactory.MatExitLocked));
            BuilderUtil.SetRef(zone, "openMaterial", AssetFactory.Mat(AssetFactory.MatExit));
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "ExitBeam";
            beam.transform.SetParent(level, false);
            beam.transform.position = floor.transform.position + Vector3.up * 2.8f;
            beam.transform.localScale = new Vector3(2f, 3f, 2f);
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.GetComponent<Renderer>().sharedMaterial = AssetFactory.Mat(AssetFactory.MatExitBeam);
            BuilderUtil.SetRef(zone, "beam", beam);
            zone.SetOpen(false);
            return zone;
        }

        private static void CreateSystems(PlayerController player, TopDownCamera camera, Enemy[] enemies, BriefcasePickup briefcase, ExitZone exit, ReinforcementWave wave)
        {
            var systems = new GameObject("Systems");
            systems.AddComponent<LevelRestart>();
            var level = systems.AddComponent<LevelManager>();
            BuilderUtil.SetRef(level, "player", player.Life);
            BuilderUtil.SetRef(level, "briefcase", briefcase);
            BuilderUtil.SetRef(level, "exit", exit);
            BuilderUtil.SetRef(level, "reinforcements", wave);
            var serialized = new SerializedObject(level);
            var list = serialized.FindProperty("enemies");
            list.arraySize = enemies.Length;
            for (int i = 0; i < enemies.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var visibility = systems.AddComponent<FloorVisibility>();
            BuilderUtil.SetRef(visibility, "player", player.transform);
            BuilderUtil.SetRef(visibility, "briefcase", briefcase);
            var blood = new GameObject("Blood").AddComponent<BloodSpawner>();
            blood.transform.SetParent(systems.transform, false);
            BuilderUtil.SetRef(blood, "bloodPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BloodPrefabPath));
            var rewind = systems.AddComponent<TimeRewinder>();
            BuilderUtil.SetRef(rewind, "player", player.Life);
            BuilderUtil.SetRef(rewind, "level", level);
            BuilderUtil.SetRef(rewind, "cameraRig", camera);
            var score = systems.AddComponent<ScoreKeeper>();
            BuilderUtil.SetRef(score, "level", level);
            BuilderUtil.SetRef(score, "rewinder", rewind);
            var hud = systems.AddComponent<GameHUD>();
            BuilderUtil.SetRef(hud, "weapons", player.GetComponent<PlayerWeapons>());
            BuilderUtil.SetRef(hud, "level", level);
            BuilderUtil.SetRef(hud, "rewinder", rewind);
            BuilderUtil.SetRef(hud, "score", score);
        }

        private static void CreateFloorWeapons()
        {
            var root = new GameObject("Weapons").transform;
            PlaceWeapon(WeaponType.Bat, new Vector3(0.8f, 0f, 23f), root);
            PlaceWeapon(WeaponType.Smg, new Vector3(-18f, 1.6f, 7f), root);
            PlaceWeapon(WeaponType.Shotgun, new Vector3(12f, -1.5f, -26f), root);
            PlaceWeapon(WeaponType.Knife, new Vector3(-11f, -2.2f, -58f), root);
        }

        private static void PlaceWeapon(WeaponType type, Vector3 position, Transform root)
        {
            var weapon = Instantiate(AssetFactory.WeaponPrefabPath(type), "Pickup_" + type, root);
            weapon.transform.position = OnNavMesh(position) + Vector3.up * 0.08f;
        }

        private static void AddWayfinding(Transform root)
        {
            FloorLabel(root, "ELYSIUM", new Vector3(0f, 0.04f, 22f), 1.1f, new Color(0.15f, 0.85f, 1f));
            FloorLabel(root, "BACKSTAGE  >", new Vector3(0f, -0.7f, -18f), 0.65f, new Color(1f, 0.2f, 0.55f));
            FloorLabel(root, "STAFF ONLY", new Vector3(-7f, -1.45f, -71f), 0.7f, new Color(1f, 0.45f, 0.15f));
        }

        private static void FloorLabel(Transform parent, string text, Vector3 position, float size, Color color)
        {
            var label = new GameObject("Sign_" + text).AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            label.text = text;
            label.fontSize = size * 10f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.rectTransform.sizeDelta = new Vector2(12f, 2f);
        }

        internal static Vector3 OnNavMesh(Vector3 requested, bool behindGate = false)
        {
            // The gate intentionally disconnects reserve guards until the briefcase opens it.
            if (behindGate && NavMesh.SamplePosition(requested, out var reserve, .8f, NavMesh.AllAreas))
                return reserve.position;
            if (!NavMesh.SamplePosition(Start, out var start, 1f, NavMesh.AllAreas))
                throw new InvalidOperationException("El inicio no tiene suelo navegable.");
            // Prefer a reachable surface over an isolated pocket underneath a raised stage.
            foreach (float height in new[] { 0f, 1f, -1f, 2f, -2f })
            {
                if (!NavMesh.SamplePosition(requested + Vector3.up * height, out var hit, 2.5f, NavMesh.AllAreas)) continue;
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    return hit.position;
            }
            throw new InvalidOperationException("No hay suelo conectado al inicio cerca de " + requested);
        }

        private static GameObject Instantiate(string path, string name, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Falta el prefab " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance;
        }

        private static void SetFloat(Object target, string name, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
