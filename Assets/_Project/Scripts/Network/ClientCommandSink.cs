using System.Collections.Generic;
using SectorLab.Battle;
using SectorLab.Objectives;
using UnityEngine;

namespace SectorLab.Network
{
    // A client's orders go to the host; the host decides. The marker shows where we asked to go.
    public sealed class ClientCommandSink : ICommandSink
    {
        private readonly ClientSession session;
        private readonly IReadOnlyList<RelayPoint> relays;

        public ClientCommandSink(ClientSession session, IReadOnlyList<RelayPoint> relays)
        {
            this.session = session;
            this.relays = relays;
        }

        public int RelayIndexOf(RelayPoint relay)
        {
            for (int i = 0; i < relays.Count; i++)
                if (relays[i] == relay) return i;
            return -1;
        }

        public bool Submit(in UnitCommand command, out Vector3 destination)
        {
            destination = command.Point;
            if (command.Kind == UnitCommandKind.MoveToRelay)
            {
                if (command.RelayIndex < 0 || command.RelayIndex >= relays.Count) return false;
                destination = relays[command.RelayIndex].transform.position;
            }
            return session.Send(command) == SendResult.Queued;
        }
    }
}
