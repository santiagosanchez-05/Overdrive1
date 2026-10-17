using UnityEngine;

namespace Overdrive.Combat
{
    /// <summary>
    /// Temporización del remate: dura <see cref="Duration"/> segundos y reparte <see cref="Hits"/> golpes.
    /// Lógica pura para poder probarla sin entrar en Play.
    /// </summary>
    public sealed class FinisherSequence
    {
        public readonly struct Step
        {
            public readonly int NewHits;
            public readonly bool Completed;

            public Step(int newHits, bool completed)
            {
                NewHits = newHits;
                Completed = completed;
            }
        }

        public float Duration { get; }
        public int Hits { get; }
        public bool Active { get; private set; }
        public float Elapsed { get; private set; }
        public int HitsDone { get; private set; }

        public FinisherSequence(float duration, int hits)
        {
            Duration = Mathf.Max(0.01f, duration);
            Hits = Mathf.Max(1, hits);
        }

        public void Start()
        {
            Active = true;
            Elapsed = 0f;
            HitsDone = 0;
        }

        public void Cancel()
        {
            Active = false;
        }

        public Step Tick(float deltaTime)
        {
            if (!Active) return default;
            Elapsed += deltaTime;
            bool completed = Elapsed >= Duration;
            int expected = completed ? Hits : Mathf.Min(Hits, Mathf.FloorToInt(Elapsed / Duration * Hits));
            int newHits = expected - HitsDone;
            HitsDone = expected;
            if (completed) Active = false;
            return new Step(newHits, completed);
        }
    }
}
