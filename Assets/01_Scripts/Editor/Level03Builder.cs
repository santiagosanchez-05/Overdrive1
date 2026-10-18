using System;
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
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Nivel 3 (oficina del Jefe): prepara el loft para jugar sin cambiar su aspecto (solo añade colliders y NavMesh)
    /// y monta la jugabilidad como en Level02: jugador, guardias, armas, el Jefe con refuerzos de furia, salida y sistemas.
    /// Las posiciones se calculan sobre el NavMesh del loft: el jugador entra por un extremo y el Jefe espera en el otro.
    /// </summary>
    internal static class Level03Builder
    {
        public const string ScenePath = "Assets/00_Scenes/Level03.unity";
        public const string DataPath = "Assets/00_Scenes/Level03_Data";
        private const string LoftName = "Scene Level03";
        private const string GameplayRoot = "Level";
        private const string BossModelPath = "Assets/02_Modelos/Characters/Jefe_Actualizado.fbx";
        private const string BossTexturePath = "Assets/02_Modelos/Characters/Jefe_2_basecolor.PNG";

        private const int GuardCount = 7;
        private const float MinGuardSpacing = 3f;
        private const float SafeStartRadius = 7f;

        // Nombres de todo lo que crea el constructor: se borra y se vuelve a crear en cada construcción.
        private static readonly string[] GeneratedRoots =
            { GameplayRoot, "Player", "CameraRig", "Enemies", "Boss Reinforcements", "Weapons", "Systems" };

        /// <summary>Solo vuelve a montar el modelo y las animaciones del Jefe en la escena abierta, sin reconstruir el nivel.</summary>
        [MenuItem("TopDown/Nivel 03/Actualizar modelo y animaciones del Jefe")]
        public static void RefreshBoss()
        {
            Scene scene = SceneManager.GetActiveScene();
            var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossEnemy>(true)).FirstOrDefault();
            if (boss == null)
            {
                Debug.LogError("[Nivel 03] No hay Jefe en la escena abierta. Usa Construir jugabilidad (Jefe).");
                return;
            }
            ApplyBossLook(boss.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Nivel 03] Jefe actualizado con Boss.controller.");
        }

        [MenuItem("TopDown/Nivel 03/Construir jugabilidad (Jefe)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode primero.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject loft = BuilderUtil.FindRoot(scene, LoftName);
            if (loft == null) throw new InvalidOperationException($"No se encontró '{LoftName}' en Level03.");
            Directory.CreateDirectory(DataPath);

            foreach (string name in GeneratedRoots)
            {
                GameObject old = BuilderUtil.FindRoot(scene, name);
                if (old != null) Object.DestroyImmediate(old);
            }
            DisableSceneCamera(scene);

            // 1. Entorno: colisión del loft y NavMesh.
            var level = new GameObject(GameplayRoot);
            int colliders = AddLoftColliders(loft);
            BakeNavigation(level, loft);

            // 2. Puntos de juego sobre el NavMesh.
            var layout = Plan(loft);

            // 3. Jugabilidad.
            var player = InstantiatePrefab(AssetFactory.PlayerPrefabPath, "Player", null).GetComponent<PlayerController>();
            player.transform.position = layout.Start;
            player.transform.rotation = Quaternion.LookRotation(Flat(layout.Boss - layout.Start));
            SetFloat(player, "moveSpeed", 10f);
            var camera = CreateCamera(player);

            var enemies = CreateGuards(player.Life, layout);
            var wave = CreateBossReinforcements(player.Life, layout);
            var boss = CreateBoss(player.Life, layout, wave);
            var all = enemies.Concat(new[] { boss }).Concat(wave.Squad.Select(ai => ai.Enemy)).ToArray();

            var exit = CreateExit(level.transform, layout);
            CreateSystems(player, camera, all, exit);
            CreateFloorWeapons(layout);
            FloorLabel(level.transform, "OFICINA DEL JEFE", layout.Start + Flat(layout.Boss - layout.Start).normalized * 2f + Vector3.up * 0.03f,
                0.7f, new Color(1f, 0.25f, 0.45f), layout.Boss);

            MenuBuilder.AddPauseMenu(scene);
            MenuBuilder.AddLevelMusic(scene);
            StoryFactory.ApplyEnemyLooks(scene);
            ApplyBossLook(boss.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddBuildSettings();
            LinkFromLevel02();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Nivel 03] Listo: {colliders} colliders, {enemies.Length} guardias, Jefe con {wave.Squad.Length} refuerzos. " +
                      $"Loft de {layout.Size.x:0.#} x {layout.Size.z:0.#} m. Inicio {layout.Start}, Jefe {layout.Boss}.");
        }

        // ---------------------------------------------------------------- Entorno

        private static void DisableSceneCamera(Scene scene)
        {
            // La cámara del loft se sustituye por el CameraRig del juego (no se borra, por si se quiere volver a usar).
            GameObject cam = BuilderUtil.FindRoot(scene, "Main Camera");
            if (cam == null) return;
            cam.tag = "Untagged";
            cam.SetActive(false);
        }

        /// <summary>MeshCollider en cada malla del loft que no tenga collider. No cambia mallas ni materiales.</summary>
        private static int AddLoftColliders(GameObject loft)
        {
            int added = 0;
            foreach (var filter in loft.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                filter.gameObject.isStatic = true;
                added++;
            }
            return added;
        }

        private static void BakeNavigation(GameObject level, GameObject loft)
        {
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            var settings = surface.GetBuildSettings();
            settings.agentClimb = 0.25f;
            settings.agentHeight = 1.8f;
            settings.agentRadius = 0.45f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.1f;

            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(loft.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            Bounds bounds = LoftBounds(loft);
            bounds.Expand(4f);
            var baked = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (baked == null) throw new InvalidOperationException("El horneado del NavMesh de Level03 no produjo datos.");
            surface.RemoveData();
            surface.navMeshData = Level02Environment.StoreAsset(baked, DataPath + "/Level03_NavMesh.asset");
            surface.AddData();
        }

        private static Bounds LoftBounds(GameObject loft)
        {
            var renderers = loft.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("El loft no tiene mallas.");
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        // ---------------------------------------------------------------- Plan sobre el NavMesh

        private sealed class Layout
        {
            public Vector3 Start, Boss, Exit, Size;
            public Vector3 Axis;
            public List<Vector3> Guards = new List<Vector3>();
            public List<Vector3> Reinforcements = new List<Vector3>();
            public List<Vector3> Weapons = new List<Vector3>();
        }

        /// <summary>
        /// Recorre el loft en rejilla y se queda con el suelo navegable más grande y conectado.
        /// El eje largo del loft marca el recorrido: inicio en un extremo, Jefe en el otro, guardias entre medias.
        /// </summary>
        private static Layout Plan(GameObject loft)
        {
            Bounds b = LoftBounds(loft);
            var candidates = new List<Vector3>();
            const float step = 1.25f;
            for (float x = b.min.x + step * 0.5f; x < b.max.x; x += step)
                for (float z = b.min.z + step * 0.5f; z < b.max.z; z += step)
                    for (float y = b.min.y + 0.5f; y < b.max.y; y += 1.5f)
                        if (NavMesh.SamplePosition(new Vector3(x, y, z), out var hit, 0.6f, NavMesh.AllAreas) && IsRoomy(hit.position))
                            candidates.Add(hit.position);
            candidates = candidates.Distinct(new NearComparer(0.3f)).ToList();
            if (candidates.Count < 20) throw new InvalidOperationException($"El loft casi no tiene suelo navegable ({candidates.Count} puntos). ¿Es demasiado pequeño para el jugador?");

            // Zona principal: la que más puntos alcanza desde un punto del centro.
            List<Vector3> region = null;
            foreach (Vector3 hub in candidates.OrderBy(p => Flat(p - b.center).sqrMagnitude).Take(6))
            {
                var reached = candidates.Where(p => Reachable(hub, p)).ToList();
                if (region == null || reached.Count > region.Count) region = reached;
            }

            Vector3 axis = b.size.x >= b.size.z ? Vector3.right : Vector3.forward;
            float Along(Vector3 p) => Vector3.Dot(p, axis);
            float min = region.Min(Along), max = region.Max(Along), length = Mathf.Max(0.01f, max - min);
            float T(Vector3 p) => (Along(p) - min) / length;

            var layout = new Layout { Size = b.size, Axis = axis };
            // Inicio y Jefe: los puntos más abiertos de cada extremo (lejos de las paredes).
            layout.Start = region.Where(p => T(p) < 0.12f).OrderByDescending(Openness).DefaultIfEmpty(region.OrderBy(T).First()).First();
            layout.Boss = region.Where(p => T(p) > 0.85f).OrderByDescending(Openness).DefaultIfEmpty(region.OrderBy(T).Last()).First();
            layout.Exit = region.Where(p => Vector3.Distance(p, layout.Start) > 1.5f && Vector3.Distance(p, layout.Start) < 3.5f && T(p) <= T(layout.Start))
                .DefaultIfEmpty(layout.Start).First();

            var taken = new List<Vector3> { layout.Start, layout.Boss, layout.Exit };
            bool Free(Vector3 p, float spacing) => taken.All(q => Vector3.Distance(p, q) >= spacing);

            // Guardias repartidos en tramos a lo largo del loft, sin acercarse al inicio.
            for (int i = 0; i < GuardCount; i++)
            {
                float from = Mathf.Lerp(0.22f, 0.8f, (float)i / GuardCount);
                float to = Mathf.Lerp(0.22f, 0.8f, (float)(i + 1) / GuardCount);
                Vector3? spot = region
                    .Where(p => T(p) >= from && T(p) <= to && Vector3.Distance(p, layout.Start) > SafeStartRadius && Free(p, MinGuardSpacing))
                    .OrderBy(p => (i % 2 == 0 ? 1f : -1f) * Vector3.Dot(p, Vector3.Cross(axis, Vector3.up)))
                    .Select(p => (Vector3?)p).FirstOrDefault();
                if (spot == null) continue;
                layout.Guards.Add(spot.Value);
                taken.Add(spot.Value);
            }

            // Refuerzos de furia: en la zona del Jefe.
            foreach (Vector3 p in region.Where(p => T(p) > 0.65f).OrderBy(p => Mathf.Abs(Vector3.Distance(p, layout.Boss) - 4f)))
            {
                if (layout.Reinforcements.Count >= 3) break;
                if (!Free(p, 2f)) continue;
                layout.Reinforcements.Add(p);
                taken.Add(p);
            }

            // Armas en el suelo: una cerca del inicio y otras en el camino.
            foreach (float t in new[] { 0.06f, 0.35f, 0.6f })
            {
                Vector3? spot = region.Where(p => Free(p, 1.5f)).OrderBy(p => Mathf.Abs(T(p) - t)).Select(p => (Vector3?)p).FirstOrDefault();
                if (spot == null) continue;
                layout.Weapons.Add(spot.Value);
                taken.Add(spot.Value);
            }
            return layout;
        }

        private static bool Reachable(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>Hay sitio para un personaje: el borde del NavMesh está a más de 0.6 m.</summary>
        private static bool IsRoomy(Vector3 p) => Openness(p) > 0.6f;

        private static float Openness(Vector3 p) =>
            NavMesh.FindClosestEdge(p, out var edge, NavMesh.AllAreas) ? edge.distance : 0f;

        private sealed class NearComparer : IEqualityComparer<Vector3>
        {
            private readonly float cell;
            public NearComparer(float cell) => this.cell = cell;
            public bool Equals(Vector3 a, Vector3 b) => Key(a) == Key(b);
            public int GetHashCode(Vector3 v) => Key(v).GetHashCode();
            private Vector3Int Key(Vector3 v) => Vector3Int.RoundToInt(v / cell);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v;
        }

        // ---------------------------------------------------------------- Personajes

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
            camera.backgroundColor = Color.black;
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

        private static Enemy[] CreateGuards(PlayerLife player, Layout layout)
        {
            var root = new GameObject("Enemies").transform;
            var types = new[] { WeaponType.Bat, WeaponType.Knife, WeaponType.Smg, WeaponType.Bat, WeaponType.Shotgun, WeaponType.Knife, WeaponType.Smg };
            var guards = new List<Enemy>();
            for (int i = 0; i < layout.Guards.Count; i++)
            {
                var enemy = CreateEnemy("Guard_" + (i + 1).ToString("D2"), root, player, layout.Guards[i], types[i % types.Length]);
                // Mirando hacia la entrada: vigilan por dónde llegará el jugador.
                enemy.transform.rotation = Quaternion.LookRotation(Flat(layout.Start - enemy.transform.position));
                if (i % 3 != 0) ConfigurePatrol(enemy.GetComponent<EnemyAI>(), i % 2 == 0 ? Vector3.Cross(layout.Axis, Vector3.up) : layout.Axis);
                guards.Add(enemy);
            }
            return guards.ToArray();
        }

        private static ReinforcementWave CreateBossReinforcements(PlayerLife player, Layout layout)
        {
            var root = new GameObject("Boss Reinforcements");
            var wave = root.AddComponent<ReinforcementWave>();
            var squad = new List<EnemyAI>();
            for (int i = 0; i < layout.Reinforcements.Count; i++)
            {
                var enemy = CreateEnemy("Bodyguard_" + (i + 1), root.transform, player, layout.Reinforcements[i], i == 0 ? WeaponType.Smg : WeaponType.Bat);
                squad.Add(enemy.GetComponent<EnemyAI>());
                enemy.gameObject.SetActive(false); // entran cuando el Jefe se enfurece
            }
            var so = new SerializedObject(wave);
            var list = so.FindProperty("squad");
            list.arraySize = squad.Count;
            for (int i = 0; i < squad.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = squad[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return wave;
        }

        private static Enemy CreateBoss(PlayerLife player, Layout layout, ReinforcementWave wave)
        {
            var root = BuilderUtil.FindRoot(SceneManager.GetActiveScene(), "Enemies").transform;
            var boss = CreateEnemy("Boss_Jefe", root, player, layout.Boss, WeaponType.Shotgun);
            boss.transform.rotation = Quaternion.LookRotation(Flat(layout.Start - boss.transform.position));
            boss.transform.localScale = Vector3.one * 1.15f;

            var ai = boss.GetComponent<EnemyAI>();
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("viewRange").floatValue = 18f;
            aiSo.FindProperty("chaseSpeed").floatValue = 4.2f;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            var armor = boss.gameObject.AddComponent<BossEnemy>();
            BuilderUtil.SetRef(armor, "rageWave", wave);
            BuilderUtil.SetRef(armor, "player", player);
            return boss;
        }

        private static Enemy CreateEnemy(string name, Transform parent, PlayerLife player, Vector3 position, WeaponType type)
        {
            var enemy = InstantiatePrefab(AssetFactory.EnemyPrefabPath, name, parent).GetComponent<Enemy>();
            enemy.transform.position = position;
            BuilderUtil.SetRef(enemy.GetComponent<EnemyAI>(), "player", player);
            var weapon = InstantiatePrefab(AssetFactory.WeaponPrefabPath(type), "Weapon_" + type, enemy.Hand).GetComponent<WeaponItem>();
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            var so = new SerializedObject(weapon);
            so.FindProperty("mode").enumValueIndex = (int)WeaponItem.Mode.Held;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            var so = new SerializedObject(ai);
            var points = so.FindProperty("patrolPoints");
            points.arraySize = 2;
            points.GetArrayElementAtIndex(0).vector3Value = start;
            points.GetArrayElementAtIndex(1).vector3Value = end.position;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>El Jefe lleva el modelo Jefe_Actualizado (humanoide, con el Animator de los enemigos) en vez del de su arma.</summary>
        private static void ApplyBossLook(GameObject boss)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(BossModelPath);
            if (importer == null)
            {
                Debug.LogWarning("[Nivel 03] Falta " + BossModelPath + "; el Jefe usa el modelo de su arma.");
                return;
            }
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }

            var controller = BossAnimations.EnsureController();
            GameObject model = CharacterFactory.EnsureModelChild(boss.transform, "Model_Jefe", BossModelPath, "Jefe", controller);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BossTexturePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetFactory.MaterialsPath + "/M_Jefe.mat");
            if (texture != null && material != null)
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTexture("_MainTex", texture);
                material.SetTexture("_EmissionMap", texture);
                EditorUtility.SetDirty(material);
            }

            var visual = boss.GetComponent<EnemyVisual>();
            if (visual == null) return;
            foreach (GameObject other in visual.Models)
                if (other != null && other != model) other.SetActive(false);
            var so = new SerializedObject(visual);
            var models = so.FindProperty("models");
            for (int i = 0; i < models.arraySize; i++) models.GetArrayElementAtIndex(i).objectReferenceValue = model;
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.Apply(WeaponType.Shotgun);
        }

        // ---------------------------------------------------------------- Nivel

        private static ExitZone CreateExit(Transform level, Layout layout)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Exit";
            floor.transform.SetParent(level, false);
            floor.transform.position = layout.Exit + Vector3.up * 0.025f;
            floor.transform.localScale = new Vector3(2.2f, 0.04f, 2.2f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            var zone = floor.AddComponent<ExitZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("size").vector2Value = new Vector2(2.2f, 2.2f);
            so.ApplyModifiedPropertiesWithoutUndo();
            BuilderUtil.SetRef(zone, "zoneRenderer", floor.GetComponent<Renderer>());
            BuilderUtil.SetRef(zone, "lockedMaterial", AssetFactory.Mat(AssetFactory.MatExitLocked));
            BuilderUtil.SetRef(zone, "openMaterial", AssetFactory.Mat(AssetFactory.MatExit));
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "ExitBeam";
            beam.transform.SetParent(level, false);
            beam.transform.position = floor.transform.position + Vector3.up * 1.5f;
            beam.transform.localScale = new Vector3(1.6f, 1.5f, 1.6f);
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.GetComponent<Renderer>().sharedMaterial = AssetFactory.Mat(AssetFactory.MatExitBeam);
            BuilderUtil.SetRef(zone, "beam", beam);
            zone.SetOpen(false);
            return zone;
        }

        private static void CreateSystems(PlayerController player, TopDownCamera camera, Enemy[] enemies, ExitZone exit)
        {
            var systems = new GameObject("Systems");
            systems.AddComponent<LevelRestart>();
            var level = systems.AddComponent<LevelManager>();
            BuilderUtil.SetRef(level, "player", player.Life);
            BuilderUtil.SetRef(level, "exit", exit);
            var so = new SerializedObject(level);
            so.FindProperty("nextScene").stringValue = "Level04"; // tras el jefe, la huida en coche
            var list = so.FindProperty("enemies");
            list.arraySize = enemies.Length;
            for (int i = 0; i < enemies.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            so.ApplyModifiedPropertiesWithoutUndo();

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
            SoundFactory.EnsureGameSounds(systems);
            var hud = systems.AddComponent<GameHUD>();
            BuilderUtil.SetRef(hud, "weapons", player.GetComponent<PlayerWeapons>());
            BuilderUtil.SetRef(hud, "level", level);
            BuilderUtil.SetRef(hud, "rewinder", rewind);
            BuilderUtil.SetRef(hud, "score", score);
        }

        private static void CreateFloorWeapons(Layout layout)
        {
            var root = new GameObject("Weapons").transform;
            var types = new[] { WeaponType.Bat, WeaponType.Smg, WeaponType.Shotgun };
            for (int i = 0; i < layout.Weapons.Count; i++)
            {
                var weapon = InstantiatePrefab(AssetFactory.WeaponPrefabPath(types[i % types.Length]), "Pickup_" + types[i % types.Length], root);
                weapon.transform.position = layout.Weapons[i] + Vector3.up * 0.08f;
            }
        }

        private static void FloorLabel(Transform parent, string text, Vector3 position, float size, Color color, Vector3 facing)
        {
            var label = new GameObject("Sign_" + text).AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.position = position;
            label.transform.rotation = Quaternion.LookRotation(Vector3.down, Flat(facing - position));
            label.text = text;
            label.fontSize = size * 10f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.rectTransform.sizeDelta = new Vector2(12f, 2f);
        }

        // ---------------------------------------------------------------- Flujo del juego

        private static void AddBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else scenes.First(s => s.path == ScenePath).enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Al terminar el nivel 2 se pasa al 3 (antes volvía al menú).</summary>
        private static void LinkFromLevel02()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Level02Builder.ScenePath) == null) return;
            Scene level03 = SceneManager.GetActiveScene();
            Scene level02 = EditorSceneManager.OpenScene(Level02Builder.ScenePath, OpenSceneMode.Additive);
            var manager = level02.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelManager>(true)).FirstOrDefault();
            if (manager != null)
            {
                var so = new SerializedObject(manager);
                so.FindProperty("nextScene").stringValue = "Level03";
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(level02);
                EditorSceneManager.SaveScene(level02);
            }
            EditorSceneManager.CloseScene(level02, true);
            SceneManager.SetActiveScene(level03);
        }

        // ---------------------------------------------------------------- Utilidades

        private static GameObject InstantiatePrefab(string path, string name, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Falta el prefab " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance;
        }

        private static void SetFloat(Object target, string name, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(name).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
