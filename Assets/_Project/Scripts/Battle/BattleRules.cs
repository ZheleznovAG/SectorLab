namespace SectorLab.Battle
{
    public enum BattleOutcome
    {
        InProgress,
        Victory,
        Defeat,
        Draw
    }

    // Win/lose rule of the placeholder match: wipe out the enemy, lose when your own side is gone.
    // The real goal of the game is still open (docs/07_DECISIONS.md, DEC-007).
    public static class BattleRules
    {
        public static BattleOutcome Evaluate(int playerUnitsAlive, int enemyUnitsAlive)
        {
            if (playerUnitsAlive <= 0)
                return BattleOutcome.Defeat;
            if (enemyUnitsAlive <= 0)
                return BattleOutcome.Victory;
            return BattleOutcome.InProgress;
        }
    }
}
