using System.Collections;
using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Player;
using SectorLab.Presentation;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SectorLab.Tests
{
    public class TutorialViewTests
    {
        private bool hadPreference;
        private int previousPreference;
        private TutorialView tutorial;
        private BattleController battle;
        private static T Field<T>(Object target, string name) => (T)target.GetType()
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(target);

        [UnitySetUp]
        public IEnumerator FirstLaunch()
        {
            hadPreference = PlayerPrefs.HasKey(TutorialView.SeenPreference);
            previousPreference = PlayerPrefs.GetInt(TutorialView.SeenPreference);
            PlayerPrefs.DeleteKey(TutorialView.SeenPreference);
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return MainLifecycleTests.WaitForReady(false);
            tutorial = Object.FindFirstObjectByType<TutorialView>();
            battle = Object.FindFirstObjectByType<BattleController>();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (hadPreference) PlayerPrefs.SetInt(TutorialView.SeenPreference, previousPreference);
            else PlayerPrefs.DeleteKey(TutorialView.SeenPreference);
            PlayerPrefs.Save();
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("AfterTutorial"); SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
            yield return null;
            Assert.That(Unit.All.Count, Is.Zero);
            Assert.That(SectorLab.Infrastructure.AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero);
            Time.timeScale = 1;
        }

        [UnityTest]
        public IEnumerator FirstLaunchPausesClockAndThreeRealNextButtonsStartTheMatch()
        {
            Assert.That(tutorial.IsShowing, Is.True);
            Assert.That(battle.State.Clock.IsPaused, Is.True);
            double elapsed = battle.State.Clock.ElapsedSeconds;
            battle.AdvanceSimulation(10);
            Assert.That(battle.State.Clock.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(Object.FindFirstObjectByType<UnitCommander>().enabled, Is.False);
            var next = Field<Button>(tutorial, "nextButton");
            for (int i = 1; i <= 3; i++)
            {
                Assert.That(Field<Text>(tutorial, "title").text, Does.StartWith(i + "/3"));
                next.onClick.Invoke();
            }
            Assert.That(tutorial.IsShowing, Is.False);
            Assert.That(battle.State.IsRunning, Is.True);
            Assert.That(PlayerPrefs.GetInt(TutorialView.SeenPreference), Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkipIsRememberedAfterActualSceneRestart()
        {
            Field<Button>(tutorial, "skipButton").onClick.Invoke();
            Assert.That(battle.State.IsRunning, Is.True);
            battle.Restart();
            yield return MainLifecycleTests.WaitForReady(false);
            tutorial = Object.FindFirstObjectByType<TutorialView>();
            Assert.That(tutorial.IsShowing, Is.False);
            Assert.That(Object.FindFirstObjectByType<BattleController>().State.IsRunning, Is.True);
        }

        [UnityTest]
        public IEnumerator HelpPreservesExistingPauseAndModalBlocksWorldInput()
        {
            Field<Button>(tutorial, "skipButton").onClick.Invoke();
            battle.SetPaused(true);
            Field<Button>(tutorial, "helpButton").onClick.Invoke();
            Assert.That(tutorial.IsShowing, Is.True);
            var commander = Object.FindFirstObjectByType<UnitCommander>();
            commander.HandleTap(new Vector2(Screen.width / 2f, Screen.height / 2f));
            Assert.That(commander.Selected, Is.Null);
            Object.FindFirstObjectByType<BattleHud>().Refresh();
            Assert.That(Field<Button>(Object.FindFirstObjectByType<BattleHud>(), "pauseButton").interactable, Is.False);
            Field<Button>(tutorial, "skipButton").onClick.Invoke();
            Assert.That(battle.State.Clock.IsPaused, Is.True);
            battle.SetPaused(false);
            Field<Button>(tutorial, "helpButton").onClick.Invoke();
            Field<Button>(tutorial, "skipButton").onClick.Invoke();
            Assert.That(battle.State.IsRunning, Is.True);
            yield return null;
        }
    }
}
