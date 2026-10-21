using System;
using System.Buffers.Binary;
using SectorLab.Battle;
using SectorLab.Units;
using UnityEngine;

namespace SectorLab.Network
{
    // Fixed 17-byte wire format for a client's order. Reading never trusts the bytes: a short buffer,
    // an unknown enum or a non-finite point is rejected before the host's MatchCommands sees it.
    public static class UnitCommandCodec
    {
        public const int Size = 17;

        public static void Write(in UnitCommand command, Span<byte> buffer)
        {
            if (buffer.Length < Size) throw new ArgumentException("Buffer is shorter than a command.", nameof(buffer));
            buffer[0] = (byte)command.Kind;
            buffer[1] = (byte)command.Issuer;
            buffer[2] = (byte)(sbyte)command.UnitId;
            buffer[3] = (byte)(sbyte)command.RelayIndex;
            buffer[4] = (byte)(sbyte)command.TargetId;
            WriteFloat(buffer.Slice(5), command.Point.x);
            WriteFloat(buffer.Slice(9), command.Point.y);
            WriteFloat(buffer.Slice(13), command.Point.z);
        }

        public static bool TryRead(ReadOnlySpan<byte> buffer, out UnitCommand command)
        {
            command = default;
            if (buffer.Length < Size) return false;
            byte kind = buffer[0], issuer = buffer[1];
            if (kind > (byte)UnitCommandKind.Attack || issuer > (byte)Team.Enemy) return false;
            int unitId = (sbyte)buffer[2], relay = (sbyte)buffer[3], target = (sbyte)buffer[4];
            var point = new Vector3(ReadFloat(buffer.Slice(5)), ReadFloat(buffer.Slice(9)), ReadFloat(buffer.Slice(13)));
            if (!IsFinite(point.x) || !IsFinite(point.y) || !IsFinite(point.z)) return false;
            command = (UnitCommandKind)kind switch
            {
                UnitCommandKind.MoveToPoint => UnitCommand.MoveToPoint((Team)issuer, unitId, point),
                UnitCommandKind.MoveToRelay => UnitCommand.MoveToRelay((Team)issuer, unitId, relay),
                _ => UnitCommand.Attack((Team)issuer, unitId, target)
            };
            return true;
        }

        internal static void WriteFloat(Span<byte> buffer, float value) =>
            BinaryPrimitives.WriteInt32LittleEndian(buffer, BitConverter.SingleToInt32Bits(value));

        internal static float ReadFloat(ReadOnlySpan<byte> buffer) =>
            BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(buffer));

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
