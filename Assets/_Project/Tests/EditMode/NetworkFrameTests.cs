using System;
using System.Buffers.Binary;
using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Network;
using SectorLab.Objectives;
using SectorLab.Units;

namespace SectorLab.Tests
{
    public class NetworkFrameTests
    {
        [Test]
        public void FlushingAnEndpointWithoutAReceiverStillConsumesTheQueue()
        {
            LoopbackChannel.CreatePair(out var sender, out _);
            sender.Send(new byte[] { 1 });
            sender.Flush();
            Assert.That(sender.Pending, Is.Zero);
        }
        private static MatchModel Model()
        {
            var model = new MatchModel { MatchId = 1, SnapshotSequence = 1, SimulationTick = 12, GameSeconds = 1.2 };
            model.SetRelayCount(3); model.SetUnitCount(2);
            model.PlayerScore.Value = 7;
            model.Relays[0].Value = new RelayView(RelayOwner.Player, 100, 1, false, true);
            model.SetUnit(0, new UnitView(0, Team.Player, UnitRole.Tank, UnitLife.Alive, 80, 100, 0, 1, 4, 1, 2));
            model.SetUnit(1, new UnitView(3, Team.Enemy, UnitRole.Tank, UnitLife.Respawning, 0, 100, 9.2f, 1, 0, 3, 4));
            return model;
        }

        private static byte[] Packet(MatchModel model)
        {
            var bytes = new byte[1 + FrameSnapshot.SizeFor(model)];
            bytes[0] = (byte)MessageType.MatchState;
            FrameSnapshot.Write(model, bytes.AsSpan(1));
            return bytes;
        }

        private static void Begin(LoopbackChannel sender, ulong match = 1, ulong config = 123)
        {
            var bytes = new byte[MatchProtocol.ControlSize];
            MatchProtocol.WriteControl(MessageType.Begin, match, config, bytes);
            sender.Send(bytes); sender.Flush();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LateMalformedUnitCannotPartiallyApplyAFrame(bool duplicate)
        {
            using var host = Model();
            using var client = new MatchModel();
            byte[] packet = Packet(host);
            int row = 1 + FrameSnapshot.HeaderSize + MatchSnapshot.SizeFor(3) + 1 + UnitsSnapshot.RecordSize;
            if (duplicate) packet[row] = 0;
            else BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(row + 24), BitConverter.SingleToInt32Bits(float.NaN));
            int commits = 0; client.Updated += () => commits++;
            Assert.That(FrameSnapshot.TryApply(packet.AsSpan(1), client), Is.False);
            Assert.That(client.Units.Length, Is.Zero);
            Assert.That(client.Relays.Length, Is.Zero);
            Assert.That(client.PlayerScore.Value, Is.Zero);
            Assert.That(client.MatchId, Is.Zero);
            Assert.That(commits, Is.Zero);
        }

        [Test]
        public void FullFramesStayConsistentAfterLossReorderingAndDuplicates()
        {
            using var host = Model();
            using var model = new MatchModel();
            LoopbackChannel.CreatePair(out var sender, out var receiver);
            using var client = new ClientSession(model, receiver, Team.Enemy, 123);
            Begin(sender);
            byte[] old = Packet(host);
            host.PlayerScore.Value = 12; host.SnapshotSequence = 3;
            host.SetUnit(0, new UnitView(0, Team.Player, UnitRole.Tank, UnitLife.Alive, 50, 100, 0, 1, 5, 7, 8));
            byte[] latest = Packet(host);
            sender.Send(latest); sender.Send(old); sender.Send(latest); sender.Flush();
            Assert.That(model.PlayerScore.Value, Is.EqualTo(12));
            Assert.That(model.Units[0].Health, Is.EqualTo(50));
            Assert.That(model.Units[0].X, Is.EqualTo(7));
            Assert.That(model.Units[1].Life, Is.EqualTo(UnitLife.Respawning));
            Assert.That(model.Units[1].RespawnSeconds, Is.EqualTo(9.2f));
            Assert.That(model.SnapshotSequence, Is.EqualTo(3));
            Assert.That(client.MalformedMessages, Is.Zero);
            Begin(sender, 2);
            sender.Send(latest); sender.Flush();
            Assert.That(client.IsSynchronized, Is.False, "A previous match's frame cannot start the new match.");
            Assert.That(model.Units.Length, Is.Zero);
        }

