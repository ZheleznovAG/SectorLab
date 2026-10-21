using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using SectorLab.Units;

namespace SectorLab.Network
{
    // One unit as a client draws it: where it stands and how hurt it is.
    public readonly struct UnitState : IEquatable<UnitState>
    {
        public readonly int Id;
        public readonly Team Team;
        public readonly float X, Z;
        public readonly int Health;

        public UnitState(int id, Team team, float x, float z, int health)
        {
            Id = id; Team = team; X = x; Z = z; Health = health;
        }

        public bool Equals(UnitState other) => Id == other.Id && Team == other.Team && X == other.X && Z == other.Z && Health == other.Health;
        public override bool Equals(object obj) => obj is UnitState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Id, Team, X, Z, Health);
    }

    // Living units only: a unit missing from the snapshot is dead or waiting to respawn.
    // Layout: count, then 12 bytes per unit (id, team, x, z as float32, health as uint16).
    public static class UnitsSnapshot
    {
        public const int MaxUnits = 16;
        private const int UnitSize = 12;

        public static int SizeFor(int count) => 1 + count * UnitSize;

        public static int Write(IReadOnlyList<Unit> units, Span<byte> buffer)
        {
            int count = 0;
            for (int i = 0; i < units.Count; i++)
                if (units[i] != null && units[i].IsAlive && units[i].Id >= 0) count++;
            if (count > MaxUnits) throw new ArgumentOutOfRangeException(nameof(units), "Too many units for a snapshot.");
            if (buffer.Length < SizeFor(count)) throw new ArgumentException("Buffer is shorter than the snapshot.", nameof(buffer));
            buffer[0] = (byte)count;
            int offset = 1;
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit == null || !unit.IsAlive || unit.Id < 0) continue;
                buffer[offset] = (byte)unit.Id;
                buffer[offset + 1] = (byte)unit.Team;
                UnitCommandCodec.WriteFloat(buffer.Slice(offset + 2), unit.transform.position.x);
                UnitCommandCodec.WriteFloat(buffer.Slice(offset + 6), unit.transform.position.z);
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(offset + 10), (ushort)Math.Min(ushort.MaxValue, unit.Health.Current));
                offset += UnitSize;
            }
            return offset;
        }

        // Fills states and returns how many were read, or -1 for malformed input.
        public static int Read(ReadOnlySpan<byte> buffer, Span<UnitState> states)
        {
            if (buffer.Length < 1) return -1;
            int count = buffer[0];
            if (count > MaxUnits || count > states.Length || buffer.Length < SizeFor(count)) return -1;
            for (int i = 0; i < count; i++)
            {
                int offset = 1 + i * UnitSize;
                int id = buffer[offset], team = buffer[offset + 1];
                float x = UnitCommandCodec.ReadFloat(buffer.Slice(offset + 2)), z = UnitCommandCodec.ReadFloat(buffer.Slice(offset + 6));
                if (id >= MaxUnits || team > (int)Team.Enemy || !float.IsFinite(x) || !float.IsFinite(z)) return -1;
                states[i] = new UnitState(id, (Team)team, x, z, BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(offset + 10)));
            }
            return count;
        }
    }
}
