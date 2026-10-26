using System;
using System.Collections;
using NUnit.Framework;
using SectorLab.AI;
using SectorLab.Battle;
using SectorLab.Network;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Unit = SectorLab.Units.Unit;

namespace SectorLab.Tests
{
    // The real Main is the host; the client is a MatchModel behind an in-memory channel.
    public class NetworkSessionTests
    {
        private BattleController battle;
        private UnitLifecycle lifecycle;
        private LoopbackChannel hostEnd, clientEnd;
        private HostSession host;
        private ClientSession client;
        private MatchModel clientModel;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return MainLifecycleTests.WaitForReady();
            battle = Object.FindAnyObjectByType<BattleController>();
            lifecycle = Object.FindAnyObjectByType<UnitLifecycle>();
            battle.enabled = false; // Ticks are driven below, next to the host's sends.
            Object.FindAnyObjectByType<EnemyTacticsController>().enabled = false; // the remote human plays red
            foreach (Unit unit in Unit.All) { unit.Attack.enabled = false; unit.Movement.Stop(); }
            LoopbackChannel.CreatePair(out hostEnd, out clientEnd);
            host = new HostSession(battle.Commands, battle.Model, hostEnd, Team.Enemy, 1, 123);
            clientModel = new MatchModel();
            client = new ClientSession(clientModel, clientEnd, Team.Enemy, 123);
            battle.PublishModel();
            NetworkTestPeer.Connect(host, hostEnd, clientEnd);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            host.Dispose();
            client.Dispose();
            clientModel.Dispose();
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("AfterNetwork");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
        }

        private bool ClientSees(Unit unit)
        {
            foreach (UnitView state in client.Units)
            {
                if (state.Id != unit.Id) continue;
                Assert.That(state.Team, Is.EqualTo(unit.Team));
                Assert.That(new Vector2(state.X - unit.transform.position.x, state.Z - unit.transform.position.z).magnitude, Is.LessThan(0.01f));
                Assert.That(state.Health, Is.EqualTo(unit.Health.Current));
                return true;
            }
            return false;
        }

        [Test]
        public void ForgedIssuerCannotMoveTheHostsUnits()
        {
            Unit blue = lifecycle.Slots[0].Unit;
            client.Send(UnitCommand.MoveToRelay(Team.Player, blue.Id, 0)); // claims to be blue
            clientEnd.Flush();
            Assert.That(host.LastResult, Is.EqualTo(CommandResult.NotOwner));
            Assert.That(blue.Movement.IsMoving, Is.False);
            Assert.That(host.Rejected, Is.EqualTo(1));
        }

        [Test]
        public void GarbageFromTheClientIsCountedAndIgnored()
        {
            clientEnd.Send(new byte[] { 1, 0, 0 });
            clientEnd.Send(new byte[] { 2, 9, 9, 9 }); // a state message, which only the host may send
            clientEnd.Send(new byte[] { 0 });
            clientEnd.Flush();
            Assert.That(host.Rejected, Is.EqualTo(3));
            Assert.That(host.Accepted, Is.Zero);
        }

        [Test]
        public void QueuedOrdersFromThePreviousMatchCannotEnterTheNewSession()
        {
            Unit red = lifecycle.Slots[3].Unit;
            client.Send(UnitCommand.MoveToRelay(Team.Enemy, red.Id, 1));
            host.Dispose();
            host = new HostSession(battle.Commands, battle.Model, hostEnd, Team.Enemy, 2, 123);
            NetworkTestPeer.Connect(host, hostEnd, clientEnd);
            Assert.That(host.Accepted, Is.Zero);
            Assert.That(host.Rejected, Is.EqualTo(1));
            Assert.That(red.Movement.IsMoving, Is.False);
            client.Send(UnitCommand.MoveToRelay(Team.Enemy, red.Id, 1));
            clientEnd.Flush();
            Assert.That(host.Accepted, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AQueuedOrderCannotControlTheNextIncarnationOfItsActor()
        {
            Unit red = lifecycle.Slots[3].Unit;
            client.Send(UnitCommand.MoveToRelay(Team.Enemy, red.Id, 1));
            red.Health.TakeDamage(red.Health.Max);
            battle.AdvanceSimulation(RespawnTimer.DelaySeconds + 0.1);
            float deadline = Time.realtimeSinceStartup + 5;
            while (lifecycle.Slots[3].Unit == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(lifecycle.Slots[3].Unit, Is.Not.Null);
            clientEnd.Flush();
            Assert.That(host.Accepted, Is.Zero);
            Assert.That(host.Rejected, Is.EqualTo(1));
            Assert.That(lifecycle.Slots[3].Unit.Movement.IsMoving, Is.False);
            host.SendState(); hostEnd.Flush();
            client.Send(UnitCommand.MoveToRelay(Team.Enemy, 3, 1)); clientEnd.Flush();
            Assert.That(host.Accepted, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RemoteRedOrderCapturesARelayAndTheClientSeesIt()
        {
            Unit red = lifecycle.Slots[3].Unit;
            client.Send(UnitCommand.MoveToRelay(Team.Enemy, red.Id, 1));
            clientEnd.Flush();
            Assert.That(host.LastResult, Is.EqualTo(CommandResult.Accepted));
            Assert.That(red.Movement.IsMoving, Is.True);

            float deadline = Time.realtimeSinceStartup + 20;
            while (battle.Relays[1].State.Owner != RelayOwner.Enemy && Time.realtimeSinceStartup < deadline)
            {
                battle.AdvanceSimulation(Time.deltaTime);
                host.SendState();
                hostEnd.Flush();
                yield return null;
            }
            Assert.That(battle.Relays[1].State.Owner, Is.EqualTo(RelayOwner.Enemy), "The red unit never captured B.");
            Assert.That(clientModel.Relays, Has.Length.EqualTo(3));
            Assert.That(clientModel.Relays[1].Value.Owner, Is.EqualTo(RelayOwner.Enemy));
            Assert.That(clientModel.RemainingSeconds.Value, Is.EqualTo(battle.Model.RemainingSeconds.Value));
            Assert.That(client.MalformedMessages, Is.Zero);

            Assert.That(ClientSees(red), Is.True, "The client's unit snapshot lacks the red unit.");
            Assert.That(client.UnitCount, Is.EqualTo(6));
        }
    }
}
