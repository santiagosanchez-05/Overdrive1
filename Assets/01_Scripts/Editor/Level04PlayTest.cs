using System.Collections.Generic;
using System.Linq;
using Overdrive.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Prueba del nivel 4 en Play Mode real: Nina habla al arrancar, el coche acelera por la autopista,
    /// llega la primera oleada y embestir a una camioneta la destroza, sin errores.
    /// Batch (sin -quit): Unity.exe -batchmode -projectPath ... -executeMethod Overdrive.EditorTools.Level04PlayTest.Run
    /// </summary>
    [InitializeOnLoad]
    public static class Level04PlayTest
    {
        private const string RunningKey = "Overdrive.Level04PlayTest.Running";
        private static readonly List<string> failures = new List<string>();
        private static int step;
        private static double stepStart;
        private static ChaseLevel level;
        private static Vector3 start;
        private static Car shootTarget;

        static Level04PlayTest()
        {
            if (SessionState.GetBool(RunningKey, false)) EditorApplication.update += Tick;
        }

        [MenuItem("TopDown/Nivel 04/Prueba en Play Mode")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(Level04Builder.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(RunningKey, true);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.isPlaying = true;
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) failures.Add("Error en juego: " + message);
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
            if (Elapsed > 40.0 && step > 0) { failures.Add($"Tiempo agotado en el paso {step}"); Finish(); return; }
            switch (step)
            {
                case 0:
                    level = Object.FindAnyObjectByType<ChaseLevel>();
                    Check(level != null && level.Player != null, "Hay ChaseLevel con coche");
                    if (level == null) { Finish(); return; }
                    Next();
                    break;
                case 1:
                    if (Elapsed < 0.5) return;
                    Check(level.Dialogue != null && level.Dialogue.Current != null, "Nina habla al arrancar");
                    start = level.Player.transform.position;
                    level.Player.GetComponent<PlayerCarInput>().Override = Vector2.zero; // sin tocar nada: debe avanzar solo
                    // En batch Unity da el botón del ratón por pulsado: el gatillo lo controla la prueba.
                    level.Player.GetComponent<CarGun>().FireOverride = false;
                    Next();
                    break;
                case 2:
                    if (Elapsed < 4.0) return;
                    float moved = Vector3.Distance(start, level.Player.transform.position);
                    Check(moved > 80f && level.Player.ForwardSpeed > 20f, $"Sin tocar nada, el coche avanza solo por la autopista ({moved:F0} m, {level.Player.ForwardSpeed:F1} m/s)");
                    level.Road.Project(level.Player.transform.position, out float drift);
                    Check(Mathf.Abs(drift) < 0.5f, $"Y va recto por su carril ({drift:F2} m del centro)");
                    Vector3 vp = Camera.main.WorldToViewportPoint(level.Player.transform.position);
                    Check(Mathf.Abs(vp.x - 0.5f) < 0.02f && Mathf.Abs(vp.y - 0.5f) < 0.02f, $"El coche va en el centro de la pantalla ({vp.x:F2}, {vp.y:F2})");
                    // Directo a la primera oleada.
                    level.Player.Rail.Along = level.Waves[0].startDistance + 10f;
                    Next();
                    break;
                case 3:
                    // Llega la primera oleada y las camionetas se ponen en los carriles de los lados.
                    if (Elapsed < 6.0) return;
                    Check(level.Spawned.Count >= ChaseLogic.Total(level.Waves[0]), $"Llega la primera oleada ({level.Spawned.Count})");
                    foreach (Car dealer in level.Spawned.Where(c => c != null && !c.Destroyed))
                    {
                        level.Road.Project(dealer.transform.position, out float lateral);
                        Check(Mathf.Abs(lateral) > 4f, $"{dealer.name} va por un carril lateral ({lateral:F1} m del centro)");
                    }
                    Next();
                    break;
                case 4:
                    // A tiros: apuntando a una camioneta y disparando, cae.
                    if (shootTarget == null) shootTarget = level.Spawned.FirstOrDefault(c => c != null && !c.Destroyed);
                    Car target = shootTarget;
                    var gun = level.Player.GetComponent<CarGun>();
                    if (target == null || gun == null)
                    {
                        Check(false, "Hay camioneta a la que disparar y pistola"); Finish(); return;
                    }
                    gun.AimOverride = target.transform.position + Vector3.up;
                    gun.FireOverride = true;
                    if (!target.Destroyed && Elapsed < 4.0) return;
                    gun.FireOverride = false;
                    Check(target.Destroyed, "Disparar con el ratón destroza la camioneta");
                    Check(level.Kills >= 1 && gun.ShotsFired > 0, $"Cuenta la baja ({level.Kills} bajas, {gun.ShotsFired} disparos)");
                    gun.AimOverride = null;
                    Check(level.State == ChaseState.Driving, "La partida sigue");
                    Finish();
                    break;
            }
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.EraseBool(RunningKey);
            bool ok = failures.Count == 0;
            foreach (string f in failures) Debug.LogWarning("[Level04 PlayTest] FALLO: " + f);
            Debug.Log(ok ? "[Level04 PlayTest] Prueba en Play Mode OK." : $"[Level04 PlayTest] {failures.Count} fallo(s).");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
