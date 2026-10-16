using System;
using Overdrive.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Constructor del juego: materiales, prefabs, escena Level01 y Build Settings.
    /// Es idempotente: se puede ejecutar varias veces sin duplicar nada.
    /// Batch: Unity.exe -batchmode -quit -projectPath ... -executeMethod Overdrive.EditorTools.TopDownBuilder.BuildAndVerify
    /// </summary>
    public static class TopDownBuilder
    {
        [MenuItem("TopDown/Construir nivel")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAndVerify();
        }

        public static void BuildAndVerify()
        {
            LevelMap map = Level01Map.Create();

            AssetFactory.EnsureMaterials();
            AssetFactory.EnsureBulletPrefab();
            AssetFactory.EnsureBloodPrefab();
            AssetFactory.EnsureWeaponPrefabs();
            AssetFactory.EnsurePlayerPrefab();
            AssetFactory.EnsureEnemyPrefab();
            SceneFactory.Build(map);
            AssetDatabase.SaveAssets();

            Checker checker = Verifier.Run(map);

            // La verificación no debe dejar la escena modificada.
            EditorSceneManager.OpenScene(SceneFactory.ScenePath, OpenSceneMode.Single);

            if (checker.Failures.Count > 0)
            {
                string message = $"[TopDownBuilder] Verificación con {checker.Failures.Count} fallo(s) " +
                                 $"y {checker.Passed} comprobaciones correctas:\n - " +
                                 string.Join("\n - ", checker.Failures);
                Debug.LogError(message);
                if (Application.isBatchMode) throw new Exception(message);
                return;
            }

            Debug.Log($"[TopDownBuilder] Verificación OK: {checker.Passed} comprobaciones correctas.");
        }
    }
}
