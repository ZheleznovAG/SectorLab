using System;
using System.Reflection;
using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SectorLab.Tests
{
    public class UnitRegistryTests
    {
        private GameObject root;
        private RelayConfig config;
        [SetUp]
        public void Setup()
        {
            root = new GameObject("RegistryIsolation");
            TestArena.CreateGround(root.transform); TestArena.Bake(root);
        }
        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(root);
            if (config != null) Object.DestroyImmediate(config);
        }
        private Unit Spawn(UnitRegistry registry, int id, Team team, Vector3 position)
        {
            var go = new GameObject("Unit"); go.SetActive(false);
            go.transform.SetParent(root.transform); go.transform.position = position;
            var unit = go.AddComponent<Unit>();
            typeof(Unit).GetField("team", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unit, team);
            go.AddComponent<UnitAttack>(); go.SetActive(true);
            unit.AssignId(id); unit.BindRegistry(registry);
            return unit;
        }

        [Test]
        public void IdenticalIdsInAnotherMatchCannotReceiveOrdersOrContestRelays()
        {
            var first = new UnitRegistry(); var second = new UnitRegistry();
            Unit player = Spawn(first, 0, Team.Player, Vector3.zero);
            Unit target = Spawn(first, 3, Team.Enemy, new Vector3(5, 0, 0));
            Unit foreignPlayer = Spawn(second, 0, Team.Player, new Vector3(0, 0, 1));
            Unit foreignTarget = Spawn(second, 3, Team.Enemy, Vector3.right);
            var commands = new MatchCommands(Array.Empty<RelayPoint>(), units: first);
            Assert.That(commands.Execute(UnitCommand.Attack(Team.Player, 0, 3)), Is.EqualTo(CommandResult.Accepted));
            Assert.That(player.Attack.Target, Is.SameAs(target));
            Assert.That(foreignPlayer.Attack.Target, Is.Null);
            player.Attack.SetTarget(foreignTarget);
            Assert.That(player.Attack.Target, Is.SameAs(target));

            config = ScriptableObject.CreateInstance<RelayConfig>();
            var relay = root.AddComponent<RelayPoint>(); relay.Configure(config); relay.BindUnits(first);
            relay.Simulate(config.captureSeconds);
            Assert.That(relay.State.Owner, Is.EqualTo(RelayOwner.Player));
            Assert.That(relay.EnemiesPresent, Is.Zero, "The nearby enemy belongs to another match.");
            Assert.That(relay.PlayersPresent, Is.EqualTo(1));
        }

        [Test]
        public void RegistryTracksEnableDisableDestroyAndRejectsRebindingOrDuplicateIds()
        {
            var first = new UnitRegistry(); var second = new UnitRegistry();
            Unit unit = Spawn(first, 0, Team.Player, Vector3.zero);
            Assert.Throws<InvalidOperationException>(() => unit.BindRegistry(second));
            unit.gameObject.SetActive(false);
            Assert.That(first.All, Is.Empty);
            unit.gameObject.SetActive(true);
            Assert.That(first.Find(0), Is.SameAs(unit));
            Unit other = Spawn(first, 1, Team.Player, Vector3.right);
            Assert.Throws<InvalidOperationException>(() => other.AssignId(0));
            Assert.Throws<InvalidOperationException>(() => Spawn(first, 0, Team.Player, Vector3.left));
            Object.DestroyImmediate(unit.gameObject);
            Assert.That(first.Find(0), Is.Null);
            Assert.That(first.All.Count, Is.EqualTo(1));
            Object.DestroyImmediate(other.gameObject);
            Assert.That(first.All, Is.Empty);
            Assert.That(second.All, Is.Empty);
        }
    }
}
