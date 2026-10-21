using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Units;
using SectorLab.Objectives;
using UnityEngine;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class BattleControllerTests
    {
        private GameObject root;
        private RelayConfig config;

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
            if (config != null) Object.DestroyImmediate(config);
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
            battle.Initialize(new MatchState(new MatchClock()));
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
        public IEnumerator LastPlayerUnitDies_DoesNotFinishRelayMatch()
        {
            Unit player = CreateUnit(Team.Player);
            CreateUnit(Team.Enemy);
            var battle = root.AddComponent<BattleController>();
            battle.Initialize(new MatchState(new MatchClock()));

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
            battle.Initialize(new MatchState(new MatchClock()));
            typeof(BattleController).GetField("commander", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(battle, commander);
            int finished = 0;
            battle.Finished += _ => finished++;
            var state = new MatchState(new MatchClock());
            state.Tick(44, playerRate > 0 ? 1 : 0, enemyRate > 0 ? 1 : 0);
            battle.Initialize(state);
            config = ScriptableObject.CreateInstance<RelayConfig>();
            var points = new System.Collections.Generic.List<RelayPoint>();
            unit.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(new Vector3(-3, 0, 0));
            if (playerRate > 0) points.Add(CreateOwnedRelay(unit));
            if (enemyRate > 0)
            {
                var enemy = CreateUnit(Team.Enemy);
                enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(new Vector3(3, 0, 0));
                points.Add(CreateOwnedRelay(enemy));
            }
            battle.InitializeRelays(points.ToArray());
            battle.AdvanceSimulation(1);
            battle.AdvanceSimulation(1);
            Assert.That(battle.Outcome, Is.EqualTo(expected));
            Assert.That(finished, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(commander.enabled, Is.False);
            Assert.That(unit.Attack.enabled, Is.False);
            Assert.That(unit.Movement.IsMoving, Is.False);
        }

        private RelayPoint CreateOwnedRelay(Unit unit)
        {
            var node = new GameObject("OwnedRelay");
            node.transform.SetParent(root.transform);
            node.transform.position = unit.transform.position;
            var relay = node.AddComponent<RelayPoint>();
            relay.Configure(config);
            relay.Simulate(5);
            return relay;
        }

        [UnityTest]
        public IEnumerator PauseFreezesWorldClockAndInputAndResumeRestoresThem()
        {
            Unit unit = CreateUnit(Team.Player);
            var battle = root.AddComponent<BattleController>();
            battle.Initialize(new MatchState(new MatchClock()));
            unit.Movement.MoveTo(new Vector3(3f, 0f, 0f));
            yield return null;
            battle.SetPaused(true);
            Vector3 position = unit.transform.position;
            double elapsed = battle.State.Clock.ElapsedSeconds;
            yield return new WaitForSecondsRealtime(0.15f);
            battle.AdvanceSimulation(10);
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
            battle.Initialize(new MatchState(new MatchClock()));
            battle.AdvanceSimulation(180);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Draw));
            Object.DestroyImmediate(battle);
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }
    }
}
