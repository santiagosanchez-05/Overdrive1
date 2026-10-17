using System.Collections.Generic;
using System.Linq;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.UI;
using Overdrive.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación de los pisos, la escalera, los huecos, el maletín y los refuerzos.</summary>
    internal static partial class Verifier
    {
        private static void VerifyFloorsMap(Checker c, LevelLayout layout)
        {
            c.Check(layout.Floors.Length == 2, "El nivel 1 tiene dos pisos");
            if (layout.Floors.Length < 2) return;
            LevelMap ground = layout.Ground, upper = layout.Floors[1];
            c.Near(upper.BaseY, Floors.Height, "El primer piso está a la altura de un piso");

            int groundEnemies = ground.Cells().Count(x => LevelMap.IsEnemy(x.c));
            int upperEnemies = upper.Cells().Count(x => LevelMap.IsEnemy(x.c));
            c.Check(groundEnemies == 6 && upperEnemies == 4, $"6 enemigos abajo y 4 arriba (hay {groundEnemies} y {upperEnemies})");
            c.Check(layout.EnemyCount == 10, "10 enemigos en total, sin contar refuerzos");

            var briefcases = layout.FindAll(LevelMap.Briefcase);
            c.Check(briefcases.Count == 1 && briefcases[0].map.FloorIndex == 1, "Hay un maletín y está en el piso de arriba");
            c.Check(ground.FindAll(LevelMap.Reinforcement).Count == 4, "Hay 4 entradas de refuerzos en la planta baja");
            c.Check(ground.BoundsOf(LevelMap.Door) != null, "Hay una puerta para los refuerzos");

            // La puerta separa a los refuerzos del resto de la planta baja.
            var (pc, pr) = ground.FindAll(LevelMap.PlayerSpawn)[0];
            HashSet<(int, int)> withoutDoor = FloodFillWhere(ground, pc, pr, (col, row) =>
                ground.IsWalkable(col, row) && ground.At(col, row) != LevelMap.Door);
            c.Check(ground.FindAll(LevelMap.Reinforcement).All(cell => !withoutDoor.Contains(cell)),
                "Con la puerta cerrada no se llega a los refuerzos");

            // --- Escalera ---
            LevelMap.Block? stairsBounds = ground.BoundsOf(LevelMap.Stairs);
            c.Check(stairsBounds != null, "Hay una escalera en la planta baja");
            if (stairsBounds != null)
            {
                LevelMap.Block s = stairsBounds.Value;
                c.Check(ground.FindAll(LevelMap.Stairs).Count == s.Width * s.Height, "La escalera es un rectángulo");
                c.Check(s.Width >= 4, "La escalera es lo bastante larga para no ser una pared");
                for (int row = s.Row; row < s.Row + s.Height; row++)
                {
                    c.Check(ground.IsWalkable(s.Col - 1, row), $"Se puede entrar a la escalera por el oeste (fila {row})");
                    c.Check(upper.IsWalkable(s.Col + s.Width, row), $"La escalera llega a suelo del piso de arriba (fila {row})");
                    for (int col = s.Col; col < s.Col + s.Width; col++)
                        c.Check(!upper.HasSlab(col, row), $"No hay techo sobre la escalera en ({col},{row})");
                }

                var arrival = (s.Col + s.Width, s.Row);
                HashSet<(int, int)> upperReach = upper.FloodFill(arrival.Item1, arrival.Item2);
                int upperWalkable = upper.Cells().Count(x => upper.IsWalkable(x.col, x.row));
                c.Check(upperReach.Count == upperWalkable,
                    $"Todo el piso de arriba se alcanza desde la escalera ({upperReach.Count}/{upperWalkable})");
            }

            // --- Huecos ---
            List<(int col, int row)> holes = upper.FindAll(LevelMap.Hole);
            c.Check(holes.Count > 0, "Hay huecos en el piso de arriba");
            c.Check(holes.All(h => ground.IsWalkable(h.col, h.row)), "Bajo cada hueco hay suelo de la planta baja");
            c.Check(holes.Any(h => LevelMap.IsEnemy(ground.At(h.col, h.row))), "Algún hueco cae justo sobre un enemigo");
        }

        private static HashSet<(int, int)> FloodFillWhere(LevelMap map, int col, int row, System.Func<int, int, bool> walkable)
        {
            var visited = new HashSet<(int, int)> { (col, row) };
            var queue = new Queue<(int, int)>();
            queue.Enqueue((col, row));
            while (queue.Count > 0)
            {
                var (c, r) = queue.Dequeue();
                foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var next = (c + dc, r + dr);
                    if (!walkable(next.Item1, next.Item2) || !visited.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }
            return visited;
        }

        private static void VerifyFloorsScene(Checker c, LevelLayout layout)
        {
            Scene scene = SceneManager.GetActiveScene();
            LevelMap ground = layout.Ground, upper = layout.Floors[1];
            foreach (string root in new[] { SceneFactory.ObjectiveName, SceneFactory.ReinforcementsName, SceneFactory.DoorsName })
            {
                int count = scene.GetRootGameObjects().Count(g => g.name == root);
                c.Check(count == 1, $"Debe haber exactamente un objeto raíz '{root}' (hay {count})");
            }

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            var stompPrefab = playerPrefab != null ? playerPrefab.GetComponent<PlayerStomp>() : null;
            c.NotNull(stompPrefab, "Prefab Player: falta PlayerStomp");
            if (stompPrefab != null)
                c.Check(stompPrefab.Controller == playerPrefab.GetComponent<PlayerController>(), "PlayerStomp.controller sin asignar");

            GameObject level = BuilderUtil.FindRoot(scene, SceneFactory.LevelRoot);
            Transform floor1 = level != null ? level.transform.Find(SceneFactory.FloorRootName(1)) : null;
            c.NotNull(floor1, "Falta Level/Floor1");
            if (floor1 != null)
            {
                Transform slabs = floor1.Find(SceneFactory.SlabsName);
                Transform walls = floor1.Find(SceneFactory.WallsName);
                c.Check(slabs != null && slabs.childCount == upper.SlabBlocks().Count,
                    $"Floor1 tiene {upper.SlabBlocks().Count} losas (hay {(slabs != null ? slabs.childCount : 0)})");
                c.Check(walls != null && walls.childCount == upper.WallBlocks().Count,
                    $"Floor1 tiene {upper.WallBlocks().Count} paredes (hay {(walls != null ? walls.childCount : 0)})");
            }

            // --- Física del suelo de arriba, huecos y rampa ---
            Physics.SyncTransforms();
            var (ec, er) = upper.FindAll('k')[0];
            c.Near(GroundProbe.GroundY(upper.CellToWorld(ec, er) + Vector3.up), Floors.Height, "El suelo de arriba está a 4 m", 0.02f);
            var (hc, hr) = upper.FindAll(LevelMap.Hole)[0];
            c.Near(GroundProbe.GroundY(upper.CellToWorld(hc, hr) + Vector3.up), 0f, "Por un hueco se ve (y se cae) hasta la planta baja", 0.02f);

            Transform stairs = level != null ? level.transform.Find(SceneFactory.StairsName(0)) : null;
            c.Check(stairs != null && stairs.GetComponent<BoxCollider>() != null, "La escalera existe y tiene collider");
            LevelMap.Block? sb = ground.BoundsOf(LevelMap.Stairs);
            if (stairs != null && sb != null)
            {
                SceneFactory.RampGeometry ramp = SceneFactory.RampFor(ground, sb.Value, upper.BaseY);
                c.Near(stairs.position, ramp.Center, "La rampa está colocada según el mapa");
                float angle = Vector3.Angle(stairs.right, Vector3.right);
                c.Check(angle > 15f && angle < 35f, $"La rampa tiene una pendiente transitable ({angle:F1}°)");
                for (float t = 0.2f; t < 0.9f; t += 0.3f)
                {
                    Vector3 probe = Vector3.Lerp(ramp.Bottom, ramp.Top, t);
                    float y = GroundProbe.GroundY(new Vector3(probe.x, probe.y + 0.5f, probe.z));
                    c.Near(y, probe.y, $"La superficie de la rampa sube de forma continua (t={t:F1})", 0.05f);
                }
                Vector3 topStep = ramp.Top + new Vector3(0.3f, 0.5f, 0f);
                c.Near(GroundProbe.GroundY(topStep), upper.BaseY, "Al final de la rampa se pisa el suelo de arriba", 0.05f);
            }

            // --- Puerta ---
            GameObject doorsRoot = BuilderUtil.FindRoot(scene, SceneFactory.DoorsName);
            Transform door = doorsRoot != null ? doorsRoot.transform.Find(SceneFactory.DoorName) : null;
            c.NotNull(door, "Falta la puerta de los refuerzos");
            if (door != null)
            {
                c.Check(door.gameObject.activeSelf && door.GetComponent<BoxCollider>() != null, "La puerta empieza cerrada y es sólida");
                var obstacle = door.GetComponent<NavMeshObstacle>();
                c.Check(obstacle != null && obstacle.carving, "La puerta corta el NavMesh mientras está cerrada");
                foreach (var (col, row) in ground.FindAll(LevelMap.Door))
                {
                    Vector3 p = ground.CellToWorld(col, row);
                    c.Check(door.GetComponent<Collider>().bounds.Contains(p + Vector3.up), $"La puerta tapa la celda ({col},{row})");
                }
            }

            // --- Maletín ---
            GameObject objective = BuilderUtil.FindRoot(scene, SceneFactory.ObjectiveName);
            var briefcase = objective != null ? objective.transform.Find(SceneFactory.BriefcaseName)?.GetComponent<BriefcasePickup>() : null;
            GameObject player = BuilderUtil.FindRoot(scene, SceneFactory.PlayerName);
            var life = player != null ? player.GetComponent<PlayerLife>() : null;
            c.NotNull(briefcase, "Falta el maletín");
            if (briefcase != null)
            {
                var (bmap, bc, br) = layout.FindAll(LevelMap.Briefcase)[0];
                c.Near(briefcase.transform.position, bmap.CellToWorld(bc, br), "El maletín está en su celda del piso de arriba");
                c.Check(briefcase.Player == life, "BriefcasePickup.player sin asignar");
                c.Check(!briefcase.IsCollected, "El maletín empieza sin recoger");
            }

            // --- Refuerzos ---
            GameObject reinforcementsRoot = BuilderUtil.FindRoot(scene, SceneFactory.ReinforcementsName);
            var wave = reinforcementsRoot != null ? reinforcementsRoot.GetComponent<ReinforcementWave>() : null;
            c.NotNull(wave, "Reinforcements: falta ReinforcementWave");
            if (wave != null)
            {
                List<(int col, int row)> cells = ground.FindAll(LevelMap.Reinforcement);
                c.Check(wave.Squad.Length == cells.Count && wave.Squad.All(a => a != null), $"La oleada tiene {cells.Count} refuerzos");
                c.Check(door != null && wave.Door == door.gameObject, "ReinforcementWave.door sin asignar");
                c.Check(!wave.Triggered, "Los refuerzos no han entrado al empezar");
                for (int i = 0; i < wave.Squad.Length && i < cells.Count; i++)
                {
                    EnemyAI ai = wave.Squad[i];
                    if (ai == null) continue;
                    string name = ai.gameObject.name;
                    c.Check(!ai.gameObject.activeSelf, $"{name}: empieza desactivado tras la puerta");
                    c.Check(ai.Player == life, $"{name}: EnemyAI.player sin asignar");
                    c.Near(ai.transform.position, ground.CellToWorld(cells[i].col, cells[i].row), $"{name}: en su celda R");
                    WeaponType expected = SceneFactory.ReinforcementWeapons[i % SceneFactory.ReinforcementWeapons.Length];
                    c.Check(ai.Enemy.Weapon != null && ai.Enemy.Weapon.Type == expected, $"{name}: debe llevar {expected}");
                    VerifyEnemyComponents(c, ai.gameObject, name);
                }
            }

            // --- Sistemas ---
            GameObject systems = BuilderUtil.FindRoot(scene, SceneFactory.SystemsName);
            var manager = systems != null ? systems.GetComponent<LevelManager>() : null;
            if (manager != null)
            {
                c.Check(manager.Briefcase == briefcase, "LevelManager.briefcase sin asignar");
                c.Check(manager.Reinforcements == wave, "LevelManager.reinforcements sin asignar");
            }
            var visibility = systems != null ? systems.GetComponent<FloorVisibility>() : null;
            c.NotNull(visibility, "Systems: falta FloorVisibility");
            if (visibility != null)
            {
                c.Check(player != null && visibility.Player == player.transform, "FloorVisibility.player sin asignar");
                c.Check(visibility.UpperFloors.Length == 1 && floor1 != null && visibility.UpperFloors[0] == floor1.gameObject,
                    "FloorVisibility conoce el piso de arriba");
                c.Check(visibility.Briefcase == briefcase, "FloorVisibility.briefcase sin asignar");
            }

            GameObject enemiesRoot = BuilderUtil.FindRoot(scene, SceneFactory.EnemiesName);
            if (enemiesRoot != null)
            {
                int upstairs = enemiesRoot.GetComponentsInChildren<Enemy>(true).Count(e => Floors.IndexOf(e.transform.position.y) == 1);
                c.Check(upstairs == 4, $"Hay 4 enemigos en el piso de arriba de la escena (hay {upstairs})");
            }
        }

        private static void VerifyFloorsLogic(Checker c)
        {
            c.Check(Floors.IndexOf(0f) == 0 && Floors.IndexOf(0.08f) == 0 && Floors.IndexOf(2f) == 0, "Alturas de la planta baja");
            c.Check(Floors.IndexOf(3.1f) == 1 && Floors.IndexOf(4f) == 1 && Floors.IndexOf(4.08f) == 1, "Alturas del primer piso");
            c.Check(Floors.SameFloor(new Vector3(0f, 4f, 0f), new Vector3(5f, 4.5f, 0f)), "Mismo piso");
            c.Check(!Floors.SameFloor(Vector3.zero, new Vector3(0f, 4f, 0f)), "Pisos distintos");

            c.Check(!FloorVisibility.IsVisible(4f, 0f), "Desde abajo no se ve el piso de arriba");
            c.Check(FloorVisibility.IsVisible(0f, 4f) && FloorVisibility.IsVisible(4f, 4f), "Desde arriba se ve todo");

            Vector3 below = Vector3.zero, above = new Vector3(0f, 4f, 0.8f);
            c.Check(!MeleeTargeting.InArc(below, Vector3.forward, above, 1.6f, 60f), "No se pega a través del techo");
            c.Check(PickupLogic.FindNearest(below, new[] { above }, 1.6f) == -1, "No se recogen armas del piso de arriba");
            c.Check(!NoiseSystem.CanHear(above, below, 20f), "Los disparos no se oyen en otro piso");
            c.Check(!ExitZone.Contains(Vector3.zero, new Vector2(3f, 3f), new Vector3(0f, 4f, 0f)), "La salida no cuenta desde otro piso");

            var progress = new LevelProgress();
            c.Check(progress.Evaluate(10, 10, false, true, false) == LevelState.Playing && !progress.HasObjective,
                "Sin el maletín no se gana aunque no queden enemigos");
            c.Check(progress.Evaluate(10, 10, false, true, true) == LevelState.Complete, "Con el maletín, sin enemigos y en la salida se gana");

            c.Check(HudText.SubMessage(LevelState.Playing, true) == HudText.FindBriefcase, "El HUD indica buscar el maletín");
            c.Check(HudText.Briefcase(true) == HudText.CarryingBriefcase && HudText.Briefcase(false) == null, "El HUD muestra el maletín");

            // Caídas: TrackFall detecta la altura caída.
            var go = new GameObject("TestFall");
            try
            {
                var controller = go.AddComponent<PlayerController>();
                float landed = -1f;
                controller.Landed += drop => landed = drop;
                controller.TrackFall(true, 4f);
                controller.TrackFall(false, 4f);
                controller.TrackFall(false, 2f);
                c.Check(landed < 0f, "En el aire todavía no aterriza");
                controller.TrackFall(true, 0f);
                c.Near(landed, 4f, "Al aterrizar avisa de la altura caída");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void VerifyFloorsSimulation(Checker c, LevelLayout layout)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            var briefcasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BriefcasePrefabPath);
            if (playerPrefab == null || enemyPrefab == null || briefcasePrefab == null) return;

            var created = new List<GameObject>();
            Vector3 basePos = new Vector3(800f, 0f, 800f);
            GameObject Make(GameObject prefab, Vector3 offset)
            {
                GameObject go = Object.Instantiate(prefab, basePos + offset, Quaternion.identity);
                created.Add(go);
                return go;
            }

            try
            {
                // --- Maletín, refuerzos y victoria ---
                var player = Make(playerPrefab, Vector3.zero).GetComponent<PlayerLife>();
                var guard = Make(enemyPrefab, new Vector3(3f, 0f, 0f)).GetComponent<Enemy>();
                var briefcase = Make(briefcasePrefab, new Vector3(10f, 0f, 0f)).GetComponent<BriefcasePickup>();
                var squad = new[]
                {
                    Make(enemyPrefab, new Vector3(-10f, 0f, 0f)).GetComponent<EnemyAI>(),
                    Make(enemyPrefab, new Vector3(-10f, 0f, 2f)).GetComponent<EnemyAI>(),
                };
                foreach (EnemyAI ai in squad) ai.gameObject.SetActive(false);
                GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.Add(door);
                door.transform.position = basePos + new Vector3(-8f, 1f, 0f);

                var waveGo = new GameObject("TestWave");
                created.Add(waveGo);
                var wave = waveGo.AddComponent<ReinforcementWave>();
                var waveSo = new SerializedObject(wave);
                SerializedProperty squadProp = waveSo.FindProperty("squad");
                squadProp.arraySize = squad.Length;
                for (int i = 0; i < squad.Length; i++) squadProp.GetArrayElementAtIndex(i).objectReferenceValue = squad[i];
                waveSo.FindProperty("door").objectReferenceValue = door;
                waveSo.ApplyModifiedPropertiesWithoutUndo();

                GameObject exitGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.Add(exitGo);
                Object.DestroyImmediate(exitGo.GetComponent<Collider>());
                exitGo.transform.position = basePos + new Vector3(0f, 0f, 10f);
                var exit = exitGo.AddComponent<ExitZone>();

                var managerGo = new GameObject("TestManager");
                created.Add(managerGo);
                var manager = managerGo.AddComponent<LevelManager>();
                BuilderUtil.SetRef(manager, "player", player);
                BuilderUtil.SetRef(manager, "exit", exit);
                BuilderUtil.SetRef(manager, "briefcase", briefcase);
                BuilderUtil.SetRef(manager, "reinforcements", wave);
                var managerSo = new SerializedObject(manager);
                SerializedProperty enemiesProp = managerSo.FindProperty("enemies");
                enemiesProp.arraySize = 1;
                enemiesProp.GetArrayElementAtIndex(0).objectReferenceValue = guard;
                managerSo.ApplyModifiedPropertiesWithoutUndo();

                c.Check(manager.Evaluate() == LevelState.Playing && !manager.Progress.HasObjective && manager.Progress.Remaining == 1,
                    "Simulación: al empezar falta el maletín y queda 1 enemigo");
                c.Check(!wave.Triggered && squad.All(a => !a.gameObject.activeSelf) && door.activeSelf,
                    "Los refuerzos esperan tras la puerta cerrada");
                guard.ReceiveLethalHit();
                c.Check(manager.Evaluate() == LevelState.Playing, "Sin el maletín, matar a todos no abre la salida");

                c.Check(!briefcase.TryCollect(player.transform), "El maletín no se recoge desde lejos");
                player.transform.position = briefcase.transform.position + new Vector3(0f, 4f, 0f);
                c.Check(!briefcase.TryCollect(player.transform), "El maletín no se recoge desde otro piso");
                player.transform.position = briefcase.transform.position + new Vector3(0.5f, 0f, 0f);
                c.Check(briefcase.TryCollect(player.transform) && briefcase.IsCollected, "Simulación: pasar por encima recoge el maletín");
                c.Check(briefcase.transform.parent == player.transform, "El maletín va a la espalda del jugador");

                manager.Evaluate();
                c.Check(wave.Triggered && !door.activeSelf && squad.All(a => a.gameObject.activeSelf),
                    "Simulación: al coger el maletín se abre la puerta y entran los refuerzos");
                c.Check(squad.All(a => a.HuntTarget == player && a.Brain.State == AIState.Investigate),
                    "Los refuerzos van a buscar al jugador");
                c.Check(manager.Progress.Remaining == 2 && manager.Progress.HasObjective, "Los refuerzos cuentan como enemigos restantes");
                c.Check(HudText.SubMessage(manager.Progress.State, !manager.Progress.HasObjective) == null,
                    "Con el maletín ya no se pide buscarlo");

                foreach (EnemyAI ai in squad) ai.Enemy.ReceiveLethalHit();
                c.Check(manager.Evaluate() == LevelState.ExitOpen, "Con el maletín y sin enemigos, se abre la salida");
                player.transform.position = exit.transform.position;
                c.Check(manager.Evaluate() == LevelState.Complete, "Simulación: con el maletín en la salida, nivel completado");

                // --- Caer sobre un enemigo ---
                var jumper = Make(playerPrefab, new Vector3(30f, 1.8f, 0f)).GetComponent<PlayerStomp>();
                var under = Make(enemyPrefab, new Vector3(30f, 0f, 0f)).GetComponent<Enemy>();
                var aside = Make(enemyPrefab, new Vector3(34f, 0f, 0f)).GetComponent<Enemy>();
                var downstairs = Make(enemyPrefab, new Vector3(30.5f, -4f, 0f)).GetComponent<Enemy>();
                c.Check(jumper.Stomp(new[] { under, aside, downstairs }) == 1, "Caer sobre un enemigo derriba solo a ese");
                c.Check(under.Posture == Posture.Down && !under.IsDead, "Simulación: caer encima derriba sin matar");
                c.Check(aside.Posture == Posture.Standing && downstairs.Posture == Posture.Standing,
                    "No afecta a los lejanos ni a los de otro piso");

                // --- Visión entre pisos ---
                var watcher = Make(enemyPrefab, new Vector3(50f, 4f, 4f)).GetComponent<EnemyAI>();
                watcher.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var below = Make(playerPrefab, new Vector3(50f, 0f, 0f)).GetComponent<PlayerLife>();
                Physics.SyncTransforms();
                c.Check(!watcher.CanSee(below), "Un enemigo no ve al jugador de otro piso");
                below.transform.position = basePos + new Vector3(50f, 4f, 0f);
                Physics.SyncTransforms();
                c.Check(watcher.CanSee(below), "En el mismo piso sí lo ve");

                // --- Armas que caen en la escena real: sobre la losa o por un hueco ---
                LevelMap upper = layout.Floors[1];
                Physics.SyncTransforms();
                var item = Make(AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.WeaponPrefabPath(WeaponType.Knife)), Vector3.zero)
                    .GetComponent<WeaponItem>();
                var (kc, kr) = upper.FindAll('k')[0];
                item.DropAt(upper.CellToWorld(kc, kr) + Vector3.up, 0f);
                c.Near(item.transform.position.y, Floors.Height + 0.08f, "Un arma soltada arriba queda sobre la losa", 0.03f);
                var (hc, hr) = upper.FindAll(LevelMap.Hole)[0];
                item.DropAt(upper.CellToWorld(hc, hr) + Vector3.up, 0f);
                c.Near(item.transform.position.y, 0.08f, "Un arma soltada sobre un hueco cae a la planta baja", 0.03f);

                // --- Visibilidad de pisos en la escena real ---
                Scene scene = SceneManager.GetActiveScene();
                var visibility = BuilderUtil.FindRoot(scene, SceneFactory.SystemsName)?.GetComponent<FloorVisibility>();
                if (visibility != null && visibility.UpperFloors.Length > 0)
                {
                    GameObject floor1 = visibility.UpperFloors[0];
                    Vector3 start = visibility.Player.position;
                    visibility.Refresh();
                    c.Check(floor1.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), "Simulación: desde abajo se oculta el piso de arriba");
                    visibility.Player.position = start + new Vector3(0f, Floors.Height, 0f);
                    visibility.Refresh();
                    c.Check(floor1.GetComponentsInChildren<Renderer>(true).All(r => r.enabled), "Simulación: arriba se ve el piso de arriba");
                    visibility.Player.position = start;
                }
            }
            finally
            {
                foreach (GameObject go in created)
                    if (go != null) Object.DestroyImmediate(go);
            }
        }
    }
}
