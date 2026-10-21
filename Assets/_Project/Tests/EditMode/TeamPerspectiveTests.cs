using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;

namespace SectorLab.Tests
{
    public class TeamPerspectiveTests
    {
        [Test]
        public void BlueSeesTheMatchAsItIs()
        {
            var blue = new TeamPerspective(Team.Player);
            Assert.That(blue.IsMine(RelayOwner.Player), Is.True);
            Assert.That(blue.IsTheirs(RelayOwner.Enemy), Is.True);
            Assert.That(blue.Side(1), Is.EqualTo(1));
            Assert.That(blue.Outcome(BattleOutcome.Victory), Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void RedSeesItsOwnRelaysAndWinsAsMine()
        {
            var red = new TeamPerspective(Team.Enemy);
            Assert.That(red.IsMine(RelayOwner.Enemy), Is.True);
            Assert.That(red.IsMine(RelayOwner.Player), Is.False);
            Assert.That(red.IsTheirs(RelayOwner.Player), Is.True);
            Assert.That(red.IsMine(RelayOwner.Neutral) || red.IsTheirs(RelayOwner.Neutral), Is.False);
            Assert.That(red.Side(2), Is.EqualTo(1));
            Assert.That(red.Side(1), Is.EqualTo(2));
            Assert.That(red.Side(0), Is.EqualTo(0));
            Assert.That(red.Outcome(BattleOutcome.Defeat), Is.EqualTo(BattleOutcome.Victory));
            Assert.That(red.Outcome(BattleOutcome.Victory), Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(red.Outcome(BattleOutcome.Draw), Is.EqualTo(BattleOutcome.Draw));
            Assert.That(red.Outcome(BattleOutcome.InProgress), Is.EqualTo(BattleOutcome.InProgress));
        }

        [Test]
        public void ScoresAndIncomeFollowTheLocalTeam()
        {
            using var model = new MatchModel();
            model.PlayerScore.Value = 10;
            model.EnemyScore.Value = 20;
            model.EnemyRate.Value = 2;
            var red = new TeamPerspective(Team.Enemy);
            Assert.That(red.MyScore(model).Value, Is.EqualTo(20));
            Assert.That(red.TheirScore(model).Value, Is.EqualTo(10));
            Assert.That(red.MyRate(model).Value, Is.EqualTo(2));
        }
    }
}
