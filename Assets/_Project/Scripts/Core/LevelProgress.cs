using System;

namespace Overdrive.Core
{
    public enum LevelState
    {
        /// <summary>Quedan enemigos vivos.</summary>
        Playing,
        /// <summary>No quedan enemigos: la salida está abierta.</summary>
        ExitOpen,
        Complete,
        Failed,
    }

    /// <summary>
    /// Progreso del nivel: se gana eliminando a todos los enemigos y llegando a la salida.
    /// Lógica pura para probarla en el editor.
    /// </summary>
    public sealed class LevelProgress
    {
        public LevelState State { get; private set; } = LevelState.Playing;
        public int Total { get; private set; }
        public int Remaining { get; private set; }

        public LevelState Evaluate(int total, int dead, bool playerDead, bool playerInExit)
        {
            Total = total;
            Remaining = Math.Max(0, total - dead);

            // Una vez completado (o perdido) el nivel, ya no cambia hasta reiniciar.
            if (State == LevelState.Complete || State == LevelState.Failed) return State;

            if (playerDead) State = LevelState.Failed;
            else if (Remaining > 0) State = LevelState.Playing;
            else State = playerInExit ? LevelState.Complete : LevelState.ExitOpen;
            return State;
        }
    }
}
