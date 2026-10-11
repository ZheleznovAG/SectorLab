using System;
using NUnit.Framework;
using SectorLab.Battle;

namespace SectorLab.Tests
{
    public class MatchStateTests
    {
        [TestCase(15)]
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void ScoreIsIndependentOfFrameRate(int fps)
        {
            var match = new MatchState(new MatchClock());
            for (int i = 0; i < fps * 20; i++)
                match.Tick(1.0 / fps, 1, 0.5);
            Assert.That(match.PlayerScore, Is.EqualTo(20).Within(0.000001));
            Assert.That(match.EnemyScore, Is.EqualTo(10).Within(0.000001));
            Assert.That(match.Clock.ElapsedSeconds, Is.EqualTo(20).Within(0.000001));
        }

        [Test]
        public void PauseFreezesTimeAndScoresUntilResume()
        {
            var match = new MatchState(new MatchClock());
            match.Tick(3, 1, 0);
            match.SetPaused(true);
            Assert.That(match.Tick(90, 2, 2), Is.Zero);
            Assert.That(match.PlayerScore, Is.EqualTo(3));
            Assert.That(match.Clock.ElapsedSeconds, Is.EqualTo(3));
            match.SetPaused(false);
            match.Tick(1, 1, 0);
            Assert.That(match.PlayerScore, Is.EqualTo(4));
        }

        [Test]
        public void TimeoutClampsTheLastTickAndUsesOnlyGameTime()
        {
            var match = new MatchState(new MatchClock());
            match.Tick(179.5, 0.1, 0.05);
            Assert.That(match.Tick(20, 0.1, 0.05), Is.EqualTo(0.5));
            Assert.That(match.Clock.ElapsedSeconds, Is.EqualTo(180));
            Assert.That(match.PlayerScore, Is.EqualTo(18).Within(0.000001));
            Assert.That(match.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [TestCase(1, 0, BattleOutcome.Victory)]
        [TestCase(0, 1, BattleOutcome.Defeat)]
        [TestCase(1, 1, BattleOutcome.Draw)]
        public void ThresholdFinishesOnceAndFurtherTicksHaveNoEffect(int players, int enemies, BattleOutcome expected)
        {
            var match = new MatchState(new MatchClock());
            match.Tick(45, players, enemies);
            Assert.That(match.Outcome, Is.EqualTo(expected));
            Assert.That(match.Tick(100, 2, 2), Is.Zero);
            Assert.That(match.Clock.ElapsedSeconds, Is.EqualTo(45));
        }

        [Test]
        public void EmptyMatchTimesOutAsDraw()
        {
            var match = new MatchState(new MatchClock());
            match.Tick(180, 0, 0);
            Assert.That(match.Outcome, Is.EqualTo(BattleOutcome.Draw));
        }

        [Test]
        public void ResetRestoresNeutralUnpausedMatch()
        {
            var match = new MatchState(new MatchClock());
            match.Tick(45, 1, 0);
            match.SetPaused(true);
            match.Reset();
            Assert.That(match.Outcome, Is.EqualTo(BattleOutcome.InProgress));
            Assert.That(match.Clock.ElapsedSeconds, Is.Zero);
            Assert.That(match.PlayerScore, Is.Zero);
            Assert.That(match.EnemyScore, Is.Zero);
            Assert.That(match.IsRunning, Is.True);
        }

        [Test]
        public void InvalidTickDoesNotAdvanceClock()
        {
            var match = new MatchState(new MatchClock());
            Assert.Throws<ArgumentOutOfRangeException>(() => match.Tick(1, -1, 0));
            Assert.That(match.Clock.ElapsedSeconds, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => match.Tick(double.NaN, 0, 0));
        }
    }
}
