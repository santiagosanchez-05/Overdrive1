using System.Collections.Generic;
using Overdrive.CameraRig;
using Overdrive.Core;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Rebobinar: graba una instantánea de todo lo que implementa <see cref="IRewindable"/> cada
    /// <see cref="Interval"/> segundos y guarda las de los últimos <see cref="Window"/> segundos.
    /// Al morir, T vuelve a la más antigua (unos 5 s atrás). Hay <see cref="MaxUses"/> usos por nivel.
    /// </summary>
    public class TimeRewinder : MonoBehaviour
    {
        [SerializeField] private PlayerLife player;
        [SerializeField] private LevelManager level;
        [SerializeField] private TopDownCamera cameraRig;
        [SerializeField] private float interval = 0.1f;
        [SerializeField] private float window = 5f;
        [SerializeField] private int maxUses = 3;

        private readonly List<IRewindable> targets = new List<IRewindable>();
        private RewindBuffer<object[]> buffer;
        private float timer;
        private int usesLeft = -1;

        public PlayerLife Player => player;
        public LevelManager Level => level;
        public TopDownCamera CameraRig => cameraRig;
        public float Interval => interval;
        public float Window => window;
        public int MaxUses => maxUses;
        public IReadOnlyList<IRewindable> Targets => targets;
        public int FrameCount => buffer?.Count ?? 0;

        public int UsesLeft
        {
            get
            {
                if (usesLeft < 0) usesLeft = maxUses;
                return usesLeft;
            }
        }

        /// <summary>Al cargar una partida guardada.</summary>
        public void SetUsesLeft(int uses) => usesLeft = Mathf.Clamp(uses, 0, maxUses);

        private RewindBuffer<object[]> Buffer => buffer ??= new RewindBuffer<object[]>(RewindBuffer<object[]>.CapacityFor(window, interval));

        /// <summary>Muerto, con usos y con algo grabado.</summary>
        public bool CanRewind => player != null && player.IsDead && UsesLeft > 0 && FrameCount > 0;

        private void Start()
        {
            Gather();
            Record();
        }

        /// <summary>El rebobinado (tecla T) está quitado del juego. Las instantáneas se siguen usando para guardar.</summary>
        public const bool Enabled = false;

        private void Update()
        {
            if (player == null || level == null) return;
            if (CanRewind)
            {
                if (Enabled && GameInput.RewindPressed) Rewind();
                return;
            }

            LevelState state = level.Progress.State;
            if (player.IsDead || state == LevelState.Complete || state == LevelState.Failed) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = interval;
            Record();
        }

        /// <summary>Busca en la escena todo lo rebobinable (también lo desactivado, como los refuerzos).</summary>
        public void Gather()
        {
            targets.Clear();
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (behaviour is IRewindable rewindable && behaviour.gameObject.scene == gameObject.scene)
                    targets.Add(rewindable);
            buffer = null;
        }

        /// <summary>Graba una instantánea ahora. Público para simularlo en el editor.</summary>
        public void Record()
        {
            var frame = new object[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                // Un objeto destruido (por ejemplo un arma que se borró) se salta en vez de romper la grabación.
                if (targets[i] is Object unityObject && unityObject == null) continue;
                frame[i] = targets[i].Capture();
            }
            Buffer.Push(frame);
        }

        /// <summary>Vuelve a la instantánea más antigua y gasta un uso. Devuelve false si no se puede.</summary>
        public bool Rewind()
        {
            if (!CanRewind) return false;
            object[] frame = Buffer.Oldest;

            // Las balas en el aire desaparecen: no forman parte de las instantáneas. Se desactivan ya,
            // porque en Play Mode Destroy() es diferido y podrían seguir moviéndose (y matar) este fotograma.
            foreach (Projectile projectile in FindObjectsByType<Projectile>(FindObjectsInactive.Include))
            {
                projectile.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(projectile.gameObject);
                else DestroyImmediate(projectile.gameObject);
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is Object unityObject && unityObject == null) continue;
                targets[i].Restore(frame[i]);
            }

            usesLeft = UsesLeft - 1;
            Buffer.ResetTo(frame);
            timer = interval;
            if (cameraRig != null) cameraRig.SnapToTarget();
            return true;
        }
    }
}
