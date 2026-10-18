using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Level03 se monta con Level03Builder. Si se abre la escena (o se le da a Play) y todavía no tiene jugabilidad
    /// (no hay Systems), el editor ofrece construirla, para que el nivel se pueda jugar abriéndolo directamente.
    /// </summary>
    [InitializeOnLoad]
    internal static class Level03AutoSetup
    {
        static Level03AutoSetup()
        {
            EditorSceneManager.sceneOpened += (scene, _) =>
            {
                if (NeedsBuild(scene)) EditorApplication.delayCall += () => Offer(false);
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode || !NeedsBuild(SceneManager.GetActiveScene())) return;
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => Offer(true);
            };
        }

        private static bool NeedsBuild(Scene scene) =>
            scene.IsValid() && scene.isLoaded && scene.path == Level03Builder.ScenePath &&
            !scene.GetRootGameObjects().Any(r => r.name == "Systems");

        private static void Offer(bool thenPlay)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !NeedsBuild(SceneManager.GetActiveScene())) return;
            bool build = EditorUtility.DisplayDialog("Nivel 03",
                "Level03 todavía no tiene jugabilidad (jugador, enemigos, Jefe y sistemas).\n\n¿Construirla ahora?",
                "Construir", "Ahora no");
            if (!build) return;
            Level03Builder.Build();
            if (thenPlay) EditorApplication.isPlaying = true;
        }
    }
}
