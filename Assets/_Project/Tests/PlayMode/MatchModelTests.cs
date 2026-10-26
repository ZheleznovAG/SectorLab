using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using R3;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;
using Unit = SectorLab.Units.Unit;

namespace SectorLab.Tests
{
    // The viewer model mirrors the authoritative state after every tick and stays quiet otherwise.
    public class MatchModelTests
    {
        private GameObject root;
        private UnitRegistry registry;
        private int nextUnitId;
        private RelayConfig config;
        private BattleController battle;
        private RelayPoint a;

        [SetUp]
        public void Setup()
        {
            registry = new UnitRegistry(); nextUnitId = 0;
            root = new GameObject("ModelTest");
            TestArena.CreateGround(root.transform);
            TestArena.Bake(root);
            config = ScriptableObject.CreateInstance<RelayConfig>();
            var node = new GameObject("Relay");
            node.transform.SetParent(root.transform);
            node.transform.position = new Vector3(-3, 0, 0);
            a = node.AddComponent<RelayPoint>();
            a.Configure(config);
            a.BindUnits(registry);
            battle = root.AddComponent<BattleController>();
            battle.enabled = false;
            battle.Initialize(new MatchState(new MatchClock(), registry));
            battle.InitializeRelays(new[] { a });
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
        public void CaptureScoreAndTimeReachTheModel()
        {
            MatchModel model = battle.Model;
            Assert.That(model.Relays, Has.Length.EqualTo(1));
            Assert.That(model.RemainingSeconds.Value, Is.EqualTo(180));
            CreateUnit(Team.Player, a.transform.position);
            battle.AdvanceSimulation(config.captureSeconds / 2);
            Assert.That(model.Relays[0].Value.Side, Is.EqualTo(1));
            Assert.That(model.Relays[0].Value.Percent, Is.EqualTo(50));
            Assert.That(model.Relays[0].Value.Owner, Is.EqualTo(RelayOwner.Neutral));
            battle.AdvanceSimulation(config.captureSeconds / 2 + 2);
            Assert.That(model.Relays[0].Value.Owner, Is.EqualTo(RelayOwner.Player));
            Assert.That(model.Relays[0].Value.OwnerPresent, Is.True);
            Assert.That(model.PlayerRate.Value, Is.EqualTo(config.scorePerSecond));
            Assert.That(model.PlayerScore.Value, Is.EqualTo(battle.State.PlayerScore));
            Assert.That(model.Forecast.Value, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(model.RemainingSeconds.Value, Is.EqualTo(MatchModel.WholeSeconds(battle.State.Clock.RemainingSeconds)));
        }

        [Test]
        public void UnchangedValuesDoNotNotifyAndWholeSecondsTickOncePerSecond()
        {
            MatchModel model = battle.Model;
            var seconds = new List<int>();
            int relayChanges = 0;
            using var timer = model.RemainingSeconds.Subscribe(seconds.Add);
            using var relay = model.Relays[0].Skip(1).Subscribe(_ => relayChanges++);
            battle.AdvanceSimulation(3); // 30 ticks, nobody on the relay
            Assert.That(seconds, Is.EqualTo(new[] { 180, 179, 178, 177 }));
            Assert.That(relayChanges, Is.Zero, "An idle relay publishes the same view every tick without notifying.");
        }

        [Test]
        public void PauseZeroesIncomeAndRunningFollowsPauseAndOutcome()
        {
            MatchModel model = battle.Model;
            var running = new List<bool>();
            using var subscription = model.Running.Subscribe(running.Add);
            CreateUnit(Team.Player, a.transform.position);
            battle.AdvanceSimulation(config.captureSeconds + 1);
            Assert.That(model.PlayerRate.Value, Is.GreaterThan(0));
            battle.SetPaused(true);
            Assert.That(model.Paused.Value, Is.True);
            Assert.That(model.PlayerRate.Value, Is.Zero, "No income is earned while paused.");
            battle.SetPaused(false);
            battle.State.Tick(0.1, 1000, 0);
            battle.PublishModel();
            Assert.That(model.Outcome.Value, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(running, Is.EqualTo(new[] { true, false, true, false }));
        }
    }
}
