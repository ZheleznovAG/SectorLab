using NUnit.Framework;
using SectorLab.Battle;

namespace SectorLab.Tests
{
    public class BattleRulesTests
    {
        [TestCase(3, 2, BattleOutcome.InProgress)]
        [TestCase(1, 0, BattleOutcome.Victory)]
        [TestCase(0, 2, BattleOutcome.Defeat)]
        [TestCase(0, 0, BattleOutcome.Defeat)]
        public void Evaluate(int players, int enemies, BattleOutcome expected)
        {
            Assert.That(BattleRules.Evaluate(players, enemies), Is.EqualTo(expected));
        }
    }
}
