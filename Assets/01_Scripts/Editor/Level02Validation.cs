using System.Collections.Generic;
using System.IO;
using System.Linq;
using Overdrive.CameraRig;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.UI;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    internal static class Level02Validation
    {
        public static List<string> Check(Scene scene)
        {
            var failures = new List<string>();
            T[] Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            void Require(bool condition, string message) { if (!condition) failures.Add(message); }
            var player = Find<PlayerController>().FirstOrDefault();
            var camera = Find<TopDownCamera>().FirstOrDefault();
            var level = Find<LevelManager>().FirstOrDefault();
            Require(player != null, "Falta el jugador y CharacterController.");
            Require(camera != null && camera.Target != null && camera.Aim != null && camera.Cam != null, "Falta la cámara de juego conectada.");
            Require(Find<Collider>().Any(c => c.enabled && !(c is CharacterController)), "El escenario no tiene colisiones.");
            Require(level != null && level.Enemies.Length > 0, "Falta LevelManager con enemigos iniciales.");
            Require(level != null && level.NextScene == "Menu", "LevelManager en Level02 debe tener nextScene = 'Menu'.");
            Require(level != null && level.Briefcase != null && level.Exit != null && level.Reinforcements != null, "Faltan maletín, refuerzos o salida.");
            Require(Find<TimeRewinder>().Length == 1 && Find<GameHUD>().Length == 1 && Find<PauseMenuController>().Length == 1, "Faltan rebobinado, HUD o pausa.");
            Require(Find<Overdrive.Core.GameSounds>().Length == 1, "Falta GameSounds en Systems.");
            Require(Find<PlayerAnimator>().Length == 1 && Find<EnemyAnimator>().Length >= 14 && Find<FemaleNpcAnimator>().Length >= 1,
                "Faltan los animadores 3D de Player, Enemy_Mask_Character o Female_Character.");
            if (player != null)
            {
                Require(Mathf.Approximately(player.MoveSpeed, 10f), "La velocidad no coincide con la instancia de Level01 (10).");
                Require(Physics.Raycast(player.transform.position + Vector3.up, Vector3.down, 2f), "El inicio del jugador no tiene suelo.");
            }
            if (level != null && player != null)
            {
                var surface = Find<NavMeshSurface>().FirstOrDefault();
                Require(surface != null && surface.navMeshData != null, "Falta la navegación horneada del nivel.");
                // Validate the baked layout separately from the intentionally closed security gate.
                // A distant temporary instance avoids changing the live obstacle or scene state.
                Vector3 offset = new Vector3(10000f, 0f, 10000f);
                var navigation = surface != null && surface.navMeshData != null
                    ? NavMesh.AddNavMeshData(surface.navMeshData, offset, Quaternion.identity)
                    : default;
                try
                {
                    foreach (var enemy in Find<EnemyAI>())
                    {
                        Require(enemy.Player == level.Player, "Enemigo sin referencia al jugador: " + enemy.name);
                        Require(NavMesh.SamplePosition(enemy.transform.position + offset, out var hit, 0.8f, NavMesh.AllAreas), "Enemigo fuera del NavMesh: " + enemy.name);
                        var patrolPath = new NavMeshPath();
                        Require(NavMesh.CalculatePath(player.transform.position + offset, enemy.transform.position + offset, NavMesh.AllAreas, patrolPath) && patrolPath.status == NavMeshPathStatus.PathComplete, "Enemigo aislado del recorrido: " + enemy.name);
                    }
                    foreach (Vector3 destination in new[] { level.Briefcase != null ? level.Briefcase.transform.position : Vector3.zero, level.Exit != null ? level.Exit.transform.position : Vector3.zero })
                    {
                        var path = new NavMeshPath();
                        bool reachable = NavMesh.SamplePosition(destination + offset, out var hit, 2f, NavMesh.AllAreas)
                            && NavMesh.CalculatePath(player.transform.position + offset, hit.position, NavMesh.AllAreas, path)
                            && path.status == NavMeshPathStatus.PathComplete;
                        Require(reachable, "Objetivo o salida sin ruta completa desde el inicio: " + destination);
                    }
                }
                finally { if (navigation.valid) navigation.Remove(); }
            }
            File.WriteAllText("Temp/Level02.validation.txt", failures.Count == 0 ? "PASS: escena, referencias, colisiones y rutas." : string.Join("\n", failures));
            return failures;
        }
    }
}
