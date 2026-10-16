using System;
using System.Collections.Generic;

namespace Overdrive.Core
{
    /// <summary>
    /// Búfer circular de instantáneas: guarda las últimas <see cref="Capacity"/> y descarta las más viejas.
    /// Lógica pura para probarla en el editor.
    /// </summary>
    public sealed class RewindBuffer<T>
    {
        private readonly Queue<T> frames = new Queue<T>();

        public int Capacity { get; }
        public int Count => frames.Count;

        public RewindBuffer(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public void Push(T frame)
        {
            frames.Enqueue(frame);
            while (frames.Count > Capacity) frames.Dequeue();
        }

        /// <summary>La instantánea más antigua que queda (la de hace más tiempo).</summary>
        public T Oldest => frames.Count > 0 ? frames.Peek() : throw new InvalidOperationException("El búfer está vacío.");

        /// <summary>Vacía el búfer y deja solo <paramref name="frame"/>: el punto al que se acaba de volver.</summary>
        public void ResetTo(T frame)
        {
            frames.Clear();
            frames.Enqueue(frame);
        }

        /// <summary>Cuántas instantáneas caben en <paramref name="seconds"/> grabando cada <paramref name="interval"/>.</summary>
        public static int CapacityFor(float seconds, float interval) => (int)Math.Round(seconds / interval) + 1;
    }
}
