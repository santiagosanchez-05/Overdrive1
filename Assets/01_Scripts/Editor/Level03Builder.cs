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
        /// <summary>Personajes más pequeños: el loft tiene habitaciones y escalera estrechas para la escala de los otros niveles.</summary>
        public const float CharacterScale = 0.7f;
        /// <summary>Balas del revólver con el que se empieza el nivel (el revólver normal trae 6).</summary>
        public const int StartingRevolverAmmo = 20;
        public const string PauseClickPath = "Assets/07_Audio/UI_Sound.mp3";
        /// <summary>Armas de los guardias que salen en el ático del Jefe (solo bate y cuchillo).</summary>
        public static readonly WeaponType[] ArenaGuardWeapons = { WeaponType.Bat, WeaponType.Knife };
        /// <summary>Por encima de esto (sobre el suelo) se considera piso superior.</summary>
        private const float UpperFloorMinHeight = 1.6f;
        private static float floorY;
        private const float MinGuardSpacing = 2.2f;
        private const float SafeStartRadius = 4.5f;

        // Nombres de todo lo que crea el constructor: se borra y se vuelve a crear en cada construcción.
        private static readonly string[] GeneratedRoots =
            { GameplayRoot, "Player", "CameraRig", "Enemies", "Boss Reinforcements", "Boss Final Wave", "Weapons", "Systems" };

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
            AddWalkableFloor(level, loft);
            // Ático del Jefe: una sala grande al lado del loft (el loft es demasiado estrecho para el combate final).
            var arena = Level03Arena.Build(level.transform, LoftBounds(loft), floorY);
            BakeNavigation(level, loft);

            // 2. Puntos de juego sobre el NavMesh.
            var layout = Plan(loft);

            // 3. Jugabilidad.
            var player = InstantiatePrefab(AssetFactory.PlayerPrefabPath, "Player", null).GetComponent<PlayerController>();
            player.transform.position = layout.Start;
            player.transform.rotation = Quaternion.LookRotation(Flat(layout.Boss - layout.Start));
            player.transform.localScale = Vector3.one * CharacterScale;
            SetFloat(player, "moveSpeed", 10f * CharacterScale);
            SetFloat(player, "sneakSpeed", 5f * CharacterScale);
            var camera = CreateCamera(player);

            var enemies = CreateGuards(player.Life, layout);
            // El Jefe, sus refuerzos y la salida pasan al ático; donde esperaba el Jefe queda el ascensor.
            Vector3 elevatorSpot = layout.Boss;
            layout.Boss = arena.Boss;
            layout.Reinforcements = arena.Rage.ToList();
            layout.Exit = arena.Exit;
            // Los guardias del ático van solo cuerpo a cuerpo: las armas de fuego quedan para el Jefe.
            var wave = CreateBossReinforcements(player.Life, layout.Reinforcements, "Boss Reinforcements", ArenaGuardWeapons);
            var finalWave = CreateBossReinforcements(player.Life, arena.Final, "Boss Final Wave", new[] { ArenaGuardWeapons[1], ArenaGuardWeapons[0] });
            var boss = CreateBoss(player.Life, layout, wave);
            boss.transform.rotation = Quaternion.LookRotation(Flat(arena.Start - arena.Boss));
            ConfigureBossFight(boss, finalWave);
            var all = enemies.Concat(new[] { boss }).Concat(wave.Squad.Select(ai => ai.Enemy)).Concat(finalWave.Squad.Select(ai => ai.Enemy)).ToArray();

            var exit = CreateExit(level.transform, layout);
            CreateSystems(player, camera, all, exit);
            Level03Arena.BuildElevator(level.transform, elevatorSpot, arena, player.Life, enemies, camera);
            // Vista cenital: el techo y el piso de arriba no tapan al jugador cuando está debajo.
            var cutaway = BuilderUtil.FindRoot(scene, "Systems").AddComponent<LoftCutaway>();
            BuilderUtil.SetRef(cutaway, "player", player.transform);
            BuilderUtil.SetRef(cutaway, "model", loft.transform);
            CreateFloorWeapons(layout);
            CreateArenaWeapons(arena);
            GiveStartingRevolver(player);
            FloorLabel(level.transform, "OFICINA DEL JEFE", layout.Start + Flat(elevatorSpot - layout.Start).normalized * 2f + Vector3.up * 0.03f,
                0.7f, new Color(1f, 0.25f, 0.45f), elevatorSpot);

            RoofFactory.BuildLevel03(scene); // techo que solo aparece en primera persona

            MenuBuilder.AddPauseMenu(scene);
            // En este nivel la pausa suena con UI_Sound (como lo dejó Mateo).
            var pauseClick = AssetDatabase.LoadAssetAtPath<AudioClip>(PauseClickPath);
            if (pauseClick != null)
                foreach (var button in BuilderUtil.FindRoot(scene, "PauseMenu").GetComponentsInChildren<NeonMenuButton>(true))
                    button.clickClip = pauseClick;
            MenuBuilder.AddLevelMusic(scene);
            StoryFactory.ApplyEnemyLooks(scene);
            ApplyBossLook(boss.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddBuildSettings();
            LinkFromLevel02();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Nivel 03] Listo: {colliders} colliders, {enemies.Length} guardias, Jefe con {wave.Squad.Length} refuerzos. " +
                      $"Loft de {layout.Size.x:0.#} x {layout.Size.z:0.#} m. Inicio {layout.Start} (abajo), Jefe {layout.Boss}. Escala {CharacterScale}.");
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

        /// <summary>
        /// Piso invisible a la altura del suelo del loft. Los suelos de FBX exportados con las normales al revés no son
        /// caminables para el NavMesh y el CharacterController los atraviesa: esta losa garantiza suelo firme sin tocar el modelo.
        /// La altura se mide con rayos (contando caras traseras): el impacto más bajo de cada rayo, y la mediana de todos.
        /// </summary>
        private static float AddWalkableFloor(GameObject level, GameObject loft)
        {
            Bounds b = LoftBounds(loft);
            Physics.SyncTransforms();
            bool backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            var heights = new List<float>();
            try
            {
                for (float x = b.min.x + 0.5f; x < b.max.x; x += 1f)
                    for (float z = b.min.z + 0.5f; z < b.max.z; z += 1f)
                    {
                        var hits = Physics.RaycastAll(new Vector3(x, b.max.y + 1f, z), Vector3.down, b.size.y + 2f, ~0, QueryTriggerInteraction.Ignore)
                            .Where(h => h.collider.transform.IsChildOf(loft.transform) && h.point.y < b.min.y + b.size.y * 0.4f).ToList();
                        if (hits.Count > 0) heights.Add(hits.Min(h => h.point.y));
                    }
            }
            finally { Physics.queriesHitBackfaces = backfaces; }
            heights.Sort();
            floorY = heights.Count > 0 ? heights[heights.Count / 2] : b.min.y;

            var floor = new GameObject("WalkFloor");
            floor.transform.SetParent(level.transform, false);
            floor.transform.position = new Vector3(b.center.x, floorY - 0.05f, b.center.z);
            var box = floor.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Max(1f, b.size.x - 0.2f), 0.1f, Mathf.Max(1f, b.size.z - 0.2f));
            floor.isStatic = true;
            Debug.Log($"[Nivel 03] Suelo del loft a y={floorY:0.00} ({heights.Count} rayos). Alto del loft {b.size.y:0.0} m.");
            return floorY;
        }

        private static void BakeNavigation(GameObject level, GameObject loft)
        {
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            var settings = surface.GetBuildSettings();
            // Agente a la escala de los personajes de este nivel (escaleras y pasillos estrechos del loft).
            settings.agentClimb = 0.3f;
            settings.agentHeight = 1.8f * CharacterScale;
            settings.agentRadius = 0.45f * CharacterScale;
            settings.agentSlope = 50f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.06f;

            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(loft.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var floorSources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(level.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), floorSources);
            sources.AddRange(floorSources);
            Bounds bounds = LoftBounds(loft);
            foreach (var c in level.GetComponentsInChildren<Collider>(true)) bounds.Encapsulate(c.bounds); // también el ático
            bounds.Expand(4f);
            var baked = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (baked == null) throw new InvalidOperationException("El horneado del NavMesh de Level03 no produjo datos.");
            surface.RemoveData();
            surface.navMeshData = Level02Environment.StoreAsset(baked, DataPath + "/Level03_NavMesh.asset");
            surface.AddData();

            var tri = NavMesh.CalculateTriangulation();
            float area = 0f;
            for (int i = 0; i + 2 < tri.indices.Length; i += 3)
            {
                Vector3 a = tri.vertices[tri.indices[i]], c = tri.vertices[tri.indices[i + 1]], d = tri.vertices[tri.indices[i + 2]];
                area += Vector3.Cross(c - a, d - a).magnitude * 0.5f;
            }
            Debug.Log($"[Nivel 03] NavMesh: {area:0} m² caminables ({sources.Count} colliders).");
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
        /// Recorre el loft en rejilla sobre el NavMesh y lo separa en planta baja y piso superior.
        /// El jugador aparece abajo, lejos de la escalera; el Jefe y sus guardias esperan arriba,
        /// así nadie le ataca nada más entrar y tiene que subir a por ellos.
        /// </summary>
        private static Layout Plan(GameObject loft)
        {
            Bounds b = LoftBounds(loft);
            var candidates = new List<Vector3>();
            const float step = 0.8f;
            for (float x = b.min.x + step * 0.5f; x < b.max.x; x += step)
                for (float z = b.min.z + step * 0.5f; z < b.max.z; z += step)
                    for (float y = b.min.y + 0.3f; y < b.max.y; y += 0.8f)
                        if (NavMesh.SamplePosition(new Vector3(x, y, z), out var hit, 0.5f, NavMesh.AllAreas))
                            candidates.Add(hit.position);
            candidates = candidates.Distinct(new NearComparer(0.3f)).ToList();
            if (candidates.Count < 12) throw new InvalidOperationException($"El loft casi no tiene suelo navegable ({candidates.Count} puntos). Revisa en la Console la altura del suelo y los m² del NavMesh.");

            bool IsUpper(Vector3 p) => p.y > floorY + UpperFloorMinHeight;

            // Zonas conectadas (los muebles pueden partir el loft). Se prefiere la que une planta baja y piso superior.
            var components = new List<List<Vector3>>();
            var pending = new List<Vector3>(candidates);
            while (pending.Count > 0)
            {
                Vector3 seed = pending[0];
                var component = pending.Where(p => p == seed || Reachable(seed, p)).ToList();
                components.Add(component);
                pending = pending.Except(component).ToList();
            }
            List<Vector3> region = components
                .OrderByDescending(c => c.Any(IsUpper) && c.Any(p => !IsUpper(p)) ? 1 : 0)
                .ThenByDescending(c => c.Count).First();

            var ground = region.Where(p => !IsUpper(p)).ToList();
            var upper = region.Where(IsUpper).ToList();
            bool twoFloors = ground.Count >= 4 && upper.Count >= 4;
            if (!twoFloors)
            {
                // Sin escalera caminable: se usa el piso más grande y se reparte de punta a punta.
                Debug.LogWarning($"[Nivel 03] La planta baja y el piso superior no están conectados por el NavMesh " +
                                 $"({components.Count(c => c.Any(IsUpper))} zona(s) arriba). Todo queda en un mismo piso.");
                var floor = ground.Count >= upper.Count ? ground : upper;
                ground = floor;
                upper = floor;
            }

            List<Vector3> Roomy(List<Vector3> list)
            {
                var r = list.Where(IsRoomy).ToList();
                return r.Count >= 4 ? r : list;
            }
            var groundRoomy = Roomy(ground);
            var upperRoomy = Roomy(upper);

            var layout = new Layout { Size = b.size };
            Vector3 upperCenter = upperRoomy.Aggregate(Vector3.zero, (acc, p) => acc + p) / upperRoomy.Count;
            // Inicio: el punto abierto de la planta baja más lejano del piso de arriba (o de la otra punta si solo hay uno).
            layout.Start = groundRoomy.OrderByDescending(p => Flat(p - upperCenter).sqrMagnitude * (1f + Openness(p))).First();
            layout.Boss = upperRoomy.OrderByDescending(p => Flat(p - layout.Start).sqrMagnitude * (1f + Openness(p))).First();
            layout.Axis = Flat(layout.Boss - layout.Start).normalized;
            layout.Exit = groundRoomy.Where(p => Vector3.Distance(p, layout.Start) > 1.2f && Vector3.Distance(p, layout.Start) < 3f)
                .OrderByDescending(p => Vector3.Distance(p, layout.Boss)).DefaultIfEmpty(layout.Start).First();

            var taken = new List<Vector3> { layout.Start, layout.Boss, layout.Exit };
            bool Free(Vector3 p, float spacing) => taken.All(q => Vector3.Distance(p, q) >= spacing);
            float spacingGuards = MinGuardSpacing * CharacterScale;
            float safe = SafeStartRadius * CharacterScale;

            // Guardias: solo en el piso superior y repartidos por todo él: cada uno va al punto libre más alejado
            // de los ya colocados (y del Jefe), en vez de amontonarse a su lado.
            var options = upperRoomy.Where(p => Vector3.Distance(p, layout.Start) >= safe).ToList();
            var placed = new List<Vector3> { layout.Boss };
            while (layout.Guards.Count < GuardCount)
            {
                Vector3? best = null;
                float bestScore = spacingGuards;
                foreach (Vector3 p in options)
                {
                    if (!Free(p, spacingGuards)) continue;
                    float score = placed.Min(q => Vector3.Distance(p, q));
                    if (score > bestScore) { bestScore = score; best = p; }
                }
                if (best == null) break;
                layout.Guards.Add(best.Value);
                placed.Add(best.Value);
                taken.Add(best.Value);
            }

            // Si arriba no caben todos, el resto en la planta baja, lejos de la entrada (que no ataquen nada más empezar).
            var groundOptions = groundRoomy.Where(p => Vector3.Distance(p, layout.Start) >= safe * 1.5f).ToList();
            while (layout.Guards.Count < GuardCount)
            {
                Vector3? best = null;
                float bestScore = spacingGuards;
                foreach (Vector3 p in groundOptions)
                {
                    if (!Free(p, spacingGuards)) continue;
                    float score = placed.Min(q => Vector3.Distance(p, q));
                    if (score > bestScore) { bestScore = score; best = p; }
                }
                if (best == null) break;
                layout.Guards.Add(best.Value);
                placed.Add(best.Value);
                taken.Add(best.Value);
            }

            // Refuerzos de furia: también arriba, cerca del Jefe.
            foreach (Vector3 p in upperRoomy.OrderBy(p => Mathf.Abs(Vector3.Distance(p, layout.Boss) - 2.5f)))
            {
                if (layout.Reinforcements.Count >= 3) break;
                if (!Free(p, 1.4f)) continue;
                layout.Reinforcements.Add(p);
                taken.Add(p);
            }

            // Armas: dos abajo cerca del inicio (para subir armado) y una arriba.
            foreach (var (list, near) in new[] { (groundRoomy, layout.Start), (groundRoomy, layout.Start), (upperRoomy, layout.Boss) })
            {
                Vector3? spot = list.Where(p => Free(p, 1.2f) && Vector3.Distance(p, near) > 1.2f)
                    .OrderBy(p => Vector3.Distance(p, near)).Select(p => (Vector3?)p).FirstOrDefault();
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
        private static bool IsRoomy(Vector3 p) => Openness(p) > 0.5f * CharacterScale;

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
            SetFloat(rig, "distance", 11f); // más cerca: personajes y habitaciones más pequeños
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

        private static ReinforcementWave CreateBossReinforcements(PlayerLife player, List<Vector3> positions, string rootName, WeaponType[] types)
        {
            var root = new GameObject(rootName);
            var wave = root.AddComponent<ReinforcementWave>();
            var squad = new List<EnemyAI>();
            string prefix = rootName == "Boss Final Wave" ? "LastGuard_" : "Bodyguard_";
            for (int i = 0; i < positions.Count; i++)
            {
                var enemy = CreateEnemy(prefix + (i + 1), root.transform, player, positions[i], types[i % types.Length]);
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
            boss.transform.localScale = Vector3.one * 1.15f * CharacterScale;

            var ai = boss.GetComponent<EnemyAI>();
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("viewRange").floatValue = 8f;
            aiSo.FindProperty("chaseSpeed").floatValue = 4.2f * CharacterScale;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            var armor = boss.gameObject.AddComponent<BossEnemy>();
            BuilderUtil.SetRef(armor, "rageWave", wave);
            BuilderUtil.SetRef(armor, "player", player);
            return boss;
        }

        /// <summary>
        /// Jefe del ático: más vida (la sala da para un combate largo), visión para toda la sala, embestidas, oleada final
        /// con un cuarto de vida y una luz roja que se enciende en su furia y al avisar de cada embestida.
        /// </summary>
        private static void ConfigureBossFight(Enemy boss, ReinforcementWave finalWave)
        {
            var ai = boss.GetComponent<EnemyAI>();
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("viewRange").floatValue = 16f;
            aiSo.FindProperty("viewHalfAngle").floatValue = 70f;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            var light = new GameObject("RageLight").AddComponent<Light>();
            light.transform.SetParent(boss.transform, false);
            light.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.1f, 0.15f);
            light.range = 7f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            var armor = boss.GetComponent<BossEnemy>();
            var so = new SerializedObject(armor);
            so.FindProperty("maxHealth").intValue = 12;
            so.FindProperty("showBarDistance").floatValue = 30f;
            so.FindProperty("finalWave").objectReferenceValue = finalWave;
            so.FindProperty("rageLight").objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateArenaWeapons(Level03Arena.Points arena)
        {
            var root = BuilderUtil.FindRoot(SceneManager.GetActiveScene(), "Weapons").transform;
            foreach (var (pos, type) in arena.Weapons)
            {
                var weapon = InstantiatePrefab(AssetFactory.WeaponPrefabPath(type), "Arena_" + type, root);
                weapon.transform.position = pos + Vector3.up * 0.08f;
            }
        }

        private static Enemy CreateEnemy(string name, Transform parent, PlayerLife player, Vector3 position, WeaponType type)
        {
            var enemy = InstantiatePrefab(AssetFactory.EnemyPrefabPath, name, parent).GetComponent<Enemy>();
            enemy.transform.position = position;
            enemy.transform.localScale = Vector3.one * CharacterScale;
            var ai = enemy.GetComponent<EnemyAI>();
            BuilderUtil.SetRef(ai, "player", player);
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("viewRange").floatValue = 6f; // el loft es pequeño: que no vean al jugador nada más entrar
            aiSo.FindProperty("viewHalfAngle").floatValue = 45f;
            foreach (string field in new[] { "walkSpeed", "chaseSpeed", "proximity", "eyeHeight", "witnessRadius" })
            {
                var prop = aiSo.FindProperty(field);
                if (prop != null) prop.floatValue *= CharacterScale;
            }
            aiSo.ApplyModifiedPropertiesWithoutUndo();
            // La animación de andar/correr se elige por velocidad: a escala, si no, parecería que se deslizan.
            var visual = enemy.GetComponent<EnemyVisual>();
            if (visual != null)
            {
                var vSo = new SerializedObject(visual);
                foreach (string field in new[] { "walkSpeed", "runSpeed" })
                {
                    var prop = vSo.FindProperty(field);
                    if (prop != null) prop.floatValue *= CharacterScale;
                }
                vSo.ApplyModifiedPropertiesWithoutUndo();
            }
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

        /// <summary>El nivel 3 empieza con un revólver en la mano y <see cref="StartingRevolverAmmo"/> balas.</summary>
        private static void GiveStartingRevolver(PlayerController player)
        {
            var weapons = player.GetComponent<PlayerWeapons>();
            var revolver = InstantiatePrefab(AssetFactory.WeaponPrefabPath(WeaponType.Revolver), "StartingRevolver", BuilderUtil.FindRoot(SceneManager.GetActiveScene(), "Weapons").transform);
            revolver.transform.position = player.transform.position + Vector3.up * 0.08f;
            var ammo = new SerializedObject(revolver.GetComponent<WeaponItem>());
            ammo.FindProperty("startAmmo").intValue = StartingRevolverAmmo;
            ammo.ApplyModifiedPropertiesWithoutUndo();
            BuilderUtil.SetRef(weapons, "startingWeapon", revolver.GetComponent<WeaponItem>());
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
                so.FindProperty("autoAdvanceDelay").floatValue = 3f;
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
