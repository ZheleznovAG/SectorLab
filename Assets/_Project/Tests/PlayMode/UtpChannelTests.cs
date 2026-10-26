using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Network;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Unit = SectorLab.Units.Unit;

namespace SectorLab.Tests
{
    // Real UDP through Unity Transport on localhost, both ends in this process.
    public class UtpChannelTests
    {
        private readonly List<UtpChannel> opened = new List<UtpChannel>();
        private static ushort NextPort() => (ushort)UnityEngine.Random.Range(47000, 48000);

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (UtpChannel channel in opened) channel.Dispose();
            opened.Clear();
            NetworkLauncher.Leave();
            Time.timeScale = 1;
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("AfterUtp");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
        }

        private UtpChannel Track(UtpChannel channel) { opened.Add(channel); return channel; }

        private static IEnumerator Pump(Func<bool> done, params UtpChannel[] channels)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!done() && Time.realtimeSinceStartup < deadline)
            {
                foreach (UtpChannel channel in channels) channel.Update();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator PeersConnectAndExchangeOrdersAndSnapshots()
        {
            ushort port = NextPort();
            UtpChannel server = Track(UtpChannel.Listen(port));
            UtpChannel client = Track(UtpChannel.Connect("127.0.0.1", port));
            yield return Pump(() => server.IsConnected && client.IsConnected, server, client);
            Assert.That(server.IsConnected && client.IsConnected, Is.True, "No UDP connection on localhost.");

            byte[] atServer = null, atClient = null;
            server.Received += m => atServer = m.ToArray();
            client.Received += m => atClient = m.ToArray();
            var order = new byte[1 + UnitCommandCodec.Size];
            order[0] = (byte)MessageType.Command;
            UnitCommandCodec.Write(UnitCommand.MoveToRelay(Team.Enemy, 4, 1), order.AsSpan(1));
            client.Send(order);
            server.Send(new byte[] { (byte)MessageType.MatchState, 1, 2, 3 });
            yield return Pump(() => atServer != null && atClient != null, server, client);

            Assert.That(atServer, Is.EqualTo(order));
            Assert.That(atClient, Is.EqualTo(new byte[] { 2, 1, 2, 3 }));
            Assert.That(server.DroppedSends + client.DroppedSends, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HostServesOnlyOneRemotePlayer()
        {
            ushort port = NextPort();
            UtpChannel server = Track(UtpChannel.Listen(port));
            UtpChannel first = Track(UtpChannel.Connect("127.0.0.1", port));
            yield return Pump(() => server.IsConnected && first.IsConnected, server, first);
            UtpChannel second = Track(UtpChannel.Connect("127.0.0.1", port));
            bool secondDropped = false;
            second.Disconnected += () => secondDropped = true;
            yield return Pump(() => secondDropped, server, first, second);
            Assert.That(secondDropped, Is.True);
            Assert.That(first.IsConnected, Is.True);
        }

        [UnityTest]
        public IEnumerator ConnectionCannotReleaseAnOpenTutorial()
        {
            ushort port = NextPort();
            NetworkLauncher.StartHost(port);
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return MainLifecycleTests.WaitForReady();
            var bootstrap = Object.FindAnyObjectByType<NetworkMatchBootstrap>();
            var battle = Object.FindAnyObjectByType<BattleController>();
            var tutorial = Object.FindAnyObjectByType<SectorLab.Presentation.TutorialView>();
            tutorial.Open();
            UtpChannel remoteEnd = Track(UtpChannel.Connect("127.0.0.1", port));
            using var remoteModel = new MatchModel();
            using var remote = new ClientSession(remoteModel, remoteEnd, NetworkLaunch.RemoteTeam);
            yield return Pump(() => !bootstrap.WaitingForPeer && remote.UnitCount == 6, remoteEnd);
            Assert.That(bootstrap.WaitingForPeer, Is.False);
            Assert.That(tutorial.IsShowing, Is.True);
            Assert.That(battle.State.Clock.IsPaused, Is.True);
            Assert.That(battle.IsPausedBy(PauseReason.WaitingForPeer), Is.False);
            Assert.That(battle.IsPausedBy(PauseReason.Tutorial), Is.True);
        }

        [UnityTest]
        public IEnumerator MainHostWaitsForTheOpponentThenPlaysOverUdp()
        {
            ushort port = NextPort();
            NetworkLauncher.StartHost(port);
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return MainLifecycleTests.WaitForReady();
            var bootstrap = Object.FindAnyObjectByType<NetworkMatchBootstrap>();
            var battle = Object.FindAnyObjectByType<BattleController>();
            Assert.That(bootstrap.WaitingForPeer, Is.True);
            yield return null;
            Assert.That(battle.State.Clock.IsPaused, Is.True, "The clock waits for the opponent, even after the tutorial.");
            var notice = Object.FindAnyObjectByType<BattleResultView>();
            Assert.That(notice.IsShowingMessage, Is.True);
            Assert.That(notice.ActionLabel, Is.EqualTo("Играть с ИИ"), "Waiting must never be a dead end.");

            UtpChannel remoteEnd = Track(UtpChannel.Connect("127.0.0.1", port));
            using var remoteModel = new MatchModel();
            using var remote = new ClientSession(remoteModel, remoteEnd, NetworkLaunch.RemoteTeam);
            yield return Pump(() => !bootstrap.WaitingForPeer && remote.UnitCount == 6, remoteEnd);
            Assert.That(bootstrap.WaitingForPeer, Is.False);
            Assert.That(battle.State.Clock.IsPaused, Is.False);
            Assert.That(remote.UnitCount, Is.EqualTo(6));

            Unit red = Object.FindAnyObjectByType<UnitLifecycle>().Slots[4].Unit;
            remote.Send(UnitCommand.MoveToRelay(Team.Enemy, red.Id, 1));
            yield return Pump(() => bootstrap.Host.Accepted == 1, remoteEnd);
            Assert.That(bootstrap.Host.Accepted, Is.EqualTo(1));
            Assert.That(red.Movement.IsMoving, Is.True);
            yield return Pump(() => remoteModel.RemainingSeconds.Value < 180, remoteEnd);
            Assert.That(remoteModel.RemainingSeconds.Value, Is.LessThan(180), "Snapshots keep arriving over UDP.");
        }
    }
}
