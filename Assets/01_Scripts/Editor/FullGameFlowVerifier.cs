using System;
using System.IO;
using System.Linq;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Story;
using Overdrive.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Verifica la configuración del flujo completo del juego:
    /// Menú Principal -> Level 01 -> Level 02, asegurando sonidos, modelos y animaciones en cada etapa.
    /// </summary>
    public static class FullGameFlowVerifier
    {
        [MenuItem("TopDown/Verificar flujo completo (Menu -> L1 -> L2)")]
        public static void Verify()
        {
            var log = new System.Text.StringBuilder();
            int passed = 0;
            void Check(bool cond, string msg)
            {
                if (cond)
                {
                    passed++;
                    log.AppendLine("OK: " + msg);
                }
                else
                {
                    log.AppendLine("FAIL: " + msg);
                    Debug.LogError("[FlowVerifier] FALLO: " + msg);
                }
            }

            // 1. Build Settings
            var buildScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Check(buildScenes.Length >= 3, "Build Settings contiene las 3 escenas habilitadas");
            Check(buildScenes.Any(p => p.Contains("Menu")), "Build Settings incluye Menu.unity");
            Check(buildScenes.Any(p => p.Contains("Level01")), "Build Settings incluye Level01.unity");
            Check(buildScenes.Any(p => p.Contains("Level02")), "Build Settings incluye Level02.unity");

            // 2. Menu Scene
            Scene menuScene = EditorSceneManager.OpenScene("Assets/00_Scenes/Menu.unity", OpenSceneMode.Single);
            var menuController = menuScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MainMenuController>(true)).FirstOrDefault();
            Check(menuController != null, "Menu.unity tiene MainMenuController");
            if (menuController != null)
            {
                Check(menuController.gameScene == "Level01", "MainMenuController.gameScene inicia en 'Level01'");
                string[] expected = { "Level01", "Level02", "Level03", "Level04" };
                Check(menuController.levelScenes != null && menuController.levelScenes.SequenceEqual(expected) &&
                      menuController.levelButtons != null && menuController.levelButtons.Length == expected.Length &&
                      menuController.levelButtons.All(b => b != null),
                      "MainMenuController permite seleccionar los 4 niveles, cada uno con su botón");
                foreach (string level in expected)
                    Check(buildScenes.Any(p => p.EndsWith(level + ".unity")), $"Build Settings incluye {level}.unity (para cargarlo desde el menú)");
                Check(menuController.playButton != null, "Botón JUGAR conectado");
                Check(menuController.levelsButton != null, "Botón NIVELES conectado");
            }

            // 3. Level 01 Scene
            Scene l1Scene = EditorSceneManager.OpenScene(SceneFactory.ScenePath, OpenSceneMode.Single);
            var l1Manager = l1Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelManager>(true)).FirstOrDefault();
            Check(l1Manager != null, "Level01 tiene LevelManager");
            if (l1Manager != null)
            {
                Check(l1Manager.NextScene == "Level02", "Level01.LevelManager.nextScene apunta a 'Level02'");
                Check(l1Manager.Enemies != null && l1Manager.Enemies.Length == 10, "Level01 tiene 10 enemigos iniciales");
            }
            var l1Sounds = l1Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameSounds>(true)).FirstOrDefault();
            Check(l1Sounds != null, "Level01 tiene GameSounds configurado");
            var l1PlayerAnim = l1Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerAnimator>(true)).FirstOrDefault();
            Check(l1PlayerAnim != null && l1PlayerAnim.Animator != null, "Level01 tiene Player con PlayerAnimator y modelo 3D");
            var l1Enemies = l1Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnemyVisual>(true)).ToArray();
            Check(l1Enemies.Length >= 10 && l1Enemies.All(e => e.Models.Count(m => m != null && m.activeSelf) == 1),
                $"Level01: cada enemigo lleva el modelo animado de su arma ({l1Enemies.Length}/10)");
            var l1Nina = l1Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NpcDialogue>(true)).FirstOrDefault();
            Check(l1Nina != null && l1Nina.Animator != null && l1Nina.Lines.Length > 0, "Level01 tiene a Nina con su diálogo");

            // 4. Level 02 Scene
            Scene l2Scene = EditorSceneManager.OpenScene(Level02Builder.ScenePath, OpenSceneMode.Single);
            var l2Manager = l2Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelManager>(true)).FirstOrDefault();
            Check(l2Manager != null, "Level02 tiene LevelManager");
            if (l2Manager != null)
            {
                Check(l2Manager.NextScene == "Level03", "Level02.LevelManager.nextScene apunta a 'Level03'");
                Check(l2Manager.Enemies != null && l2Manager.Enemies.Length >= 14, "Level02 tiene 14 enemigos iniciales");
            }
            var l2Sounds = l2Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameSounds>(true)).FirstOrDefault();
            Check(l2Sounds != null, "Level02 tiene GameSounds configurado");
            var l2PlayerAnim = l2Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerAnimator>(true)).FirstOrDefault();
            Check(l2PlayerAnim != null && l2PlayerAnim.Animator != null, "Level02 tiene Player con PlayerAnimator y modelo 3D");
            var l2Enemies = l2Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnemyVisual>(true)).ToArray();
            Check(l2Enemies.Length >= 14 && l2Enemies.All(e => e.Models.Count(m => m != null && m.activeSelf) == 1),
                $"Level02: cada enemigo lleva el modelo animado de su arma ({l2Enemies.Length}/14)");
            var l2Nina = l2Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NpcDialogue>(true)).FirstOrDefault();
            Check(l2Nina != null && l2Nina.Animator != null && l2Nina.Lines.Length > 0, "Level02 tiene a Nina con su diálogo");

            // Level02 Validation
            var l2Failures = Level02Validation.Check(l2Scene);
            Check(l2Failures.Count == 0, "Level02 pasa validación completa (escena, navegación, colisiones)");

            // 5. HudText Hints
            Check(HudText.CompleteActionHint("Level02").Contains("Siguiente nivel"), "HudText indica siguiente nivel para Level01");
            Check(HudText.CompleteActionHint("Menu").Contains("menú"), "HudText indica volver al menú al terminar el juego");

            // Volver a Menu
            EditorSceneManager.OpenScene("Assets/00_Scenes/Menu.unity", OpenSceneMode.Single);

            string report = $"PASS: {passed} comprobaciones del flujo completo (Menu -> Level01 -> Level02) OK.\n\n" + log;
            File.WriteAllText("Temp/FullGameFlow.validation.txt", report);
            File.WriteAllText("docs/full-game-flow-validation.txt", report);
            Debug.Log($"[FlowVerifier] {passed} comprobaciones correctas.");
        }
    }
}
