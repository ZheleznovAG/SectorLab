using System;
using NUnit.Framework;
using SectorLab.Battle;

namespace SectorLab.Tests
{
    public class MatchRulesTests
    {
        [TestCase(0, 0, 0, BattleOutcome.InProgress)]
        [TestCase(44, 43, 179, BattleOutcome.InProgress)]
        [TestCase(45, 44, 10, BattleOutcome.Victory)]
        [TestCase(44, 45, 10, BattleOutcome.Defeat)]
        [TestCase(45, 45, 10, BattleOutcome.Draw)]
        [TestCase(12, 11, 180, BattleOutcome.Victory)]
        [TestCase(11, 12, 180, BattleOutcome.Defeat)]
        [TestCase(12, 12, 180, BattleOutcome.Draw)]
        [TestCase(0, 0, 180, BattleOutcome.Draw)]
        public void ScoreAndTimeoutDetermineOutcome(double player, double enemy, double seconds, BattleOutcome expected)
        {
            Assert.That(MatchRules.Evaluate(player, enemy, seconds), Is.EqualTo(expected));
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidInputIsRejected(double invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchRules.Evaluate(invalid, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchRules.Evaluate(0, invalid, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchRules.Evaluate(0, 0, invalid));
        }
    }
}
