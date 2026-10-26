using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Network;
using SectorLab.Objectives;
using SectorLab.Player;
using SectorLab.Presentation;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SectorLab.Tests
{
    public class ClientPresentationTests
    {
        private LoopbackChannel hostEnd, clientEnd;
        private MatchModel model;
        private BattleController battle;
        private UnitLifecycle lifecycle;
        private UnitCommander commander;
        private BattleHud hud;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            LoopbackChannel.CreatePair(out hostEnd, out clientEnd);
            NetworkLaunch.Client(clientEnd);
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Main");
            yield return MainLifecycleTests.WaitForReady();
            battle = Object.FindAnyObjectByType<BattleController>();
            lifecycle = Object.FindAnyObjectByType<UnitLifecycle>();
            commander = Object.FindAnyObjectByType<UnitCommander>();
            hud = Object.FindAnyObjectByType<BattleHud>();
            model = new MatchModel();
            model.SetRelayCount(3);
            NetworkTestPeer.Begin(model, hostEnd, clientEnd);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            model?.Dispose();
            NetworkLaunch.Offline();
            Time.timeScale = 1;
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("AfterClientPresentation"));
                yield return SceneManager.UnloadSceneAsync(main);
            }
        }

        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private void SendMatch() => NetworkTestPeer.Send(model, hostEnd);

        private void SendUnits(params Unit[] units)
        {
            model.SetUnitCount(lifecycle.Slots.Count);
            for (int i = 0; i < lifecycle.Slots.Count; i++)
            {
                Unit unit = lifecycle.Slots[i].Unit;
                model.SetUnit(i, NetworkTestPeer.View(unit, Array.IndexOf(units, unit) >= 0));
            }
            SendMatch();
            hud.Refresh();
        }

        [Test]
        public void CapturedAndContestedRelayUpdatesItsWorldRingWithoutSimulation()
        {
            model.Relays[1].Value = new RelayView(RelayOwner.Enemy, 100, 2, false, true);
            SendMatch();
            Assert.That(battle.Relays[1].CaptureRingFill, Is.EqualTo(1));
            Color captured = battle.Relays[1].CaptureRingColor;
            model.Relays[1].Value = new RelayView(RelayOwner.Enemy, 100, 2, true, true);
            SendMatch();
            Assert.That(battle.Relays[1].CaptureRingColor, Is.Not.EqualTo(captured));
            Assert.That(battle.Relays[1].State.Owner, Is.EqualTo(RelayOwner.Neutral), "The view must not simulate capture.");
        }

        [Test]
        public void AbsentUnitLosesSelectionAndCannotBeSelectedThroughItsCard()
        {
            SendMatch();
            Unit red = lifecycle.Slots[3].Unit;
            SendUnits(red);
            Assert.That(commander.SelectFriendly(red), Is.True);
            SendUnits();
            Assert.That(commander.Selected, Is.Null, "A vanished proxy must release selection.");
            Assert.That(Field<Button[]>(hud, "unitButtons")[0].interactable, Is.False);
            Assert.That(Field<Text[]>(hud, "unitLabels")[0].text, Does.Not.Contain("HP"));
            Assert.That(commander.SelectFriendly(red), Is.False);
            SendUnits(red);
            Assert.That(Field<Button[]>(hud, "unitButtons")[0].interactable, Is.True);
            Assert.That(commander.SelectFriendly(red), Is.True, "The same proxy can represent a respawn without local lifecycle events.");
        }

        private void SetShotCounter(Unit unit, uint generation, uint shots)
        {
            UnitView view = NetworkTestPeer.View(unit, true, -1, generation);
            model.SetUnit(unit.Id, new UnitView(view.Id, view.Team, view.Role, view.Life, view.Health,
                view.MaxHealth, 0, generation, shots, view.X, view.Z));
        }

        [UnityTest]
        public IEnumerator RemoteShotsUseCountersWithoutReplayAfterDuplicationOrRespawn()
        {
            var audio = Object.FindAnyObjectByType<MatchAudio>();
            audio.SetMuted(false, false);
            Unit red = lifecycle.Slots[3].Unit;
            model.SetUnitCount(lifecycle.Slots.Count);
            for (int i = 0; i < lifecycle.Slots.Count; i++)
                model.SetUnit(i, NetworkTestPeer.View(lifecycle.Slots[i].Unit, i == 3));
            SetShotCounter(red, 1, 20);
            SendMatch();
            Assert.That(audio.PlayedShots, Is.Zero, "Joining does not replay earlier shots.");
            SetShotCounter(red, 1, 21);
            SendMatch(); SendMatch();
            Assert.That(audio.PlayedShots, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(0.09f);
            SetShotCounter(red, 1, 25);
            SendMatch();
            Assert.That(audio.PlayedShots, Is.EqualTo(2), "A missing burst becomes at most one current sound.");
            yield return new WaitForSecondsRealtime(0.09f);
            SetShotCounter(red, 2, 7);
            SendMatch();
            Assert.That(audio.PlayedShots, Is.EqualTo(2), "A new incarnation starts a new baseline.");
            SetShotCounter(red, 2, 8);
            SendMatch();
            Assert.That(audio.PlayedShots, Is.EqualTo(3));
            audio.SetMuted(true, false);
            SetShotCounter(red, 2, 9); SendMatch();
            yield return new WaitForSecondsRealtime(0.09f);
            audio.SetMuted(false, false); SendMatch();
            Assert.That(audio.PlayedShots, Is.EqualTo(3), "Unmuting does not replay a muted shot.");
        }

        [Test]
        public void RemoteForecastSettlesInHostGameTimeAndIgnoresPauseHeartbeats()
        {
            var audio = Object.FindAnyObjectByType<MatchAudio>();
            audio.SetMuted(false, false);
            SendMatch();
            model.Forecast.Value = BattleOutcome.Defeat; // red leads
            model.GameSeconds = 0.5; SendMatch();
            model.GameSeconds = 1.1; SendMatch();
            Assert.That(audio.PlayedLeadChanges, Is.EqualTo(1));
            model.Forecast.Value = BattleOutcome.Victory;
            model.GameSeconds = 1.2; SendMatch();
            model.Paused.Value = true;
            for (int i = 0; i < 20; i++) SendMatch();
            Assert.That(audio.PlayedLeadChanges, Is.EqualTo(1));
            model.Paused.Value = false;
            model.GameSeconds = 2.2; SendMatch();
            Assert.That(audio.PlayedLeadChanges, Is.EqualTo(2));
        }

        [Test]
        public void RepeatedResultPlaysExactlyOnceAndNextMatchCanPlayAgain()
        {
            var audio = Object.FindAnyObjectByType<MatchAudio>();
            audio.SetMuted(false, false);
            model.Outcome.Value = BattleOutcome.Defeat;
            SendMatch(); SendMatch();
            Assert.That(audio.PlayedResults, Is.EqualTo(1));
            Assert.That(audio.LastSound, Does.Contain("victory"), "Red's perspective.");
            model.Outcome.Value = BattleOutcome.InProgress;
            model.MatchId++;
            NetworkTestPeer.Begin(model, hostEnd, clientEnd);
            SendMatch();
            model.Outcome.Value = BattleOutcome.Victory;
            SendMatch(); SendMatch();
            Assert.That(audio.PlayedResults, Is.EqualTo(2));
            Assert.That(audio.LastSound, Does.Contain("defeat"));
        }
    }
}
