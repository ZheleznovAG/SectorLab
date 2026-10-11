using System;

namespace SectorLab.Battle
{
    // Pure rules for Relay Skirmish. Both scores are evaluated together after a simulation tick.
    public static class MatchRules
    {
        public const double ScoreLimit = 45;
        public const double DurationSeconds = 180;
        internal const double Epsilon = 0.000001;

        public static BattleOutcome Evaluate(double playerScore, double enemyScore, double elapsedSeconds)
        {
            if (!IsValid(playerScore) || !IsValid(enemyScore) || !IsValid(elapsedSeconds))
                throw new ArgumentOutOfRangeException("Match values must be finite and nonnegative.");

            bool playerReached = playerScore >= ScoreLimit - Epsilon;
            bool enemyReached = enemyScore >= ScoreLimit - Epsilon;
            if (playerReached && enemyReached)
                return BattleOutcome.Draw;
            if (playerReached)
                return BattleOutcome.Victory;
            if (enemyReached)
                return BattleOutcome.Defeat;
            if (elapsedSeconds < DurationSeconds - Epsilon)
                return BattleOutcome.InProgress;
            if (Math.Abs(playerScore - enemyScore) < Epsilon)
                return BattleOutcome.Draw;
            return playerScore > enemyScore ? BattleOutcome.Victory : BattleOutcome.Defeat;
        }

        internal static bool IsValid(double value) => value >= 0 && !double.IsInfinity(value) && !double.IsNaN(value);
    }
}
