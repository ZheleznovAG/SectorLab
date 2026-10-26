using System;

namespace SectorLab.Battle
{
    // Each reason has one owner; closing a menu cannot release loading or a user's pause.
    [Flags]
    public enum PauseReason { None = 0, User = 1, Loading = 2, Tutorial = 4, Menu = 8, WaitingForPeer = 16 }
}
