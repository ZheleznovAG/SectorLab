using System.Collections;
using NUnit.Framework;
using SectorLab.AI;
using SectorLab.Battle;
using SectorLab.Presentation;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SectorLab.Tests
{
    public class MatchAudioTests
    {
        private bool hadPreference;
        private int previousPreference;
        private MatchAudio audio;
        private BattleController battle;
        private UnitLifecycle lifecycle;
        private static T Field<T>(Object target, string name) => (T)target.GetType()
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(target);

        [UnitySetUp]
        public IEnumerator Load()
        {
            hadPreference = PlayerPrefs.HasKey(MatchAudio.MutedPreference);
            previousPreference = PlayerPrefs.GetInt(MatchAudio.MutedPreference);
            PlayerPrefs.SetInt(MatchAudio.MutedPreference, 0);
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return MainLifecycleTests.WaitForReady();
            audio = Object.FindFirstObjectByType<MatchAudio>();
            battle = Object.FindFirstObjectByType<BattleController>();
            lifecycle = Object.FindFirstObjectByType<UnitLifecycle>();
            battle.enabled = false;
            Object.FindFirstObjectByType<EnemyTacticsController>().enabled = false;
            foreach (Unit unit in Unit.All) { unit.Attack.enabled = false; unit.Movement.Stop(); }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (hadPreference) PlayerPrefs.SetInt(MatchAudio.MutedPreference, previousPreference);
            else PlayerPrefs.DeleteKey(MatchAudio.MutedPreference);
            PlayerPrefs.Save();
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("AfterAudio"); SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
            yield return null; yield return null;
            foreach (AudioClip clip in Resources.FindObjectsOfTypeAll<AudioClip>())
                Assert.That(clip.name.StartsWith("SectorLab "), Is.False, "Scene-owned procedural clips must be released.");
            Assert.That(Unit.All.Count, Is.Zero);
            Assert.That(SectorLab.Infrastructure.AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero);
            Time.timeScale = 1;
        }

        private void ArrangeShot()
        {
            Unit tank = lifecycle.Slots[0].Unit, enemy = lifecycle.Slots[4].Unit;
            Vector3 center = battle.Relays[0].transform.position;
            Assert.That(tank.GetComponent<NavMeshAgent>().Warp(center), Is.True);
            Assert.That(enemy.GetComponent<NavMeshAgent>().Warp(center + Vector3.right * 1.1f), Is.True);
            tank.Attack.enabled = true; tank.Attack.SetTarget(enemy);
        }

        [UnityTest]
        public IEnumerator RealShotCaptureAndResultPlaySceneOwnedSound()
        {
            ArrangeShot();
            yield return null; yield return null;
            Assert.That(audio.PlayedShots, Is.GreaterThan(0));
            Unit tank = lifecycle.Slots[0].Unit;
            tank.Attack.enabled = false; tank.Attack.ClearTarget();
            Assert.That(lifecycle.Slots[4].Unit.GetComponent<NavMeshAgent>().Warp(Vector3.zero), Is.True);
            battle.AdvanceSimulation(5.1);
            Assert.That(audio.PlayedCaptures, Is.GreaterThan(0));
            battle.State.Tick(0.1, 440, 0);
            battle.AdvanceSimulation(1);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(audio.PlayedResults, Is.EqualTo(1));
            yield return null;
            Assert.That(audio.GetComponent<AudioSource>().isPlaying, Is.True);
        }

        [UnityTest]
        public IEnumerator MuteButtonStopsSoundAndPreferenceSurvivesActualRestart()
        {
            Field<Button>(audio, "muteButton").onClick.Invoke();
            Assert.That(audio.Muted, Is.True);
            Assert.That(audio.GetComponent<AudioSource>().mute, Is.True);
            ArrangeShot();
            yield return null; yield return null;
            Assert.That(audio.PlayedShots, Is.Zero);
            Assert.That(audio.GetComponent<AudioSource>().isPlaying, Is.False);
            battle.Restart();
            yield return MainLifecycleTests.WaitForReady();
            audio = Object.FindFirstObjectByType<MatchAudio>();
            Assert.That(audio.Muted, Is.True);
            Field<Button>(audio, "muteButton").onClick.Invoke();
            Assert.That(audio.Muted, Is.False);
            Assert.That(PlayerPrefs.GetInt(MatchAudio.MutedPreference), Is.Zero);
        }
    }
}
