using System;
using System.Buffers.Binary;
using SectorLab.Battle;

namespace SectorLab.Network
{
    public static class MatchProtocol
    {
        public const byte Version = 2;
        public const int ControlSize = 18;
        public const int CommandHeaderSize = 18;
        public const int CommandSize = CommandHeaderSize + UnitCommandCodec.Size;

        public static void WriteControl(MessageType type, ulong match, ulong configuration, Span<byte> bytes)
        {
            bytes[0] = (byte)type; bytes[1] = Version;
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(2), match);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(10), configuration);
        }

        public static bool TryControl(ReadOnlySpan<byte> bytes, out ulong match, out ulong configuration)
        {
            match = configuration = 0;
            if (bytes.Length != ControlSize || bytes[1] != Version) return false;
            match = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(2));
            configuration = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(10));
            return match != 0;
        }

        public static void WriteCommand(in UnitCommand command, ulong match, uint actor, uint target, Span<byte> bytes)
        {
            bytes[0] = (byte)MessageType.Command; bytes[1] = Version;
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(2), match);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(10), actor);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(14), target);
            UnitCommandCodec.Write(command, bytes.Slice(CommandHeaderSize));
        }

        public static bool TryCommand(ReadOnlySpan<byte> bytes, out UnitCommand command,
            out ulong match, out uint actor, out uint target)
        {
            command = default; match = 0; actor = target = 0;
            if (bytes.Length != CommandSize || bytes[0] != (byte)MessageType.Command || bytes[1] != Version) return false;
            match = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(2));
            actor = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(10));
            target = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(14));
            return match != 0 && UnitCommandCodec.TryRead(bytes.Slice(CommandHeaderSize), out command);
        }
    }
}
