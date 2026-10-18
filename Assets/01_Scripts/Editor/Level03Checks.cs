using System;
using System.Linq;
using Overdrive.Combat;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    /// <summary>Construye el nivel 3 y comprueba el ático del Jefe, el ascensor y el combate (lógica, escena y simulación).</summary>
    internal static class Level03Checks
    {
        [MenuItem("TopDown/Nivel 03/Construir y verificar")]
        public static void BuildAndVerify()
        {
            Level03Builder.Build();
            var c = new Checker();
            Verify(c);
            EditorSceneManager.OpenScene(Level03Builder.ScenePath, OpenSceneMode.Single);
            if (c.Failures.Count > 0)
            {
                string message = $"[Level03] Verificación con {c.Failures.Count} fallo(s) y {c.Passed} correctas:\n - " + string.Join("\n - ", c.Failures);
                Debug.LogError(message);
                if (Application.isBatchMode) throw new Exception(message);
                return;
            }
            Debug.Log($"[Level03] Verificación OK: {c.Passed} comprobaciones correctas.");
        }

        public static void Verify(Checker c)
        {
            VerifyLogic(c);
            Scene scene = EditorSceneManager.OpenScene(Level03Builder.ScenePath, OpenSceneMode.Single);
            VerifyScene(c, scene);
            VerifySimulation(c, scene);
        }

        private static void VerifyLogic(Checker c)
        {
            c.Check(BossLogic.ShouldCharge(0f, false, 6f, 2.5f, 11f) && !BossLogic.ShouldCharge(1f, false, 6f, 2.5f, 11f) &&
                    !BossLogic.ShouldCharge(0f, true, 6f, 2.5f, 11f) && !BossLogic.ShouldCharge(0f, false, 1f, 2.5f, 11f) &&
                    !BossLogic.ShouldCharge(0f, false, 20f, 2.5f, 11f), "El Jefe embiste solo a media distancia, sin estar aturdido y tras su espera");
            c.Check(BossLogic.Phase(12, 12) == 0 && BossLogic.Phase(6, 12) == 1 && BossLogic.Phase(3, 12) == 2, "Fases: normal, furia (mitad) y desesperado (un cuarto)");
            c.Check(BossLogic.ChargeCooldown(2) < BossLogic.ChargeCooldown(1) && BossLogic.ChargeCooldown(1) < BossLogic.ChargeCooldown(0),
                "Cuanto más herido, más a menudo embiste");
            c.Check(ElevatorLogic.Label(3).Contains("3") && ElevatorLogic.Label(0).Contains("ÁTICO"), "El ascensor dice cuántos guardias quedan y cuándo se abre");
        }

        private static void VerifyScene(Checker c, Scene scene)
        {
            GameObject level = BuilderUtil.FindRoot(scene, "Level");
            Transform arena = level != null ? level.transform.Find(Level03Arena.ArenaName) : null;
            c.NotNull(arena, "Hay ático del Jefe");
            if (arena == null) return;
            var points = Level03Arena.Layout(arena.position);

            // Espacio: el ático es grande y el NavMesh lo cubre casi entero.
            Physics.SyncTransforms();
            int samples = 0, walkable = 0;
            for (float x = -Level03Arena.Width * 0.5f + 1f; x < Level03Arena.Width * 0.5f; x += 1f)
            for (float z = -Level03Arena.Depth * 0.5f + 1f; z < Level03Arena.Depth * 0.5f; z += 1f)
            {
                samples++;
                if (NavMesh.SamplePosition(arena.position + new Vector3(x, 0f, z), out _, 0.3f, NavMesh.AllAreas)) walkable++;
            }
            float area = walkable * 1f;
            c.Check(area > 330f, $"El ático tiene sitio para moverse: {area:0} m² caminables de {samples} m² (el loft es estrecho)");
            var path = new NavMeshPath();
            c.Check(NavMesh.CalculatePath(points.Start, points.Boss, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                "Se llega andando de la entrada del ático al Jefe");
            c.Check(NavMesh.CalculatePath(points.Start, points.Exit, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                "Y a la salida");
            foreach (Vector3 w in points.Weapons.Select(x => x.pos))
                c.Check(NavMesh.SamplePosition(w, out _, 0.5f, NavMesh.AllAreas), "Cada arma del ático está en suelo caminable");

            // Coberturas: cada una tapa la vista entre dos puntos a sus lados.
            int covering = 0;
            foreach (Vector3 cover in points.Covers)
            {
                Vector3 a = cover + new Vector3(0f, 1f, -2.5f), b = cover + new Vector3(0f, 1f, 2.5f);
                if (!LineOfSight.IsClear(a, b, null)) covering++;
            }
            c.Check(covering == points.Covers.Count, $"Las {points.Covers.Count} coberturas paran balas y vista ({covering})");

            var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossEnemy>(true)).FirstOrDefault();
            c.NotNull(boss, "Hay Jefe");
            if (boss == null) return;
            c.Check(Vector3.Distance(boss.transform.position, points.Boss) < 0.5f, "El Jefe espera en su trono del ático");
            var bossSo = new SerializedObject(boss);
            c.Check(bossSo.FindProperty("rageWave").objectReferenceValue != null && bossSo.FindProperty("finalWave").objectReferenceValue != null &&
                    bossSo.FindProperty("rageLight").objectReferenceValue != null && bossSo.FindProperty("maxHealth").intValue >= 12,
                "El Jefe tiene refuerzos de furia, oleada final, luz de aviso y vida para un combate largo");

            var elevator = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaElevator>(true)).FirstOrDefault();
            c.NotNull(elevator, "Hay ascensor al ático");
            if (elevator == null) return;
            c.Check(elevator.Guards.Length >= 5 && elevator.Guards.All(g => g != null && !g.GetComponent<BossEnemy>()), $"El ascensor espera a los {elevator.Guards.Length} guardias del loft");
            c.Check(elevator.ArenaStart != null && Vector3.Distance(elevator.ArenaStart.position, points.Start) < 0.1f, "Lleva a la entrada del ático");
            var start = scene.GetRootGameObjects().First(r => r.CompareTag("Player")).transform.position;
            c.Check(NavMesh.CalculatePath(start, elevator.transform.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                "Desde el inicio se llega andando al ascensor (arriba del loft)");

            var manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelManager>(true)).First();
            c.Check(manager.Exit != null && Vector3.Distance(manager.Exit.transform.position, points.Exit) < 0.5f, "La salida está en el ático, tras el Jefe");
            c.Check(manager.Enemies.Contains(boss.GetComponent<Enemy>()) && manager.Enemies.Length >= elevator.Guards.Length + 1 + 5,
                $"El nivel cuenta guardias, Jefe y refuerzos ({manager.Enemies.Length})");
            c.Check(manager.NextScene == "Level04", "Al acabar pasa a la huida en coche");
            var music = BuilderUtil.FindRoot(scene, "Music");
            var musicSource = music != null ? music.GetComponent<AudioSource>() : null;
            c.Check(musicSource != null && musicSource.clip != null && musicSource.clip.name == "DurantePartida_Nvl3" && musicSource.loop && musicSource.playOnAwake,
                "Suena el techno del nivel 3 en bucle, también en el combate contra el Jefe");
            var arenaGuards = new[] { "Boss Reinforcements", "Boss Final Wave" }
                .Select(n => BuilderUtil.FindRoot(scene, n)).Where(r => r != null)
                .SelectMany(r => r.GetComponentsInChildren<Enemy>(true)).ToArray();
            c.Check(arenaGuards.Length >= 5 && arenaGuards.All(g => g.Weapon != null && g.Weapon.Definition.IsMelee),
                $"Los guardias del ático solo llevan bate o cuchillo ({string.Join(", ", arenaGuards.Select(g => g.Weapon != null ? g.Weapon.Definition.Type.ToString() : "nada"))})");
            int arenaWeapons = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Count(t => t.name.StartsWith("Arena_"));
            c.Check(arenaWeapons >= 5, $"Hay armas en el ático ({arenaWeapons})");
        }

        /// <summary>
        /// Simulación: el ascensor no se abre con guardias vivos y lleva al ático al acabar con ellos; el Jefe avisa y
        /// embiste quitando vida; la furia trae refuerzos, el último cuarto la oleada final, y el último golpe lo mata.
        /// </summary>
        private static void VerifySimulation(Checker c, Scene scene)
        {
            var elevator = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaElevator>(true)).FirstOrDefault();
            var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossEnemy>(true)).FirstOrDefault();
            if (elevator == null || boss == null) return;
            var player = scene.GetRootGameObjects().First(r => r.CompareTag("Player")).GetComponent<PlayerLife>();

            c.Check(!elevator.IsOpen, "Simulación: con guardias vivos el ascensor está cerrado");
            foreach (Enemy guard in elevator.Guards) guard.ReceiveLethalHit();
            c.Check(elevator.IsOpen, "Simulación: sin guardias se abre");
            elevator.Teleport();
            c.Check(Vector3.Distance(player.transform.position, elevator.ArenaStart.position) < 0.1f, "Simulación: lleva al jugador a la entrada del ático");

            // Embestida: el jugador a media distancia delante del Jefe.
            boss.Init();
            player.transform.position = boss.transform.position + boss.transform.forward * 0.6f;
            float before = player.CurrentHealth;
            boss.StartTelegraph();
            c.Check(boss.Telegraphing, "Simulación: antes de embestir avisa (se para y se ilumina)");
            for (int i = 0; i < 40 && !boss.Charging; i++) boss.TickCharge(0.05f);
            c.Check(boss.Charging && boss.Charges == 1, "Simulación: tras el aviso, embiste");
            for (int i = 0; i < 20 && boss.Charging; i++) boss.TickCharge(0.05f);
            c.Check(player.CurrentHealth <= before - boss.ChargeDamage + 0.01f && !player.IsDead, $"Simulación: la embestida quita {boss.ChargeDamage:0} de vida (no mata de golpe)");

            // Fases.
            var so = new SerializedObject(boss);
            var rage = (ReinforcementWave)so.FindProperty("rageWave").objectReferenceValue;
            var final = (ReinforcementWave)so.FindProperty("finalWave").objectReferenceValue;
            int max = boss.MaxHealth;
            int hits = 0;
            bool rageAtHalf = false, finalAtQuarter = false;
            while (hits < max + 2)
            {
                boss.ClockOffset += 1f;
                hits++;
                bool absorbed = boss.AbsorbLethal(KillCause.Shot);
                if (boss.Health == max / 2) rageAtHalf = boss.Raging && rage.Triggered;
                if (boss.Health == max / 4) finalAtQuarter = boss.Desperate && final.Triggered;
                if (!absorbed) break;
            }
            c.Check(rageAtHalf, "Simulación: a mitad de vida entra en furia y llegan sus guardaespaldas");
            c.Check(finalAtQuarter, "Simulación: con un cuarto de vida llama a los últimos");
            c.Check(hits == max, $"Simulación: cae con {max} golpes ({hits})");
        }
    }
}
