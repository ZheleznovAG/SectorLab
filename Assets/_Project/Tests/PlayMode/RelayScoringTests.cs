using System.Reflection;
using System.Collections;
using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class RelayScoringTests
    {
        private GameObject root;
        private RelayConfig config;
        private BattleController battle;
        private RelayPoint a;
        private RelayPoint b;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("ScoringTest");
            TestArena.CreateGround(root.transform);
            TestArena.Bake(root);
            config = ScriptableObject.CreateInstance<RelayConfig>();
            a = CreateRelay(new Vector3(-3, 0, 0));
            b = CreateRelay(new Vector3(3, 0, 0));
            battle = root.AddComponent<BattleController>();
            battle.Initialize(new MatchState(new MatchClock()));
            battle.InitializeRelays(new[] { a, b });
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        private RelayPoint CreateRelay(Vector3 position)
        {
            var node = new GameObject("Relay");
            node.transform.SetParent(root.transform);
            node.transform.position = position;
            var relay = node.AddComponent<RelayPoint>();
            relay.Configure(config);
            return relay;
        }

        private Unit CreateUnit(Team team, Vector3 position)
        {
            var go = new GameObject("Unit");
            go.SetActive(false);
            go.transform.SetParent(root.transform);
            go.transform.position = position;
            var unit = go.AddComponent<Unit>();
            typeof(Unit).GetField("team", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unit, team);
            go.SetActive(true);
            return unit;
        }

        [Test]
        public void BothOwnedEmptyRelaysScoreTwoPointsPerGameSecond()
        {
            var first = CreateUnit(Team.Player, a.transform.position);
            var second = CreateUnit(Team.Player, b.transform.position);
            battle.AdvanceSimulation(5);
            Assert.That(battle.State.PlayerScore, Is.EqualTo(0).Within(1e-6));
            first.gameObject.SetActive(false);
            second.gameObject.SetActive(false);
            battle.AdvanceSimulation(3);
            Assert.That(battle.State.PlayerScore, Is.EqualTo(6).Within(1e-6));
            battle.SetPaused(true);
            battle.AdvanceSimulation(20);
            Assert.That(battle.State.PlayerScore, Is.EqualTo(6).Within(1e-6));
        }

        [Test]
        public void ContestAndNeutralizationImmediatelyStopScoring()
        {
            var player = CreateUnit(Team.Player, a.transform.position);
            battle.AdvanceSimulation(6);
            double score = battle.State.PlayerScore;
            CreateUnit(Team.Enemy, a.transform.position);
            battle.AdvanceSimulation(2);
            Assert.That(battle.State.PlayerScore, Is.EqualTo(score));
            player.gameObject.SetActive(false);
            battle.AdvanceSimulation(5);
            Assert.That(battle.State.PlayerScore, Is.EqualTo(score));
            Assert.That(battle.State.EnemyScore, Is.Zero);
            Assert.That(a.State.Progress, Is.EqualTo(0).Within(1e-6));
        }

        [Test]
        public void LargeFrameStopsAtWinningTickAndDoesNotOverchargeTime()
        {
            CreateUnit(Team.Player, a.transform.position);
            battle.AdvanceSimulation(180);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(battle.State.PlayerScore, Is.EqualTo(45).Within(1e-6));
            Assert.That(battle.State.Clock.ElapsedSeconds, Is.EqualTo(50).Within(1e-6));
        }

        [Test]
        public void OppositeRelaysReachThresholdInSameTickAsDraw()
        {
            CreateUnit(Team.Player, a.transform.position);
            CreateUnit(Team.Enemy, b.transform.position);
            battle.AdvanceSimulation(60);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Draw));
            Assert.That(battle.State.PlayerScore, Is.EqualTo(45).Within(1e-6));
            Assert.That(battle.State.EnemyScore, Is.EqualTo(45).Within(1e-6));
        }

        [Test]
        public void RepairRelayDoesNotScore()
        {
            config.kind = RelayKind.Repair;
            CreateUnit(Team.Player, a.transform.position);
            battle.AdvanceSimulation(20);
            Assert.That(battle.State.PlayerScore, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AlliedGroupStopsInsideRelayAndCanAutoDefend()
        {
            var allies = new[]
            {
                CreateUnit(Team.Player, new Vector3(0, 0, 2)),
                CreateUnit(Team.Player, new Vector3(0, 0, 0)),
                CreateUnit(Team.Player, new Vector3(0, 0, -2))
            };
            foreach (Unit unit in allies)
            {
                var attack = unit.gameObject.AddComponent<UnitAttack>();
                typeof(UnitAttack).GetField("autoAcquireRange", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(attack, 2.5f);
                Assert.That(a.TryGetApproachPoint(unit, out Vector3 point), Is.True);
                Assert.That(unit.Movement.MoveTo(point, a.GetArrivalDistance(point)), Is.True);
            }
            float deadline = Time.realtimeSinceStartup + 6;
            while ((allies[0].Movement.IsMoving || allies[1].Movement.IsMoving || allies[2].Movement.IsMoving) && Time.realtimeSinceStartup < deadline)
                yield return null;
            foreach (Unit unit in allies)
            {
                Assert.That(unit.Movement.IsMoving, Is.False);
                Assert.That(a.Contains(unit), Is.True);
            }
            Unit enemy = CreateUnit(Team.Enemy, a.transform.position);
            yield return new WaitForSecondsRealtime(1);
            Assert.That(enemy == null || enemy.Health.Current < enemy.Health.Max, Is.True);
        }
    }
}
