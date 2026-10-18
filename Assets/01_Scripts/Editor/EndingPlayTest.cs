using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Overdrive.Core;
using Overdrive.Driving;
using Overdrive.Level;
using Overdrive.Story;
using Overdrive.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    [InitializeOnLoad]
    public static class EndingPlayTest
    {
        private const string Running = "Overdrive.EndingPlayTest";
        private static readonly List<string> checks = new List<string>();
        private static int step, frame;
        private static double started, stepStarted;
        private static float unscaledBefore;
        private static bool background;
        private static EndingScreen ending;
        private static ChaseLevel chase;

        static EndingPlayTest()
        {
            EditorApplication.playModeStateChanged += OnPlayState;
            if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
        }

        [MenuItem("TopDown/Final/Probar victoria, cierre y regreso al menú")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("La prueba comienza fuera de Play Mode.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Guarda las escenas antes de ejecutar la prueba.");
            SessionState.SetString("Ending.PreviousScene", SceneManager.GetActiveScene().path);
            SessionState.SetBool("Ending.HadSave", File.Exists(SaveSystem.FilePath));
            if (File.Exists(SaveSystem.FilePath)) File.Copy(SaveSystem.FilePath, "Temp/Ending-save.backup", true);
            EditorSceneManager.OpenScene("Assets/00_Scenes/Level04.unity", OpenSceneMode.Single);
            SessionState.SetBool(Running, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                checks.Clear(); step = 0; frame = Time.frameCount;
                started = stepStarted = EditorApplication.timeSinceStartup;
                background = Application.runInBackground;
                Application.runInBackground = true;
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
                Application.logMessageReceived += OnLog;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                Application.runInBackground = background;
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                if (SessionState.GetBool("Ending.HadSave", false)) File.Copy("Temp/Ending-save.backup", SaveSystem.FilePath, true);
                else if (File.Exists(SaveSystem.FilePath)) File.Delete(SaveSystem.FilePath);
                SessionState.SetBool(Running, false);
                string previous = SessionState.GetString("Ending.PreviousScene", EndingBuilder.ScenePath);
                EditorApplication.delayCall += () => EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            }
        }

        private static void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) checks.Add("FAIL Error de Unity: " + message);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Running, false)) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - started > 75) throw new Exception("Tiempo máximo de prueba excedido en paso " + step);
                if (now - stepStarted < .8 || Time.frameCount < frame + 4) return;
                switch (step)
                {
                    case 0:
                        chase = Object.FindAnyObjectByType<ChaseLevel>();
                        Check(chase != null && !chase.ContinueAfterWin(), "No se puede abrir el final durante la persecución");
                        chase.Player.Rail.Along = chase.Road.Length - 20f;
                        chase.Player.Rail.Speed = 0f;
                        chase.Player.Rail.Step(.0001f);
                        chase.Tick(.1f);
                        Check(chase.State == ChaseState.Won, "Llegar al piso franco gana la persecución real");
                        Check(chase.Dialogue.Current == ChaseLines.Finish[0], "Se conserva la despedida de Nina en Level04");
                        Check(!chase.ContinueAfterWin(), "La tecla de victoria no salta inmediatamente el cierre");
                        Check(File.ReadAllText(SaveSystem.FilePath).Contains("Ending"), "El checkpoint permite continuar en el final");
                        Next(); break;
                    case 1:
                        if (now - stepStarted < 1.3) return;
                        Check(chase.ContinueAfterWin(), "La victoria carga la escena de cierre");
                        Next(); break;
                    case 2:
                        ending = Object.FindAnyObjectByType<EndingScreen>();
                        Check(ending != null && SceneManager.GetActiveScene().name == "Ending", "Level04 conduce a Ending en Play Mode");
                        Check(ending.Timeline.Phase == EndingPhase.Epilogue, "El epílogo aparece antes de los créditos");
                        Check(Time.timeScale == 1f && !GameInput.Blocked && Cursor.visible, "El final recupera tiempo, entrada y cursor");
                        Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a => a.enabled) == 1, "Hay un único AudioListener");
                        Check(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a => a.isPlaying && a.clip == ending.Content.music), "La música del final está sonando");
                        foreach (string name in new[] { "Antonio Santiago Sanchez Angulo", "Jhael Elisa Guevara Silva", "Johnny Suarez Abasto", "Mateo Alejandro Valeriano Sanchez", "Rommel Rodrigo Gutierrez Herrera" })
                            Check(ending.Content.credits.Contains(name), "Créditos incluyen a " + name);
                        Check(ending.Content.credits.Contains("chatcitos, Claudes") && ending.Content.credits.Contains("Antigravity"), "Agradecimientos solicitados incluidos");
                        EndingCapture.Save("Temp/Ending-epilogue.png", 1600, 900);
                        unscaledBefore = ending.Timeline.Elapsed;
                        Time.timeScale = 0f;
                        Next(); break;
                    case 3:
                        Check(ending.Timeline.Elapsed > unscaledBefore + .2f, "El final avanza con tiempo no escalado");
                        Time.timeScale = 1f;
                        var buttons = ending.GetComponentsInChildren<Button>(true);
                        buttons.Single(b => b.name == "Skip").onClick.Invoke();
                        Check(ending.Timeline.Phase == EndingPhase.Credits, "El botón permite saltar al comienzo de los créditos");
                        ending.Timeline.Tick(8f);
                        Next(); break;
                    case 4:
                        Check(ending.Timeline.Phase == EndingPhase.Credits, "Los créditos siguen visibles durante su recorrido");
                        EndingCapture.Save("Temp/Ending-credits.png", 1600, 900);
                        EndingCapture.Save("Temp/Ending-credits-4x3.png", 1280, 960);
                        ending.Timeline.Tick(300f);
                        Next(); break;
                    case 5:
                        Check(ending.Timeline.Phase == EndingPhase.Thanks, "Al acabar el recorrido aparece el agradecimiento final");
                        EndingCapture.Save("Temp/Ending-thanks.png", 1600, 900);
                        ending.GetComponentsInChildren<Button>(true).Single(b => b.name == "Menu").onClick.Invoke();
                        Check(ending.Timeline.Phase == EndingPhase.Leaving, "El botón de menú inicia el fundido de salida");
                        Next(); break;
                    case 6:
                        Check(SceneManager.GetActiveScene().name == "Menu" && Object.FindAnyObjectByType<MainMenuController>() != null, "El final devuelve al menú principal real");
                        Check(!SaveSystem.HasSave, "La campaña completada no deja un checkpoint en Level04");
                        Check(Time.timeScale == 1f && !GameInput.Blocked, "El menú recupera entrada y tiempo normales");
                        Finish(null); break;
                }
            }
            catch (Exception e) { Finish(e); }
        }

        private static void Check(bool ok, string description)
        {
            if (!ok) throw new InvalidOperationException(description);
            checks.Add("PASS " + description);
        }
        private static void Next() { step++; stepStarted = EditorApplication.timeSinceStartup; frame = Time.frameCount; }
        private static void Finish(Exception failure)
        {
            if (failure != null) checks.Add("FAIL " + failure);
            File.WriteAllLines("Temp/Ending.playtest.txt", checks);
            Time.timeScale = 1f; GameInput.Blocked = false;
            Application.runInBackground = background;
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
        }
    }
}
