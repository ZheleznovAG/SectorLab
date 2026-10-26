using System;
using System.Buffers.Binary;
using SectorLab.Battle;

namespace SectorLab.Network
{
    // One complete, bounded frame in the latest-only stream. No independently sequenced halves.
    public static class FrameSnapshot
    {
        public const int HeaderSize = 27;
        public const int MaxSize = HeaderSize + 47 + 1 + UnitsSnapshot.MaxUnits * UnitsSnapshot.RecordSize;
        public static int SizeFor(MatchModel model) => HeaderSize + MatchSnapshot.SizeFor(model.Relays.Length)
            + UnitsSnapshot.SizeFor(model.Units.Length);

        public static int Write(MatchModel model, Span<byte> buffer)
        {
            int size = SizeFor(model);
            if (buffer.Length < size) throw new ArgumentException("Short frame buffer.", nameof(buffer));
            buffer[0] = MatchProtocol.Version;
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(1), model.MatchId);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(9), model.SnapshotSequence);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(13), model.SimulationTick);
            BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(17), BitConverter.DoubleToInt64Bits(model.GameSeconds));
            int section = MatchSnapshot.Write(model, buffer.Slice(HeaderSize));
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(25), (ushort)section);
            UnitsSnapshot.Write(model.Units, buffer.Slice(HeaderSize + section));
            return size;
        }

        public static bool TryHeader(ReadOnlySpan<byte> buffer, out ulong matchId, out uint sequence)
        {
            matchId = 0; sequence = 0;
            if (buffer.Length < HeaderSize || buffer[0] != MatchProtocol.Version) return false;
            matchId = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(1));
            sequence = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(9));
            return matchId != 0 && sequence != 0;
        }

        public static bool TryApply(ReadOnlySpan<byte> buffer, MatchModel model)
        {
            if (!TryHeader(buffer, out ulong matchId, out uint sequence)) return false;
            uint tick = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(13));
            double elapsed = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(17)));
            int section = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(25));
            if (!double.IsFinite(elapsed) || elapsed < 0 || elapsed > MatchRules.DurationSeconds + MatchRules.Epsilon
                || section > buffer.Length - HeaderSize || section < MatchSnapshot.SizeFor(0)) return false;
            if (model.IsSynchronized && model.MatchId == matchId
                && (sequence <= model.SnapshotSequence || tick < model.SimulationTick || elapsed < model.GameSeconds)) return false;
            ReadOnlySpan<byte> match = buffer.Slice(HeaderSize, section);
            Span<UnitView> units = stackalloc UnitView[UnitsSnapshot.MaxUnits];
            int count = UnitsSnapshot.Read(buffer.Slice(HeaderSize + section), units);
            if (count < 0 || !MatchSnapshot.TryApply(match, null, false)) return false;

            model.MatchId = matchId; model.SnapshotSequence = sequence;
            model.SimulationTick = tick; model.GameSeconds = elapsed;
            model.SetUnitCount(count);
            for (int i = 0; i < count; i++) model.SetUnit(i, units[i]);
            model.IsSynchronized = true;
            MatchSnapshot.TryApply(match, model, false);
            model.Commit();
            return true;
        }
    }
}
