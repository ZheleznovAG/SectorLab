using System;
using System.Collections.Generic;
using SectorLab.Battle;
using SectorLab.Units;

namespace SectorLab.Network
{
    public enum SendResult { Queued, Disconnected, QueueFull, InvalidMessage, Failed }

    public interface IMessageChannel
    {
        SendResult Send(ReadOnlySpan<byte> message);
        event Action<ArraySegment<byte>> Received;
    }

    // 3 was the obsolete partial Units packet. Never reuse it.
    public enum MessageType : byte { Command = 1, MatchState = 2, Begin = 4, Ready = 5, Reject = 6 }

    public sealed class HostSession : IDisposable
    {
        private readonly MatchCommands commands;
        private readonly MatchModel model;
        private readonly IMessageChannel channel;
        private readonly byte[] buffer = new byte[1 + FrameSnapshot.MaxSize];
        private readonly ulong configuration;
        public Team ClientTeam { get; }
        public ulong MatchId { get; }
        public bool Ready { get; private set; }
        public int Accepted { get; private set; }
        public int Rejected { get; private set; }
        public CommandResult LastResult { get; private set; }
        public event Action PeerReady;

        public HostSession(MatchCommands commands, MatchModel model, IMessageChannel channel,
            Team clientTeam, ulong matchId, ulong configuration)
        {
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
            if (matchId == 0) throw new ArgumentOutOfRangeException(nameof(matchId));
            ClientTeam = clientTeam; MatchId = matchId; this.configuration = configuration;
            model.MatchId = matchId; model.SnapshotSequence = 0;
            channel.Received += OnReceived;
        }

        private void OnReceived(ArraySegment<byte> message)
        {
            ReadOnlySpan<byte> data = message.AsSpan();
            if (data.Length > 0 && data[0] == (byte)MessageType.Ready)
            {
                if (!MatchProtocol.TryControl(data, out ulong epoch, out ulong config)
                    || epoch != MatchId || config != configuration) { Rejected++; return; }
                if (!Ready) { Ready = true; PeerReady?.Invoke(); }
                SendState();
                return;
            }
            if (!Ready || !MatchProtocol.TryCommand(data, out UnitCommand sent, out ulong match,
                out uint actorGeneration, out uint targetGeneration) || match != MatchId
                || !model.TryGetUnit(sent.UnitId, out UnitView actor) || !actor.Alive || actor.Generation != actorGeneration
                || (sent.Kind == UnitCommandKind.Attack && (!model.TryGetUnit(sent.TargetId, out UnitView target)
                    || !target.Alive || target.Generation != targetGeneration)))
            { Rejected++; return; }
            LastResult = commands.Execute(sent.WithIssuer(ClientTeam));
            if (LastResult == CommandResult.Accepted) Accepted++;
            else Rejected++;
        }

        public void PeerDisconnected() => Ready = false;

        public SendResult SendState()
        {
            if (!Ready)
            {
                MatchProtocol.WriteControl(MessageType.Begin, MatchId, configuration, buffer);
                return channel.Send(buffer.AsSpan(0, MatchProtocol.ControlSize));
            }
            model.SnapshotSequence++;
            buffer[0] = (byte)MessageType.MatchState;
            int size = FrameSnapshot.Write(model, buffer.AsSpan(1));
            return channel.Send(buffer.AsSpan(0, size + 1));
        }

        public void Dispose() => channel.Received -= OnReceived;
    }

    public sealed class ClientSession : IDisposable
    {
        private readonly MatchModel model;
        private readonly IMessageChannel channel;
        private readonly ulong configuration;
        private readonly byte[] commandBuffer = new byte[MatchProtocol.CommandSize];
        private readonly byte[] controlBuffer = new byte[MatchProtocol.ControlSize];
        private readonly IConnectionStatus status;
        public Team Team { get; }
        public int UnitCount => model.Units.Length;
        public ReadOnlySpan<UnitView> Units => model.Units;
        public int MalformedMessages { get; private set; }
        public bool IsSynchronized => model.IsSynchronized;
        public string Failure { get; private set; }
        public event Action UnitsUpdated, MatchStarted;
        public event Action<string> Failed;
        public event Action<SendResult> SendFailed;

