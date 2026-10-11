using System;
using NUnit.Framework;
using SectorLab.Units;

namespace SectorLab.Tests
{
    public class HealthTests
    {
        [Test]
        public void NewHealth_IsFull()
        {
            var health = new Health(100);

            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(health.Fraction, Is.EqualTo(1f));
            Assert.That(health.IsDead, Is.False);
        }

        [Test]
        public void NonPositiveMax_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Health(0));
        }

        [Test]
        public void TakeDamage_ReducesCurrentAndNotifies()
        {
            var health = new Health(100);
            int changes = 0;
            health.Changed += () => changes++;

            health.TakeDamage(30);

            Assert.That(health.Current, Is.EqualTo(70));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void TakeDamage_IgnoresZeroAndNegative()
        {
            var health = new Health(100);
            int changes = 0;
            health.Changed += () => changes++;

            health.TakeDamage(0);
            health.TakeDamage(-10);

            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void Overkill_ClampsToZeroAndDiesOnce()
        {
            var health = new Health(50);
            int deaths = 0;
            health.Died += () => deaths++;

            health.TakeDamage(80);
            health.TakeDamage(10);

            Assert.That(health.Current, Is.Zero);
            Assert.That(health.IsDead, Is.True);
            Assert.That(deaths, Is.EqualTo(1));
        }
    }
}
