using System;
using System.Collections.Generic;
using Overdrive.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación de los efectos de sonido: GameSounds en la escena y aviso de los sonidos sin clip.</summary>
    internal static partial class Verifier
    {
        private static void VerifySoundsScene(Checker c)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject systems = BuilderUtil.FindRoot(scene, SceneFactory.SystemsName);
            var sounds = systems != null ? systems.GetComponent<GameSounds>() : null;
            c.NotNull(sounds, "Systems: falta GameSounds");
            if (sounds == null) return;

            foreach (var (sound, _, file) in SoundFactory.Assignments)
                if (SoundFactory.Load(file) != null)
                    c.Check(sounds.Get(sound)?.clip != null, $"GameSounds: el sonido {sound} debería usar {file}");

            // Un sonido sin clip no es un fallo (simplemente no suena), pero se avisa para no olvidarlo.
            var missing = new List<string>();
            foreach (Sound sound in Enum.GetValues(typeof(Sound)))
                if (sounds.Get(sound)?.clip == null) missing.Add(sound.ToString());
            if (missing.Count > 0)
                Debug.LogWarning($"[Verify] GameSounds: sonidos sin clip (no sonarán): {string.Join(", ", missing)}");
        }
    }
}
