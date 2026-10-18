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
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación del nivel completo: salida, victoria, HUD y sangre.</summary>
    internal static partial class Verifier
    {
        private static void VerifyLevelScene(Checker c, LevelLayout layout)
        {
            LevelMap map = layout.Ground;
            Scene scene = SceneManager.GetActiveScene();

            var bloodPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BloodPrefabPath);
            c.NotNull(bloodPrefab, "Falta el prefab Blood");
            if (bloodPrefab != null)
            {
                Renderer quad = bloodPrefab.GetComponentInChildren<Renderer>(true);
                c.Check(quad != null && quad.sharedMaterial == AssetFactory.Mat(AssetFactory.MatBlood), "Blood: debe ser un quad con M_Blood");
                c.Check(bloodPrefab.GetComponentsInChildren<Collider>(true).Length == 0, "Blood: no debe tener colliders");
            }

            // --- Salida ---
            GameObject level = BuilderUtil.FindRoot(scene, SceneFactory.LevelRoot);
            ExitZone exit = level != null ? level.transform.Find(SceneFactory.ExitName)?.GetComponent<ExitZone>() : null;
            c.NotNull(exit, "Exit: falta ExitZone");
            if (exit != null)
            {
                List<(int col, int row)> cells = map.FindAll(LevelMap.Exit);
                int width = cells.Max(x => x.col) - cells.Min(x => x.col) + 1;
                int height = cells.Max(x => x.row) - cells.Min(x => x.row) + 1;
                c.Near(exit.Size.x, width * map.CellSize, "La salida cubre el ancho de las celdas X");
                c.Near(exit.Size.y, height * map.CellSize, "La salida cubre el fondo de las celdas X");
                foreach (var (col, row) in cells)
                    c.Check(exit.Contains(map.CellToWorld(col, row)), $"La celda X ({col},{row}) está dentro de la salida");
                c.Check(exit.ZoneRenderer != null && exit.ZoneRenderer.sharedMaterial == exit.LockedMaterial,
                    "La salida empieza apagada");
                c.Check(exit.LockedMaterial == AssetFactory.Mat(AssetFactory.MatExitLocked) &&
                        exit.OpenMaterial == AssetFactory.Mat(AssetFactory.MatExit), "ExitZone: materiales sin asignar");
                var (pc, pr) = map.FindAll(LevelMap.PlayerSpawn)[0];
                c.Check(!exit.Contains(map.CellToWorld(pc, pr)), "El jugador no empieza dentro de la salida");
            }

            // --- Sistemas ---
            GameObject systems = BuilderUtil.FindRoot(scene, SceneFactory.SystemsName);
            if (systems == null) return;
            GameObject player = BuilderUtil.FindRoot(scene, SceneFactory.PlayerName);
            Enemy[] sceneEnemies = BuilderUtil.FindRoot(scene, SceneFactory.EnemiesName)?.GetComponentsInChildren<Enemy>(true) ?? new Enemy[0];

            var manager = systems.GetComponent<LevelManager>();
            c.NotNull(manager, "Systems: falta LevelManager");
            if (manager != null)
            {
                c.Check(player != null && manager.Player == player.GetComponent<PlayerLife>(), "LevelManager.player sin asignar");
                c.Check(manager.Exit != null && manager.Exit == exit, "LevelManager.exit sin asignar");
                c.Check(manager.Enemies.Length == sceneEnemies.Length && manager.Enemies.Length == 10 &&
                        manager.Enemies.All(e => e != null) && manager.Enemies.Distinct().Count() == 10,
                    $"LevelManager debe conocer a los 10 enemigos (conoce {manager.Enemies.Length})");
                c.Check(manager.NextScene == "Level02", "LevelManager debe tener nextScene = 'Level02'");
            }

            var hud = systems.GetComponent<GameHUD>();
            c.NotNull(hud, "Systems: falta GameHUD");
            if (hud != null)
            {
                c.Check(player != null && hud.Weapons == player.GetComponent<PlayerWeapons>(), "GameHUD.weapons sin asignar");
                c.Check(hud.Level == manager, "GameHUD.level sin asignar");
            }

            var spawner = systems.transform.Find(SceneFactory.BloodName)?.GetComponent<BloodSpawner>();
            c.NotNull(spawner, "Systems/Blood: falta BloodSpawner");
            if (spawner != null) c.Check(spawner.BloodPrefab == bloodPrefab, "BloodSpawner.bloodPrefab sin asignar");

            // --- Variedad de armas entre los enemigos ---
            var types = new HashSet<WeaponType>(layout.AllCells().Where(x => LevelMap.IsEnemy(x.c)).Select(x => LevelMap.WeaponFor(x.c)));
            c.Check(types.Count == 4, "Hay enemigos con bate, cuchillo, escopeta y subfusil");
        }

        private static void VerifyLevelLogic(Checker c)
        {
            var progress = new LevelProgress();
            c.Check(progress.Evaluate(10, 0, false, false) == LevelState.Playing && progress.Remaining == 10, "Al empezar quedan 10 enemigos");
            c.Check(progress.Evaluate(10, 9, false, true) == LevelState.Playing, "Con enemigos vivos, la salida no sirve");
            c.Check(progress.Evaluate(10, 10, false, false) == LevelState.ExitOpen && progress.Remaining == 0,
                "Sin enemigos, se abre la salida");
            c.Check(progress.Evaluate(10, 10, false, true) == LevelState.Complete, "Sin enemigos y en la salida: nivel completado");
            c.Check(progress.Evaluate(10, 10, true, false) == LevelState.Complete, "Una vez completado no se puede perder");

            var lost = new LevelProgress();
            c.Check(lost.Evaluate(10, 3, true, false) == LevelState.Failed, "Si el jugador muere, el nivel está perdido");
            c.Check(lost.Evaluate(10, 10, false, true) == LevelState.Failed, "Perdido no se puede ganar sin reiniciar");

            Vector3 center = new Vector3(10f, 0f, -5f);
            var size = new Vector2(3f, 3f);
            c.Check(ExitZone.Contains(center, size, center + new Vector3(1.4f, 0f, -1.4f)), "Un punto dentro de la salida cuenta");
            c.Check(!ExitZone.Contains(center, size, center + new Vector3(1.6f, 0f, 0f)), "Un punto fuera de la salida no cuenta");

            c.Check(HudText.Weapon(null, 0) == HudText.Unarmed, "HUD: sin arma muestra los puños");
            c.Check(HudText.Weapon(WeaponDefinition.Bat, 0) == "Bate", "HUD: arma cuerpo a cuerpo sin munición");
            c.Check(HudText.Weapon(WeaponDefinition.Shotgun, 4) == "Escopeta  4/6", "HUD: arma de fuego con munición");
            c.Check(HudText.Weapon(WeaponDefinition.Smg, 0).EndsWith("(vacía)"), "HUD: arma vacía");
            c.Check(HudText.Enemies(3) == "Enemigos: 3", "HUD: enemigos restantes");
            c.Check(HudText.CenterMessage(LevelState.Failed) == "Presiona R para reiniciar", "HUD: mensaje al morir");
            c.Check(HudText.CenterMessage(LevelState.Complete) == "Nivel completado", "HUD: mensaje al ganar");
            c.Check(HudText.CenterMessage(LevelState.Playing) == null && HudText.SubMessage(LevelState.Playing) == null,
                "HUD: jugando no hay mensajes centrales");
            c.Check(HudText.SubMessage(LevelState.ExitOpen) == HudText.GoToExit, "HUD: sin enemigos indica ir a la salida");
        }

        /// <summary>Victoria con un LevelManager real y sangre al morir.</summary>
        private static void VerifyLevelSimulation(Checker c)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            if (playerPrefab == null || enemyPrefab == null) return;

            var created = new List<GameObject>();
            Vector3 basePos = new Vector3(700f, 0f, 700f);
            GameObject Make(GameObject prefab, Vector3 offset)
            {
                GameObject go = Object.Instantiate(prefab, basePos + offset, Quaternion.identity);
                created.Add(go);
                return go;
            }
            (LevelManager manager, ExitZone exit) MakeLevel(PlayerLife player, Enemy[] enemies, Vector3 exitOffset)
            {
                GameObject exitGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.Add(exitGo);
                Object.DestroyImmediate(exitGo.GetComponent<Collider>());
                exitGo.transform.position = basePos + exitOffset;
                var zone = exitGo.AddComponent<ExitZone>();
                BuilderUtil.SetRef(zone, "zoneRenderer", exitGo.GetComponent<Renderer>());
                BuilderUtil.SetRef(zone, "lockedMaterial", AssetFactory.Mat(AssetFactory.MatExitLocked));
                BuilderUtil.SetRef(zone, "openMaterial", AssetFactory.Mat(AssetFactory.MatExit));

                var root = new GameObject("TestLevelManager");
                created.Add(root);
                var lm = root.AddComponent<LevelManager>();
                BuilderUtil.SetRef(lm, "player", player);
                BuilderUtil.SetRef(lm, "exit", zone);
                var so = new SerializedObject(lm);
                SerializedProperty list = so.FindProperty("enemies");
                list.arraySize = enemies.Length;
                for (int i = 0; i < enemies.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                return (lm, zone);
            }

            try
            {
                // --- Victoria: eliminar a todos y llegar a la salida ---
                var player = Make(playerPrefab, Vector3.zero).GetComponent<PlayerLife>();
                var e1 = Make(enemyPrefab, new Vector3(3f, 0f, 0f)).GetComponent<Enemy>();
                var e2 = Make(enemyPrefab, new Vector3(-3f, 0f, 0f)).GetComponent<Enemy>();
                var (manager, exit) = MakeLevel(player, new[] { e1, e2 }, new Vector3(0f, 0f, 10f));

                c.Check(manager.Evaluate() == LevelState.Playing && manager.Progress.Remaining == 2, "Simulación: quedan 2 enemigos");
                c.Check(!exit.IsOpen && exit.ZoneRenderer.sharedMaterial == exit.LockedMaterial, "Con enemigos vivos la salida está apagada");

                e1.ReceiveKnockdown();
                c.Check(manager.Evaluate() == LevelState.Playing && manager.Progress.Remaining == 2, "Un derribado todavía cuenta como vivo");
                e1.CompleteFinish();
                c.Check(manager.Progress.Remaining == 2 && manager.Evaluate() == LevelState.Playing && manager.Progress.Remaining == 1,
                    "Al rematarlo, queda 1 enemigo");

                player.transform.position = exit.transform.position;
                c.Check(manager.Evaluate() == LevelState.Playing, "Simulación: llegar a la salida con enemigos vivos no gana");
                player.transform.position = basePos;

                e2.ReceiveLethalHit();
                c.Check(manager.Evaluate() == LevelState.ExitOpen && exit.IsOpen &&
                        exit.ZoneRenderer.sharedMaterial == exit.OpenMaterial, "Sin enemigos, la salida se enciende");
                c.Check(HudText.SubMessage(manager.Progress.State) == HudText.GoToExit, "El HUD indica ir a la salida");

                player.transform.position = exit.transform.position + new Vector3(0.3f, 0f, -0.3f);
                c.Check(manager.Evaluate() == LevelState.Complete, "Simulación: sin enemigos y en la salida, nivel completado");
                c.Check(HudText.CenterMessage(manager.Progress.State) == "Nivel completado", "El HUD muestra \"Nivel completado\"");
                c.Check(player.GetComponent<PlayerController>().MovementLocked && !player.GetComponent<PlayerWeapons>().enabled,
                    "Al completar el nivel, el jugador se queda quieto y sin atacar");

                // --- Derrota ---
                var victim = Make(playerPrefab, new Vector3(20f, 0f, 0f)).GetComponent<PlayerLife>();
                var e3 = Make(enemyPrefab, new Vector3(23f, 0f, 0f)).GetComponent<Enemy>();
                var (lostManager, _) = MakeLevel(victim, new[] { e3 }, new Vector3(20f, 0f, 10f));
                victim.Kill();
                c.Check(lostManager.Evaluate() == LevelState.Failed, "Simulación: si el jugador muere, el nivel se pierde");
                c.Check(HudText.CenterMessage(lostManager.Progress.State) == "Presiona R para reiniciar",
                    "El HUD muestra \"Presiona R para reiniciar\" al morir");

                // --- Sangre ---
                BloodSpawner spawner = BloodSpawner.Instance;
                c.NotNull(spawner, "Hay un BloodSpawner en la escena");
                if (spawner != null)
                {
                    // La sangre solo se pega a suelo de verdad (caja o malla): suelo de prueba bajo los dos cadáveres.
                    GameObject testFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    created.Add(testFloor);
                    testFloor.transform.position = basePos + new Vector3(2.5f, -0.5f, -10f);
                    testFloor.transform.localScale = new Vector3(16f, 1f, 10f);
                    Physics.SyncTransforms();

                    int before = spawner.SplatCount;
                    var e4 = Make(enemyPrefab, new Vector3(0f, 0f, -10f)).GetComponent<Enemy>();
                    e4.ReceiveLethalHit();
                    c.Check(spawner.SplatCount == before + 3, "Al morir un enemigo aparecen manchas de sangre");
                    Transform splat = spawner.transform.GetChild(spawner.transform.childCount - 1);
                    c.Check(Vector3.Angle(splat.rotation * Vector3.back, Vector3.up) < 1f, "La sangre queda plana sobre el suelo");
                    c.Check(splat.position.y > 0f && splat.position.y < 0.05f, "La sangre está justo encima del suelo");
                    c.Check(EnemyBrain.FlatDistance(splat.position, e4.transform.position) < 1.5f, "La sangre aparece donde murió");

                    int beforePlayer = spawner.SplatCount;
                    Make(playerPrefab, new Vector3(5f, 0f, -10f)).GetComponent<PlayerLife>().Kill();
                    c.Check(spawner.SplatCount == beforePlayer + 3, "Al morir el jugador también sangra");
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
