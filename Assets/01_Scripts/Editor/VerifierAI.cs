using System.Collections.Generic;
using System.Linq;
using Overdrive.Combat;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Weapons;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación de la IA: referencias, NavMesh, visión, máquina de estados y ataques.</summary>
    internal static partial class Verifier
    {
        private static void VerifyAIAssets(Checker c)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            if (prefab == null) return;
            var agent = prefab.GetComponent<NavMeshAgent>();
            var ai = prefab.GetComponent<EnemyAI>();
            c.NotNull(agent, "Prefab Enemy: falta NavMeshAgent");
            c.NotNull(ai, "Prefab Enemy: falta EnemyAI");
            if (agent != null)
                c.Check(Mathf.Approximately(agent.radius, 0.4f) && Mathf.Approximately(agent.height, 1.8f),
                    "El NavMeshAgent tiene el tamaño del enemigo");
            if (ai != null)
            {
                c.Check(ai.Enemy == prefab.GetComponent<Enemy>(), "EnemyAI.enemy sin asignar");
                c.Check(ai.Agent == agent, "EnemyAI.agent sin asignar");
            }
        }

        private static void VerifyAIScene(Checker c, LevelLayout layout)
        {
            LevelMap map = layout.Ground;
            Scene scene = SceneManager.GetActiveScene();
            GameObject level = BuilderUtil.FindRoot(scene, SceneFactory.LevelRoot);
            var surface = level != null ? level.GetComponent<NavMeshSurface>() : null;
            c.NotNull(surface, "Level: falta NavMeshSurface");
            c.Check(surface != null && surface.navMeshData != null &&
                    AssetDatabase.GetAssetPath(surface.navMeshData) == SceneFactory.NavMeshPath,
                "El NavMesh debe estar horneado y guardado en " + SceneFactory.NavMeshPath);

            var (pc, pr) = map.FindAll(LevelMap.PlayerSpawn)[0];
            Vector3 spawn = map.CellToWorld(pc, pr);
            c.Check(NavMesh.SamplePosition(spawn, out _, 0.5f, NavMesh.AllAreas), "El punto de inicio está sobre el NavMesh");

            var path = new NavMeshPath();
            // Los enemigos, armas, maletín y salida de todos los pisos deben estar conectados.
            foreach (var (floorMap, col, row, ch) in layout.AllCells())
            {
                if (!LevelMap.IsEnemy(ch) && !LevelMap.IsFloorWeapon(ch) && ch != LevelMap.Exit && ch != LevelMap.Briefcase) continue;
                Vector3 target = floorMap.CellToWorld(col, row);
                bool found = NavMesh.CalculatePath(spawn, target, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                c.Check(found, $"Hay camino por el NavMesh desde el inicio hasta '{ch}' en ({col},{row}) del piso {floorMap.FloorIndex}");
            }

            // Los refuerzos están tras la puerta: su NavMeshObstacle corta el paso mientras está cerrada,
            // así que se comprueba que su zona está horneada y que se llega hasta justo delante de la puerta.
            foreach (var (col, row) in map.FindAll(LevelMap.Reinforcement))
                c.Check(NavMesh.SamplePosition(map.CellToWorld(col, row), out _, 0.5f, NavMesh.AllAreas),
                    $"La entrada de refuerzos ({col},{row}) está sobre el NavMesh");
            LevelMap.Block? doorBounds = map.BoundsOf(LevelMap.Door);
            if (doorBounds != null)
            {
                // Basta con llegar delante de uno de los huecos de la puerta (puede haber decoración junto al otro).
                bool reach = false;
                for (int r = doorBounds.Value.Row; r < doorBounds.Value.Row + doorBounds.Value.Height && !reach; r++)
                {
                    Vector3 inFront = map.CellToWorld(doorBounds.Value.Col - 1, r);
                    reach = NavMesh.CalculatePath(spawn, inFront, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                }
                c.Check(reach, "Hay camino por el NavMesh hasta delante de la puerta de los refuerzos");
            }

            GameObject player = BuilderUtil.FindRoot(scene, SceneFactory.PlayerName);
            var life = player != null ? player.GetComponent<PlayerLife>() : null;
            GameObject enemiesRoot = BuilderUtil.FindRoot(scene, SceneFactory.EnemiesName);
            if (enemiesRoot == null) return;

            EnemyAI[] ais = enemiesRoot.GetComponentsInChildren<EnemyAI>(true);
            int patrollers = 0;
            foreach (EnemyAI ai in ais)
            {
                string name = ai.gameObject.name;
                c.Check(ai.Player != null && ai.Player == life, $"{name}: EnemyAI.player debe ser el Player de la escena");
                Vector3[] points = ai.PatrolPoints ?? new Vector3[0];
                c.Check(points.Length == 0 || points.Length == 2, $"{name}: la patrulla debe tener 0 o 2 puntos");
                if (points.Length == 2) patrollers++;
                foreach (Vector3 point in points)
                    c.Check(NavMesh.SamplePosition(point, out _, 0.3f, NavMesh.AllAreas), $"{name}: punto de patrulla fuera del NavMesh");
                c.Check(NavMesh.SamplePosition(ai.transform.position, out _, 0.5f, NavMesh.AllAreas), $"{name}: está fuera del NavMesh");
            }
            c.Check(patrollers >= 3 && patrollers < ais.Length, $"Unos enemigos patrullan y otros esperan quietos ({patrollers}/{ais.Length})");
        }

        private static void VerifyAILogic(Checker c, LevelLayout layout)
        {
            // --- Cono de visión ---
            c.Check(VisionLogic.InCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 8f), 14f, 55f, 1.5f), "Ve lo que tiene delante");
            c.Check(!VisionLogic.InCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -8f), 14f, 55f, 1.5f), "No ve a su espalda");
            c.Check(!VisionLogic.InCone(Vector3.zero, Vector3.forward, new Vector3(8f, 0f, 1f), 14f, 55f, 1.5f), "No ve fuera del cono");
            c.Check(!VisionLogic.InCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 20f), 14f, 55f, 1.5f), "No ve más allá de su alcance");
            c.Check(VisionLogic.InCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -1f), 14f, 55f, 1.5f), "Nota a alguien pegado a su espalda");

            // --- Rutas de patrulla del mapa ---
            foreach (var (map, col, row, ch) in layout.AllCells())
            {
                if (!LevelMap.IsEnemy(ch)) continue;
                Vector3[] route = map.PatrolRoute(col, row, SceneFactory.PatrolCells);
                c.Check(route.Length == 0 || route.Length == 2, $"Ruta de patrulla válida en ({col},{row}) del piso {map.FloorIndex}");
                foreach (Vector3 point in route)
                    c.Check(Mathf.Approximately(point.y, map.BaseY), $"La patrulla de ({col},{row}) va por su propio piso");
            }

            BrainSettings s = BrainSettings.Default;
            Perception See(Vector3 self, Vector3 player, WeaponKind kind, float range) => new Perception
            {
                SelfPosition = self, CanSeePlayer = true, PlayerPosition = player, Weapon = kind, AttackRange = range,
            };
            Perception Blind(Vector3 self, WeaponKind kind, float range) => new Perception
            {
                SelfPosition = self, CanSeePlayer = false, Weapon = kind, AttackRange = range,
            };

            // --- Quieto → detecta → persigue → ataca (cuerpo a cuerpo) ---
            // La detección gradual se verifica aparte (VerifyStealthLogic); aquí se parte de un enemigo que ya te detectó.
            var melee = new EnemyBrain(Vector3.zero, null, s);
            melee.Tick(0.1f, Blind(Vector3.zero, WeaponKind.Melee, 1.5f));
            c.Check(melee.State == AIState.Idle && !melee.HasMoveTarget, "Sin ver al jugador se queda quieto");
            melee.Alert(new Vector3(5f, 0f, 0f));
            melee.Tick(0.1f, See(Vector3.zero, new Vector3(5f, 0f, 0f), WeaponKind.Melee, 1.5f));
            c.Check(melee.State == AIState.Chase && melee.HasMoveTarget && melee.MoveTarget == new Vector3(5f, 0f, 0f),
                "Tras detectar al jugador lo persigue");
            c.Check(!melee.WantsAttack, "Lejos no ataca");
            melee.Tick(0.1f, See(new Vector3(4f, 0f, 0f), new Vector3(5f, 0f, 0f), WeaponKind.Melee, 1.5f));
            c.Check(melee.State == AIState.Attack && !melee.HasMoveTarget, "Al alcance se para a atacar");
            c.Check(!melee.WantsAttack, "Tarda un momento en reaccionar");
            melee.Tick(0.2f, See(new Vector3(4f, 0f, 0f), new Vector3(5f, 0f, 0f), WeaponKind.Melee, 1.5f));
            c.Check(melee.WantsAttack, "Tras reaccionar, ataca");
            c.Check(melee.HasLookTarget && melee.LookTarget == new Vector3(5f, 0f, 0f), "Mira al jugador mientras ataca");

            // --- Armas de fuego: dispara desde lejos ---
            var ranged = new EnemyBrain(Vector3.zero, null, s);
            ranged.Alert(new Vector3(8f, 0f, 0f));
            ranged.Tick(0.1f, See(Vector3.zero, new Vector3(8f, 0f, 0f), WeaponKind.Ranged, 11f));
            c.Check(ranged.State == AIState.Attack && !ranged.HasMoveTarget, "Con arma de fuego dispara desde la distancia sin acercarse");
            ranged.Tick(0.3f, See(Vector3.zero, new Vector3(8f, 0f, 0f), WeaponKind.Ranged, 11f));
            c.Check(ranged.WantsAttack, "Con arma de fuego dispara tras reaccionar");
            ranged.Tick(0.1f, See(Vector3.zero, new Vector3(15f, 0f, 0f), WeaponKind.Ranged, 11f));
            c.Check(ranged.State == AIState.Chase && !ranged.WantsAttack, "Si el jugador se aleja demasiado, se acerca");

            // --- Pierde de vista al jugador: busca y vuelve ---
            var search = new EnemyBrain(Vector3.zero, null, s);
            search.Alert(new Vector3(6f, 0f, 0f));
            search.Tick(0.1f, See(Vector3.zero, new Vector3(6f, 0f, 0f), WeaponKind.Melee, 1.5f));
            search.Tick(2f, Blind(new Vector3(2f, 0f, 0f), WeaponKind.Melee, 1.5f));
            c.Check(search.State == AIState.Chase && search.MoveTarget == new Vector3(6f, 0f, 0f),
                "Recién perdido de vista, va al último punto conocido");
            search.Tick(1f, Blind(new Vector3(3f, 0f, 0f), WeaponKind.Melee, 1.5f));
            c.Check(search.State == AIState.Investigate, "Tras un rato sin verlo, pasa a buscar");
            search.Tick(1f, Blind(new Vector3(6f, 0f, 0f), WeaponKind.Melee, 1.5f));
            c.Check(search.State == AIState.Investigate && !search.HasMoveTarget, "Busca en el último punto conocido");
            search.Tick(3f, Blind(new Vector3(6f, 0f, 0f), WeaponKind.Melee, 1.5f));
            search.Tick(0.1f, Blind(new Vector3(6f, 0f, 0f), WeaponKind.Melee, 1.5f));
            c.Check(search.State == AIState.Idle && search.HasMoveTarget && search.MoveTarget == Vector3.zero,
                "Si no lo encuentra, vuelve a su puesto");

            // --- Ruido ---
            var listener = new EnemyBrain(Vector3.zero, null, s);
            listener.HearNoise(new Vector3(10f, 0f, 0f));
            listener.Tick(0.1f, Blind(Vector3.zero, WeaponKind.Melee, 1.5f));
            c.Check(listener.State == AIState.Investigate && listener.MoveTarget == new Vector3(10f, 0f, 0f),
                "Un disparo cercano lo hace ir a investigar");
            melee.HearNoise(new Vector3(-20f, 0f, 0f));
            c.Check(melee.State == AIState.Attack, "Si ya está atacando, el ruido no lo distrae");

            // --- Patrulla ---
            var patrol = new EnemyBrain(Vector3.zero, new[] { Vector3.zero, new Vector3(4f, 0f, 0f) }, s);
            c.Check(patrol.State == AIState.Patrol, "Con ruta, empieza patrullando");
            patrol.Tick(0.5f, Blind(Vector3.zero, WeaponKind.Melee, 1.5f));
            patrol.Tick(0.6f, Blind(Vector3.zero, WeaponKind.Melee, 1.5f));
            c.Check(patrol.PatrolIndex == 1, "Tras esperar en un punto, va al siguiente");
            patrol.Tick(0.1f, Blind(Vector3.zero, WeaponKind.Melee, 1.5f));
            c.Check(patrol.HasMoveTarget && patrol.MoveTarget == new Vector3(4f, 0f, 0f), "Camina hacia el siguiente punto");

            // --- Desarmado: busca un arma ---
            var unarmed = new EnemyBrain(Vector3.zero, null, s);
            Perception withWeapon = See(Vector3.zero, new Vector3(6f, 0f, 0f), WeaponKind.None, WeaponDefinition.UnarmedRange);
            withWeapon.HasNearbyWeapon = true;
            withWeapon.NearbyWeaponPosition = new Vector3(0f, 0f, 2f);
            unarmed.Alert(new Vector3(6f, 0f, 0f));
            unarmed.Tick(0.1f, withWeapon);
            c.Check(unarmed.State == AIState.Retrieve && unarmed.MoveTarget == new Vector3(0f, 0f, 2f),
                "Desarmado y alerta, va a recoger un arma");
            unarmed.Tick(0.1f, See(Vector3.zero, new Vector3(6f, 0f, 0f), WeaponKind.Melee, 1.5f));
            c.Check(unarmed.State == AIState.Chase, "Con el arma recogida, vuelve a perseguir");

            var cornered = new EnemyBrain(Vector3.zero, null, s);
            Perception close = See(Vector3.zero, new Vector3(1f, 0f, 0f), WeaponKind.None, WeaponDefinition.UnarmedRange);
            close.HasNearbyWeapon = true;
            close.NearbyWeaponPosition = new Vector3(0f, 0f, 5f);
            cornered.Tick(0.5f, close);
            c.Check(cornered.State == AIState.Attack && cornered.WantsAttack,
                "Con el jugador pegado lo detecta al instante y, desarmado, pega con los puños");

            // --- Jugador muerto ---
            Perception dead = See(new Vector3(4f, 0f, 0f), new Vector3(5f, 0f, 0f), WeaponKind.Melee, 1.5f);
            dead.CanSeePlayer = false;
            dead.PlayerDead = true;
            melee.Tick(0.1f, dead);
            c.Check(melee.State == AIState.Idle && !melee.WantsAttack, "Con el jugador muerto deja de atacar");
        }

        /// <summary>Visión con paredes, golpe cuerpo a cuerpo, disparo y ruido con los prefabs reales.</summary>
        private static void VerifyAISimulation(Checker c)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            if (playerPrefab == null || enemyPrefab == null) return;

            var created = new List<GameObject>();
            Vector3 basePos = new Vector3(600f, 0f, -600f);
            T Spawn<T>(GameObject prefab, Vector3 offset, float yaw) where T : Component
            {
                GameObject go = Object.Instantiate(prefab, basePos + offset, Quaternion.Euler(0f, yaw, 0f));
                created.Add(go);
                return go.GetComponent<T>();
            }
            void Arm(Enemy enemy, WeaponType type)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.WeaponPrefabPath(type));
                GameObject go = Object.Instantiate(prefab);
                created.Add(go);
                enemy.Equip(go.GetComponent<WeaponItem>());
            }

            try
            {
                // --- Visión y paredes ---
                var player = Spawn<PlayerLife>(playerPrefab, Vector3.zero, 0f);
                var watcher = Spawn<EnemyAI>(enemyPrefab, new Vector3(0f, 0f, 6f), 180f);
                Physics.SyncTransforms();
                c.Check(watcher.CanSee(player), "Simulación: el enemigo ve al jugador que tiene delante");

                var blocker = Spawn<Enemy>(enemyPrefab, new Vector3(0f, 0f, 3f), 0f);
                Physics.SyncTransforms();
                c.Check(watcher.CanSee(player), "Otros enemigos no tapan la vista");
                Object.DestroyImmediate(blocker.gameObject);

                watcher.transform.rotation = Quaternion.identity;
                Physics.SyncTransforms();
                c.Check(!watcher.CanSee(player), "Simulación: de espaldas no ve al jugador");
                watcher.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.Add(wall);
                wall.transform.position = basePos + new Vector3(0f, 1.25f, 3f);
                wall.transform.localScale = new Vector3(4f, 2.5f, 0.5f);
                Physics.SyncTransforms();
                c.Check(!watcher.CanSee(player), "Simulación: el enemigo no ve al jugador a través de una pared");
                Object.DestroyImmediate(wall);
                Physics.SyncTransforms();

                // --- Ruido ---
                c.Check(watcher.HearNoise(basePos, 15f) && watcher.Brain.State == AIState.Investigate,
                    "Simulación: un disparo dentro del radio alerta al enemigo");
                var far = Spawn<EnemyAI>(enemyPrefab, new Vector3(0f, 0f, 30f), 180f);
                c.Check(!far.HearNoise(basePos, 15f) && far.Brain.State == AIState.Idle, "Un enemigo lejano no oye el disparo");
                far.Enemy.ReceiveKnockdown();
                c.Check(!far.HearNoise(far.transform.position, 15f), "Un enemigo derribado no oye nada");

                // --- Golpe cuerpo a cuerpo ---
                var knifer = Spawn<EnemyAI>(enemyPrefab, new Vector3(3f, 0f, 0f), -90f);
                Arm(knifer.Enemy, WeaponType.Knife);
                BuilderUtil.SetRef(knifer, "player", player);
                Physics.SyncTransforms();
                c.Check(knifer.CurrentWeaponKind == WeaponKind.Melee, "Un enemigo con cuchillo es de cuerpo a cuerpo");
                c.Check(!knifer.PerformMeleeHit() && !player.IsDead, "Un golpe fuera de alcance no mata");
                knifer.transform.position = basePos + new Vector3(1.2f, 0f, 0f);
                Physics.SyncTransforms();
                c.Check(knifer.PerformMeleeHit() && player.IsDead, "Simulación: el golpe del enemigo mata al jugador de un golpe");

                // --- Disparo a distancia ---
                var target = Spawn<PlayerLife>(playerPrefab, new Vector3(20f, 0f, 0f), 0f);
                // A 4 m la dispersión del subfusil (±3°) siempre impacta; más lejos puede fallar, como en el juego.
                var gunner = Spawn<EnemyAI>(enemyPrefab, new Vector3(20f, 0f, 4f), 180f);
                Arm(gunner.Enemy, WeaponType.Smg);
                Physics.SyncTransforms();
                c.Check(gunner.CurrentWeaponKind == WeaponKind.Ranged && gunner.AttackRange > 5f,
                    "Un enemigo con subfusil ataca desde lejos");
                Random.InitState(42);
                var spawned = new List<Projectile>();
                c.Check(gunner.FireAt(target, spawned) == 1, "El enemigo dispara al jugador");
                c.Check(gunner.Enemy.Weapon.State.Ammo == WeaponDefinition.Smg.MaxAmmo, "Los enemigos no gastan munición");
                StepProjectiles(spawned);
                c.Check(target.IsDead, "Simulación: el disparo del enemigo mata al jugador");
                c.Check(!gunner.CanSee(target), "No sigue viendo a un jugador muerto");
            }
            finally
            {
                DestroyAllProjectiles();
                foreach (GameObject go in created)
                    if (go != null) Object.DestroyImmediate(go);
            }
        }
    }
}
