using System;

namespace Overdrive.Core
{
    /// <summary>
    /// Vida de un golpe: todo personaje muere con un solo impacto letal.
    /// Lógica pura, sin dependencias de Unity, para poder probarla en el editor.
    /// </summary>
    public sealed class Health
    {
        public bool IsDead { get; private set; }

        public event Action Died;

        /// <summary>Mata al personaje. Devuelve false si ya estaba muerto.</summary>
        public bool Kill()
        {
            if (IsDead) return false;
            IsDead = true;
            Died?.Invoke();
            return true;
        }
    }
}
