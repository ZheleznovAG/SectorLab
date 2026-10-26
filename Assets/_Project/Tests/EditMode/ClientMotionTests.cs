using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Network;
using SectorLab.Units;

namespace SectorLab.Tests
{
    public class ClientMotionTests
    {
        private static void Set(MatchModel model, float x, uint generation = 1, bool alive = true)
        {
            model.SetUnitCount(1);
            model.SetUnit(0, new UnitView(0, Team.Player, UnitRole.Tank,
                alive ? UnitLife.Alive : UnitLife.Respawning, alive ? 100 : 0, 100,
                alive ? 0 : 10, generation, 0, x, 0));
        }

        [Test]
        public void PositionConvergesContinuouslyAndFreezesWhenPacketsStop()
        {
            using var model = new MatchModel { MatchId = 1 };
            var motion = new ClientMotion();
            Set(model, 0); motion.Push(model, 0);
            Set(model, 2); motion.Push(model, 1);
            Assert.That(motion.TryPosition(0, 1.05, out var middle), Is.True);
            Assert.That(middle.x, Is.EqualTo(1).Within(0.001));
            Set(model, 3); motion.Push(model, 1.05);
            motion.TryPosition(0, 1.05, out var continuous);
            Assert.That(continuous, Is.EqualTo(middle));
            motion.TryPosition(0, 10, out var stopped);
            Assert.That(stopped.x, Is.EqualTo(3), "No extrapolation when packets stop.");
        }

        [Test]
        public void DeathRespawnAndNewMatchNeverInterpolateFromThePreviousLife()
        {
            using var model = new MatchModel { MatchId = 1 };
            var motion = new ClientMotion();
            Set(model, 0); motion.Push(model, 0);
            Set(model, 2); motion.Push(model, 1);
            Set(model, 2, 1, false); motion.Push(model, 1.01);
            Assert.That(motion.TryPosition(0, 1.02, out _), Is.False);
            Set(model, -5, 2); motion.Push(model, 1.03);
            motion.TryPosition(0, 1.03, out var respawn);
            Assert.That(respawn.x, Is.EqualTo(-5));
            // The death packet can be missing: a new generation still snaps immediately.
            Set(model, 8, 3); motion.Push(model, 1.04);
            motion.TryPosition(0, 1.04, out var skippedDeath);
            Assert.That(skippedDeath.x, Is.EqualTo(8));
            model.MatchId = 2; Set(model, -9, 1); motion.Push(model, 1.05);
            motion.TryPosition(0, 1.05, out var restarted);
            Assert.That(restarted.x, Is.EqualTo(-9));
        }

        [Test]
        public void PauseSnapsToTheAuthoritativePositionAndDisconnectHidesIt()
        {
            using var model = new MatchModel { MatchId = 1 };
            var motion = new ClientMotion();
            Set(model, 0); motion.Push(model, 0);
            Set(model, 2); motion.Push(model, 1);
            model.Paused.Value = true; motion.Push(model, 1.01);
            motion.TryPosition(0, 1.01, out var paused);
            Assert.That(paused.x, Is.EqualTo(2));
            model.IsSynchronized = false; motion.Push(model, 1.02);
            Assert.That(motion.TryPosition(0, 1.02, out _), Is.False);
        }
    }
}