        [Test]
        public void IncompatibleConfigurationBlocksTheSessionAndExplainsWhy()
        {
            using var model = new MatchModel();
            LoopbackChannel.CreatePair(out var sender, out var receiver);
            using var client = new ClientSession(model, receiver, Team.Enemy, 123);
            Begin(sender, 1, 456);
            Assert.That(client.IsSynchronized, Is.False);
            Assert.That(client.Failure, Is.Not.Null.And.Not.Empty);
            Assert.That(receiver.Pending, Is.Zero, "No Ready acknowledgment for incompatible content.");
        }

        [Test]
        public void AClientOrderIsAcceptedOnlyWhenTheChannelQueuesIt()
        {
            using var host = Model();
            using var model = new MatchModel();
            LoopbackChannel.CreatePair(out var sender, out var receiver);
            using var client = new ClientSession(model, receiver, Team.Player, 123);
            var sink = new ClientCommandSink(client, Array.Empty<RelayPoint>());
            var order = UnitCommand.MoveToPoint(Team.Player, 0, UnityEngine.Vector3.zero);
            Assert.That(sink.Submit(order, out _), Is.False, "No frame yet.");
            Begin(sender); receiver.Flush(); sender.Send(Packet(host)); sender.Flush();
            receiver.Capacity = 0;
            Assert.That(client.Send(order), Is.EqualTo(SendResult.QueueFull));
            Assert.That(sink.Submit(order, out _), Is.False);
            receiver.Capacity = 1; receiver.IsConnected = false;
            Assert.That(client.Send(order), Is.EqualTo(SendResult.Disconnected));
            Assert.That(sink.Submit(order, out _), Is.False);
            receiver.IsConnected = true;
            Assert.That(sink.Submit(order, out _), Is.True);
            Assert.That(receiver.Pending, Is.EqualTo(1));
        }

        [Test]
        public void ShotEndpointRoundTripsAndNonFiniteLateEndpointLeavesTheFrameUntouched()
        {
            using var host = Model();
            host.SetUnit(0, new UnitView(0, Team.Player, UnitRole.Ranger, UnitLife.Alive, 80, 80,
                0, 1, 7, 1, 2, -3.5f, 4.25f));
            using var client = new MatchModel();
            byte[] packet = Packet(host);
            Assert.That(FrameSnapshot.TryApply(packet.AsSpan(1), client), Is.True);
            Assert.That(client.Units[0], Is.EqualTo(host.Units[0]));
            host.SnapshotSequence++;
            packet = Packet(host);
            int lastRow = 1 + FrameSnapshot.HeaderSize + MatchSnapshot.SizeFor(3) + 1 + UnitsSnapshot.RecordSize;
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(lastRow + 32), BitConverter.SingleToInt32Bits(float.NaN));
            int commits = 0;
            client.Updated += () => commits++;
            Assert.That(FrameSnapshot.TryApply(packet.AsSpan(1), client), Is.False);
            Assert.That(client.SnapshotSequence, Is.EqualTo(1));
            Assert.That(client.Units[0].ShotX, Is.EqualTo(-3.5f));
            Assert.That(commits, Is.Zero);
        }

        [Test]
        public void ProtocolTwoFramesAndHandshakesAreRejected()
        {
            using var host = Model();
            using var client = new MatchModel();
            byte[] packet = Packet(host);
            packet[1] = 2;
            Assert.That(FrameSnapshot.TryApply(packet.AsSpan(1), client), Is.False);
            byte[] control = new byte[MatchProtocol.ControlSize];
            MatchProtocol.WriteControl(MessageType.Begin, 1, 123, control);
            control[1] = 2;
            Assert.That(MatchProtocol.TryControl(control, out _, out _), Is.False);
        }

        [Test]
        public void UnitReaderPreservesAllDestinationRecordsOnALateFailure()
        {
            using var host = Model();
            var states = new UnitView[] { host.Units[1], host.Units[0] };
            var bytes = new byte[UnitsSnapshot.SizeFor(2)];
            UnitsSnapshot.Write(host.Units, bytes);
            bytes[1 + UnitsSnapshot.RecordSize] = 0; // duplicate only in the last record
            Assert.That(UnitsSnapshot.Read(bytes, states), Is.EqualTo(-1));
            Assert.That(states[0], Is.EqualTo(host.Units[1]));
            Assert.That(states[1], Is.EqualTo(host.Units[0]));
        }
    }
}
