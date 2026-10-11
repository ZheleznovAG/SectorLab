using System;
using SectorLab.Battle;

namespace SectorLab.Units
{
    // Advances only by the MatchClock's accepted game delta; loading is a separate lifecycle state.
    public sealed class RespawnTimer
    {
        public const double DelaySeconds = 10;
        public bool IsWaiting { get; private set; }
        public double RemainingSeconds { get; private set; }

        public bool Schedule()
        {
            if (IsWaiting) return false;
            IsWaiting = true;
            RemainingSeconds = DelaySeconds;
            return true;
        }

        public bool Advance(double gameSeconds)
        {
            if (!MatchRules.IsValid(gameSeconds)) throw new ArgumentOutOfRangeException(nameof(gameSeconds));
            if (!IsWaiting) return false;
            RemainingSeconds = Math.Max(0, RemainingSeconds - gameSeconds);
            return RemainingSeconds <= MatchRules.Epsilon;
        }

        public void Complete()
        {
            IsWaiting = false;
            RemainingSeconds = 0;
        }
    }
}
