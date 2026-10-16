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

        // Who wins if the current income stays as it is: whoever reaches the limit first,
        // otherwise the higher score at the time limit. Draw means nobody is ahead.
        public static BattleOutcome Forecast(double playerScore, double enemyScore,
            double playerRate, double enemyRate, double remainingSeconds)
        {
            if (!IsValid(playerScore) || !IsValid(enemyScore) || !IsValid(playerRate)
                || !IsValid(enemyRate) || !IsValid(remainingSeconds))
                throw new ArgumentOutOfRangeException("Forecast values must be finite and nonnegative.");

            double playerTime = SecondsToLimit(playerScore, playerRate);
            double enemyTime = SecondsToLimit(enemyScore, enemyRate);
            if (Math.Min(playerTime, enemyTime) <= remainingSeconds + Epsilon)
            {
                if (Math.Abs(playerTime - enemyTime) < Epsilon)
                    return BattleOutcome.Draw;
                return playerTime < enemyTime ? BattleOutcome.Victory : BattleOutcome.Defeat;
            }
            double player = playerScore + playerRate * remainingSeconds;
            double enemy = enemyScore + enemyRate * remainingSeconds;
            if (Math.Abs(player - enemy) < Epsilon)
                return BattleOutcome.Draw;
            return player > enemy ? BattleOutcome.Victory : BattleOutcome.Defeat;
        }

        private static double SecondsToLimit(double score, double rate)
        {
            if (score >= ScoreLimit - Epsilon) return 0;
            return rate > Epsilon ? (ScoreLimit - score) / rate : double.PositiveInfinity;
        }

        internal static bool IsValid(double value) => value >= 0 && !double.IsInfinity(value) && !double.IsNaN(value);
    }
}
