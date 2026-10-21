using System;
using System.Collections.Generic;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;

namespace SectorLab.Battle
{
    // The authority's entry point for player orders. Every order is validated here — running match,
    // known living unit, issuer owns it, valid target — before it touches movement or combat.
    // Locally the commander calls Execute directly; a host would call it for each received command.
    public sealed class MatchCommands : ICommandSink
    {
        private readonly IReadOnlyList<RelayPoint> relays;
        private readonly MatchState state;

        public MatchCommands(IReadOnlyList<RelayPoint> relays, MatchState state = null)
        {
            this.relays = relays ?? throw new ArgumentNullException(nameof(relays));
            this.state = state;
        }

        // Fired after an accepted command: a log, a replay or a network relay can listen here.
        public event Action<UnitCommand> Executed;
        // Where an accepted move is heading, for the destination marker.
        public Vector3 LastDestination { get; private set; }

        public int RelayIndexOf(RelayPoint relay)
        {
            for (int i = 0; i < relays.Count; i++)
                if (relays[i] == relay) return i;
            return -1;
        }

        public bool Submit(in UnitCommand command, out Vector3 destination)
        {
            bool accepted = Execute(command) == CommandResult.Accepted;
            destination = LastDestination;
            return accepted;
        }

        public CommandResult Execute(in UnitCommand command)
        {
            if (state != null && !state.IsRunning) return CommandResult.MatchNotRunning;
            Unit unit = Unit.Find(command.UnitId);
            if (unit == null) return CommandResult.UnknownUnit;
            if (unit.Team != command.Issuer) return CommandResult.NotOwner;
            if (!unit.IsAlive) return CommandResult.UnitDead;

            CommandResult result = command.Kind switch
            {
                UnitCommandKind.MoveToPoint => Move(unit, command.Point, 0),
                UnitCommandKind.MoveToRelay => MoveToRelay(unit, command.RelayIndex),
                UnitCommandKind.Attack => Attack(unit, command.TargetId),
                _ => CommandResult.InvalidTarget
            };
            if (result == CommandResult.Accepted) Executed?.Invoke(command);
            return result;
        }

        private CommandResult MoveToRelay(Unit unit, int index)
        {
            if (index < 0 || index >= relays.Count || relays[index] == null) return CommandResult.InvalidTarget;
            RelayPoint relay = relays[index];
            if (!relay.TryGetApproachPoint(unit, out Vector3 point)) return CommandResult.Unreachable;
            return Move(unit, point, relay.GetArrivalDistance(point));
        }

        private CommandResult Move(Unit unit, Vector3 point, float stoppingDistance)
        {
            if (!unit.Movement.MoveTo(point, stoppingDistance)) return CommandResult.Unreachable;
            // Only an accepted move replaces the previous attack order.
            if (unit.Attack != null) unit.Attack.ClearTarget();
            LastDestination = point;
            return CommandResult.Accepted;
        }

        private static CommandResult Attack(Unit unit, int targetId)
        {
            Unit target = Unit.Find(targetId);
            if (unit.Attack == null || target == null || !target.IsAlive || !unit.IsHostileTo(target))
                return CommandResult.InvalidTarget;
            unit.Attack.SetTarget(target);
            return CommandResult.Accepted;
        }
    }
}
