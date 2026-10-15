using System.Linq;
using Overdrive.CameraRig;
using Overdrive.Core;
using Overdrive.Level;
using Overdrive.Player;
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
    internal static class Verifier
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
            return c;
        }

        // ---------- Referencias ----------

        private static void VerifyAssets(Checker c)
        {
            foreach (var (name, _, _) in AssetFactory.MaterialDefs)
                c.NotNull(AssetFactory.Mat(name), $"Falta el material M_{name}");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            c.NotNull(prefab, "Falta el prefab Player");
            if (prefab == null) return;
            VerifyPlayerComponents(c, prefab, "Prefab Player");
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
        }

        private static void VerifyScene(Checker c, LevelMap map)
        {
            Scene scene = SceneManager.GetActiveScene();
            c.Check(scene.path == SceneFactory.ScenePath, $"La escena activa debería ser {SceneFactory.ScenePath} (es '{scene.path}')");

            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            c.Check(buildScenes.Length > 0 && buildScenes[0].path == SceneFactory.ScenePath && buildScenes[0].enabled,
                "Level01 debe ser la primera escena habilitada en Build Settings");

            foreach (string root in new[] { SceneFactory.LevelRoot, SceneFactory.PlayerName, SceneFactory.CameraRigName,
                         SceneFactory.SystemsName, SceneFactory.LightName })
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
    }
}
