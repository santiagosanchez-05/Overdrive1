using System;

namespace Overdrive.Story
{
    public enum EndingPhase { Epilogue, Credits, Thanks, Leaving }

    /// <summary>Secuencia independiente del tiempo de juego y de la interfaz.</summary>
    public sealed class EndingTimeline
    {
        private readonly int cards;
        private readonly float cardSeconds, creditsSeconds;
        public EndingPhase Phase { get; private set; }
        public int Card { get; private set; }
        public float Elapsed { get; private set; }
        public float CreditsProgress => Math.Min(1f, Elapsed / creditsSeconds);

        public EndingTimeline(int cards, float cardSeconds, float creditsSeconds)
        {
            this.cards = Math.Max(0, cards);
            this.cardSeconds = Math.Max(1f, cardSeconds);
            this.creditsSeconds = Math.Max(1f, creditsSeconds);
            Phase = cards > 0 ? EndingPhase.Epilogue : EndingPhase.Credits;
        }

        public void Tick(float seconds)
        {
            Elapsed += Math.Max(0f, seconds);
            while (Phase == EndingPhase.Epilogue && Elapsed >= cardSeconds)
            {
                Elapsed -= cardSeconds;
                Card++;
                if (Card >= cards) Phase = EndingPhase.Credits;
            }
            if (Phase == EndingPhase.Credits && Elapsed >= creditsSeconds)
            {
                Elapsed -= creditsSeconds;
                Phase = EndingPhase.Thanks;
            }
        }

        public void Advance()
        {
            switch (Phase)
            {
                case EndingPhase.Epilogue:
                    Card++;
                    SetPhase(Card >= cards ? EndingPhase.Credits : EndingPhase.Epilogue);
                    break;
                case EndingPhase.Credits: SetPhase(EndingPhase.Thanks); break;
                case EndingPhase.Thanks: RequestReturn(); break;
            }
        }

        public void SkipEpilogue()
        {
            if (Phase == EndingPhase.Epilogue) SetPhase(EndingPhase.Credits);
        }

        public void RequestReturn()
        {
            if (Phase != EndingPhase.Leaving) SetPhase(EndingPhase.Leaving);
        }

        private void SetPhase(EndingPhase phase) { Phase = phase; Elapsed = 0f; }
    }
}
