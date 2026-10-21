using SectorLab.Objectives;
using UnityEngine;

namespace SectorLab.Battle
{
    // Where a device's orders go: straight into the local authority, or over the network to a host.
    public interface ICommandSink
    {
        int RelayIndexOf(RelayPoint relay);
        // True when the order was accepted (or, remotely, sent); destination is for the marker.
        bool Submit(in UnitCommand command, out Vector3 destination);
    }
}
