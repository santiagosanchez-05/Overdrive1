using System.Linq;
using Overdrive.CameraRig;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Verificación automática: referencias de assets y escena, y simulación de la lógica
    /// llamando a los métodos en modo editor, sin entrar en Play.
    /// </summary>
    internal static partial class Verifier
    {
        public static Checker Run(LevelMap map)
        {
            var c = new Checker();
            VerifyAssets(c);
            VerifyScene(c, map);
            VerifyMap(c, map);
            VerifyMovementLogic(c);
            VerifyAimLogic(c);
            VerifyCameraLogic(c);
            VerifyLifeLogic(c);
            VerifyCombatStateLogic(c);
            VerifyTargetingLogic(c);
            VerifyFinisherSequenceLogic(c);
            VerifyCombatSimulation(c);
            VerifyWeaponLogic(c);
            VerifyWeaponSimulation(c);
            return c;
        }

        // ---------- Referencias ----------

        private static void VerifyAssets(Checker c)
        {
            foreach (var (name, _, _) in AssetFactory.MaterialDefs)
                c.NotNull(AssetFactory.Mat(name), $"Falta el material M_{name}");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            c.NotNull(prefab, "Falta el prefab Player");
            if (prefab != null) VerifyPlayerComponents(c, prefab, "Prefab Player");

            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            c.NotNull(enemyPrefab, "Falta el prefab Enemy");
            if (enemyPrefab != null) VerifyEnemyComponents(c, enemyPrefab, "Prefab Enemy");

            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BulletPrefabPath);
            c.Check(bullet != null && bullet.GetComponent<Projectile>() != null, "Falta el prefab Bullet con Projectile");
            if (bullet != null)
                c.Check(bullet.GetComponentsInChildren<Collider>(true).Length == 0, "La bala no debe tener colliders");

            foreach (WeaponType type in AssetFactory.AllWeaponTypes)
            {
                var weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.WeaponPrefabPath(type));
                WeaponItem item = weaponPrefab != null ? weaponPrefab.GetComponent<WeaponItem>() : null;
                c.NotNull(item, $"Falta el prefab Weapon_{type} con WeaponItem");
                if (item == null) continue;
                c.Check(item.Type == type, $"Weapon_{type}: tipo incorrecto");
                c.Check(item.CurrentMode == WeaponItem.Mode.Ground, $"Weapon_{type}: debe empezar en el suelo");
                bool isMelee = WeaponDefinition.Get(type).IsMelee;
                c.Check(isMelee ? item.ProjectilePrefab == null : item.ProjectilePrefab != null,
                    $"Weapon_{type}: {(isMelee ? "no debe tener" : "debe tener")} proyectil");
                c.Check(weaponPrefab.GetComponentsInChildren<Collider>(true).Length == 0, $"Weapon_{type}: no debe tener colliders");
                c.Check(weaponPrefab.GetComponentsInChildren<Renderer>(true).Length >= 2, $"Weapon_{type}: le falta el modelo");
            }
        }

        private static void VerifyEnemyComponents(Checker c, GameObject go, string label)
        {
            var enemy = go.GetComponent<Enemy>();
            c.NotNull(enemy, $"{label}: falta Enemy");
            if (enemy == null) return;
            c.NotNull(enemy.Body, $"{label}: Enemy.body sin asignar");
            c.Check(enemy.BodyCollider != null && enemy.BodyCollider.gameObject == go, $"{label}: Enemy.bodyCollider debe ser el collider raíz");
            c.NotNull(enemy.BodyRenderer, $"{label}: Enemy.bodyRenderer sin asignar");
            c.Check(enemy.DeadMaterial == AssetFactory.Mat(AssetFactory.MatEnemyDead), $"{label}: Enemy.deadMaterial debe ser M_EnemyDead");
            c.Check(enemy.KnockdownDuration > 2f && enemy.KnockdownDuration < 4f, $"{label}: el derribo debe durar unos 3 s");
            c.Check(enemy.Hand != null && enemy.Hand == go.transform.Find(AssetFactory.HandName), $"{label}: Enemy.hand debe ser el hijo Hand");
        }

        private static void VerifyPlayerComponents(Checker c, GameObject player, string label)
        {
            c.Check(player.CompareTag("Player"), $"{label}: no tiene la etiqueta Player");
            var controller = player.GetComponent<PlayerController>();
            var aim = player.GetComponent<PlayerAim>();
            var life = player.GetComponent<PlayerLife>();
            c.NotNull(player.GetComponent<CharacterController>(), $"{label}: falta CharacterController");
            c.NotNull(controller, $"{label}: falta PlayerController");
            c.NotNull(aim, $"{label}: falta PlayerAim");
            c.NotNull(life, $"{label}: falta PlayerLife");
            if (controller != null)
            {
                c.NotNull(controller.Controller, $"{label}: PlayerController.controller sin asignar");
                c.Check(controller.Life == life, $"{label}: PlayerController.life sin asignar");
            }
            if (aim != null) c.Check(aim.Life == life, $"{label}: PlayerAim.life sin asignar");
            if (life != null) c.NotNull(life.Body, $"{label}: PlayerLife.body sin asignar");
            c.NotNull(player.transform.Find("Body/Nose"), $"{label}: falta Body/Nose");

            var melee = player.GetComponent<PlayerMelee>();
            var finisher = player.GetComponent<PlayerFinisher>();
            c.NotNull(melee, $"{label}: falta PlayerMelee");
            c.NotNull(finisher, $"{label}: falta PlayerFinisher");
            if (melee != null)
            {
                c.Check(melee.Fist == player.transform.Find("Body/Nose"), $"{label}: PlayerMelee.fist debe ser Body/Nose");
            }

            var weapons = player.GetComponent<PlayerWeapons>();
            c.NotNull(weapons, $"{label}: falta PlayerWeapons");
            if (weapons != null)
            {
                c.Check(weapons.Hand != null && weapons.Hand == player.transform.Find(AssetFactory.HandName),
                    $"{label}: PlayerWeapons.hand debe ser el hijo Hand");
                c.Check(weapons.Life == life, $"{label}: PlayerWeapons.life sin asignar");
                c.Check(weapons.Finisher == finisher, $"{label}: PlayerWeapons.finisher sin asignar");
                c.Check(weapons.Melee == melee, $"{label}: PlayerWeapons.melee sin asignar");
            }
            if (finisher != null)
            {
                c.Check(finisher.Controller == controller, $"{label}: PlayerFinisher.controller sin asignar");
                c.Check(finisher.Life == life, $"{label}: PlayerFinisher.life sin asignar");
            }
        }

        private static void VerifyScene(Checker c, LevelMap map)
        {
            Scene scene = SceneManager.GetActiveScene();
            c.Check(scene.path == SceneFactory.ScenePath, $"La escena activa debería ser {SceneFactory.ScenePath} (es '{scene.path}')");

            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            c.Check(buildScenes.Length > 0 && buildScenes[0].path == SceneFactory.ScenePath && buildScenes[0].enabled,
                "Level01 debe ser la primera escena habilitada en Build Settings");

            foreach (string root in new[] { SceneFactory.LevelRoot, SceneFactory.PlayerName, SceneFactory.EnemiesName, SceneFactory.WeaponsName,
                         SceneFactory.CameraRigName, SceneFactory.SystemsName, SceneFactory.LightName })
            {
                int count = scene.GetRootGameObjects().Count(g => g.name == root);
                c.Check(count == 1, $"Debe haber exactamente un objeto raíz '{root}' (hay {count})");
            }

            GameObject level = BuilderUtil.FindRoot(scene, SceneFactory.LevelRoot);
            if (level != null)
            {
                Transform floor = level.transform.Find(SceneFactory.FloorName);
                c.Check(floor != null && floor.GetComponent<Collider>() != null, "El suelo debe existir y tener collider");
                Transform walls = level.transform.Find(SceneFactory.WallsName);
                int expected = map.WallBlocks().Count;
                c.Check(walls != null && walls.childCount == expected,
                    $"Debe haber {expected} bloques de pared (hay {(walls != null ? walls.childCount : 0)})");
                c.NotNull(level.transform.Find(SceneFactory.ExitName), "Falta la zona de salida");
            }

            GameObject player = BuilderUtil.FindRoot(scene, SceneFactory.PlayerName);
            if (player != null)
            {
                VerifyPlayerComponents(c, player, "Escena Player");
                c.Check(PrefabUtility.IsPartOfPrefabInstance(player), "El Player de la escena debe ser instancia del prefab");
                var (col, row) = map.FindAll(LevelMap.PlayerSpawn)[0];
                c.Near(player.transform.position, map.CellToWorld(col, row), "Player en el punto de inicio");
                var aim = player.GetComponent<PlayerAim>();
                if (aim != null) c.NotNull(aim.AimCamera, "PlayerAim.aimCamera sin asignar en la escena");
            }

            GameObject enemiesRoot = BuilderUtil.FindRoot(scene, SceneFactory.EnemiesName);
            if (enemiesRoot != null)
            {
                Enemy[] enemies = enemiesRoot.GetComponentsInChildren<Enemy>(true);
                int expected = map.Cells().Count(cell => LevelMap.IsEnemy(cell.c));
                c.Check(enemies.Length == expected, $"Debe haber {expected} enemigos en la escena (hay {enemies.Length})");
                c.Check(enemiesRoot.transform.childCount == enemies.Length, "Enemies solo debe contener enemigos");
                foreach (var (col, row, ch) in map.Cells())
                {
                    if (!LevelMap.IsEnemy(ch)) continue;
                    string name = SceneFactory.EnemyName(ch, col, row);
                    Transform t = enemiesRoot.transform.Find(name);
                    c.NotNull(t, $"Falta el enemigo {name}");
                    if (t == null) continue;
                    c.Check(PrefabUtility.IsPartOfPrefabInstance(t.gameObject), $"{name} debe ser instancia del prefab Enemy");
                    c.Near(t.position, map.CellToWorld(col, row), $"{name} en su celda del mapa");
                    VerifyEnemyComponents(c, t.gameObject, name);

                    Enemy enemy = t.GetComponent<Enemy>();
                    WeaponItem weapon = enemy != null ? enemy.Weapon : null;
                    c.NotNull(weapon, $"{name}: debe llevar un arma");
                    if (weapon == null) continue;
                    c.Check(weapon.Type == LevelMap.WeaponFor(ch), $"{name}: debe llevar {LevelMap.WeaponFor(ch)} (lleva {weapon.Type})");
                    c.Check(weapon.CurrentMode == WeaponItem.Mode.Held, $"{name}: su arma debe estar en modo Held");
                    c.Check(weapon.transform.parent == enemy.Hand, $"{name}: su arma debe estar en la mano");
                    c.Check(enemy.Hand.GetComponentsInChildren<WeaponItem>(true).Length == 1, $"{name}: solo debe tener un arma");
                }
            }

            GameObject weaponsRoot = BuilderUtil.FindRoot(scene, SceneFactory.WeaponsName);
            if (weaponsRoot != null)
            {
                int expected = map.Cells().Count(cell => LevelMap.IsFloorWeapon(cell.c));
                c.Check(weaponsRoot.transform.childCount == expected,
                    $"Debe haber {expected} armas en el suelo (hay {weaponsRoot.transform.childCount})");
                foreach (var (col, row, ch) in map.Cells())
                {
                    if (!LevelMap.IsFloorWeapon(ch)) continue;
                    string name = SceneFactory.FloorWeaponName(ch, col, row);
                    WeaponItem item = weaponsRoot.transform.Find(name)?.GetComponent<WeaponItem>();
                    c.NotNull(item, $"Falta el arma del suelo {name}");
                    if (item == null) continue;
                    c.Check(item.Type == LevelMap.WeaponFor(ch) && item.IsOnGround, $"{name}: tipo o modo incorrecto");
                }
            }

            GameObject rig = BuilderUtil.FindRoot(scene, SceneFactory.CameraRigName);
            var topDown = rig != null ? rig.GetComponent<TopDownCamera>() : null;
            c.NotNull(topDown, "CameraRig: falta TopDownCamera");
            if (topDown != null)
            {
                c.Check(player != null && topDown.Target == player.transform, "TopDownCamera.target debe ser el Player");
                c.Check(player != null && topDown.Aim == player.GetComponent<PlayerAim>(), "TopDownCamera.aim debe ser el PlayerAim");
                c.NotNull(topDown.Cam, "TopDownCamera.cam sin asignar");
                c.NotNull(rig.transform.Find(SceneFactory.ShakeName)?.GetComponent<CameraShake>(), "CameraRig/Shake: falta CameraShake");
                if (topDown.Cam != null)
                {
                    c.Check(topDown.Cam.CompareTag("MainCamera"), "La cámara debe tener la etiqueta MainCamera");
                    c.Check(topDown.Cam.transform.parent == rig.transform.Find(SceneFactory.ShakeName),
                        "La cámara debe ser hija de CameraRig/Shake");
                    // La cámara debe mirar al centro del rig (el jugador al empezar).
                    var ray = new Ray(topDown.Cam.transform.position, topDown.Cam.transform.forward);
                    bool hit = AimMath.TryGetPointOnPlane(ray, rig.transform.position.y, out Vector3 point);
                    c.Check(hit, "La cámara debe mirar hacia el suelo");
                    c.Near(point, rig.transform.position, "La cámara apunta al jugador", 0.01f);
                    float pitch = topDown.Cam.transform.eulerAngles.x;
                    c.Check(pitch >= 60f && pitch < 90f, $"La cámara debe estar casi vertical y algo inclinada (pitch {pitch})");
                }
            }

            GameObject systems = BuilderUtil.FindRoot(scene, SceneFactory.SystemsName);
            c.NotNull(systems != null ? systems.GetComponent<LevelRestart>() : null, "Systems: falta LevelRestart");
            c.Check(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).Count() == 1,
                "Debe haber una sola cámara en la escena");
        }

        // ---------- Mapa ----------

        private static void VerifyMap(Checker c, LevelMap map)
        {
            c.Check(map.FindAll(LevelMap.PlayerSpawn).Count == 1, "El mapa debe tener exactamente un 'P'");
            c.Check(map.FindAll(LevelMap.Exit).Count > 0, "El mapa debe tener zona de salida 'X'");
            int enemies = map.Cells().Count(cell => LevelMap.IsEnemy(cell.c));
            c.Check(enemies == 10, $"El mapa debe tener 10 enemigos (tiene {enemies})");

            bool closed = true;
            for (int col = 0; col < map.Width; col++)
                closed &= map.IsWall(col, 0) && map.IsWall(col, map.Height - 1);
            for (int row = 0; row < map.Height; row++)
                closed &= map.IsWall(0, row) && map.IsWall(map.Width - 1, row);
            c.Check(closed, "El borde del mapa debe ser pared");

            var (pc, pr) = map.FindAll(LevelMap.PlayerSpawn)[0];
            var reachable = map.FloodFill(pc, pr);
            int walkable = map.Cells().Count(cell => cell.c != LevelMap.Wall);
            c.Check(reachable.Count == walkable,
                $"Todas las celdas transitables deben ser alcanzables desde P ({reachable.Count}/{walkable})");

            foreach (LevelMap.Block block in map.WallBlocks())
                for (int col = block.Col; col < block.Col + block.Width; col++)
                    for (int row = block.Row; row < block.Row + block.Height; row++)
                        if (!map.IsWall(col, row)) c.Check(false, $"El bloque {block.Name} cubre una celda que no es pared");
            int wallCells = map.Cells().Count(cell => cell.c == LevelMap.Wall);
            int blockCells = map.WallBlocks().Sum(b => b.Width * b.Height);
            c.Check(wallCells == blockCells, $"Los bloques deben cubrir cada pared una sola vez ({blockCells}/{wallCells})");
        }

        // ---------- Simulación de lógica ----------

        private static void VerifyMovementLogic(Checker c)
        {
            c.Near(PlayerMovementLogic.WorldVelocity(new Vector2(0f, 1f), 7f), new Vector3(0f, 0f, 7f), "W mueve hacia +Z del mundo");
            c.Near(PlayerMovementLogic.WorldVelocity(new Vector2(1f, 0f), 7f), new Vector3(7f, 0f, 0f), "D mueve hacia +X del mundo");
            c.Near(PlayerMovementLogic.WorldVelocity(new Vector2(1f, 1f), 7f).magnitude, 7f, "En diagonal no se va más rápido");
            c.Near(PlayerMovementLogic.WorldVelocity(Vector2.zero, 7f), Vector3.zero, "Sin teclas no hay movimiento");
        }

        private static void VerifyAimLogic(Checker c)
        {
            var target = new Vector3(3f, 0f, 4f);
            var from = new Vector3(0f, 10f, -2f);
            bool hit = AimMath.TryGetPointOnPlane(new Ray(from, target - from), 0f, out Vector3 point);
            c.Check(hit, "El rayo del mouse debe tocar el plano del suelo");
            c.Near(point, target, "Punto de apuntado en el suelo");
            c.Check(!AimMath.TryGetPointOnPlane(new Ray(from, Vector3.up), 0f, out _), "Un rayo hacia arriba no toca el suelo");

            c.Check(AimMath.TryGetFlatLookRotation(Vector3.zero, new Vector3(5f, 3f, 0f), out Quaternion rotation),
                "Debe poder girar hacia un punto");
            c.Near(rotation * Vector3.forward, Vector3.right, "El giro ignora la altura y mira al punto");
            c.Check(!AimMath.TryGetFlatLookRotation(Vector3.zero, new Vector3(0f, 2f, 0f), out _),
                "Si el mouse está encima del jugador, no gira");
        }

        private static void VerifyCameraLogic(Checker c)
        {
            CameraLookAhead.Settings s = CameraLookAhead.Settings.Default;
            Vector3 player = new Vector3(2f, 0f, 2f);
            Vector3 near = player + new Vector3(4f, 0f, 0f);
            Vector3 far = player + new Vector3(100f, 5f, 0f);

            Vector3 normalOffset = CameraLookAhead.ComputeOffset(player, near, false, s);
            Vector3 shiftOffset = CameraLookAhead.ComputeOffset(player, near, true, s);
            c.Check(normalOffset.x > 0f, "La cámara se adelanta hacia el mouse");
            c.Check(shiftOffset.magnitude > normalOffset.magnitude, "Con Shift se adelanta más");
            c.Near(CameraLookAhead.ComputeOffset(player, far, false, s).magnitude, s.normalMax, "Adelanto normal limitado");
            c.Near(CameraLookAhead.ComputeOffset(player, far, true, s).magnitude, s.farMax, "Adelanto con Shift limitado");
            c.Near(CameraLookAhead.ComputeOffset(player, far, true, s).y, 0f, "El adelanto es horizontal");

            var shake = new ShakeState();
            shake.Add(0.5f);
            c.Check(shake.Intensity > 0f, "El temblor aumenta al golpear");
            shake.Add(5f);
            c.Near(shake.Trauma, 1f, "El temblor está limitado a 1");
            shake.Tick(10f, 2.5f);
            c.Near(shake.Intensity, 0f, "El temblor se apaga con el tiempo");
        }

        private static void VerifyLifeLogic(Checker c)
        {
            var health = new Health();
            int deaths = 0;
            health.Died += () => deaths++;
            c.Check(!health.IsDead, "Al empezar está vivo");
            c.Check(health.Kill(), "Un golpe mata");
            c.Check(health.IsDead, "Después del golpe está muerto");
            c.Check(!health.Kill(), "No se puede matar dos veces");
            c.Check(deaths == 1, "El evento de muerte se dispara una vez");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            if (prefab == null) return;
            GameObject temp = Object.Instantiate(prefab);
            try
            {
                var life = temp.GetComponent<PlayerLife>();
                int playerDeaths = 0;
                life.Died += () => playerDeaths++;
                c.Check(life.Kill() && life.IsDead, "El jugador muere de un solo golpe");
                c.Check(playerDeaths == 1, "PlayerLife avisa de la muerte una vez");
                c.Check(Vector3.Angle(life.Body.up, Vector3.up) > 80f, "Al morir el cuerpo queda tumbado");
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        private static void VerifyCombatStateLogic(Checker c)
        {
            var s = new CombatState();
            c.Check(s.Posture == Posture.Standing, "Un enemigo empieza de pie");
            c.Check(s.KnockDown(3f), "El puñetazo derriba");
            c.Check(s.IsDown && !s.IsDead, "El puñetazo derriba pero no mata");
            c.Check(!s.KnockDown(3f), "No se puede derribar a alguien que ya está en el suelo");
            c.Check(!s.Tick(2.9f) && s.IsDown, "A los 2,9 s sigue en el suelo");
            c.Check(s.Tick(0.2f) && s.Posture == Posture.Standing, "Si no lo rematas, se levanta a los 3 s");
            c.Check(!s.Finish(), "No se puede rematar a alguien de pie");

            s.KnockDown(3f);
            c.Check(s.BeginFinish(), "Se puede empezar a rematar a un derribado");
            c.Check(!s.BeginFinish(), "No se puede empezar dos remates a la vez");
            c.Check(!s.Tick(10f) && s.IsDown, "Mientras lo rematan no se levanta");
            c.Check(s.Finish() && s.IsDead, "El remate mata a un derribado");
            c.Check(!s.KnockDown(3f) && !s.Tick(5f) && s.IsDead, "Un muerto no se derriba ni se levanta");

            var cancelled = new CombatState();
            cancelled.KnockDown(3f);
            cancelled.Tick(1f);
            cancelled.BeginFinish();
            cancelled.CancelFinish();
            c.Check(!cancelled.Tick(1.9f) && cancelled.Tick(0.2f), "Si se cancela el remate, se levanta al acabar su tiempo");

            var lethal = new CombatState();
            c.Check(lethal.Kill() && lethal.IsDead, "Un golpe con arma mata a un enemigo de pie");
        }

        private static void VerifyTargetingLogic(Checker c)
        {
            Vector3 origin = Vector3.zero, forward = Vector3.forward;
            c.Check(MeleeTargeting.InArc(origin, forward, new Vector3(0f, 0f, 1.2f), 1.6f, 60f), "Golpea a quien está delante");
            c.Check(!MeleeTargeting.InArc(origin, forward, new Vector3(0f, 0f, -1f), 1.6f, 60f), "No golpea a quien está detrás");
            c.Check(!MeleeTargeting.InArc(origin, forward, new Vector3(0f, 0f, 3f), 1.6f, 60f), "No golpea fuera de alcance");

            var targets = new[]
            {
                new MeleeTargeting.Target(new Vector3(0f, 0f, 1.4f), Posture.Standing),
                new MeleeTargeting.Target(new Vector3(0.2f, 0f, 0.9f), Posture.Standing),
                new MeleeTargeting.Target(new Vector3(0f, 0f, 0.5f), Posture.Down),
                new MeleeTargeting.Target(new Vector3(0f, 0f, 0.4f), Posture.Dead),
            };
            c.Check(MeleeTargeting.FindPunchTarget(origin, forward, targets, 1.6f, 60f) == 1,
                "El puñetazo elige al enemigo de pie más cercano");
            c.Check(MeleeTargeting.FindFinishTarget(origin, targets, 1.6f) == 2, "El remate elige al derribado");
            c.Check(MeleeTargeting.FindFinishTarget(new Vector3(10f, 0f, 0f), targets, 1.6f) == -1,
                "Sin un derribado cerca no hay remate");
        }

        private static void VerifyFinisherSequenceLogic(Checker c)
        {
            var seq = new FinisherSequence(0.6f, 3);
            seq.Start();
            FinisherSequence.Step a = seq.Tick(0.25f);
            c.Check(a.NewHits == 1 && !a.Completed, "El remate da el primer golpe pronto");
            FinisherSequence.Step b = seq.Tick(0.25f);
            c.Check(b.NewHits == 1 && !b.Completed, "El remate da el segundo golpe");
            FinisherSequence.Step d = seq.Tick(0.2f);
            c.Check(d.NewHits == 1 && d.Completed && !seq.Active, "El remate termina con el tercer golpe");
            c.Check(seq.Tick(1f).NewHits == 0, "Terminado, no da más golpes");
        }

        /// <summary>Simula puñetazo, derribo, levantarse y remate con los prefabs reales.</summary>
        private static void VerifyCombatSimulation(Checker c)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            if (playerPrefab == null || enemyPrefab == null) return;

            // Lejos del nivel para no interferir con la escena.
            Vector3 basePos = new Vector3(500f, 0f, 500f);
            GameObject player = Object.Instantiate(playerPrefab, basePos, Quaternion.identity);
            GameObject enemyA = Object.Instantiate(enemyPrefab, basePos + new Vector3(0f, 0f, 1.2f), Quaternion.identity);
            GameObject enemyB = Object.Instantiate(enemyPrefab, basePos + new Vector3(0f, 0f, -1.2f), Quaternion.identity);
            try
            {
                var melee = player.GetComponent<PlayerMelee>();
                var finisher = player.GetComponent<PlayerFinisher>();
                var controller = player.GetComponent<PlayerController>();
                var a = enemyA.GetComponent<Enemy>();
                var b = enemyB.GetComponent<Enemy>();
                var enemies = new[] { a, b };

                Enemy hit = melee.TryPunch(enemies);
                c.Check(hit == a, "El puñetazo golpea al enemigo de delante, no al de detrás");
                c.Check(a.Posture == Posture.Down && !a.IsDead, "Simulación: el puñetazo derriba y no mata");
                c.Check(!a.BodyCollider.enabled, "Derribado, el enemigo no bloquea el paso");
                c.Check(Vector3.Angle(a.Body.up, Vector3.up) > 80f, "Derribado, el cuerpo queda tumbado");
                c.Check(b.Posture == Posture.Standing, "El enemigo de detrás sigue de pie");
                c.Check(melee.TryPunch(enemies) == null, "Hay un enfriamiento entre puñetazos");

                a.Tick(a.KnockdownDuration + 0.1f);
                c.Check(a.Posture == Posture.Standing && a.BodyCollider.enabled, "Simulación: el enemigo se levanta si no lo rematas");
                c.Check(Vector3.Angle(a.Body.up, Vector3.up) < 1f, "Al levantarse, el cuerpo vuelve a estar de pie");

                melee.Tick(1f);
                c.Check(melee.TryPunch(enemies) == a, "Se le puede volver a derribar");
                c.Check(finisher.TryStart(enemies), "Espacio cerca de un derribado empieza el remate");
                c.Check(controller.MovementLocked, "Durante el remate el jugador no se puede mover");
                c.Check(MeleeTargeting.FlatDistance(player.transform.position, enemyA.transform.position) < 0.01f,
                    "El jugador se coloca encima del enemigo");
                a.Tick(10f);
                c.Check(a.Posture == Posture.Down, "Mientras lo rematan no se levanta");
                finisher.Advance(0.3f);
                c.Check(finisher.IsFinishing && !a.IsDead, "El remate dura un momento");
                finisher.Advance(0.4f);
                c.Check(a.IsDead && !finisher.IsFinishing, "Simulación: el remate mata al derribado");
                c.Check(!controller.MovementLocked, "Tras el remate el jugador se puede mover");
                c.Check(a.BodyRenderer.sharedMaterial == a.DeadMaterial, "El muerto cambia de material");

                // Remate interrumpido: el jugador muere a mitad.
                player.transform.SetPositionAndRotation(basePos, Quaternion.Euler(0f, 180f, 0f));
                melee.Tick(1f);
                c.Check(melee.TryPunch(enemies) == b, "Golpea al enemigo hacia el que mira");
                c.Check(finisher.TryStart(enemies), "Empieza el remate del segundo enemigo");
                player.GetComponent<PlayerLife>().Kill();
                finisher.Cancel();
                c.Check(!b.IsDead && !b.State.IsBeingFinished, "Si el jugador muere, el remate se cancela");
                b.Tick(b.KnockdownDuration + 0.1f);
                c.Check(b.Posture == Posture.Standing, "El enemigo no rematado se levanta");

                c.Check(!finisher.TryStart(enemies), "No hay remate sin un derribado cerca");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(enemyA);
                Object.DestroyImmediate(enemyB);
            }
        }
    }
}
