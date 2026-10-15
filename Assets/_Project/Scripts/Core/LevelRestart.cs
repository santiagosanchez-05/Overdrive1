using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.Core
{
    /// <summary>R reinicia el nivel al instante, vivo o muerto.</summary>
    public class LevelRestart : MonoBehaviour
    {
        private void Update()
        {
            if (GameInput.RestartPressed) Restart();
        }

        public static void Restart()
        {
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }
    }
}
