using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Scoring;
using Overdrive.UI;
using Overdrive.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    [InitializeOnLoad]
    public static class Level02PlayTest
    {
        private const string Running = "Overdrive.Level02.PlayTest";
        private const string RecordKey = "Overdrive.Record.Level02";
        private static readonly List<string> checks = new List<string>();
        private static int step, corner, stageFrame;
        private static bool previousRunInBackground;
        private static double started, stageStarted;
        private static LevelManager level;
        private static PlayerController player;
        private static TimeRewinder rewind;
        private static Vector3[] route;
        private static Vector3 lastPosition;
        private static float stuckTime;

        static Level02PlayTest()
        {
            EditorApplication.playModeStateChanged += OnPlayState;
            if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
        }

        [MenuItem("TopDown/Nivel 02/Prueba completa en Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("La prueba debe comenzar fuera de Play Mode.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(Level02Builder.ScenePath, OpenSceneMode.Single);
            // Preserve the player's existing save and record, even if the test fails.
            SessionState.SetBool("L02.HadSave", File.Exists(SaveSystem.FilePath));
            if (File.Exists(SaveSystem.FilePath)) File.Copy(SaveSystem.FilePath, "Temp/Level02-save.backup", true);
            SessionState.SetBool("L02.HadRecord", PlayerPrefs.HasKey(RecordKey));
            SessionState.SetInt("L02.Record", PlayerPrefs.GetInt(RecordKey));
            SessionState.SetBool(Running, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                previousRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                step = 0; checks.Clear(); started = stageStarted = EditorApplication.timeSinceStartup;
                stageFrame = Time.frameCount;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                RestoreUserData();
                SessionState.SetBool(Running, false);
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Running, false)) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - started > 100) throw new Exception("Tiempo máximo de prueba excedido en paso " + step);
                if (now - stageStarted < 0.3 || Time.frameCount < stageFrame + 3) return;
                switch (step)
                {
                    case 0:
                        Bind();
                        Check(level.Enemies.Length == 14 && level.Reinforcements.Squad.Length == 7, "14 guardias y 7 refuerzos conectados, incluido el grupo de salida");
                        Check(level.Reinforcements.Squad.Count(a => a.transform.position.z < -50f && a.transform.position.z > -65f) == 4
                            && level.Reinforcements.Squad.Count(a => Vector3.Distance(a.transform.position, level.Exit.transform.position) < 3f) == 3,
                            "Cuatro refuerzos en backstage y tres junto a la salida");
                        Check(player.Controller.isGrounded, "Jugador apoyado sobre suelo físico al iniciar");
                        GameInput.Blocked = true;
                        foreach (var ai in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include)) ai.enabled = false;
                        Vector3 original = player.transform.position;
                        player.Controller.Move(Vector3.right * 8f);
                        Check(player.transform.position.x < 2f, "Las paredes del acceso detienen al CharacterController");
                        Warp(original);
                        var weapon = player.GetComponent<PlayerWeapons>().TryPickup(WeaponItem.All);
                        Check(weapon != null && weapon.Type == WeaponType.Bat, "Recogida real del bate inicial");
                        Check(SaveSystem.Save(), "Guardado de la escena Level02");
                        File.Copy(SaveSystem.FilePath, "Temp/Level02-test-save.json", true);
                        Warp(original + Vector3.back);
                        Check(SaveSystem.Continue(), "Carga de Level02 desde la partida guardada");
                        Advance();
                        break;
                    case 1:
                        Bind();
                        GameInput.Blocked = true;
                        foreach (var ai in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include)) ai.enabled = false;
                        Check(Vector3.Distance(player.transform.position, Level02Gameplay.Start) < 0.4f, "La carga restaura la posición guardada");
                        Check(player.GetComponent<PlayerWeapons>().Current != null, "La carga restaura el arma equipada");
                        var journey = new List<Vector3> { player.transform.position };
                        foreach (Vector3 destination in new[] { level.Enemies[1].transform.position, level.Enemies[2].transform.position, level.Enemies[12].transform.position, level.Briefcase.transform.position })
                        {
                            var path = new NavMeshPath();
                            Check(NavMesh.CalculatePath(journey.Last(), destination, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "Ruta completa en Play Mode a " + destination);
                            journey.AddRange(path.corners.Skip(1));
                        }
                        route = journey.ToArray(); corner = 1; lastPosition = player.transform.position; stuckTime = 0f;
                        Advance();
                        break;
                    case 2:
                        WalkRoute();
                        break;
                    case 3:
                        Check(level.Briefcase.IsCollected && level.Reinforcements.Triggered, "Recoger el maletín activa la oleada");
                        Check(level.Reinforcements.Squad.All(a => a.gameObject.activeSelf), "Los siete refuerzos están activos");
                        foreach (var guard in level.Reinforcements.Squad)
                        {
                            var pursuit = new NavMeshPath();
                            Check(NavMesh.CalculatePath(guard.transform.position, player.transform.position, NavMesh.AllAreas, pursuit)
                                && pursuit.status == NavMeshPathStatus.PathComplete, "Refuerzo con ruta al jugador: " + guard.name);
                        }
                        Check(!level.Reinforcements.Door.GetComponent<Collider>().enabled, "La puerta libera su collider al abrirse");
                        var returnPath = new NavMeshPath();
                        Check(NavMesh.CalculatePath(player.transform.position, level.Exit.transform.position, NavMesh.AllAreas, returnPath) && returnPath.status == NavMeshPathStatus.PathComplete,
                            "La puerta abierta deja una ruta completa de regreso a la salida");
                        player.Life.Kill();
                        level.Evaluate();
                        Check(level.Progress.State == LevelState.Failed && rewind.CanRewind, "Derrota y rebobinado disponibles");
                        Check(rewind.Rewind(), "Rebobinado real ejecutado");
                        Check(!player.Life.IsDead && rewind.UsesLeft == 2, "El rebobinado revive y consume un uso");
                        Check(!level.Briefcase.IsCollected && !level.Reinforcements.Triggered, "El rebobinado restaura maletín y refuerzos");
                        Advance();
                        break;
                    case 4:
                        Warp(level.Briefcase.transform.position);
                        level.Briefcase.TryCollect(player.transform);
                        level.Evaluate();
                        foreach (var enemy in level.Enemies) enemy.ReceiveLethalHit(KillCause.Shot);
                        foreach (var ai in level.Reinforcements.Squad) ai.Enemy.ReceiveLethalHit(KillCause.Melee);
                        Check(level.Evaluate() == LevelState.ExitOpen && level.Exit.IsOpen, "Eliminar la oleada completa abre la salida");
                        Warp(level.Exit.transform.position);
                        Check(level.Evaluate() == LevelState.Complete, "Llegar a la salida completa el nivel");
                        Advance();
                        break;
                    case 5:
                        Check(Object.FindAnyObjectByType<ScoreKeeper>().Summary != null, "Puntuación y resumen final generados");
                        var pause = Object.FindAnyObjectByType<PauseMenuController>();
                        pause.Pause();
                        Check(Time.timeScale == 0f && GameInput.Blocked, "Pausa detiene tiempo y entrada");
                        pause.Resume();
                        Check(Time.timeScale == 1f && !GameInput.Blocked, "Reanudar recupera tiempo y entrada");
                        Finish(null);
                        break;
                }
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void Bind()
        {
            level = Object.FindAnyObjectByType<LevelManager>();
            player = level.Player.GetComponent<PlayerController>();
            rewind = Object.FindAnyObjectByType<TimeRewinder>();
        }

        private static void WalkRoute()
        {
            // Guards remain solid while standing. Clear the route with the actual equipped bat.
            foreach (var enemy in level.Enemies)
            {
                if (enemy.IsDead || !MeleeTargeting.SameLevel(player.transform.position, enemy.transform.position)) continue;
                Vector3 direction = enemy.transform.position - player.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 1.7f * 1.7f) continue;
                if (direction.sqrMagnitude > 0.001f) player.transform.rotation = Quaternion.LookRotation(direction);
                player.GetComponent<PlayerWeapons>().Attack(true, true, Enemy.All);
                if (enemy.IsDead && !checks.Contains("PASS Combate real con el bate despeja el recorrido")) checks.Add("PASS Combate real con el bate despeja el recorrido");
                break;
            }
            Vector3 delta = route[corner] - player.transform.position;
            delta.y = 0f;
            if (delta.magnitude < 0.35f)
            {
                corner++;
                if (corner >= route.Length) { Advance(); return; }
                delta = route[corner] - player.transform.position; delta.y = 0f;
            }
            float dt = Mathf.Min(Time.deltaTime, 0.04f);
            player.Controller.Move(Vector3.ClampMagnitude(delta, 8f * dt));
            if (Vector3.Distance(lastPosition, player.transform.position) < 0.004f) stuckTime += dt;
            else stuckTime = 0f;
            lastPosition = player.transform.position;
            if (stuckTime > 2f) throw new Exception("El CharacterController no puede atravesar " + player.transform.position + " hacia " + route[corner]);
        }

        private static void Warp(Vector3 position)
        {
            player.Controller.enabled = false;
            player.transform.position = position;
            player.Controller.enabled = true;
            player.ResetMotion();
            Physics.SyncTransforms();
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks.Add("PASS " + message);
        }

        private static void Advance() { step++; stageStarted = EditorApplication.timeSinceStartup; stageFrame = Time.frameCount; }

        private static void Finish(Exception exception)
        {
            EditorApplication.update -= Tick;
            if (exception != null) checks.Add("FAIL " + exception.Message);
            File.WriteAllText("Temp/Level02.playtest.txt", string.Join("\n", checks));
            RestoreUserData();
            SessionState.SetBool(Running, false);
            GameInput.Blocked = false;
            Time.timeScale = 1f;
            Application.runInBackground = previousRunInBackground;
            EditorApplication.isPlaying = false;
            if (exception != null) Debug.LogError("[Level02 PlayTest] " + exception);
            else Debug.Log("[Level02 PlayTest] " + checks.Count + " comprobaciones correctas.");
        }

        private static void RestoreUserData()
        {
            if (SessionState.GetBool("L02.HadSave", false)) File.Copy("Temp/Level02-save.backup", SaveSystem.FilePath, true);
            else if (File.Exists(SaveSystem.FilePath)) File.Delete(SaveSystem.FilePath);
            if (SessionState.GetBool("L02.HadRecord", false)) PlayerPrefs.SetInt(RecordKey, SessionState.GetInt("L02.Record", 0));
            else PlayerPrefs.DeleteKey(RecordKey);
            PlayerPrefs.Save();
        }
    }
}
