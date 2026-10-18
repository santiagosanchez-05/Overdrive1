using System.Collections.Generic;
using System.Linq;
using Overdrive.Combat;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Prueba del nivel 3 en Play Mode real: acabar con los guardias abre el ascensor, pisarlo lleva al ático, el Jefe
    /// avisa y embiste, cae a tiros (con sus refuerzos) y la salida se abre. Sin errores durante la partida.
    /// Batch (sin -quit): Unity.exe -batchmode -projectPath ... -executeMethod Overdrive.EditorTools.Level03PlayTest.Run
    /// </summary>
    [InitializeOnLoad]
    public static class Level03PlayTest
    {
        private const string RunningKey = "Overdrive.Level03PlayTest.Running";
        private static readonly List<string> failures = new List<string>();
        private static int step;
        private static double stepStart;
        private static ArenaElevator elevator;
        private static BossEnemy boss;
        private static PlayerLife player;
        private static LevelManager level;

        static Level03PlayTest()
        {
            if (SessionState.GetBool(RunningKey, false)) EditorApplication.update += Tick;
        }

        [MenuItem("TopDown/Nivel 03/Prueba en Play Mode")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(Level03Builder.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(RunningKey, true);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.isPlaying = true;
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception) failures.Add("Excepción en juego: " + message);
        }

        private static void Check(bool ok, string message)
        {
            if (!ok) failures.Add(message);
        }

        private static void Next()
        {
            step++;
            stepStart = EditorApplication.timeSinceStartup;
        }

        private static double Elapsed => EditorApplication.timeSinceStartup - stepStart;

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (step > 0 && Elapsed > 30.0) { failures.Add($"Tiempo agotado en el paso {step}"); Finish(); return; }
            switch (step)
            {
                case 0:
                    elevator = Object.FindAnyObjectByType<ArenaElevator>();
                    boss = Object.FindAnyObjectByType<BossEnemy>();
                    level = Object.FindAnyObjectByType<LevelManager>();
                    player = Object.FindAnyObjectByType<PlayerLife>();
                    Check(elevator != null && boss != null && level != null && player != null, "Hay ascensor, Jefe, LevelManager y jugador");
                    if (failures.Count > 0) { Finish(); return; }
                    GameInputBlock(true);
                    Next();
                    break;
                case 1:
                    if (Elapsed < 0.5) return;
                    Check(!elevator.IsOpen, "Con guardias vivos el ascensor está cerrado");
                    foreach (Enemy guard in elevator.Guards) guard.ReceiveLethalHit();
                    Next();
                    break;
                case 2:
                    if (Elapsed < 0.3) return;
                    Check(elevator.IsOpen, "Sin guardias el ascensor se abre");
                    var controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    player.transform.position = elevator.transform.position;
                    controller.enabled = true;
                    Next();
                    break;
                case 3:
                    if (Vector3.Distance(player.transform.position, elevator.ArenaStart.position) > 0.5f && Elapsed < 4.0) return;
                    Check(Vector3.Distance(player.transform.position, elevator.ArenaStart.position) < 0.5f, "Al pisar el ascensor llega al ático");
                    // El Jefe a media distancia: debería avisar y embestir solo.
                    boss.GetComponent<EnemyAI>().Hunt(player);
                    Next();
                    break;
                case 4:
                    if (boss.Charges == 0 && Elapsed < 12.0) return;
                    Check(boss.Charges > 0, "En el ático el Jefe avisa y embiste");
                    Next();
                    break;
                case 5:
                    // A tiros: un golpe cada poco hasta que cae.
                    if (!boss.GetComponent<Enemy>().IsDead)
                    {
                        if (Time.frameCount % 20 == 0) boss.GetComponent<Enemy>().ReceiveLethalHit(KillCause.Shot);
                        return;
                    }
                    Check(boss.Raging && boss.Desperate, "Pasó por la furia y la desesperación antes de caer");
                    foreach (Enemy e in level.Enemies) if (e != null && !e.IsDead) e.ReceiveLethalHit();
                    Next();
                    break;
                case 6:
                    if (Elapsed < 0.5) return;
                    Check(level.Exit != null && level.Exit.IsOpen, "Sin nadie en pie, la salida del ático se abre");
                    Finish();
                    break;
            }
        }

        private static void GameInputBlock(bool blocked) => Overdrive.Core.GameInput.Blocked = blocked;

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.EraseBool(RunningKey);
            GameInputBlock(false);
            bool ok = failures.Count == 0;
            foreach (string f in failures) Debug.LogWarning("[Level03 PlayTest] FALLO: " + f);
            Debug.Log(ok ? "[Level03 PlayTest] Prueba en Play Mode OK." : $"[Level03 PlayTest] {failures.Count} fallo(s).");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
