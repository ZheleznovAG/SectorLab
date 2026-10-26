using System;
using System.Buffers.Binary;
using SectorLab.Battle;
using SectorLab.Units;

namespace SectorLab.Network
{
    // Every roster slot travels, including loading, death and respawn. Reading is atomic.
    public static class UnitsSnapshot
    {
        public const int MaxUnits = 16;
        public const int RecordSize = 28;
        public static int SizeFor(int count) => 1 + count * RecordSize;

        public static int Write(ReadOnlySpan<UnitView> units, Span<byte> buffer)
        {
            if (units.Length > MaxUnits) throw new ArgumentOutOfRangeException(nameof(units));
            int size = SizeFor(units.Length);
            if (buffer.Length < size) throw new ArgumentException("Short snapshot buffer.", nameof(buffer));
            buffer[0] = (byte)units.Length;
            for (int i = 0; i < units.Length; i++)
            {
                UnitView unit = units[i];
                Span<byte> row = buffer.Slice(1 + i * RecordSize, RecordSize);
                row[0] = (byte)unit.Id; row[1] = (byte)unit.Team;
                row[2] = (byte)unit.Role; row[3] = (byte)unit.Life;
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(4), (ushort)unit.Health);
                BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(6), (ushort)unit.MaxHealth);
                UnitCommandCodec.WriteFloat(row.Slice(8), unit.RespawnSeconds);
                BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(12), unit.Generation);
                BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(16), unit.Shots);
                UnitCommandCodec.WriteFloat(row.Slice(20), unit.X);
                UnitCommandCodec.WriteFloat(row.Slice(24), unit.Z);
            }
            return size;
        }

        public static int Read(ReadOnlySpan<byte> buffer, Span<UnitView> states)
        {
            if (buffer.Length < 1) return -1;
            int count = buffer[0];
            if (count > MaxUnits || count > states.Length || buffer.Length != SizeFor(count)) return -1;
            Span<UnitView> parsed = stackalloc UnitView[MaxUnits];
            uint ids = 0;
            for (int i = 0; i < count; i++)
            {
                ReadOnlySpan<byte> row = buffer.Slice(1 + i * RecordSize, RecordSize);
                int id = row[0], team = row[1], role = row[2], life = row[3];
                int health = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(4));
                int max = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(6));
                float respawn = UnitCommandCodec.ReadFloat(row.Slice(8));
                uint generation = BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(12));
                uint shots = BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(16));
                float x = UnitCommandCodec.ReadFloat(row.Slice(20)), z = UnitCommandCodec.ReadFloat(row.Slice(24));
                if (id >= MaxUnits || (ids & (1u << id)) != 0 || team > (int)Team.Enemy
                    || role > (int)UnitRole.Ranger || life > (int)UnitLife.Spawning
                    || !float.IsFinite(x) || !float.IsFinite(z) || !float.IsFinite(respawn) || respawn < 0
                    || health > max || (life == (int)UnitLife.Alive ? health == 0 || generation == 0 : health != 0)) return -1;
                ids |= 1u << id;
                parsed[i] = new UnitView(id, (Team)team, (UnitRole)role, (UnitLife)life,
                    health, max, respawn, generation, shots, x, z);
            }
            parsed.Slice(0, count).CopyTo(states);
            return count;
        }
    }
}
