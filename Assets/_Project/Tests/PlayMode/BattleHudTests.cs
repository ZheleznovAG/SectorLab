using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SectorLab.AI;
using SectorLab.Battle;
using SectorLab.Infrastructure;
using SectorLab.Player;
using SectorLab.Presentation;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SectorLab.Tests
{
    public class BattleHudTests
    {
        private BattleController battle;
        private BattleHud hud;
        private UnitLifecycle lifecycle;
        private UnitCommander commander;
        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return MainLifecycleTests.WaitForReady();
            battle = Object.FindFirstObjectByType<BattleController>();
            hud = Object.FindFirstObjectByType<BattleHud>();
            lifecycle = Object.FindFirstObjectByType<UnitLifecycle>();
            commander = Object.FindFirstObjectByType<UnitCommander>();
            battle.enabled = false;
            Object.FindFirstObjectByType<EnemyTacticsController>().enabled = false;
            foreach (Unit unit in Unit.All) { unit.Attack.enabled = false; unit.Movement.Stop(); }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("AfterHud"); SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
            yield return null;
            Assert.That(Unit.All.Count, Is.Zero);
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero);
            Time.timeScale = 1;
        }

        [UnityTest]
        public IEnumerator RoleAndRelayButtonsUseRealSelectionMovementHealthAndRespawn()
        {
            var buttons = Field<Button[]>(hud, "unitButtons");
            buttons[0].onClick.Invoke();
            Unit tank = lifecycle.Slots[0].Unit;
            Assert.That(commander.Selected, Is.SameAs(tank));
            Field<Button[]>(hud, "relayButtons")[0].onClick.Invoke();
            Assert.That(tank.Movement.IsMoving, Is.True);
            tank.Health.TakeDamage(23);
            hud.Refresh();
            Assert.That(Field<Text>(hud, "selection").text, Does.Contain("Tank").And.Contain(tank.Health.Current.ToString()));
            tank.Health.TakeDamage(tank.Health.Max);
            hud.Refresh();
            Assert.That(commander.Selected, Is.Null);
            Assert.That(Field<Text[]>(hud, "unitLabels")[0].text, Does.Contain("Возврат: 10"));
            Assert.That(buttons[0].interactable, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseButtonFreezesClockAndHudShowsAuthoritativeScoreAndTime()
        {
            battle.State.Tick(1, 3, 2);
            hud.Refresh();
            Assert.That(Field<Text>(hud, "playerScore").text, Does.Contain("3"));
            Assert.That(Field<Text>(hud, "enemyScore").text, Does.Contain("2"));
            Assert.That(Field<Text>(hud, "timer").text, Does.StartWith("02:59"));
            var pause = Field<Button>(hud, "pauseButton");
            pause.onClick.Invoke();
            double elapsed = battle.State.Clock.ElapsedSeconds;
            battle.AdvanceSimulation(10);
            hud.Refresh();
            Assert.That(battle.State.Clock.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(Field<Text>(hud, "pauseLabel").text, Is.EqualTo("Продолжить"));
            Assert.That(commander.enabled, Is.False);
            pause.onClick.Invoke();
            hud.Refresh();
            Assert.That(commander.enabled, Is.True);
            Assert.That(Field<Text>(hud, "pauseLabel").text, Is.EqualTo("Пауза"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HudBlocksWorldTapAndArenaViewportStaysBetweenPanels()
        {
            Canvas.ForceUpdateCanvases();
            yield return null;
            var button = Field<Button[]>(hud, "unitButtons")[0].GetComponent<RectTransform>();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, button.position);
            Assert.That(commander.IsOverUi(position), Is.True);
            commander.HandleTap(position);
            Assert.That(commander.Selected, Is.Null);
            Assert.That(Camera.main.rect.yMin, Is.GreaterThan(0));
            Assert.That(Camera.main.rect.yMax, Is.LessThan(1));
        }
    }
}
