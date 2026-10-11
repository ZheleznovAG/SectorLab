using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class UnitAttackTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TestRoot");
            TestArena.CreateGround(root.transform);
            TestArena.Bake(root);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        private static UnitStats Stats(int maxHealth, float speed, int damage, float range, float cooldown)
        {
            var stats = ScriptableObject.CreateInstance<UnitStats>();
            stats.maxHealth = maxHealth;
            stats.speed = speed;
            stats.damage = damage;
            stats.attackRange = range;
            stats.attackCooldown = cooldown;
            return stats;
        }

        private Unit CreateUnit(Team team, Vector3 position, int maxHealth = 100, float autoAcquireRange = 0f, UnitStats stats = null)
        {
            var go = new GameObject(team + "Unit");
            go.SetActive(false);
            go.transform.SetParent(root.transform);
            go.transform.position = position;
            go.AddComponent<UnitMovement>();
            Unit unit = go.AddComponent<Unit>();
            SetField(unit, "team", team);
            SetField(unit, "maxHealth", maxHealth);
            SetField(unit, "stats", stats);
            UnitAttack attack = go.AddComponent<UnitAttack>();
            SetField(attack, "autoAcquireRange", autoAcquireRange);
            go.SetActive(true);
            return unit;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        [Test]
        public void SetTarget_IgnoresAllies()
        {
            Unit a = CreateUnit(Team.Player, Vector3.zero);
            Unit b = CreateUnit(Team.Player, Vector3.right);

            a.Attack.SetTarget(b);

            Assert.That(a.Attack.Target, Is.Null);
        }

        [UnityTest]
        public IEnumerator TargetInRange_TakesDamageRightAway()
        {
            Unit attacker = CreateUnit(Team.Player, Vector3.zero);
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(1f, 0f, 0f));

            attacker.Attack.SetTarget(enemy);
            yield return null;

            Assert.That(enemy.Health.Current, Is.LessThan(enemy.Health.Max));
            Assert.That(attacker.Movement.IsMoving, Is.False);
        }

        [UnityTest]
        public IEnumerator TargetOutOfRange_AttackerWalksUpToIt()
        {
            Unit attacker = CreateUnit(Team.Player, Vector3.zero);
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(4f, 0f, 0f));

            attacker.Attack.SetTarget(enemy);
            yield return null;

            Assert.That(attacker.Movement.IsMoving, Is.True);
            Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Max));

            for (int frame = 0; frame < 600 && enemy.Health.Current == enemy.Health.Max; frame++)
                yield return null;

            Assert.That(attacker.Attack.IsInRange(enemy), Is.True);
            Assert.That(enemy.Health.Current, Is.LessThan(enemy.Health.Max));
        }

        [UnityTest]
        public IEnumerator Cooldown_LimitsHitsPerSecond()
        {
            Unit attacker = CreateUnit(Team.Player, Vector3.zero);
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(1f, 0f, 0f), maxHealth: 1000);

            attacker.Attack.SetTarget(enemy);
            yield return null;
            int afterFirstHit = enemy.Health.Current;
            yield return null;
            yield return null;

            Assert.That(enemy.Health.Current, Is.EqualTo(afterFirstHit));
        }

        [UnityTest]
        public IEnumerator IdleUnit_AcquiresNearestHostileInRange()
        {
            Unit guard = CreateUnit(Team.Enemy, Vector3.zero, autoAcquireRange: 4f);
            Unit far = CreateUnit(Team.Player, new Vector3(3.5f, 0f, 0f));
            Unit near = CreateUnit(Team.Player, new Vector3(0f, 0f, 2.5f));
            CreateUnit(Team.Enemy, new Vector3(1f, 0f, 0f));

            yield return null;

            Assert.That(guard.Attack.Target, Is.SameAs(near));
            Assert.That(far.Health.Current, Is.EqualTo(far.Health.Max));
        }

        [UnityTest]
        public IEnumerator IdleUnit_IgnoresHostilesOutsideRange()
        {
            Unit guard = CreateUnit(Team.Enemy, Vector3.zero, autoAcquireRange: 4f);
            CreateUnit(Team.Player, new Vector3(5f, 0f, 0f));

            yield return null;

            Assert.That(guard.Attack.Target, Is.Null);
        }

        [UnityTest]
        public IEnumerator MovingUnit_DoesNotAcquireTargets()
        {
            Unit runner = CreateUnit(Team.Player, Vector3.zero, autoAcquireRange: 4f);
            CreateUnit(Team.Enemy, new Vector3(2f, 0f, 0f));

            runner.Movement.MoveTo(new Vector3(0f, 0f, -8f));
            yield return null;

            Assert.That(runner.Attack.Target, Is.Null);
            Assert.That(runner.Movement.IsMoving, Is.True);
        }

        [Test]
        public void Stats_AreAppliedToHealthMovementAndAttack()
        {
            Unit tank = CreateUnit(Team.Player, Vector3.zero, stats: Stats(250, 2f, 12, 1.5f, 1.2f));

            Assert.That(tank.Health.Max, Is.EqualTo(250));
            Assert.That(tank.Movement.Speed, Is.EqualTo(2f));
            Assert.That(tank.Attack.Damage, Is.EqualTo(12));
            Assert.That(tank.Attack.Range, Is.EqualTo(1.5f));
            Assert.That(tank.Attack.Cooldown, Is.EqualTo(1.2f));
        }

        [UnityTest]
        public IEnumerator RangedUnit_HitsFromDistanceWithoutMoving()
        {
            Unit ranger = CreateUnit(Team.Player, Vector3.zero, stats: Stats(70, 3f, 20, 4f, 1f));
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(3.5f, 0f, 0f));

            ranger.Attack.SetTarget(enemy);
            yield return null;

            Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Max - 20));
            // Only horizontal position matters: the agent snaps the unit onto the NavMesh height.
            Vector3 flat = ranger.transform.position;
            flat.y = 0f;
            Assert.That(flat.magnitude, Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator IdleRangedUnit_AcquiresAnythingWithinWeaponRange()
        {
            Unit ranger = CreateUnit(Team.Player, Vector3.zero, autoAcquireRange: 2.5f, stats: Stats(70, 3f, 20, 4f, 1f));
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(3.5f, 0f, 0f));

            yield return null;

            Assert.That(ranger.Attack.Target, Is.SameAs(enemy));
        }

        [UnityTest]
        public IEnumerator KilledTarget_IsDestroyedAndCleared()
        {
            Unit attacker = CreateUnit(Team.Player, Vector3.zero);
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(1f, 0f, 0f), maxHealth: 1);
            bool diedRaised = false;
            enemy.Died += _ => diedRaised = true;

            attacker.Attack.SetTarget(enemy);
            yield return null;
            yield return null;

            Assert.That(diedRaised, Is.True);
            Assert.That(enemy == null, Is.True, "Dead unit should be destroyed");
            Assert.That(attacker.Attack.Target == null, Is.True);
        }
    }
}
