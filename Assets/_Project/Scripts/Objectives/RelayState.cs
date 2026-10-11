using System;
using SectorLab.Battle;

namespace SectorLab.Objectives
{
    public enum RelayOwner { Neutral, Player, Enemy }

    // One occupant is enough; additional allies never accelerate capture.
    public sealed class RelayState
    {
        private readonly double captureSeconds;
        public double Progress { get; private set; }
        public bool IsContested { get; private set; }
        public double EffectiveSeconds { get; private set; }
        public RelayOwner Owner => Progress >= 1 - MatchRules.Epsilon ? RelayOwner.Player
            : Progress <= -1 + MatchRules.Epsilon ? RelayOwner.Enemy : RelayOwner.Neutral;

        public RelayState(double captureSeconds = 5)
        {
            if (!MatchRules.IsValid(captureSeconds) || captureSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(captureSeconds));
            this.captureSeconds = captureSeconds;
        }

        public void Step(double seconds, int players, int enemies)
        {
            if (!MatchRules.IsValid(seconds) || players < 0 || enemies < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            EffectiveSeconds = 0;
            IsContested = players > 0 && enemies > 0;
            if (IsContested)
                return;
            if (players == 0 && enemies == 0)
            {
                // A fully owned scoring relay continues working when empty.
                EffectiveSeconds = Owner == RelayOwner.Neutral ? 0 : seconds;
                return;
            }

            int direction = players > 0 ? 1 : -1;
            double timeUntilOwned = (1 - direction * Progress) * captureSeconds;
            Progress = Math.Max(-1, Math.Min(1, Progress + direction * seconds / captureSeconds));
            // Never score/heal the portion of a tick still spent capturing.
            if (Owner != RelayOwner.Neutral)
                EffectiveSeconds = Math.Max(0, seconds - timeUntilOwned);
        }
    }
}
