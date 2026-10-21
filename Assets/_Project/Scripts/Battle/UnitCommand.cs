using UnityEngine;
using SectorLab.Units;

namespace SectorLab.Battle
{
    public enum UnitCommandKind { MoveToPoint, MoveToRelay, Attack }

    public enum CommandResult { Accepted, MatchNotRunning, UnknownUnit, NotOwner, UnitDead, InvalidTarget, Unreachable }

    // An order as plain data: ids and numbers only, so it can cross a network boundary unchanged.
    // Issuer is the team that sent it; the executing side checks it against the unit's team.
    public readonly struct UnitCommand
    {
        public readonly UnitCommandKind Kind;
        public readonly Team Issuer;
        public readonly int UnitId;
        public readonly int RelayIndex;
        public readonly int TargetId;
        public readonly Vector3 Point;

        private UnitCommand(UnitCommandKind kind, Team issuer, int unitId, int relayIndex, int targetId, Vector3 point)
        {
            Kind = kind; Issuer = issuer; UnitId = unitId; RelayIndex = relayIndex; TargetId = targetId; Point = point;
        }

        public static UnitCommand MoveToPoint(Team issuer, int unitId, Vector3 point) =>
            new UnitCommand(UnitCommandKind.MoveToPoint, issuer, unitId, -1, -1, point);

        public static UnitCommand MoveToRelay(Team issuer, int unitId, int relayIndex) =>
            new UnitCommand(UnitCommandKind.MoveToRelay, issuer, unitId, relayIndex, -1, default);

        public static UnitCommand Attack(Team issuer, int unitId, int targetId) =>
            new UnitCommand(UnitCommandKind.Attack, issuer, unitId, -1, targetId, default);

        public override string ToString() => $"{Kind} by {Issuer}: unit {UnitId}, relay {RelayIndex}, target {TargetId}, point {Point}";
    }
}
