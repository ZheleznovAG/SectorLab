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
            using var remote = new ClientSession(remoteModel, clientEnd, NetworkLaunch.RemoteTeam, Object.FindAnyObjectByType<NetworkMatchBootstrap>().Configuration);
            NetworkTestPeer.Connect(Object.FindAnyObjectByType<NetworkMatchBootstrap>().Host, hostEnd, clientEnd);
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
            var tutorial = Object.FindAnyObjectByType<SectorLab.Presentation.TutorialView>();
            tutorial.Open();
            Assert.That(Time.timeScale, Is.EqualTo(1), "A client's tutorial must not freeze time effects.");
            Assert.That(commander.isActiveAndEnabled, Is.False, "The modal blocks local orders without owning host time.");
            tutorial.Dismiss();
            Assert.That(commander.isActiveAndEnabled, Is.False, "No host state has arrived yet.");

            using var hostModel = new MatchModel();
            hostModel.SetRelayCount(3);
            hostModel.PlayerScore.Value = 11;
            hostModel.EnemyScore.Value = 20;
            hostModel.EnemyRate.Value = 1;
            hostModel.RemainingSeconds.Value = 75;
            hostModel.Relays[1].Value = new RelayView(RelayOwner.Enemy, 100, 2, false, true);
            NetworkTestPeer.Begin(hostModel, hostEnd, clientEnd);
            Unit red = lifecycle.Slots[4].Unit, blue = lifecycle.Slots[0].Unit;
            hostModel.SetUnitCount(lifecycle.Slots.Count);
            for (int i = 0; i < lifecycle.Slots.Count; i++)
                hostModel.SetUnit(i, NetworkTestPeer.View(lifecycle.Slots[i].Unit, i == 4, 33));
            hostModel.SetUnit(4, new UnitView(red.Id, Team.Enemy, red.Stats.role, UnitLife.Alive,
                33, red.Health.Max, 0, 1, 0, 1.25f, -0.5f));
            NetworkTestPeer.Send(hostModel, hostEnd);
            Assert.That(commander.isActiveAndEnabled, Is.True, "The host has published a running match.");

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
            Assert.That(MatchProtocol.TryCommand(sent[0], out UnitCommand order, out _, out _, out _), Is.True);
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
            NetworkTestPeer.Begin(hostModel, hostEnd, clientEnd);
            NetworkTestPeer.Send(hostModel, hostEnd);
            var view = Object.FindAnyObjectByType<BattleResultView>();
            var title = (Text)typeof(BattleResultView).GetField("title",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(view);
            Assert.That(title.text, Does.StartWith("Победа"));
            Assert.That(title.text, Does.Contain("45 : 30"));
            var commander = Object.FindAnyObjectByType<UnitCommander>();
            Assert.That(commander.enabled, Is.False);

            // Only the host restarts: the client's button explains that and offers a way out.
            var restart = (Button)typeof(BattleResultView).GetField("restartButton",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(view);
            restart.onClick.Invoke();
            Assert.That(view.IsShowingMessage, Is.True);
            Assert.That(title.text, Does.Contain("хост"));
            Assert.That(view.ActionLabel, Is.EqualTo("Играть с ИИ"));

            // The host's next match arrives: the client follows into it without reloading.
            hostModel.Outcome.Value = BattleOutcome.InProgress;
            hostModel.RemainingSeconds.Value = 180;
            hostModel.MatchId++;
            NetworkTestPeer.Begin(hostModel, hostEnd, clientEnd);
            NetworkTestPeer.Send(hostModel, hostEnd);
            Assert.That(view.IsVisible, Is.False);
            Assert.That(commander.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator HostRepeatsStateWhileTheClockStands()
        {
            NetworkLaunch.Host(hostEnd);
            yield return LoadMain();
            var battle = Object.FindAnyObjectByType<BattleController>();
            using var remoteModel = new MatchModel();
            using var remote = new ClientSession(remoteModel, clientEnd, NetworkLaunch.RemoteTeam, Object.FindAnyObjectByType<NetworkMatchBootstrap>().Configuration);
            NetworkTestPeer.Connect(Object.FindAnyObjectByType<NetworkMatchBootstrap>().Host, hostEnd, clientEnd);
            battle.SetPaused(true);
            hostEnd.Flush(); // whatever went out before the pause is lost, as UDP may lose it
            remoteModel.Paused.Value = false;
            float deadline = Time.realtimeSinceStartup + NetworkMatchBootstrap.HeartbeatSeconds * 3;
            while (hostEnd.Pending == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            hostEnd.Flush();
            Assert.That(remoteModel.Paused.Value, Is.True, "No ticks run while paused; a heartbeat still carries the state.");
        }
    }
}
