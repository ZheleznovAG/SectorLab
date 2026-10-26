using System;
using System.Collections.Generic;
using NUnit.Framework;
using R3;
using SectorLab.Battle;
using SectorLab.Network;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;

namespace SectorLab.Tests
{
    public class NetworkCodecTests
    {
        [Test]
        public void CommandsSurviveTheWireUnchanged()
        {
            var buffer = new byte[UnitCommandCodec.Size];
            UnitCommand[] commands =
            {
                UnitCommand.MoveToPoint(Team.Enemy, 4, new Vector3(1.5f, 0, -2.25f)),
                UnitCommand.MoveToRelay(Team.Player, 2, 1),
                UnitCommand.Attack(Team.Player, 0, 5),
                UnitCommand.HoldPosition(Team.Enemy, 3),
                UnitCommand.Stop(Team.Player, 0)
            };
            foreach (UnitCommand sent in commands)
            {
                UnitCommandCodec.Write(sent, buffer);
                Assert.That(UnitCommandCodec.TryRead(buffer, out UnitCommand received), Is.True);
                Assert.That(received.ToString(), Is.EqualTo(sent.ToString()));
            }
        }

        [Test]
        public void MalformedCommandsAreRejectedBeforeTheHostSeesThem()
        {
            var buffer = new byte[UnitCommandCodec.Size];
            Assert.That(UnitCommandCodec.TryRead(buffer.AsSpan(0, 10), out _), Is.False, "short");
            UnitCommandCodec.Write(UnitCommand.MoveToRelay(Team.Player, 1, 0), buffer);
            buffer[0] = 9;
            Assert.That(UnitCommandCodec.TryRead(buffer, out _), Is.False, "unknown kind");
            UnitCommandCodec.Write(UnitCommand.MoveToRelay(Team.Player, 1, 0), buffer);
            buffer[1] = 7;
            Assert.That(UnitCommandCodec.TryRead(buffer, out _), Is.False, "unknown team");
            UnitCommandCodec.Write(UnitCommand.MoveToPoint(Team.Player, 1, new Vector3(float.NaN, 0, 0)), buffer);
            Assert.That(UnitCommandCodec.TryRead(buffer, out _), Is.False, "non-finite point");
        }

        private static MatchModel HostModel()
        {
            var model = new MatchModel();
            model.SetRelayCount(3);
            model.PlayerScore.Value = 12.5;
            model.EnemyScore.Value = 30;
            model.PlayerRate.Value = 1;
            model.RemainingSeconds.Value = 97;
            model.Forecast.Value = BattleOutcome.Defeat;
            model.Relays[0].Value = new RelayView(RelayOwner.Player, 100, 1, false, true);
            model.Relays[1].Value = new RelayView(RelayOwner.Enemy, 100, 2, true, false);
            model.Relays[2].Value = new RelayView(RelayOwner.Neutral, 42, 1, false, false);
            return model;
        }

        [Test]
        public void ClientModelMatchesTheHostAfterOneSnapshotAndViewsAreNotified()
        {
            using MatchModel host = HostModel();
            using var client = new MatchModel();
            var buffer = new byte[MatchSnapshot.SizeFor(3)];
            Assert.That(MatchSnapshot.Write(host, buffer), Is.EqualTo(32));
            var timers = new List<int>();
            using var timer = client.RemainingSeconds.Subscribe(timers.Add);

            Assert.That(MatchSnapshot.TryApply(buffer, client), Is.True);
            Assert.That(client.PlayerScore.Value, Is.EqualTo(12.5));
            Assert.That(client.EnemyScore.Value, Is.EqualTo(30));
            Assert.That(client.PlayerRate.Value, Is.EqualTo(1));
            Assert.That(client.Forecast.Value, Is.EqualTo(BattleOutcome.Defeat));
            for (int i = 0; i < 3; i++) Assert.That(client.Relays[i].Value, Is.EqualTo(host.Relays[i].Value));
            Assert.That(timers, Is.EqualTo(new[] { 180, 97 }));

            Assert.That(MatchSnapshot.TryApply(buffer, client), Is.True);
            Assert.That(timers, Has.Count.EqualTo(2), "A repeated snapshot changes nothing and notifies nobody.");
        }

        [Test]
        public void MalformedSnapshotsLeaveTheClientModelUntouched()
        {
            using MatchModel host = HostModel();
            using var client = new MatchModel();
            var buffer = new byte[MatchSnapshot.SizeFor(3)];
            MatchSnapshot.Write(host, buffer);
            var corrupt = (byte[])buffer.Clone();
            corrupt[0] = 99;
            Assert.That(MatchSnapshot.TryApply(corrupt, client), Is.False, "version");
            Assert.That(MatchSnapshot.TryApply(buffer.AsSpan(0, 20), client), Is.False, "truncated");
            corrupt = (byte[])buffer.Clone();
            corrupt[24] = 200; // relay 0 percent
            Assert.That(MatchSnapshot.TryApply(corrupt, client), Is.False, "percent");
            corrupt = (byte[])buffer.Clone();
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(corrupt.AsSpan(5), BitConverter.SingleToInt32Bits(-1f));
            Assert.That(MatchSnapshot.TryApply(corrupt, client), Is.False, "negative score");
            Assert.That(client.Relays, Is.Empty);
            Assert.That(client.PlayerScore.Value, Is.Zero);
        }

        [Test]
        public void MalformedUnitSnapshotsAreRejected()
        {
            var states = new UnitView[UnitsSnapshot.MaxUnits];
            var buffer = new byte[UnitsSnapshot.SizeFor(1)];
            var sent = new UnitView(4, Team.Enemy, UnitRole.Scout, UnitLife.Alive, 80, 100, 0, 1, 0, 1, 2);
            UnitsSnapshot.Write(new[] { sent }, buffer);
            Assert.That(UnitsSnapshot.Read(buffer, states), Is.EqualTo(1));
            Assert.That(states[0], Is.EqualTo(sent));
            Assert.That(UnitsSnapshot.Read(new byte[] { 200 }, states), Is.EqualTo(-1));
            Assert.That(UnitsSnapshot.Read(buffer.AsSpan(0, 5), states), Is.EqualTo(-1));
            buffer[2] = 9;
            Assert.That(UnitsSnapshot.Read(buffer, states), Is.EqualTo(-1));
            buffer[2] = 0;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(21), BitConverter.SingleToInt32Bits(float.PositiveInfinity));
            Assert.That(UnitsSnapshot.Read(buffer, states), Is.EqualTo(-1));
            Assert.That(states[0], Is.EqualTo(sent), "Malformed data leaves the destination untouched.");
        }
    }
}
