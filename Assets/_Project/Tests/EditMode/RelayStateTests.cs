using System;
using NUnit.Framework;
using SectorLab.Objectives;

namespace SectorLab.Tests
{
    public class RelayStateTests
    {
        [TestCase(1, 0, RelayOwner.Player, 1)]
        [TestCase(3, 0, RelayOwner.Player, 1)]
        [TestCase(0, 1, RelayOwner.Enemy, -1)]
        [TestCase(0, 3, RelayOwner.Enemy, -1)]
        public void NeutralCaptureTakesFiveSecondsRegardlessOfOccupants(int players, int enemies, RelayOwner owner, int direction)
        {
            var state = new RelayState();
            state.Step(4, players, enemies);
            Assert.That(state.Owner, Is.EqualTo(RelayOwner.Neutral));
            Assert.That(state.Progress, Is.EqualTo(direction * 0.8).Within(1e-6));
            state.Step(1, players, enemies);
            Assert.That(state.Owner, Is.EqualTo(owner));
            Assert.That(state.EffectiveSeconds, Is.EqualTo(0).Within(1e-6));
        }

        [Test]
        public void OppositeCaptureTakesTenSecondsAndImmediatelyStopsEffect()
        {
            var state = new RelayState();
            state.Step(5, 1, 0);
            state.Step(5, 0, 1);
            Assert.That(state.Progress, Is.Zero);
            Assert.That(state.Owner, Is.EqualTo(RelayOwner.Neutral));
            Assert.That(state.EffectiveSeconds, Is.Zero);
            state.Step(5, 0, 1);
            Assert.That(state.Owner, Is.EqualTo(RelayOwner.Enemy));
            Assert.That(state.EffectiveSeconds, Is.Zero);
        }

        [Test]
        public void ContestFreezesProgressAndDisablesOwnedEffect()
        {
            var state = new RelayState();
            state.Step(5, 1, 0);
            state.Step(100, 1, 3);
            Assert.That(state.IsContested, Is.True);
            Assert.That(state.Progress, Is.EqualTo(1));
            Assert.That(state.EffectiveSeconds, Is.Zero);
            state.Step(1, 1, 0);
            Assert.That(state.IsContested, Is.False);
            Assert.That(state.EffectiveSeconds, Is.EqualTo(1));
        }

        [Test]
        public void EmptyRelayKeepsPartialProgressAndFullOwnership()
        {
            var state = new RelayState();
            state.Step(2, 1, 0);
            state.Step(20, 0, 0);
            Assert.That(state.Progress, Is.EqualTo(0.4));
            Assert.That(state.EffectiveSeconds, Is.Zero);
            state.Step(3, 1, 0);
            state.Step(20, 0, 0);
            Assert.That(state.Owner, Is.EqualTo(RelayOwner.Player));
            Assert.That(state.EffectiveSeconds, Is.EqualTo(20));
        }

        [TestCase(15)]
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void CaptureAndEffectAreIndependentOfFrameRate(int fps)
        {
            var state = new RelayState();
            double points = 0;
            for (int i = 0; i < fps * 8; i++)
            {
                state.Step(1.0 / fps, 1, 0);
                points += state.EffectiveSeconds;
            }
            Assert.That(state.Owner, Is.EqualTo(RelayOwner.Player));
            Assert.That(points, Is.EqualTo(3).Within(1e-6));
        }

        [Test]
        public void LargeTickOnlyIncludesTimeAfterCaptureAndNeverOvershoots()
        {
            var state = new RelayState();
            state.Step(8, 1, 0);
            Assert.That(state.Progress, Is.EqualTo(1));
            Assert.That(state.EffectiveSeconds, Is.EqualTo(3));
            state.Step(12, 0, 1);
            Assert.That(state.Progress, Is.EqualTo(-1));
            Assert.That(state.EffectiveSeconds, Is.EqualTo(2));
        }

        [Test]
        public void InvalidInputsDoNotMutateProgress()
        {
            var state = new RelayState();
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Step(double.NaN, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Step(-1, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Step(1, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RelayState(0));
            Assert.That(state.Progress, Is.Zero);
        }
    }
}
