using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SectorLab.AI;
using SectorLab.Battle;
using SectorLab.Network;
using SectorLab.Objectives;
using SectorLab.Player;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Unit = SectorLab.Units.Unit;

namespace SectorLab.Tests
{
    // Main started as a host or as a client through NetworkLaunch, the other side faked in memory.
    public class NetworkMatchTests
    {
        private LoopbackChannel hostEnd, clientEnd;

        [SetUp]
        public void CreateChannel() => LoopbackChannel.CreatePair(out hostEnd, out clientEnd);

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            NetworkLaunch.Offline();
            Time.timeScale = 1;
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("AfterNetworkMatch");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
        }

        private static IEnumerator LoadMain()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return MainLifecycleTests.WaitForReady();
        }

        [UnityTest]
        public IEnumerator HostRunsTheMatchHandsRedToTheRemotePlayerAndStreamsState()
        {
            NetworkLaunch.Host(hostEnd);
            yield return LoadMain();
            var bootstrap = Object.FindAnyObjectByType<NetworkMatchBootstrap>();
            Assert.That(bootstrap.Role, Is.EqualTo(SessionRole.Host));
            Assert.That(Object.FindAnyObjectByType<EnemyTacticsController>().enabled, Is.False, "A human replaces the red AI.");

            using var remoteModel = new MatchModel();
            using var remote = new ClientSession(remoteModel, clientEnd, NetworkLaunch.RemoteTeam);
            var battle = Object.FindAnyObjectByType<BattleController>();
            // Wait on game time: a busy machine may run far fewer game seconds than real ones.
            float deadline = Time.realtimeSinceStartup + 10;
            while (battle.State.Clock.ElapsedSeconds < 1.2 && Time.realtimeSinceStartup < deadline) yield return null;
            hostEnd.Flush();
            Assert.That(remoteModel.RemainingSeconds.Value, Is.EqualTo(battle.Model.RemainingSeconds.Value));
            Assert.That(remoteModel.RemainingSeconds.Value, Is.LessThan(180),
                $"paused={battle.State.Clock.IsPaused} outcome={battle.Outcome} timeScale={Time.timeScale} enabled={battle.enabled} elapsed={battle.State.Clock.ElapsedSeconds}");
            Assert.That(remote.UnitCount, Is.EqualTo(6));

            var lifecycle = Object.FindAnyObjectByType<UnitLifecycle>();
            Unit red = lifecycle.Slots[4].Unit;
            remote.Send(UnitCommand.MoveToRelay(Team.Enemy, red.Id, 1));
            remote.Send(UnitCommand.MoveToRelay(Team.Enemy, lifecycle.Slots[0].Unit.Id, 1)); // blue: not theirs
            clientEnd.Flush();
            Assert.That(bootstrap.Host.Accepted, Is.EqualTo(1));
            Assert.That(bootstrap.Host.Rejected, Is.EqualTo(1));
            Assert.That(red.Movement.IsMoving, Is.True);
        }

        [UnityTest]
        public IEnumerator ClientDrawsTheHostsStateAndSendsOrdersInsteadOfMovingUnits()
        {
            NetworkLaunch.Client(clientEnd);
            yield return LoadMain();
            var bootstrap = Object.FindAnyObjectByType<NetworkMatchBootstrap>();
            var battle = Object.FindAnyObjectByType<BattleController>();
            var lifecycle = Object.FindAnyObjectByType<UnitLifecycle>();
            var commander = Object.FindAnyObjectByType<UnitCommander>();
            Assert.That(bootstrap.Role, Is.EqualTo(SessionRole.Client));
            Assert.That(battle.enabled, Is.False, "The client never simulates.");
            Assert.That(commander.LocalTeam, Is.EqualTo(Team.Enemy));

            using var hostModel = new MatchModel();
            hostModel.SetRelayCount(3);
            hostModel.PlayerScore.Value = 11;
            hostModel.EnemyScore.Value = 20;
            hostModel.EnemyRate.Value = 1;
            hostModel.RemainingSeconds.Value = 75;
            hostModel.Relays[1].Value = new RelayView(RelayOwner.Enemy, 100, 2, false, true);
            var message = new byte[1 + MatchSnapshot.SizeFor(3)];
            message[0] = (byte)MessageType.MatchState;
            MatchSnapshot.Write(hostModel, message.AsSpan(1));
            hostEnd.Send(message);

            Unit red = lifecycle.Slots[4].Unit, blue = lifecycle.Slots[0].Unit;
            var units = new byte[1 + UnitsSnapshot.SizeFor(1)];
            units[0] = (byte)MessageType.Units;
            units[1] = 1;
            units[2] = (byte)red.Id;
            units[3] = (byte)Team.Enemy;
            BinaryPrimitives.WriteInt32LittleEndian(units.AsSpan(4), BitConverter.SingleToInt32Bits(1.25f));
            BinaryPrimitives.WriteInt32LittleEndian(units.AsSpan(8), BitConverter.SingleToInt32Bits(-0.5f));
            BinaryPrimitives.WriteUInt16LittleEndian(units.AsSpan(12), 33);
            hostEnd.Send(units);
            hostEnd.Flush();

            var hudScore = (Text)typeof(SectorLab.Presentation.BattleHud).GetField("playerScore",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(Object.FindAnyObjectByType<SectorLab.Presentation.BattleHud>());
            Assert.That(hudScore.text, Does.StartWith("ВЫ  20/45"), "Red's own score from the host.");
            Assert.That(battle.Model.RemainingSeconds.Value, Is.EqualTo(75));
            Assert.That(red.transform.position.x, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(red.transform.position.z, Is.EqualTo(-0.5f).Within(0.001f));
            Assert.That(red.Health.Current, Is.EqualTo(33));
            Assert.That(blue.GetComponentInChildren<Renderer>().enabled, Is.False, "Absent from the snapshot: dead on the host.");

            var sent = new List<byte[]>();
            hostEnd.Received += m => sent.Add(m.ToArray());
            Assert.That(commander.SelectFriendly(red), Is.True);
            Assert.That(commander.OrderSelectedToRelay(battle.Relays[0]), Is.True);
            Assert.That(red.Movement.IsMoving, Is.False, "Only the host moves units.");
            clientEnd.Flush();
            Assert.That(sent, Has.Count.EqualTo(1));
            Assert.That(sent[0][0], Is.EqualTo((byte)MessageType.Command));
            Assert.That(UnitCommandCodec.TryRead(sent[0].AsSpan(1), out UnitCommand order), Is.True);
            Assert.That(order.Kind, Is.EqualTo(UnitCommandKind.MoveToRelay));
            Assert.That(order.UnitId, Is.EqualTo(red.Id));
            Assert.That(order.RelayIndex, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ClientShowsTheHostsResultFromItsOwnSide()
        {
            NetworkLaunch.Client(clientEnd);
            yield return LoadMain();
            using var hostModel = new MatchModel();
            hostModel.SetRelayCount(3);
            hostModel.PlayerScore.Value = 30;
            hostModel.EnemyScore.Value = 45;
            hostModel.Outcome.Value = BattleOutcome.Defeat; // blue lost, so red won
            var message = new byte[1 + MatchSnapshot.SizeFor(3)];
            message[0] = (byte)MessageType.MatchState;
            MatchSnapshot.Write(hostModel, message.AsSpan(1));
            hostEnd.Send(message);
            hostEnd.Flush();
            var view = Object.FindAnyObjectByType<BattleResultView>();
            var title = (Text)typeof(BattleResultView).GetField("title",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(view);
            Assert.That(title.text, Does.StartWith("Победа"));
            Assert.That(title.text, Does.Contain("45 : 30"));
            Assert.That(Object.FindAnyObjectByType<UnitCommander>().enabled, Is.False);
        }
    }
}
