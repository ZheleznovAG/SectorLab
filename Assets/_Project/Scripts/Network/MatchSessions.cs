using System;
using System.Collections.Generic;
using SectorLab.Battle;
using SectorLab.Units;

namespace SectorLab.Network
{
    // A reliable, ordered byte pipe to the other side. Netcode's named messages, a socket or an
    // in-memory loopback can implement it; sessions never see the transport.
    public interface IMessageChannel
    {
        void Send(ReadOnlySpan<byte> message);
        event Action<ArraySegment<byte>> Received;
    }

    public enum MessageType : byte { Command = 1, MatchState = 2, Units = 3 }

    // Runs next to the authoritative match. Every received order is re-issued as the team this
    // connection was assigned: a client cannot command the other side by forging the issuer byte.
    public sealed class HostSession : IDisposable
    {
        private readonly MatchCommands commands;
        private readonly MatchModel model;
        private readonly IMessageChannel channel;
        private readonly byte[] buffer = new byte[1 + Math.Max(MatchSnapshot.SizeFor(MatchSnapshot.MaxRelays), UnitsSnapshot.SizeFor(UnitsSnapshot.MaxUnits))];

        public Team ClientTeam { get; }
        public int Accepted { get; private set; }
        public int Rejected { get; private set; }
        public CommandResult LastResult { get; private set; }

        public HostSession(MatchCommands commands, MatchModel model, IMessageChannel channel, Team clientTeam)
        {
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
            ClientTeam = clientTeam;
            channel.Received += OnReceived;
        }

        private void OnReceived(ArraySegment<byte> message)
        {
            ReadOnlySpan<byte> data = message.AsSpan();
            if (data.Length < 1 || data[0] != (byte)MessageType.Command || !UnitCommandCodec.TryRead(data.Slice(1), out UnitCommand sent))
            {
                Rejected++;
                return;
            }
            LastResult = commands.Execute(sent.WithIssuer(ClientTeam));
            if (LastResult == CommandResult.Accepted) Accepted++;
            else Rejected++;
        }

        // Sends the current state; call after each match tick (10 Hz).
        public void SendState(IReadOnlyList<Unit> units)
        {
            buffer[0] = (byte)MessageType.MatchState;
            int size = MatchSnapshot.Write(model, buffer.AsSpan(1));
            channel.Send(buffer.AsSpan(0, size + 1));
            buffer[0] = (byte)MessageType.Units;
            size = UnitsSnapshot.Write(units, buffer.AsSpan(1));
            channel.Send(buffer.AsSpan(0, size + 1));
        }

        public void Dispose() => channel.Received -= OnReceived;
    }

    // Runs on the remote device: sends orders, fills its MatchModel and unit states from the host.
    public sealed class ClientSession : IDisposable
    {
        private readonly MatchModel model;
        private readonly IMessageChannel channel;
        private readonly byte[] commandBuffer = new byte[1 + UnitCommandCodec.Size];
        private readonly UnitState[] units = new UnitState[UnitsSnapshot.MaxUnits];

        public Team Team { get; }
        public int UnitCount { get; private set; }
        public ReadOnlySpan<UnitState> Units => units.AsSpan(0, UnitCount);
        public int MalformedMessages { get; private set; }
        public event Action UnitsUpdated;

        public ClientSession(MatchModel model, IMessageChannel channel, Team team)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
            Team = team;
            channel.Received += OnReceived;
        }

        public void Send(in UnitCommand command)
        {
            commandBuffer[0] = (byte)MessageType.Command;
            UnitCommandCodec.Write(command, commandBuffer.AsSpan(1));
            channel.Send(commandBuffer);
        }

        private void OnReceived(ArraySegment<byte> message)
        {
            ReadOnlySpan<byte> data = message.AsSpan();
            bool ok = data.Length > 0;
            if (ok && data[0] == (byte)MessageType.MatchState) ok = MatchSnapshot.TryApply(data.Slice(1), model);
            else if (ok && data[0] == (byte)MessageType.Units)
            {
                int count = UnitsSnapshot.Read(data.Slice(1), units);
                ok = count >= 0;
                if (ok)
                {
                    UnitCount = count;
                    UnitsUpdated?.Invoke();
                }
            }
            else ok = false;
            if (!ok) MalformedMessages++;
        }

        public void Dispose() => channel.Received -= OnReceived;
    }

    // Two connected in-memory endpoints. Messages are copied, as a real transport would, and
    // delivered when Flush is called, so tests control latency and ordering.
    public sealed class LoopbackChannel : IMessageChannel
    {
        private readonly Queue<byte[]> outgoing = new Queue<byte[]>();
        private LoopbackChannel peer;
        public event Action<ArraySegment<byte>> Received;
        public int Pending => outgoing.Count;

        public static void CreatePair(out LoopbackChannel host, out LoopbackChannel client)
        {
            host = new LoopbackChannel();
            client = new LoopbackChannel();
            host.peer = client;
            client.peer = host;
        }

        public void Send(ReadOnlySpan<byte> message) => outgoing.Enqueue(message.ToArray());

        public void Flush()
        {
            while (outgoing.Count > 0)
            {
                byte[] message = outgoing.Dequeue();
                peer.Received?.Invoke(new ArraySegment<byte>(message));
            }
        }
    }
}
