using System;

namespace Overdrive.Core
{
    public enum LevelState
    {
        /// <summary>Quedan enemigos vivos o falta el objetivo.</summary>
        Playing,
        /// <summary>No quedan enemigos y se tiene el objetivo: la salida está abierta.</summary>
        ExitOpen,
        Complete,
        Failed,
    }

    /// <summary>
    /// Progreso del nivel: se gana eliminando a todos los enemigos, con el objetivo (el maletín)
    /// y llegando a la salida. Lógica pura para probarla en el editor.
    /// </summary>
    public sealed class LevelProgress
    {
        public LevelState State { get; private set; } = LevelState.Playing;
        public int Total { get; private set; }
        public int Remaining { get; private set; }
        public bool HasObjective { get; private set; } = true;

        public LevelState Evaluate(int total, int dead, bool playerDead, bool playerInExit, bool hasObjective = true)
        {
            Total = total;
            Remaining = Math.Max(0, total - dead);
            HasObjective = hasObjective;

            // Una vez completado (o perdido) el nivel, ya no cambia hasta reiniciar.
            if (State == LevelState.Complete || State == LevelState.Failed) return State;

            if (playerDead) State = LevelState.Failed;
            else if (Remaining > 0 || !hasObjective) State = LevelState.Playing;
            else State = playerInExit ? LevelState.Complete : LevelState.ExitOpen;
            return State;
        }
    }
}
