using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class BattleControllerTests
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

        private Unit CreateUnit(Team team)
        {
            var go = new GameObject(team + "Unit");
            go.SetActive(false);
            go.transform.SetParent(root.transform);
            go.AddComponent<UnitMovement>();
            Unit unit = go.AddComponent<Unit>();
            typeof(Unit).GetField("team", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unit, team);
            go.SetActive(true);
            return unit;
        }

        [UnityTest]
        public IEnumerator LastEnemyDies_Victory()
        {
            CreateUnit(Team.Player);
            Unit enemy = CreateUnit(Team.Enemy);
            var battle = root.AddComponent<BattleController>();
            BattleOutcome? finishedWith = null;
            battle.Finished += outcome => finishedWith = outcome;

            yield return null;
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.InProgress));

            enemy.Health.TakeDamage(enemy.Health.Max);
            yield return null;
            yield return null;

            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(finishedWith, Is.EqualTo(BattleOutcome.Victory));
        }

        [UnityTest]
        public IEnumerator LastPlayerUnitDies_Defeat()
        {
            Unit player = CreateUnit(Team.Player);
            CreateUnit(Team.Enemy);
            var battle = root.AddComponent<BattleController>();

            player.Health.TakeDamage(player.Health.Max);
            yield return null;
            yield return null;

            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Defeat));
        }
    }
}
