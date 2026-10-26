using System;
using System.Buffers.Binary;
using SectorLab.Battle;
using SectorLab.Objectives;

namespace SectorLab.Network
{
    // The viewer-facing match state the host sends to clients, a few times a second.
    // Layout: header (version, relay count, flags, outcome, forecast), scores and income as
    // float32, remaining seconds as uint16, then 3 bytes per relay. 3 relays fit in 32 bytes.
    public static class MatchSnapshot
    {
        public const byte Version = 1;
        public const int MaxRelays = 8;
        private const int HeaderSize = 5, ValuesSize = 4 * 4 + 2, RelaySize = 3;

        public static int SizeFor(int relayCount) => HeaderSize + ValuesSize + relayCount * RelaySize;

        public static int Write(MatchModel model, Span<byte> buffer)
        {
            int relays = model.Relays.Length;
            if (relays > MaxRelays) throw new ArgumentOutOfRangeException(nameof(model), "Too many relays for a snapshot.");
            int size = SizeFor(relays);
            if (buffer.Length < size) throw new ArgumentException("Buffer is shorter than the snapshot.", nameof(buffer));
            buffer[0] = Version;
            buffer[1] = (byte)relays;
            buffer[2] = (byte)(model.Paused.Value ? 1 : 0);
            buffer[3] = (byte)model.Outcome.Value;
            buffer[4] = (byte)model.Forecast.Value;
            UnitCommandCodec.WriteFloat(buffer.Slice(5), (float)model.PlayerScore.Value);
            UnitCommandCodec.WriteFloat(buffer.Slice(9), (float)model.EnemyScore.Value);
            UnitCommandCodec.WriteFloat(buffer.Slice(13), (float)model.PlayerRate.Value);
            UnitCommandCodec.WriteFloat(buffer.Slice(17), (float)model.EnemyRate.Value);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(21), (ushort)Math.Max(0, Math.Min(ushort.MaxValue, model.RemainingSeconds.Value)));
            for (int i = 0; i < relays; i++)
            {
                RelayView view = model.Relays[i].Value;
                int offset = HeaderSize + ValuesSize + i * RelaySize;
                buffer[offset] = (byte)((int)view.Owner | view.Side << 2 | (view.Contested ? 16 : 0) | (view.OwnerPresent ? 32 : 0));
                buffer[offset + 1] = (byte)view.Percent;
                buffer[offset + 2] = 0; // reserved
            }
            return size;
        }

        // Applies a received snapshot to a client's model. Malformed input leaves the model untouched.
        public static bool TryApply(ReadOnlySpan<byte> buffer, MatchModel model, bool commit = true)
        {
            if (buffer.Length < HeaderSize || buffer[0] != Version) return false;
            int relays = buffer[1];
            if (relays > MaxRelays || buffer.Length != SizeFor(relays)) return false;
            if (buffer[2] > 1 || buffer[3] > (byte)BattleOutcome.Draw || buffer[4] > (byte)BattleOutcome.Draw) return false;
            Span<RelayView> views = stackalloc RelayView[MaxRelays];
            for (int i = 0; i < relays; i++)
            {
                int offset = HeaderSize + ValuesSize + i * RelaySize;
                byte bits = buffer[offset];
                int owner = bits & 3, side = bits >> 2 & 3, percent = buffer[offset + 1];
                if (owner > (int)RelayOwner.Enemy || side > 2 || percent > 100 || (bits & 0xC0) != 0 || buffer[offset + 2] != 0) return false;
                views[i] = new RelayView((RelayOwner)owner, percent, side, (bits & 16) != 0, (bits & 32) != 0);
            }
            float player = UnitCommandCodec.ReadFloat(buffer.Slice(5)), enemy = UnitCommandCodec.ReadFloat(buffer.Slice(9));
            float playerRate = UnitCommandCodec.ReadFloat(buffer.Slice(13)), enemyRate = UnitCommandCodec.ReadFloat(buffer.Slice(17));
            if (!Valid(player) || !Valid(enemy) || !Valid(playerRate) || !Valid(enemyRate)) return false;
            if (model == null) return true; // Validation pass for a complete frame, before any observable changes.

            model.SetRelayCount(relays);
            for (int i = 0; i < relays; i++) model.Relays[i].Value = views[i];
            model.PlayerScore.Value = player;
            model.EnemyScore.Value = enemy;
            model.PlayerRate.Value = playerRate;
            model.EnemyRate.Value = enemyRate;
            model.RemainingSeconds.Value = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(21));
            model.Paused.Value = buffer[2] == 1;
            model.Forecast.Value = (BattleOutcome)buffer[4];
            model.Outcome.Value = (BattleOutcome)buffer[3];
            if (commit) model.Commit();
            return true;
        }

        private static bool Valid(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
