using System;

namespace SectorLab.Battle
{
    // One match owns one clock. Score rates are sampled together, including simultaneous threshold ties.
    public sealed class MatchState
    {
        public MatchClock Clock { get; }
        public double PlayerScore { get; private set; }
        public double EnemyScore { get; private set; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.InProgress;
        public bool IsRunning => Outcome == BattleOutcome.InProgress && !Clock.IsPaused;

        public MatchState(MatchClock clock) => Clock = clock ?? throw new ArgumentNullException(nameof(clock));

        public double Tick(double seconds, double playerScorePerSecond, double enemyScorePerSecond)
        {
            if (!MatchRules.IsValid(seconds) || !MatchRules.IsValid(playerScorePerSecond) || !MatchRules.IsValid(enemyScorePerSecond))
                throw new ArgumentOutOfRangeException("Tick values must be finite and nonnegative.");
            if (!IsRunning)
                return 0;
            double delta = Clock.Advance(seconds);
            PlayerScore = Math.Min(MatchRules.ScoreLimit, PlayerScore + delta * playerScorePerSecond);
            EnemyScore = Math.Min(MatchRules.ScoreLimit, EnemyScore + delta * enemyScorePerSecond);
            Outcome = MatchRules.Evaluate(PlayerScore, EnemyScore, Clock.ElapsedSeconds);
            return delta;
        }

        public void SetPaused(bool paused) => Clock.SetPaused(paused);

        public void Reset()
        {
            Clock.Reset();
            PlayerScore = EnemyScore = 0;
            Outcome = BattleOutcome.InProgress;
        }
    }
}
