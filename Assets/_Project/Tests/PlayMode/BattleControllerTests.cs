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
        public IEnumerator LastEnemyDies_DoesNotFinishRelayMatch()
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

            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.InProgress));
            Assert.That(finishedWith, Is.Null);
        }

        [UnityTest]
        public IEnumerator CountsLivingUnitsPerSide()
        {
            CreateUnit(Team.Player);
            CreateUnit(Team.Player);
            Unit enemy = CreateUnit(Team.Enemy);
            CreateUnit(Team.Enemy);
            var battle = root.AddComponent<BattleController>();

            yield return null;
            Assert.That(battle.PlayerUnitsAlive, Is.EqualTo(2));
            Assert.That(battle.EnemyUnitsAlive, Is.EqualTo(2));

            enemy.Health.TakeDamage(enemy.Health.Max);
            yield return null;
            yield return null;
            Assert.That(battle.EnemyUnitsAlive, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LastPlayerUnitDies_DoesNotFinishRelayMatch()
        {
            Unit player = CreateUnit(Team.Player);
            CreateUnit(Team.Enemy);
            var battle = root.AddComponent<BattleController>();

            player.Health.TakeDamage(player.Health.Max);
            yield return null;
            yield return null;

            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.InProgress));
        }

        [TestCase(45, 0, BattleOutcome.Victory)]
        [TestCase(0, 45, BattleOutcome.Defeat)]
        [TestCase(45, 45, BattleOutcome.Draw)]
        public void ScoreOutcomeFinishesOnceAndStopsCombat(int playerRate, int enemyRate, BattleOutcome expected)
        {
            Unit unit = CreateUnit(Team.Player);
            unit.gameObject.AddComponent<UnitAttack>();
            var commander = root.AddComponent<SectorLab.Player.UnitCommander>();
            var battle = root.AddComponent<BattleController>();
            typeof(BattleController).GetField("commander", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(battle, commander);
            int finished = 0;
            battle.Finished += _ => finished++;
            battle.AdvanceSimulation(1, playerRate, enemyRate);
            battle.AdvanceSimulation(1, 45, 45);
            Assert.That(battle.Outcome, Is.EqualTo(expected));
            Assert.That(finished, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(commander.enabled, Is.False);
            Assert.That(unit.Attack.enabled, Is.False);
            Assert.That(unit.Movement.IsMoving, Is.False);
        }

        [UnityTest]
        public IEnumerator PauseFreezesWorldClockAndInputAndResumeRestoresThem()
        {
            Unit unit = CreateUnit(Team.Player);
            var battle = root.AddComponent<BattleController>();
            unit.Movement.MoveTo(new Vector3(3f, 0f, 0f));
            yield return null;
            battle.SetPaused(true);
            Vector3 position = unit.transform.position;
            double elapsed = battle.State.Clock.ElapsedSeconds;
            yield return new WaitForSecondsRealtime(0.15f);
            battle.AdvanceSimulation(10, 2, 2);
            Assert.That(battle.State.Clock.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(battle.State.PlayerScore, Is.Zero);
            Assert.That(unit.transform.position, Is.EqualTo(position));
            battle.SetPaused(false);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            yield return new WaitForSeconds(0.15f);
            Assert.That(unit.transform.position.x, Is.GreaterThan(position.x));
        }

        [Test]
        public void TimeoutDrawAndSceneCleanupRestoreTimeScale()
        {
            var battle = root.AddComponent<BattleController>();
            battle.AdvanceSimulation(180);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Draw));
            Object.DestroyImmediate(battle);
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }
    }
}
