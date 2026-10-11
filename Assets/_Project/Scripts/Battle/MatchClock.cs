using System;

namespace SectorLab.Battle
{
    // The authoritative elapsed game time. Input is supplied by the match simulation, never wall time.
    public sealed class MatchClock
    {
        public double ElapsedSeconds { get; private set; }
        public double RemainingSeconds => Math.Max(0, MatchRules.DurationSeconds - ElapsedSeconds);
        public bool IsPaused { get; private set; }

        public double Advance(double seconds)
        {
            if (!MatchRules.IsValid(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (IsPaused)
                return 0;
            double advanced = Math.Min(seconds, RemainingSeconds);
            ElapsedSeconds += advanced;
            return advanced;
        }

        public void SetPaused(bool paused) => IsPaused = paused;

        public void Reset()
        {
            ElapsedSeconds = 0;
            IsPaused = false;
        }
    }
}
