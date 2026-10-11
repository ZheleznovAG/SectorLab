using NUnit.Framework;
using SectorLab.Units;

namespace SectorLab.Tests
{
    public class HealingTests
    {
        [Test]
        public void HealClampsAndOnlyNotifiesActualChanges()
        {
            var health = new Health(100);
            health.TakeDamage(40);
            int changed = 0;
            health.Changed += () => changed++;
            Assert.That(health.Heal(20), Is.EqualTo(20));
            Assert.That(health.Current, Is.EqualTo(80));
            Assert.That(health.Heal(int.MaxValue), Is.EqualTo(20));
            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(health.Heal(1), Is.Zero);
            Assert.That(changed, Is.EqualTo(2));
        }

        [Test]
        public void HealDoesNotResurrectOrNotifyDeadHealth()
        {
            var health = new Health(100);
            int deaths = 0;
            health.Died += () => deaths++;
            health.TakeDamage(100);
            int changed = 0;
            health.Changed += () => changed++;
            Assert.That(health.Heal(100), Is.Zero);
            Assert.That(health.IsDead, Is.True);
            Assert.That(changed, Is.Zero);
            Assert.That(deaths, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveHealIsIgnored(int amount)
        {
            var health = new Health(100);
            health.TakeDamage(50);
            Assert.That(health.Heal(amount), Is.Zero);
            Assert.That(health.Current, Is.EqualTo(50));
        }
    }
}
