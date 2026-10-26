using System;
using SectorLab.Battle;
using SectorLab.Network;
using SectorLab.Units;
using UnityEngine;

namespace SectorLab.Tests
{
    internal static class NetworkTestPeer
    {
        public static void Connect(HostSession host, LoopbackChannel hostEnd, LoopbackChannel clientEnd)
        {
            host.SendState(); hostEnd.Flush(); clientEnd.Flush(); hostEnd.Flush();
        }

        public static void Begin(MatchModel model, LoopbackChannel hostEnd, LoopbackChannel clientEnd)
        {
            if (model.MatchId == 0) model.MatchId = 1;
            var bytes = new byte[MatchProtocol.ControlSize];
            MatchProtocol.WriteControl(MessageType.Begin, model.MatchId,
                UnityEngine.Object.FindAnyObjectByType<NetworkMatchBootstrap>().Configuration, bytes);
            hostEnd.Send(bytes); hostEnd.Flush(); clientEnd.Flush();
        }

        public static void Send(MatchModel model, LoopbackChannel hostEnd)
        {
            model.SnapshotSequence++;
            var bytes = new byte[1 + FrameSnapshot.SizeFor(model)];
            bytes[0] = (byte)MessageType.MatchState;
            FrameSnapshot.Write(model, bytes.AsSpan(1));
            hostEnd.Send(bytes); hostEnd.Flush();
        }

        public static UnitView View(Unit unit, bool alive = true, int health = -1, uint generation = 1)
        {
            Vector3 position = unit.transform.position;
            return new UnitView(unit.Id, unit.Team, unit.Stats.role, alive ? UnitLife.Alive : UnitLife.Respawning,
                alive ? (health < 0 ? unit.Health.Max : health) : 0, unit.Health.Max,
                alive ? 0 : 10, generation, 0, position.x, position.z);
        }
    }
}
