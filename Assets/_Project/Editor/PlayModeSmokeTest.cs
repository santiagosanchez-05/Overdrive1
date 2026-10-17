using System.Collections.Generic;
using System.Linq;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Prueba en Play Mode real: lo que la verificación en modo editor no puede ver (por ejemplo,
    /// que Destroy() sea diferido). Juega unos fotogramas, mata, llena de sangre, rebobina y comprueba
    /// que el juego sigue avanzando y que todo volvió a su estado.
    /// Batch (sin -quit): Unity.exe -batchmode -projectPath ... -executeMethod Overdrive.EditorTools.PlayModeSmokeTest.Run
    /// El proceso termina solo con código 0 (bien) o 1 (fallo); si se cuelga, lo detecta el tiempo límite de quien lo lanza.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeSmokeTest
    {
        private const string RunningKey = "Overdrive.PlayModeSmokeTest.Running";
        private const int MaxFrames = 3000;

        private static readonly List<string> failures = new List<string>();
        private static int step;
        private static int startFrame = -1;
        private static int waitUntilFrame;

        // Estado de la partida simulada.
        private static TimeRewinder rewinder;
        private static LevelManager level;
        private static PlayerLife player;
        private static BloodSpawner blood;
        private static Enemy victim;
        private static Vector3 playerStart;
        private static int bloodAtStart;

        // Tras la recarga de dominio al entrar en Play Mode, el constructor estático vuelve a engancharse.
        static PlayModeSmokeTest()
        {
            if (SessionState.GetBool(RunningKey, false)) EditorApplication.update += Tick;
        }

        [MenuItem("TopDown/Prueba en Play Mode")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(SceneFactory.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(RunningKey, true);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (startFrame < 0) startFrame = Time.frameCount;
            int frame = Time.frameCount - startFrame;
            if (frame > MaxFrames)
            {
                Fail($"La prueba no terminó en {MaxFrames} fotogramas (¿el juego no avanza?)");
                Finish();
                return;
            }
            if (frame < waitUntilFrame) return;

            switch (step)
            {
                case 0: // Arranque: buscar los sistemas de la escena.
                    rewinder = Object.FindAnyObjectByType<TimeRewinder>();
                    level = Object.FindAnyObjectByType<LevelManager>();
                    player = Object.FindAnyObjectByType<PlayerLife>();
                    blood = BloodSpawner.Instance;
                    victim = Enemy.All.FirstOrDefault(e => Floors.IndexOf(e.transform.position.y) == 0);
                    Check(rewinder != null && level != null && player != null && blood != null && victim != null,
                        "La escena tiene rebobinador, nivel, jugador, sangre y enemigos activos");
                    if (failures.Count > 0) { Finish(); return; }
                    playerStart = player.transform.position;
                    bloodAtStart = blood.SplatCount;
                    Next(frame, 15);
                    break;

                case 1: // Mucha sangre: pasa del límite de manchas (antes colgaba Unity en Play Mode).
                    for (int i = 0; i < 100; i++) BloodSpawner.SpawnAt(playerStart + new Vector3(i * 0.1f, 0f, 3f), 0.5f);
                    Check(blood.SplatCount <= 200, $"Con muchas muertes se respeta el límite de manchas ({blood.SplatCount})");
                    Next(frame, 5);
                    break;

                case 2: // Pasan cosas y el jugador muere.
                    victim.ReceiveLethalHit();
                    player.Kill();
                    Next(frame, 5);
                    break;

                case 3: // Muerto: se puede rebobinar (lo mismo que hace la tecla T).
                    Check(level.Progress.State == LevelState.Failed, "Al morir el nivel queda perdido");
                    Check(rewinder.CanRewind, "Al morir se puede rebobinar");
                    Check(rewinder.Rewind(), "Rebobinar devuelve true");
                    Next(frame, 10);
                    break;

                case 4: // El juego sigue avanzando y todo volvió.
                    Check(!player.IsDead, "Tras rebobinar el jugador está vivo");
                    Check(Vector3.Distance(player.transform.position, playerStart) < 0.5f, "Tras rebobinar el jugador vuelve al inicio");
                    Check(!victim.IsDead && victim.Posture == Posture.Standing, "Tras rebobinar el enemigo muerto vuelve a estar de pie");
                    Check(blood.SplatCount == bloodAtStart, $"Tras rebobinar no queda sangre posterior ({blood.SplatCount}/{bloodAtStart})");
                    Check(level.Progress.State == LevelState.Playing, "Tras rebobinar se sigue jugando");
                    Check(rewinder.UsesLeft == rewinder.MaxUses - 1, "Rebobinar gastó un uso");
                    Next(frame, 30);
                    break;

                case 5: // Unos fotogramas más de juego normal sin errores.
                    Check(!player.IsDead && level.Progress.State == LevelState.Playing, "El juego sigue con normalidad tras rebobinar");
                    Finish();
                    break;
            }
        }

        private static void Next(int frame, int wait)
        {
            step++;
            waitUntilFrame = frame + wait;
        }

        private static void Check(bool condition, string message)
        {
            if (condition) Debug.Log("[SmokeTest] OK: " + message);
            else Fail(message);
        }

        private static void Fail(string message)
        {
            failures.Add(message);
            Debug.LogError("[SmokeTest] FALLO: " + message);
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            SessionState.EraseBool(RunningKey);
            bool ok = failures.Count == 0;
            Debug.Log(ok
                ? "[SmokeTest] Prueba en Play Mode OK."
                : $"[SmokeTest] Prueba en Play Mode con {failures.Count} fallo(s).");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
