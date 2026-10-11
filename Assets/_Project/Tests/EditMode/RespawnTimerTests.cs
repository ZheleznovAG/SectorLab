using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Units;
using UnityEngine;

namespace SectorLab.Tests
{
    public class RespawnTimerTests
    {
        [Test]
        public void DeathSchedulesTenSecondsAndDuplicateNotificationDoesNotResetIt()
        {
            var timer = new RespawnTimer();
            Assert.That(timer.Advance(20), Is.False);
            Assert.That(timer.Schedule(), Is.True);
            Assert.That(timer.Advance(9.9), Is.False);
            Assert.That(timer.Schedule(), Is.False);
            Assert.That(timer.RemainingSeconds, Is.EqualTo(0.1).Within(1e-6));
            Assert.That(timer.Advance(0.1), Is.True);
            timer.Complete();
            Assert.That(timer.Advance(20), Is.False);
            Assert.That(timer.Schedule(), Is.True);
            Assert.That(timer.RemainingSeconds, Is.EqualTo(10));
        }

        [Test]
        public void PauseFreezesCountdownAndFinishedMatchCannotAdvanceIt()
        {
            var state = new MatchState(new MatchClock());
            var timer = new RespawnTimer();
            timer.Schedule();
            timer.Advance(state.Tick(4, 0, 0));
            state.SetPaused(true);
            timer.Advance(state.Tick(20, 0, 0));
            Assert.That(timer.RemainingSeconds, Is.EqualTo(6));
            state.SetPaused(false);
            Assert.That(timer.Advance(state.Tick(6, 0, 0)), Is.True);
            timer.Complete();
            timer.Schedule();
            state.Tick(1, 45, 0);
            timer.Advance(state.Tick(20, 0, 0));
            Assert.That(timer.RemainingSeconds, Is.EqualTo(10));
        }

        [TestCase(15)]
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void CountdownIsIndependentOfFrameRate(int fps)
        {
            var state = new MatchState(new MatchClock());
            var timer = new RespawnTimer();
            timer.Schedule();
            for (int i = 0; i < fps * 10 - 1; i++)
                Assert.That(timer.Advance(state.Tick(1.0 / fps, 0, 0)), Is.False);
            Assert.That(timer.Advance(state.Tick(1.0 / fps, 0, 0)), Is.True);
            Assert.That(timer.RemainingSeconds, Is.Zero.Within(1e-6));
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidDeltaIsRejected(double delta)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new RespawnTimer().Advance(delta));
        }

        [Test]
        public void RosterRequiresExactlyOneSlotForEachTeamAndRole()
        {
            var roster = ScriptableObject.CreateInstance<UnitRoster>();
            try
            {
                Assert.Throws<System.InvalidOperationException>(roster.Validate);
                roster.slots = new UnitSpawnSlot[6];
                for (int i = 0; i < 6; i++) roster.slots[i] = new UnitSpawnSlot {
                    team = i < 3 ? Team.Player : Team.Enemy, role = (UnitRole)(i % 3), prefabKey = "role-" + i
                };
                Assert.DoesNotThrow(roster.Validate);
                roster.slots[5].role = UnitRole.Tank;
                Assert.Throws<System.InvalidOperationException>(roster.Validate);
            }
            finally { Object.DestroyImmediate(roster); }
        }
    }
}
