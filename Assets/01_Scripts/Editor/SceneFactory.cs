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
        public static string ScenesPath => AssetFactory.ScenesPath;
        public static string ScenePath => ScenesPath + "/Level01.unity";
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
        public const string ExitBeamName = "ExitBeam";
        public const float ExitBeamHeight = 6f;
        public const string PropsName = "Props";
        public const string HaloName = "Halo";
        public const float PropWallGap = 0.03f;
        public const string DecorName = "Decor";
        public const string AmbienceName = "Ambience";
        public static string NavMeshPath => ScenesPath + "/Level01_NavMesh.asset";
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
            BuildProps(level.transform, ground);
            for (int i = 1; i < layout.Floors.Length; i++)
                BuildProps(level.transform.Find(FloorRootName(i)), layout.Floors[i]);
            AdoptDecor(scene, level);
            EnsureDecorColliders(level.transform);
            BuildAmbience(level.transform, layout);
            BuildLedCables(level.transform, layout);
            BakeNavMesh(level);

            GameObject door = EnsureDoor(scene, ground);
            GameObject player = EnsurePlayer(scene, ground);
            var life = player.GetComponent<PlayerLife>();
            EnsureEnemies(scene, layout, life);
            EnsureFloorWeapons(scene, layout);
            BriefcasePickup briefcase = EnsureBriefcase(scene, layout, life);
            ReinforcementWave wave = EnsureReinforcements(scene, ground, door, life);
            EnsureCamera(scene, player);
            StoryFactory.EnsureLevel01(scene, ground);
            StoryFactory.ApplyEnemyLooks(scene);

            EnsureSystems(scene, level, player, briefcase, wave);
            MenuBuilder.AddPauseMenu(scene);
            MenuBuilder.AddLevelMusic(scene);

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
                // Solo colisión: la rampa naranja no se ve, se ven los escalones del decorado.
                go.GetComponent<Renderer>().enabled = false;
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
            BuilderUtil.SetMaterial(door, AssetFactory.Mat(AssetFactory.MatDoorDissolve));

            var obstacle = BuilderUtil.EnsureComponent<NavMeshObstacle>(door);
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = Vector3.zero;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            obstacle.enabled = true;
            door.GetComponent<Collider>().enabled = true;

            // Al llegar los refuerzos se disuelve con borde brillante (shader Overdrive/NoteDissolve).
            var dissolve = BuilderUtil.EnsureComponent<DoorDissolve>(door);
            BuilderUtil.SetRef(dissolve, "doorRenderer", door.GetComponent<Renderer>());
            BuilderUtil.SetRef(dissolve, "doorCollider", door.GetComponent<Collider>());
            BuilderUtil.SetRef(dissolve, "obstacle", obstacle);
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

            // Haz de luz verde (shader Overdrive/LightBeam) que se enciende al abrirse la salida.
            // Es hermano del suelo de la salida (no hijo) para no heredar su escala aplanada.
            GameObject beam = BuilderUtil.EnsurePrimitiveChild(level, ExitBeamName, PrimitiveType.Cylinder, false);
            float diameter = Mathf.Min(block.Width, block.Height) * map.CellSize * 0.8f;
            // La tapa inferior del cilindro queda bajo el suelo: si no, se ve como un disco verde sólido.
            BuilderUtil.SetLocal(beam,
                map.BlockCenter(block) + new Vector3(0f, ExitBeamHeight * 0.5f - 0.1f, 0f),
                new Vector3(diameter, ExitBeamHeight * 0.5f, diameter),
                Quaternion.identity);
            BuilderUtil.SetMaterial(beam, AssetFactory.Mat(AssetFactory.MatExitBeam));
            var beamRenderer = beam.GetComponent<Renderer>();
            beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;
            beam.SetActive(false);
            BuilderUtil.SetRef(zone, "beam", beam);
        }

        /// <summary>¿Es la raíz de una instancia de un mueble o modelo del equipo (Mnostva_Art, Kenney, NN_Modelos)?</summary>
        public static bool IsDecorInstance(GameObject go)
        {
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) return false;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
            return path != null && AssetFactory.DecorRoots.Any(root => path.StartsWith(root + "/"));
        }

        /// <summary>Más bajo que esto, un mueble es plano (alfombra): se puede pisar y no lleva colisión.</summary>
        public const float FlatDecorHeight = 0.15f;

        /// <summary>
        /// Muebles adoptados sin colisión (los modelos de Kenney vienen sin collider): cada malla recibe una caja
        /// ajustada a su forma, para que bloqueen el paso, las balas y entren en el NavMesh. No se tocan los
        /// archivos del kit, solo las instancias de la escena. Se excluyen las alfombras (planas) y lo que toque
        /// la rampa de la escalera, para no bloquear la subida.
        /// </summary>
        private static void EnsureDecorColliders(Transform level)
        {
            Physics.SyncTransforms();
            List<Bounds> stairs = level.Cast<Transform>().Where(t => t.name.StartsWith("Stairs"))
                .Select(t => t.GetComponent<Collider>().bounds).ToList();

            foreach (Transform decor in level.GetComponentsInChildren<Transform>(true).Where(t => t.name == DecorName).ToList())
            {
                foreach (Transform item in decor)
                {
                    if (!NeedsDecorCollider(item.gameObject, stairs)) continue;
                    foreach (MeshFilter mf in item.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null) continue;
                        var box = BuilderUtil.EnsureComponent<BoxCollider>(mf.gameObject);
                        box.center = mf.sharedMesh.bounds.center;
                        box.size = mf.sharedMesh.bounds.size;
                    }
                }
            }
        }

        /// <summary>¿Hay que ponerle colisión? Solo si no tiene ya, no es plano y no toca la rampa.</summary>
        public static bool NeedsDecorCollider(GameObject item, IEnumerable<Bounds> stairs)
        {
            bool hasOwn = item.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger && !(c is BoxCollider && IsAddedDecorBox(c)));
            if (hasOwn) return false;
            Bounds b = PropBounds(item);
            if (b.size.y < FlatDecorHeight) return false;
            return !stairs.Any(s => s.Intersects(b));
        }

        /// <summary>¿Es una caja que añadió el constructor (componente añadido a una instancia de un modelo)?</summary>
        private static bool IsAddedDecorBox(Collider c) =>
            PrefabUtility.IsAddedComponentOverride(c);

        /// <summary>
        /// Muebles colocados a mano (por el equipo) en la raíz de la escena: se mueven, sin cambiar su posición,
        /// bajo Level/Decor o Level/FloorN/Decor. Así entran en el NavMesh (los enemigos los rodean en vez de
        /// atravesarlos) y los del piso de arriba se ocultan con su piso.
        /// </summary>
        private static void AdoptDecor(Scene scene, GameObject level)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!IsDecorInstance(root)) continue;
                int floor = Floors.IndexOf(PropBounds(root).min.y);
                Transform parent = floor == 0 ? level.transform : level.transform.Find(FloorRootName(floor));
                if (parent == null) parent = level.transform;
                Transform decor = BuilderUtil.EnsureChild(parent, DecorName).transform;
                root.transform.SetParent(decor, true);
            }
        }

        public static Transform FloorParent(Transform level, int floor) =>
            floor == 0 ? level : level.Find(FloorRootName(floor));

        /// <summary>Posición en el mundo de una luz de ambiente (centro de su celda, a ras del suelo de su piso).</summary>
        public static Vector3 AmbientBase(LevelLayout layout, AmbientLight light)
        {
            LevelMap map = layout.Floors[light.Floor];
            return new Vector3(light.Col * map.CellSize, map.BaseY, -light.Row * map.CellSize);
        }

        /// <summary>
        /// Luces de ambiente oscuras: halos (Overdrive/Aura) colgando o apoyados en las mesas y columnas de luz
        /// (Overdrive/LightBeam) que caen del techo. Sin colliders. Van bajo Level/Ambience o Level/FloorN/Ambience.
        /// </summary>
        private static void BuildAmbience(Transform level, LevelLayout layout)
        {
            Physics.SyncTransforms();
            var expected = new Dictionary<Transform, HashSet<string>>();
            foreach (AmbientLight light in layout.Ambience)
            {
                Transform container = BuilderUtil.EnsureChild(FloorParent(level, light.Floor), AmbienceName).transform;
                if (!expected.TryGetValue(container, out HashSet<string> names)) expected[container] = names = new HashSet<string>();
                names.Add(light.Name);

                Vector3 floorPoint = AmbientBase(layout, light);
                Material mat = AssetFactory.Mat(AssetFactory.AmbientMaterial(light.Kind));
                GameObject go;
                if (light.IsBeam)
                {
                    // Cilindro boca abajo: el shader brilla en su base local, que así queda arriba (luz que cae del techo).
                    go = BuilderUtil.EnsurePrimitiveChild(container, light.Name, PrimitiveType.Cylinder, false);
                    float diameter = light.Kind == AmbientKind.BeamViolet ? 2.4f : 1.4f;
                    BuilderUtil.SetLocal(go, floorPoint + Vector3.up * (light.Height * 0.5f),
                        new Vector3(diameter, light.Height * 0.5f, diameter), Quaternion.Euler(180f, 0f, 0f));
                    BuilderUtil.SetMaterial(go, mat);
                    var r = go.GetComponent<Renderer>();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                else
                {
                    go = AssetFactory.EnsureHalo(container, light.Name, mat);
                    float y = floorPoint.y + light.Height;
                    if (light.Kind == AmbientKind.Ember)
                    {
                        // Apoyada sobre lo que haya debajo (una mesa): primer collider bajo el punto.
                        Vector3 from = floorPoint + Vector3.up * 2.5f;
                        y = Physics.Raycast(from, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore)
                            ? hit.point.y + 0.12f
                            : floorPoint.y + 0.9f;
                    }
                    BuilderUtil.SetLocal(go, new Vector3(floorPoint.x, y, floorPoint.z), Vector3.one, Quaternion.identity);
                }
            }

            // Borra luces que ya no están en la lista.
            foreach (LevelMap map in layout.Floors)
            {
                Transform container = FloorParent(level, map.FloorIndex)?.Find(AmbienceName);
                if (container == null) continue;
                expected.TryGetValue(container, out HashSet<string> names);
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    GameObject child = container.GetChild(i).gameObject;
                    if (names == null || !names.Contains(child.name)) Object.DestroyImmediate(child);
                }
            }
        }

        public const string LedCablesName = "LedCables";
        /// <summary>Contenedor de los cables LED anteriores (encima de las paredes), que ya no se usan.</summary>
        public const string OldLedsName = "Leds";
        public const float LedWidth = 0.22f;
        public const float LedLightSpacing = 3f;
        public const float LedLightIntensity = 1.6f;
        public const float LedLightRangeLying = 2.6f;
        public const float LedLightRangeHanging = 3.8f;

        public static string LedCableName(int index, LedCable cable) => $"Cable_{index}_{cable.Color}{(cable.Flicker ? "_Rota" : "")}";

        /// <summary>Recorrido de un cable en metros del mundo (curva suave o tramos colgantes).</summary>
        public static Vector3[] LedWorldPath(LevelLayout layout, LedCable cable)
        {
            LevelMap map = layout.Floors[cable.Floor];
            Vector3[] anchors = cable.Points
                .Select(p => new Vector3(p.x * map.CellSize, map.BaseY + p.y, -p.z * map.CellSize)).ToArray();
            return cable.Layout == LedLayout.Hanging
                ? LedPath.Hang(anchors, cable.Sag, 12, cable.Bow)
                : LedPath.Smooth(anchors, 8);
        }

        /// <summary>
        /// Cables de LEDs de un solo color (shader Overdrive/LedLine en un LineRenderer) con luces puntuales reales
        /// de su color repartidas a lo largo, que iluminan el suelo, las paredes y los muebles. Las tiras rotas
        /// parpadean, y sus luces con ellas (FlickerLights). Sin colisión. Van bajo Level/LedCables o Level/FloorN/LedCables.
        /// </summary>
        private static void BuildLedCables(Transform level, LevelLayout layout)
        {
            // Los cables anteriores (encima de las paredes) se quitan.
            foreach (Transform old in level.GetComponentsInChildren<Transform>(true).Where(t => t.name == OldLedsName).ToList())
                Object.DestroyImmediate(old.gameObject);

            var expected = new Dictionary<Transform, HashSet<string>>();
            for (int i = 0; i < layout.LedCables.Length; i++)
            {
                LedCable cable = layout.LedCables[i];
                Transform container = BuilderUtil.EnsureChild(FloorParent(level, cable.Floor), LedCablesName).transform;
                if (!expected.TryGetValue(container, out HashSet<string> names)) expected[container] = names = new HashSet<string>();
                string name = LedCableName(i, cable);
                names.Add(name);

                GameObject go = BuilderUtil.EnsureChild(container, name);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Vector3[] path = LedWorldPath(layout, cable);

                var line = BuilderUtil.EnsureComponent<LineRenderer>(go);
                line.useWorldSpace = true;
                line.positionCount = path.Length;
                line.SetPositions(path);
                line.widthMultiplier = LedWidth;
                line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.numCornerVertices = 3;
                line.numCapVertices = 2;
                line.colorGradient = new Gradient();
                line.sharedMaterial = AssetFactory.Mat(AssetFactory.LedMaterial(cable.Color, cable.Flicker));
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;

                // Luces reales de su color cada ~3 m (un poco separadas del cable para no quemar la superficie).
                Vector3[] lightPoints = LedPath.EvenlySpaced(path, LedLightSpacing);
                var lights = new List<Light>();
                for (int k = 0; k < lightPoints.Length; k++)
                {
                    GameObject lightGo = BuilderUtil.EnsureChild(go.transform, $"Light_{k}");
                    lightGo.transform.position = lightPoints[k] + Vector3.up * 0.15f;
                    var light = BuilderUtil.EnsureComponent<Light>(lightGo);
                    light.type = LightType.Point;
                    light.color = AssetFactory.LedRgb(cable.Color);
                    light.intensity = LedLightIntensity;
                    light.range = cable.Layout == LedLayout.Hanging ? LedLightRangeHanging : LedLightRangeLying;
                    light.shadows = LightShadows.None;
                    lights.Add(light);
                }
                for (int k = go.transform.childCount - 1; k >= 0; k--)
                {
                    Transform child = go.transform.GetChild(k);
                    if (!child.name.StartsWith("Light_") || int.Parse(child.name.Substring(6)) >= lightPoints.Length)
                        Object.DestroyImmediate(child.gameObject);
                }

                var flicker = go.GetComponent<FlickerLights>();
                if (cable.Flicker)
                {
                    if (flicker == null) flicker = go.AddComponent<FlickerLights>();
                    var so = new SerializedObject(flicker);
                    SerializedProperty list = so.FindProperty("lights");
                    list.arraySize = lights.Count;
                    for (int k = 0; k < lights.Count; k++) list.GetArrayElementAtIndex(k).objectReferenceValue = lights[k];
                    so.FindProperty("baseIntensity").floatValue = LedLightIntensity;
                    so.FindProperty("seed").floatValue = AssetFactory.LedSeed(cable.Color);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else if (flicker != null)
                {
                    Object.DestroyImmediate(flicker);
                }
            }

            foreach (LevelMap map in layout.Floors)
            {
                Transform container = FloorParent(level, map.FloorIndex)?.Find(LedCablesName);
                if (container == null) continue;
                expected.TryGetValue(container, out HashSet<string> names);
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    GameObject child = container.GetChild(i).gameObject;
                    if (names == null || !names.Contains(child.name)) Object.DestroyImmediate(child);
                }
            }
        }

        public static string PropName(PropCatalog.Entry entry, int col, int row) => $"Prop_{entry.Prefab}_c{col}_r{row}";

        /// <summary>
        /// Muebles del mapa (dígitos 0-9): un prop de Mnostva_Art por celda, escalado a su tamaño de catálogo
        /// y pegado a la pared de esa celda. Van bajo Level (o FloorN) para entrar en el NavMesh y ocultarse con su piso.
        /// </summary>
        private static void BuildProps(Transform parent, LevelMap map)
        {
            Transform props = BuilderUtil.EnsureChild(parent, PropsName).transform;
            var expected = new HashSet<string>();

            foreach (var (col, row, c) in map.Cells())
            {
                if (!PropCatalog.IsProp(c)) continue;
                PropCatalog.Entry entry = PropCatalog.Get(c);
                string name = PropName(entry, col, row);
                expected.Add(name);

                Transform existing = props.Find(name);
                GameObject go;
                if (existing != null)
                {
                    go = existing.gameObject;
                }
                else
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{AssetFactory.PropsPath}/{entry.Prefab}.prefab");
                    if (prefab == null) throw new System.InvalidOperationException($"No se encontró el prop {entry.Prefab} en {AssetFactory.PropsPath}.");
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, props);
                }
                go.name = name;
                PlaceProp(go, map, col, row, entry);

                if (entry.HasHalo)
                {
                    GameObject halo = AssetFactory.EnsureHalo(go.transform, HaloName, AssetFactory.Mat(AssetFactory.MatAuraNeonPink));
                    Bounds b = PropBounds(go);
                    halo.transform.position = new Vector3(b.center.x, b.max.y + 0.25f, b.center.z);
                    halo.transform.rotation = Quaternion.identity;
                }
            }

            for (int i = props.childCount - 1; i >= 0; i--)
            {
                GameObject child = props.GetChild(i).gameObject;
                if (!expected.Contains(child.name)) Object.DestroyImmediate(child);
            }
        }

        /// <summary>Caja de los renderers del prop, sin contar su halo.</summary>
        public static Bounds PropBounds(GameObject prop)
        {
            bool any = false;
            var bounds = new Bounds(prop.transform.position, Vector3.zero);
            foreach (Renderer r in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name == HaloName) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        private static void PlaceProp(GameObject go, LevelMap map, int col, int row, PropCatalog.Entry entry)
        {
            go.transform.localScale = Vector3.one;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Bounds natural = PropBounds(go);

            // De frente al centro de la sala (espalda contra la pared) y con el lado largo paralelo a la pared.
            var (dc, dr) = map.WallSideOf(col, row);
            float yaw = dr == -1 ? 180f : dr == 1 ? 0f : dc == -1 ? 90f : dc == 1 ? -90f : 0f;
            bool modelLongAlongX = natural.size.x >= natural.size.z;
            bool quarterTurn = Mathf.Abs(Mathf.Repeat(yaw, 180f) - 90f) < 1f;
            bool longAlongX = quarterTurn ? !modelLongAlongX : modelLongAlongX;
            bool wantAlongX = dc == 0; // pared norte/sur (o ninguna): lado largo a lo largo de X
            if (longAlongX != wantAlongX) yaw += 90f;

            float longest = Mathf.Max(natural.size.x, natural.size.z);
            float scale = longest > 0.0001f ? entry.Footprint / longest : 1f;
            go.transform.localScale = Vector3.one * scale;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Bounds b = PropBounds(go);

            // Pegado a la pared: el borde de la caja toca el borde de la celda junto a la pared.
            var toWall = new Vector3(dc, 0f, -dr);
            float depth = dc != 0 ? b.size.x : b.size.z;
            float push = Mathf.Max(0f, map.CellSize * 0.5f - depth * 0.5f - PropWallGap);
            Vector3 cell = map.CellToWorld(col, row);
            Vector3 target = cell + toWall * push;
            go.transform.position += new Vector3(target.x - b.center.x, map.BaseY - b.min.y, target.z - b.center.z);

            // En una esquina, el lado largo no debe meterse en la pared perpendicular: se desliza a lo largo de la pared.
            b = PropBounds(go);
            float half = map.CellSize * 0.5f;
            var slide = Vector3.zero;
            if (map.IsWall(col - 1, row)) slide.x = Mathf.Max(slide.x, cell.x - half + PropWallGap - b.min.x);
            if (map.IsWall(col + 1, row)) slide.x = Mathf.Min(slide.x, cell.x + half - PropWallGap - b.max.x);
            // Fila anterior = norte = +Z.
            if (map.IsWall(col, row - 1)) slide.z = Mathf.Min(slide.z, cell.z + half - PropWallGap - b.max.z);
            if (map.IsWall(col, row + 1)) slide.z = Mathf.Max(slide.z, cell.z - half + PropWallGap - b.min.z);
            go.transform.position += slide;
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
            var mgrSo = new SerializedObject(manager);
            SerializedProperty nextProp = mgrSo.FindProperty("nextScene");
            if (nextProp != null) nextProp.stringValue = "Level02";
            mgrSo.ApplyModifiedPropertiesWithoutUndo();

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

            SoundFactory.EnsureGameSounds(systems);

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
            // Quita escenas que ya no existen (por ejemplo, la SampleScene de la plantilla, borrada).
            int removed = scenes.RemoveAll(s => string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(s.guid.ToString())));
            // Level01 tiene que estar y habilitada, pero no se fuerza su orden: el menú principal va antes.
            int index = scenes.FindIndex(s => s.path == ScenePath);
            bool changed = removed > 0;
            if (index < 0)
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                changed = true;
            }
            else if (!scenes[index].enabled)
            {
                scenes[index] = new EditorBuildSettingsScene(ScenePath, true);
                changed = true;
            }
            if (changed) EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
