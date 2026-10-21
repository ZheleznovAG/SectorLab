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
            yield return null;
            battle.AdvanceSimulation(BattleController.TickSeconds); // Shots fire on match ticks.
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
        public IEnumerator RealRoleWeaponsHaveDifferentSounds()
        {
            Unit enemy = lifecycle.Slots[4].Unit;
            Vector3 center = battle.Relays[0].transform.position;
            Assert.That(enemy.GetComponent<NavMeshAgent>().Warp(center + Vector3.right * 1.1f), Is.True);
            string[] fields = { "tankShot", "scoutShot", "rangerShot" };
            var sounds = new System.Collections.Generic.HashSet<string>();
            var lengths = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 3; i++)
            {
                Unit unit = lifecycle.Slots[i].Unit;
                Assert.That(unit.GetComponent<NavMeshAgent>().Warp(center), Is.True);
                unit.Attack.enabled = true; unit.Attack.SetTarget(enemy);
                int shots = audio.PlayedShots;
                yield return null;
                battle.AdvanceSimulation(BattleController.TickSeconds);
                Assert.That(audio.PlayedShots, Is.GreaterThan(shots));
                AudioClip clip = Field<AudioClip>(audio, fields[i]);
                Assert.That(audio.LastSound, Is.EqualTo(clip.name));
                var samples = new float[clip.samples];
                Assert.That(clip.GetData(samples, 0), Is.True);
                Assert.That(System.Array.Exists(samples, value => Mathf.Abs(value) > 0.001f), Is.True, "The routed weapon clip must contain audible samples.");
                sounds.Add(audio.LastSound); lengths.Add(clip.samples);
                unit.Attack.enabled = false; unit.Attack.ClearTarget();
                Assert.That(unit.GetComponent<NavMeshAgent>().Warp(Vector3.zero), Is.True);
            }
            Assert.That(sounds.Count, Is.EqualTo(3));
            Assert.That(lengths.Count, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator RelaySuccessContestAndEnemyCaptureHaveMatchingNoticeAndSound()
        {
            Unit tank = lifecycle.Slots[0].Unit, enemy = lifecycle.Slots[4].Unit;
            Vector3 tankSpawn = tank.transform.position;
            Vector3 center = battle.Relays[0].transform.position;
            Assert.That(tank.GetComponent<NavMeshAgent>().Warp(center), Is.True);
            battle.AdvanceSimulation(5.1);
            Assert.That(audio.LastNotice, Does.Contain("Вы захватили A").And.Contain("+1 очк/с"));
            Assert.That(audio.LastSound, Is.EqualTo(Field<AudioClip>(audio, "capture").name));
            Text notice = Field<Text>(audio, "noticeLabel");
            Assert.That(notice.text, Is.EqualTo(audio.LastNotice));
            Assert.That(notice.transform.parent.gameObject.activeSelf, Is.True);
            Assert.That(notice.transform.parent.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(enemy.GetComponent<NavMeshAgent>().Warp(center + Vector3.right * 0.8f), Is.True);
            battle.AdvanceSimulation(0.1);
            Assert.That(audio.LastNotice, Does.Contain("СПОР").And.Contain("эффект остановлен"));
            Assert.That(audio.LastSound, Is.EqualTo(Field<AudioClip>(audio, "warning").name));
            Assert.That(audio.PlayedAlerts, Is.EqualTo(1));
            battle.AdvanceSimulation(0.1);
            Assert.That(audio.PlayedAlerts, Is.EqualTo(1), "A continuing contest must not repeat the alert every tick.");
            Assert.That(tank.GetComponent<NavMeshAgent>().Warp(tankSpawn), Is.True);
            battle.AdvanceSimulation(10.1);
            Assert.That(audio.LastNotice, Does.Contain("Враг захватил A"));
            Assert.That(audio.LastSound, Is.EqualTo(Field<AudioClip>(audio, "warning").name));
            Assert.That(audio.PlayedCaptures, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MuteButtonStopsSoundAndPreferenceSurvivesActualRestart()
        {
            Vector3 enemySpawn = lifecycle.Slots[4].Unit.transform.position;
            Field<Button>(audio, "muteButton").onClick.Invoke();
            Assert.That(audio.Muted, Is.True);
            Assert.That(audio.GetComponent<AudioSource>().mute, Is.True);
            ArrangeShot();
            yield return null;
            battle.AdvanceSimulation(BattleController.TickSeconds); // Shots fire on match ticks.
            Assert.That(audio.PlayedShots, Is.Zero);
            Assert.That(audio.GetComponent<AudioSource>().isPlaying, Is.False);
            lifecycle.Slots[0].Unit.Attack.enabled = false;
            Assert.That(lifecycle.Slots[4].Unit.GetComponent<NavMeshAgent>().Warp(enemySpawn), Is.True);
            battle.AdvanceSimulation(5.1);
            Assert.That(audio.PlayedCaptures, Is.Zero);
            Assert.That(audio.LastNotice, Does.Contain("Вы захватили A"), "Muting sound must preserve visible feedback.");
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
