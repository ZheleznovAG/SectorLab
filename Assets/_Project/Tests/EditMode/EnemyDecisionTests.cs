using NUnit.Framework;
using SectorLab.AI;
using SectorLab.Objectives;

namespace SectorLab.Tests
{
    public class EnemyDecisionTests
    {
        private static RelayOption Score(RelayOwner owner, float cost = 1, bool threatened = false, bool reachable = true)
            => new RelayOption(RelayKind.Score, owner, threatened, reachable, cost);
        private static RelayOption Repair(RelayOwner owner = RelayOwner.Enemy)
            => new RelayOption(RelayKind.Repair, owner, false, true, 0);

        [Test]
        public void ThreatenedScoreRelayHasHighestPriority()
        {
            var goal = EnemyDecisionRules.Choose(new[] { Score(RelayOwner.Neutral), Score(RelayOwner.Enemy, 5, true), Repair() }, 0.1f, true);
            Assert.That(goal.Kind, Is.EqualTo(EnemyGoalKind.Defend));
            Assert.That(goal.RelayIndex, Is.EqualTo(1));
        }

        [Test]
        public void CapturePrecedesRepairAndAttack()
        {
            var goal = EnemyDecisionRules.Choose(new[] { Score(RelayOwner.Player), Repair() }, 0.1f, true);
            Assert.That(goal.Kind, Is.EqualTo(EnemyGoalKind.Capture));
            Assert.That(goal.RelayIndex, Is.Zero);
        }

        [Test]
        public void CriticalUnitRepairsWhenScoringRelaysAreSecure()
        {
            var goal = EnemyDecisionRules.Choose(new[] { Score(RelayOwner.Enemy), Repair() }, 0.35f, true);
            Assert.That(goal.Kind, Is.EqualTo(EnemyGoalKind.Repair));
            Assert.That(goal.RelayIndex, Is.EqualTo(1));
        }

        [Test]
        public void HealthyUnitAttacksInterference()
        {
            var goal = EnemyDecisionRules.Choose(new[] { Score(RelayOwner.Enemy), Repair() }, 1, true);
            Assert.That(goal.Kind, Is.EqualTo(EnemyGoalKind.Attack));
        }

        [Test]
        public void SecureTeamCanCaptureRepairBase()
        {
            var goal = EnemyDecisionRules.Choose(new[] { Score(RelayOwner.Enemy), Repair(RelayOwner.Neutral) }, 1, false);
            Assert.That(goal.Kind, Is.EqualTo(EnemyGoalKind.Capture));
            Assert.That(goal.RelayIndex, Is.EqualTo(1));
        }

        [Test]
        public void EqualCostsAlwaysUseStableArrayOrder()
        {
            for (int i = 0; i < 20; i++)
                Assert.That(EnemyDecisionRules.Choose(new[] { Score(RelayOwner.Neutral), Score(RelayOwner.Neutral) }, 1, false).RelayIndex, Is.Zero);
        }

        [Test]
        public void ClaimedCostSpreadsUnitsAndUnreachableRelaysAreIgnored()
        {
            var goal = EnemyDecisionRules.Choose(new[] { Score(RelayOwner.Neutral, 9), Score(RelayOwner.Neutral, 3), Score(RelayOwner.Neutral, 0, false, false) }, 1, false);
            Assert.That(goal.RelayIndex, Is.EqualTo(1));
            goal = EnemyDecisionRules.Choose(new[] { Score(RelayOwner.Neutral, 0, false, false) }, 1, false);
            Assert.That(goal.Kind, Is.EqualTo(EnemyGoalKind.Hold));
            Assert.That(goal.RelayIndex, Is.EqualTo(-1));
        }
    }
}
