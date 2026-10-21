using System;
using UnityEngine;

namespace SectorLab.Network
{
    // Optional connection state for channels that can be offline (a loopback is always connected).
    public interface IConnectionStatus
    {
        bool IsConnected { get; }
        event Action Connected, Disconnected;
    }

    // Keeps the UDP channel alive across Main restarts and moves its packets every frame,
    // before gameplay scripts read or write.
    [DefaultExecutionOrder(-1000)]
    public sealed class NetworkPump : MonoBehaviour
    {
        private static NetworkPump instance;
        private UtpChannel channel;

        public static void Run(UtpChannel channel)
        {
            Stop();
            var host = new GameObject("SectorLab Network");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<NetworkPump>();
            instance.channel = channel;
        }

        public static void Stop()
        {
            if (instance == null) return;
            instance.channel?.Dispose();
            instance.channel = null;
            Destroy(instance.gameObject);
            instance = null;
        }

        private void Update() => channel?.Update();
        private void OnDestroy() => channel?.Dispose();
    }

    // Entry points for a lobby, a test or the command line:
    //   SectorLab.exe -sectorlab-host [port]
    //   SectorLab.exe -sectorlab-join 192.168.1.20 [port]
    public static class NetworkLauncher
    {
        public const ushort DefaultPort = 7777;

        public static UtpChannel StartHost(ushort port = DefaultPort)
        {
            UtpChannel channel = UtpChannel.Listen(port);
            NetworkPump.Run(channel);
            NetworkLaunch.Host(channel);
            return channel;
        }

        public static UtpChannel Join(string address, ushort port = DefaultPort)
        {
            UtpChannel channel = UtpChannel.Connect(address, port);
            NetworkPump.Run(channel);
            NetworkLaunch.Client(channel);
            return channel;
        }

        public static void Leave()
        {
            NetworkPump.Stop();
            NetworkLaunch.Offline();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void FromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-sectorlab-host") StartHost(Port(args, i + 1));
                else if (args[i] == "-sectorlab-join" && i + 1 < args.Length) Join(args[i + 1], Port(args, i + 2));
            }
        }

        // IPv4 addresses a phone on the same Wi-Fi can join.
        public static string LocalAddressesText()
        {
            var text = new System.Text.StringBuilder();
            try
            {
                foreach (var address in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                {
                    if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                    if (text.Length > 0) text.Append(", ");
                    text.Append(address);
                }
            }
            catch (System.Net.Sockets.SocketException) { }
            return text.Length > 0 ? text.ToString() : "неизвестен";
        }

        private static ushort Port(string[] args, int index) =>
            index < args.Length && ushort.TryParse(args[index], out ushort port) ? port : DefaultPort;
    }
}
