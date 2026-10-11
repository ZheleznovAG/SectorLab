using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SectorLab.AI;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class EnemyTacticsTests
    {
        private GameObject root;
        private BattleController battle;
        private EnemyTacticsController ai;
        private RelayPoint a;
        private RelayPoint b;
        private RelayConfig config;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("AiTest");
            TestArena.CreateGround(root.transform);
            TestArena.Bake(root);
            config = ScriptableObject.CreateInstance<RelayConfig>();
            a = CreateRelay(new Vector3(-3, 0, 0));
            b = CreateRelay(new Vector3(3, 0, 0));
            battle = root.AddComponent<BattleController>();
            battle.InitializeRelays(new[] { a, b });
            battle.enabled = false;
            ai = root.AddComponent<EnemyTacticsController>();
            ai.Initialize(battle);
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
            go.AddComponent<UnitAttack>();
            go.SetActive(true);
            return unit;
        }

        [Test]
        public void DecisionsUseGameSecondsAndUnchangedPathsAreNotSpam()
        {
            CreateUnit(Team.Enemy, new Vector3(0, 0, 3));
            battle.AdvanceSimulation(10);
            Assert.That(ai.DecisionCount, Is.EqualTo(10));
            Assert.That(ai.MoveOrdersIssued, Is.EqualTo(1));
            battle.SetPaused(true);
            battle.AdvanceSimulation(20);
            Assert.That(ai.DecisionCount, Is.EqualTo(10));
            battle.SetPaused(false);
            ai.enabled = false;
            battle.AdvanceSimulation(2);
            Assert.That(ai.DecisionCount, Is.EqualTo(10));
            ai.enabled = true;
            battle.AdvanceSimulation(180);
            int decisions = ai.DecisionCount;
            battle.AdvanceSimulation(20);
            Assert.That(ai.DecisionCount, Is.EqualTo(decisions));
        }

        [Test]
        public void StableAssignmentsSpreadTwoUnitsAcrossScoringRelays()
        {
            Unit first = CreateUnit(Team.Enemy, new Vector3(0, 0, 3));
            Unit second = CreateUnit(Team.Enemy, new Vector3(0, 0, 2));
            battle.AdvanceSimulation(1);
            Assert.That(ai.GetAssignedRelay(first), Is.Not.Null);
            Assert.That(ai.GetAssignedRelay(second), Is.Not.Null);
            Assert.That(ai.GetAssignedRelay(first), Is.Not.SameAs(ai.GetAssignedRelay(second)));
        }

        [UnityTest]
        public IEnumerator EnemyWalksAndCapturesUsingExistingNavMeshMovement()
        {
            CreateUnit(Team.Enemy, new Vector3(0, 0, 3));
            float deadline = Time.realtimeSinceStartup + 10;
            while (a.State.Owner != RelayOwner.Enemy && battle.State.IsRunning && Time.realtimeSinceStartup < deadline)
            {
                battle.AdvanceSimulation(Time.deltaTime);
                yield return null;
            }
            Assert.That(a.State.Owner, Is.EqualTo(RelayOwner.Enemy));
            Assert.That(ai.MoveOrdersIssued, Is.LessThanOrEqualTo(ai.DecisionCount));
        }

        [UnityTest]
        public IEnumerator EnemyAttacksAPlayerBlockingItsRelay()
        {
            Unit enemy = CreateUnit(Team.Enemy, a.transform.position);
            Unit player = CreateUnit(Team.Player, a.transform.position + Vector3.right * 0.8f);
            battle.AdvanceSimulation(1);
            Assert.That(enemy.Attack.Target, Is.SameAs(player));
            yield return null;
            yield return null;
            Assert.That(player.Health.Current, Is.LessThan(player.Health.Max));
            Assert.That(a.State.IsContested, Is.True);
        }
    }
}
