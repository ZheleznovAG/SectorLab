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
        private static string nextNotice;
        private static ulong hostMatch;
        public static ulong NextHostMatch() => checked(++hostMatch);

        public static void Host(IMessageChannel channel) { Role = SessionRole.Host; Channel = channel; hostMatch = 0; nextNotice = null; }
        public static void Client(IMessageChannel channel) { Role = SessionRole.Client; Channel = channel; nextNotice = null; }
        public static void Offline(string notice = null) { Role = SessionRole.Offline; Channel = null; nextNotice = notice; }
        public static string TakeNotice() { string notice = nextNotice; nextNotice = null; return notice; }
    }
}
