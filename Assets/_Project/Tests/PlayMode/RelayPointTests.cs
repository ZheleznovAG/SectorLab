using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class RelayPointTests
    {
        private GameObject root;
        private UnitRegistry registry;
        private int nextUnitId;
        private RelayPoint point;
        private RelayConfig config;

        [SetUp]
        public void Setup()
        {
            registry = new UnitRegistry(); nextUnitId = 0;
            root = new GameObject("RelayTest");
            TestArena.CreateGround(root.transform);
            TestArena.Bake(root);
            config = ScriptableObject.CreateInstance<RelayConfig>();
            var node = new GameObject("Relay");
            node.transform.SetParent(root.transform);
            point = node.AddComponent<RelayPoint>();
            point.Configure(config);
            point.BindUnits(registry);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
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
            unit.BindRegistry(registry);
            if (unit.Id < 0) unit.AssignId(nextUnitId++);
            return unit;
        }

        [Test]
        public void CaptureRingFillsWithProgressInTheCapturingSidesColour()
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            typeof(RelayPoint).GetField("ringMaterial", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(point, material);
            var player = CreateUnit(Team.Player, new Vector3(0.5f, 1, 0));
            point.Simulate(config.captureSeconds / 2);
            var ring = point.GetComponentInChildren<LineRenderer>();
            Assert.That(ring, Is.Not.Null);
            Assert.That(ring.enabled, Is.True);
            Assert.That(point.CaptureRingFill, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(ring.positionCount, Is.EqualTo(33), "Half of the 64 segments plus the starting point.");
            Assert.That(point.CaptureRingColor.b, Is.GreaterThan(point.CaptureRingColor.r));

            var enemy = CreateUnit(Team.Enemy, new Vector3(-0.5f, 1, 0));
            point.Simulate(1);
            Assert.That(point.CaptureRingColor.r, Is.GreaterThan(0.9f).And.GreaterThan(point.CaptureRingColor.b), "A contest turns the ring yellow.");

            player.gameObject.SetActive(false);
            point.Simulate(config.captureSeconds);
            Assert.That(point.State.Progress, Is.LessThan(0));
            Assert.That(point.CaptureRingColor.r, Is.GreaterThan(point.CaptureRingColor.b));
            enemy.gameObject.SetActive(false);
            point.Simulate(0.1);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void NeutralRelayWithoutProgressHidesItsRing()
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            typeof(RelayPoint).GetField("ringMaterial", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(point, material);
            point.Simulate(1);
            Assert.That(point.CaptureRingFill, Is.Zero);
            var ring = point.GetComponentInChildren<LineRenderer>(true);
            Assert.That(ring == null || !ring.enabled, Is.True);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void CaptureRadiusIsHorizontalAndBothSidesContest()
        {
            CreateUnit(Team.Player, new Vector3(1, 1, 0));
            var enemy = CreateUnit(Team.Enemy, new Vector3(1.4f, 1, 0));
            point.Simulate(10);
            Assert.That(point.State.IsContested, Is.True);
            Assert.That(point.State.Progress, Is.Zero);
            enemy.gameObject.SetActive(false);
            point.Simulate(5);
            Assert.That(point.State.Owner, Is.EqualTo(RelayOwner.Player));
        }

        [UnityTest]
        public IEnumerator DyingUnitLeavesOccupancyWithoutStaleTriggerState()
        {
            Unit unit = CreateUnit(Team.Player, Vector3.zero);
            point.Simulate(2);
            unit.Health.TakeDamage(unit.Health.Max);
            // Even before deferred Destroy, dead units cannot capture.
            point.Simulate(20);
            Assert.That(point.PlayersPresent, Is.Zero);
            Assert.That(point.State.Progress, Is.EqualTo(0.4));
            yield return null;
            Assert.That(Unit.All.Count, Is.Zero);
        }

        [Test]
        public void PauseAndFinishPreventCapture()
        {
            CreateUnit(Team.Player, Vector3.zero);
            var battle = root.AddComponent<BattleController>();
            battle.Initialize(new MatchState(new MatchClock(), registry));
            battle.InitializeRelays(new[] { point });
            battle.SetPaused(true);
            battle.AdvanceSimulation(10);
            Assert.That(point.State.Progress, Is.Zero);
            battle.SetPaused(false);
            battle.AdvanceSimulation(2);
            Assert.That(point.State.Progress, Is.EqualTo(0.4).Within(1e-6));
            battle.AdvanceSimulation(180);
            double progress = point.State.Progress;
            battle.AdvanceSimulation(10);
            Assert.That(point.State.Progress, Is.EqualTo(progress));
        }

        [Test]
        public void ApproachRequiresReachablePointInsideRadius()
        {
            Unit unit = CreateUnit(Team.Player, new Vector3(3, 0, 0));
            Assert.That(point.TryGetApproachPoint(unit, out Vector3 destination), Is.True);
            Assert.That(destination.magnitude, Is.LessThan(config.radius));
            point.transform.position = new Vector3(50, 0, 0);
            Assert.That(point.TryGetApproachPoint(unit, out _), Is.False);
        }

        [TestCase(15, Team.Player)]
        [TestCase(30, Team.Enemy)]
        [TestCase(60, Team.Player)]
        [TestCase(144, Team.Enemy)]
        public void RepairAccumulatesSixHpPerSecondAtDifferentFrameRates(int fps, Team team)
        {
            config.kind = RelayKind.Repair;
            Unit unit = CreateUnit(team, Vector3.zero);
            unit.Health.TakeDamage(50);
            point.Simulate(5);
            Assert.That(unit.Health.Current, Is.EqualTo(50));
            for (int i = 0; i < fps * 3; i++) point.Simulate(1.0 / fps);
            Assert.That(unit.Health.Current, Is.EqualTo(68));
            point.Simulate(100);
            Assert.That(unit.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void RepairRequiresPresenceAndStopsDuringContestPauseAndFinish()
        {
            config.kind = RelayKind.Repair;
            Unit player = CreateUnit(Team.Player, Vector3.zero);
            player.Health.TakeDamage(40);
            var battle = root.AddComponent<BattleController>();
            battle.Initialize(new MatchState(new MatchClock(), registry));
            battle.InitializeRelays(new[] { point });
            battle.AdvanceSimulation(5);
            battle.AdvanceSimulation(1);
            Assert.That(player.Health.Current, Is.EqualTo(66));
            battle.SetPaused(true);
            battle.AdvanceSimulation(10);
            Assert.That(player.Health.Current, Is.EqualTo(66));
            battle.SetPaused(false);
            var enemy = CreateUnit(Team.Enemy, Vector3.zero);
            battle.AdvanceSimulation(5);
            Assert.That(player.Health.Current, Is.EqualTo(66));
            player.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(new Vector3(3, 0, 0));
            battle.AdvanceSimulation(5);
            Assert.That(player.Health.Current, Is.EqualTo(66));
            enemy.gameObject.SetActive(false);
            battle.AdvanceSimulation(180);
            player.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(Vector3.zero);
            point.State.Step(5, 1, 0);
            battle.AdvanceSimulation(10);
            Assert.That(player.Health.Current, Is.EqualTo(66));
        }

        [Test]
        public void FullHealthDoesNotBankFutureHealingAndDeadUnitIsNotRepaired()
        {
            config.kind = RelayKind.Repair;
            Unit player = CreateUnit(Team.Player, Vector3.zero);
            point.Simulate(50);
            player.Health.TakeDamage(10);
            point.Simulate(0.05);
            Assert.That(player.Health.Current, Is.EqualTo(90));
            point.Simulate(0.05);
            Assert.That(player.Health.Current, Is.EqualTo(90));
            player.Health.TakeDamage(100);
            point.Simulate(20);
            Assert.That(player.Health.Current, Is.Zero);
            Assert.That(point.PlayersPresent, Is.Zero);
        }
    }
}
