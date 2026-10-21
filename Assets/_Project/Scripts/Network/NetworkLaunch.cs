using SectorLab.Units;

namespace SectorLab.Network
{
    public enum SessionRole { Offline, Host, Client }

    // How the next Main starts: chosen by a lobby (or a test) before the scene loads.
    // The host plays blue; the remote player plays red.
    public static class NetworkLaunch
    {
        public const Team RemoteTeam = Team.Enemy;
        public static SessionRole Role { get; private set; } = SessionRole.Offline;
        public static IMessageChannel Channel { get; private set; }

        public static void Host(IMessageChannel channel) { Role = SessionRole.Host; Channel = channel; }
        public static void Client(IMessageChannel channel) { Role = SessionRole.Client; Channel = channel; }
        public static void Offline() { Role = SessionRole.Offline; Channel = null; }
    }
}
