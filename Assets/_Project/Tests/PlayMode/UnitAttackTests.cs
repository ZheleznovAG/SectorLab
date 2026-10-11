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
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        private Unit CreateUnit(Team team, Vector3 position, int maxHealth = 100)
        {
            var go = new GameObject(team + "Unit");
            go.SetActive(false);
            go.transform.SetParent(root.transform);
            go.transform.position = position;
            go.AddComponent<UnitMovement>();
            Unit unit = go.AddComponent<Unit>();
            SetField(unit, "team", team);
            SetField(unit, "maxHealth", maxHealth);
            go.AddComponent<UnitAttack>();
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