        public ClientSession(MatchModel model, IMessageChannel channel, Team team, ulong configuration)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
            this.configuration = configuration;
            Team = team; model.IsSynchronized = false;
            channel.Received += OnReceived;
            status = channel as IConnectionStatus;
            if (status != null) status.Disconnected += Disconnect;
        }

        public void Disconnect()
        {
            model.IsSynchronized = false;
            model.Commit();
        }

        public SendResult Send(in UnitCommand command)
        {
            SendResult result;
            if (!model.IsSynchronized || Failure != null || (status != null && !status.IsConnected)) result = SendResult.Disconnected;
            else if (!model.TryGetUnit(command.UnitId, out UnitView actor) || !actor.Alive) result = SendResult.InvalidMessage;
            else
            {
                uint target = model.TryGetUnit(command.TargetId, out UnitView targetView) ? targetView.Generation : 0;
                MatchProtocol.WriteCommand(command, model.MatchId, actor.Generation, target, commandBuffer);
                result = channel.Send(commandBuffer);
            }
            if (result != SendResult.Queued) SendFailed?.Invoke(result);
            return result;
        }

        private void OnReceived(ArraySegment<byte> message)
        {
            ReadOnlySpan<byte> data = message.AsSpan();
            if (data.Length > 0 && data[0] == (byte)MessageType.Begin)
            {
                if (!MatchProtocol.TryControl(data, out ulong match, out ulong config) || config != configuration)
                {
                    Failure = "Версии игры или настройки матча различаются. Установите одинаковую сборку.";
                    model.IsSynchronized = false; model.Commit(); Failed?.Invoke(Failure); return;
                }
                if (match < model.MatchId) return;
                if (match != model.MatchId)
                {
                    model.MatchId = match; model.SnapshotSequence = model.SimulationTick = 0;
                    model.GameSeconds = 0; model.IsSynchronized = false; model.SetUnitCount(0);
                    model.Outcome.Value = BattleOutcome.InProgress;
                    Failure = null;
                    MatchStarted?.Invoke(); model.Commit();
                }
                MatchProtocol.WriteControl(MessageType.Ready, match, configuration, controlBuffer);
                channel.Send(controlBuffer);
                return;
            }
            if (data.Length > 0 && data[0] == (byte)MessageType.MatchState
                && FrameSnapshot.TryHeader(data.Slice(1), out ulong epoch, out uint sequence))
            {
                if (epoch != model.MatchId || sequence <= model.SnapshotSequence || Failure != null) return;
                if (FrameSnapshot.TryApply(data.Slice(1), model)) { UnitsUpdated?.Invoke(); return; }
            }
            MalformedMessages++;
        }

        public void Dispose()
        {
            channel.Received -= OnReceived;
            if (status != null) status.Disconnected -= Disconnect;
        }
    }

    // Delivery is explicit so tests can keep messages in flight across scene/session changes.
    public sealed class LoopbackChannel : IMessageChannel
    {
        private readonly Queue<byte[]> outgoing = new Queue<byte[]>();
        private LoopbackChannel peer;
        public event Action<ArraySegment<byte>> Received;
        public int Pending => outgoing.Count;
        public int Capacity { get; set; } = int.MaxValue;
        public bool IsConnected { get; set; } = true;

        public static void CreatePair(out LoopbackChannel host, out LoopbackChannel client)
        {
            host = new LoopbackChannel(); client = new LoopbackChannel();
            host.peer = client; client.peer = host;
        }

        public SendResult Send(ReadOnlySpan<byte> message)
        {
            if (message.Length == 0 || message.Length > 1400) return SendResult.InvalidMessage;
            if (!IsConnected) return SendResult.Disconnected;
            if (outgoing.Count >= Capacity) return SendResult.QueueFull;
            outgoing.Enqueue(message.ToArray()); return SendResult.Queued;
        }

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
