using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;

namespace SectorLab.Tests
{
    // The checks a host must make for every order it receives from a client.
    public class MatchCommandsTests
    {
        private GameObject root;
        private RelayConfig config;
        private readonly List<RelayPoint> relays = new List<RelayPoint>();
        private MatchState state;
        private MatchCommands commands;
        private Unit player, ally, enemy;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("CommandsTest");
            TestArena.CreateGround(root.transform);
            TestArena.Bake(root);
            config = ScriptableObject.CreateInstance<RelayConfig>();
            var node = new GameObject("Relay");
            node.transform.SetParent(root.transform);
            node.transform.position = new Vector3(3, 0, 0);
            var relay = node.AddComponent<RelayPoint>();
            relay.Configure(config);
            relays.Clear();
            relays.Add(relay);
            state = new MatchState(new MatchClock());
            commands = new MatchCommands(relays, state);
            player = CreateUnit(Team.Player, new Vector3(-3, 0, 0), 0);
            ally = CreateUnit(Team.Player, new Vector3(-3, 0, 2), 1);
            enemy = CreateUnit(Team.Enemy, new Vector3(0, 0, -3), 3);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        private Unit CreateUnit(Team team, Vector3 position, int id)
        {
            var go = new GameObject(team + "Unit" + id);
            go.SetActive(false);
            go.transform.SetParent(root.transform);
            go.transform.position = position;
            go.AddComponent<UnitMovement>();
            var unit = go.AddComponent<Unit>();
            typeof(Unit).GetField("team", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unit, team);
            go.AddComponent<UnitAttack>();
            go.SetActive(true);
            unit.AssignId(id);
            return unit;
        }

        [Test]
        public void OwnerCanSendItsUnitToARelayAndListenersSeeTheOrder()
        {
            var seen = new List<UnitCommand>();
            commands.Executed += c => seen.Add(c);
            UnitCommand order = UnitCommand.MoveToRelay(Team.Player, player.Id, 0);
            Assert.That(commands.Execute(order), Is.EqualTo(CommandResult.Accepted));
            Assert.That(player.Movement.IsMoving, Is.True);
            Assert.That(seen, Has.Count.EqualTo(1));
            Assert.That(seen[0].UnitId, Is.EqualTo(player.Id));
        }

        [Test]
        public void ATeamCannotCommandTheOtherTeamsUnits()
        {
            Assert.That(commands.Execute(UnitCommand.MoveToRelay(Team.Enemy, player.Id, 0)), Is.EqualTo(CommandResult.NotOwner));
            Assert.That(commands.Execute(UnitCommand.Attack(Team.Player, enemy.Id, player.Id)), Is.EqualTo(CommandResult.NotOwner));
            Assert.That(player.Movement.IsMoving, Is.False);
            Assert.That(enemy.Attack.Target, Is.Null);
        }

        [Test]
        public void UnknownUnitsTargetsAndRelaysAreRejected()
        {
            Assert.That(commands.Execute(UnitCommand.MoveToRelay(Team.Player, 42, 0)), Is.EqualTo(CommandResult.UnknownUnit));
            Assert.That(commands.Execute(UnitCommand.MoveToRelay(Team.Player, player.Id, 7)), Is.EqualTo(CommandResult.InvalidTarget));
            Assert.That(commands.Execute(UnitCommand.Attack(Team.Player, player.Id, 99)), Is.EqualTo(CommandResult.InvalidTarget));
            Assert.That(commands.Execute(UnitCommand.Attack(Team.Player, player.Id, ally.Id)), Is.EqualTo(CommandResult.InvalidTarget));
            Assert.That(commands.Execute(UnitCommand.Attack(Team.Player, player.Id, enemy.Id)), Is.EqualTo(CommandResult.Accepted));
            Assert.That(player.Attack.Target, Is.SameAs(enemy));
        }

        [Test]
        public void AcceptedMoveReplacesAnAttackOrder()
        {
            commands.Execute(UnitCommand.Attack(Team.Player, player.Id, enemy.Id));
            Assert.That(commands.Execute(UnitCommand.MoveToPoint(Team.Player, player.Id, new Vector3(-1, 0, -1))), Is.EqualTo(CommandResult.Accepted));
            Assert.That(player.Attack.Target, Is.Null);
            Assert.That(commands.LastDestination, Is.EqualTo(new Vector3(-1, 0, -1)));
        }

        [Test]
        public void PausedOrFinishedMatchAcceptsNoOrders()
        {
            state.SetPaused(true);
            Assert.That(commands.Execute(UnitCommand.MoveToRelay(Team.Player, player.Id, 0)), Is.EqualTo(CommandResult.MatchNotRunning));
            state.SetPaused(false);
            state.Tick(0.1, 1000, 0);
            Assert.That(state.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(commands.Execute(UnitCommand.MoveToRelay(Team.Player, player.Id, 0)), Is.EqualTo(CommandResult.MatchNotRunning));
        }

        [Test]
        public void DeadUnitsTakeNoOrdersAndIdsStayWithTheirUnit()
        {
            Assert.That(Unit.Find(ally.Id), Is.SameAs(ally));
            Assert.Throws<System.InvalidOperationException>(() => ally.AssignId(5));
            player.Health.TakeDamage(player.Health.Max);
            Assert.That(commands.Execute(UnitCommand.MoveToRelay(Team.Player, player.Id, 0)), Is.Not.EqualTo(CommandResult.Accepted));
        }
    }
}
