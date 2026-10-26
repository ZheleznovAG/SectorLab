using System;
using Unity.Networking.Transport;

namespace SectorLab.Network
{
    // IMessageChannel over Unity Transport (UDP), one peer per side. Orders travel reliably and in
    // order; snapshots travel unreliably and late ones are dropped, because only the newest matters.
    // Call Update once per frame (NetworkPump does) to move packets and raise events.
    public sealed class UtpChannel : IMessageChannel, IConnectionStatus, IDisposable
    {
        private NetworkDriver driver;
        private readonly NetworkPipeline reliable, latestOnly;
        private NetworkConnection connection;
        private readonly bool isServer;
        private readonly byte[] receiveBuffer = new byte[1400];

        public event Action<ArraySegment<byte>> Received;
        public event Action Connected, Disconnected;
        public bool IsConnected => connection.IsCreated && driver.IsCreated
            && driver.GetConnectionState(connection) == NetworkConnection.State.Connected;
        public int DroppedSends { get; private set; }

        private UtpChannel(bool server)
        {
            isServer = server;
            driver = NetworkDriver.Create();
            reliable = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            latestOnly = driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage));
        }

        public static UtpChannel Listen(ushort port)
        {
            var channel = new UtpChannel(true);
            if (channel.driver.Bind(NetworkEndpoint.AnyIpv4.WithPort(port)) != 0 || channel.driver.Listen() != 0)
            {
                channel.Dispose();
                throw new InvalidOperationException($"Cannot listen on UDP port {port}.");
            }
            return channel;
        }

        public static UtpChannel Connect(string address, ushort port)
        {
            if (!NetworkEndpoint.TryParse(address, port, out NetworkEndpoint endpoint))
                throw new ArgumentException("Not an IP address: " + address, nameof(address));
            var channel = new UtpChannel(false);
            channel.connection = channel.driver.Connect(endpoint);
            return channel;
        }

        public void Update()
        {
            if (!driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();
            if (isServer)
            {
                NetworkConnection incoming;
                while ((incoming = driver.Accept()) != default)
                {
                    if (connection.IsCreated) { driver.Disconnect(incoming); continue; } // one remote player
                    connection = incoming;
                    Connected?.Invoke();
                }
            }
            NetworkEvent.Type type;
            while ((type = driver.PopEvent(out NetworkConnection from, out var reader)) != NetworkEvent.Type.Empty)
            {
                if (from != connection) continue;
                if (type == NetworkEvent.Type.Connect) Connected?.Invoke();
                else if (type == NetworkEvent.Type.Disconnect)
                {
                    connection = default;
                    Disconnected?.Invoke();
                }
                else if (type == NetworkEvent.Type.Data)
                {
                    int length = reader.Length;
                    if (length <= 0 || length > receiveBuffer.Length) continue;
                    reader.ReadBytes(receiveBuffer.AsSpan(0, length));
                    // Valid only during the callback; sessions copy what they keep.
                    Received?.Invoke(new ArraySegment<byte>(receiveBuffer, 0, length));
                }
            }
        }

        public SendResult Send(ReadOnlySpan<byte> message)
        {
            if (message.Length == 0 || message.Length > receiveBuffer.Length) return Dropped(SendResult.InvalidMessage);
            if (!IsConnected) return Dropped(SendResult.Disconnected);
            NetworkPipeline pipe = message[0] == (byte)MessageType.MatchState ? latestOnly : reliable;
            int started = driver.BeginSend(pipe, connection, out var writer);
            if (started != 0) return Dropped(Status(started));
            Span<byte> copy = stackalloc byte[message.Length];
            message.CopyTo(copy);
            if (!writer.WriteBytes(copy)) { driver.AbortSend(writer); return Dropped(SendResult.InvalidMessage); }
            int ended = driver.EndSend(writer);
            return ended >= 0 ? SendResult.Queued : Dropped(Status(ended));
        }

        private SendResult Dropped(SendResult result) { DroppedSends++; return result; }
        private static SendResult Status(int code) => code == (int)Unity.Networking.Transport.Error.StatusCode.NetworkSendQueueFull
            ? SendResult.QueueFull : SendResult.Failed;

        public void Dispose()
        {
            if (!driver.IsCreated) return;
            if (connection.IsCreated) { driver.Disconnect(connection); driver.ScheduleUpdate().Complete(); }
            driver.Dispose();
        }
    }
}
