using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SectorLab.Battle;
using SectorLab.Units;
using UnityEngine;

namespace SectorLab.Network
{
    public static class MatchConfiguration
    {
        // Calculated once after local prefabs load. BinaryWriter has a defined little-endian format.
        public static ulong Compute(BattleController battle, UnitLifecycle lifecycle)
        {
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
            {
                writer.Write(MatchProtocol.Version); writer.Write(Application.version);
                writer.Write(MatchRules.ScoreLimit); writer.Write(MatchRules.DurationSeconds);
                writer.Write(BattleController.TickSeconds); writer.Write(RespawnTimer.DelaySeconds);
                writer.Write(lifecycle.Slots.Count);
                foreach (UnitSlotState slot in lifecycle.Slots)
                {
                    writer.Write((int)slot.Config.team); writer.Write((int)slot.Config.role);
                    writer.Write(slot.Config.prefabKey); WritePosition(writer, slot.Config.position);
                    UnitStats stats = slot.Unit.Stats;
                    writer.Write(stats.maxHealth); writer.Write(stats.speed); writer.Write(stats.damage);
                    writer.Write(stats.attackRange); writer.Write(stats.attackCooldown);
                    writer.Write(slot.SpawnRadius);
                }
                writer.Write(battle.Relays.Count);
                foreach (var relay in battle.Relays)
                {
                    WritePosition(writer, relay.transform.position);
                    var config = relay.Config;
                    writer.Write((int)config.kind); writer.Write(config.radius); writer.Write(config.captureSeconds);
                    writer.Write(config.scorePerSecond); writer.Write(config.healingPerSecond);
                }
            }
            using var hash = SHA256.Create();
            return BinaryPrimitives.ReadUInt64LittleEndian(hash.ComputeHash(bytes.ToArray()));
        }

        private static void WritePosition(BinaryWriter writer, Vector3 point)
        { writer.Write(point.x); writer.Write(point.y); writer.Write(point.z); }
    }
}
