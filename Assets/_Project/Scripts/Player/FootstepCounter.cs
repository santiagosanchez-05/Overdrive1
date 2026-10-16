namespace Overdrive.Player
{
    /// <summary>Cuenta la distancia andada y avisa de cada paso. Lógica pura.</summary>
    public sealed class FootstepCounter
    {
        private readonly float stepLength;
        private float accumulated;

        public FootstepCounter(float stepLength)
        {
            this.stepLength = stepLength;
        }

        /// <summary>Suma distancia; devuelve true si en este tramo se ha dado al menos un paso.</summary>
        public bool Advance(float distance)
        {
            accumulated += distance;
            if (accumulated < stepLength) return false;
            accumulated %= stepLength;
            return true;
        }

        public void Reset() => accumulated = 0f;
    }
}
