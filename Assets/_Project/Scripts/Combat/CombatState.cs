using Overdrive.Core;

namespace Overdrive.Combat
{
    public enum Posture
    {
        Standing,
        Down,
        Dead,
    }

    /// <summary>
    /// Estado de combate de un enemigo: de pie, derribado o muerto.
    /// Un puñetazo derriba (no mata); en el suelo se puede rematar; si no, se levanta.
    /// Lógica pura, sin dependencias de Unity, para probarla en el editor.
    /// </summary>
    public sealed class CombatState
    {
        private readonly Health health = new Health();
        private float downTimer;

        public Posture Posture { get; private set; } = Posture.Standing;
        public bool IsDead => Posture == Posture.Dead;
        public bool IsDown => Posture == Posture.Down;

        /// <summary>Mientras lo están rematando no puede levantarse.</summary>
        public bool IsBeingFinished { get; private set; }

        public float DownTimeRemaining => downTimer;

        /// <summary>Derriba durante <paramref name="duration"/> segundos. Solo funciona si está de pie.</summary>
        public bool KnockDown(float duration)
        {
            if (Posture != Posture.Standing) return false;
            Posture = Posture.Down;
            downTimer = duration;
            return true;
        }

        /// <summary>Avanza el tiempo. Devuelve true si en este paso se levantó.</summary>
        public bool Tick(float deltaTime)
        {
            if (Posture != Posture.Down || IsBeingFinished) return false;
            downTimer -= deltaTime;
            if (downTimer > 0f) return false;
            downTimer = 0f;
            Posture = Posture.Standing;
            return true;
        }

        /// <summary>Empieza un remate: el derribado queda inmovilizado hasta que termine o se cancele.</summary>
        public bool BeginFinish()
        {
            if (Posture != Posture.Down || IsBeingFinished) return false;
            IsBeingFinished = true;
            return true;
        }

        public void CancelFinish()
        {
            IsBeingFinished = false;
        }

        /// <summary>Completa el remate: mata a un enemigo derribado.</summary>
        public bool Finish()
        {
            if (Posture != Posture.Down) return false;
            return Kill();
        }

        /// <summary>Golpe letal (arma o remate).</summary>
        public bool Kill()
        {
            if (!health.Kill()) return false;
            Posture = Posture.Dead;
            IsBeingFinished = false;
            downTimer = 0f;
            return true;
        }
    }
}
