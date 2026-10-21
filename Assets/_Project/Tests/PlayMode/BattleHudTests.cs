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
            Assert.That(Field<Text>(hud, "selection").text, Does.Contain("удерживайте узел"));
            Assert.That(Field<Text[]>(hud, "unitLabels")[0].text, Does.Contain("защита"));
            Assert.That(Field<Text[]>(hud, "unitLabels")[1].text, Does.Contain("манёвр"));
            Assert.That(Field<Text[]>(hud, "unitLabels")[2].text, Does.Contain("дальний бой"));
            tank.Health.TakeDamage(tank.Health.Max);
            hud.Refresh();
            Assert.That(commander.Selected, Is.Null);
            Assert.That(Field<Text[]>(hud, "unitLabels")[0].text, Does.Contain("Возврат: 10"));
            Assert.That(buttons[0].interactable, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldTapOnOccupiedRelayKeepsTheRoleSelectedByItsCard()
        {
            Unit tank = lifecycle.Slots[0].Unit, scout = lifecycle.Slots[1].Unit;
            var relay = battle.Relays[0];
            Assert.That(relay.TryGetApproachPoint(scout, out Vector3 point), Is.True);
            Assert.That(scout.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(point), Is.True);
            Physics.SyncTransforms();
            Canvas.ForceUpdateCanvases();
            yield return null;
            Field<Button[]>(hud, "unitButtons")[0].onClick.Invoke();
            // Free relay surface on the far side from the scout, not the scout's body.
            Vector3 away = relay.transform.position - scout.transform.position;
            away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = Vector3.right;
            Vector3 surface = relay.transform.position + away.normalized * relay.Config.radius * 0.6f;
            Vector2 tap = Camera.main.WorldToScreenPoint(surface);
            Assert.That(commander.IsOverUi(tap), Is.False);
            Assert.That(relay.TryGetApproachPoint(tank, out Vector3 tankPoint), Is.True);
            commander.HandleTap(tap);
            Assert.That(commander.Selected, Is.SameAs(tank));
            Assert.That(tank.Movement.IsMoving, Is.True);
            Assert.That(Vector3.Distance(tank.Movement.Destination, tankPoint), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator DirectWorldTapOnAllyAtRelaySelectsIt()
        {
            Unit tank = lifecycle.Slots[0].Unit, scout = lifecycle.Slots[1].Unit;
            var relay = battle.Relays[0];
            Assert.That(relay.TryGetApproachPoint(scout, out Vector3 point), Is.True);
            Assert.That(scout.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(point), Is.True);
            Physics.SyncTransforms();
            Canvas.ForceUpdateCanvases();
            yield return null;
            Field<Button[]>(hud, "unitButtons")[0].onClick.Invoke();
            Vector2 tap = Camera.main.WorldToScreenPoint(scout.transform.position);
            Assert.That(commander.IsOverUi(tap), Is.False);
            commander.HandleTap(tap);
            Assert.That(commander.Selected, Is.SameAs(scout));
            Assert.That(tank.Movement.IsMoving, Is.False);
        }

        [UnityTest]
        public IEnumerator PauseButtonFreezesClockAndHudShowsAuthoritativeScoreAndTime()
        {
            battle.State.Tick(1, 3, 2);
            battle.PublishModel(); // The HUD shows what the authority publishes.
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
        public IEnumerator LeadFrameAndNoticeFollowTheForecastWinner()
        {
            var audio = Object.FindFirstObjectByType<MatchAudio>();
            hud.Refresh();
            Assert.That(hud.LeadFrameVisible, Is.False, "Nobody is ahead at the start");

            Unit tank = lifecycle.Slots[0].Unit;
            Unit enemyA = lifecycle.Slots[4].Unit, enemyB = lifecycle.Slots[5].Unit;
            Vector3 a = battle.Relays[0].transform.position, b = battle.Relays[1].transform.position;
            Assert.That(tank.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(a), Is.True);
            battle.AdvanceSimulation(5.1);
            hud.Refresh();
            Assert.That(hud.ShownForecast, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(hud.LeadFrameVisible, Is.True);
            Assert.That(hud.LeadFrameColor.b, Is.GreaterThan(hud.LeadFrameColor.r));
            battle.AdvanceSimulation(1.1);
            Assert.That(audio.LastNotice, Does.Contain("вы побеждаете"));

            // Contesting A stops the player's income while the enemy takes B.
            Assert.That(enemyA.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(a + Vector3.right * 0.8f), Is.True);
            Assert.That(enemyB.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(b), Is.True);
            battle.AdvanceSimulation(5.1);
            hud.Refresh();
            Assert.That(hud.ShownForecast, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(hud.LeadFrameColor.r, Is.GreaterThan(hud.LeadFrameColor.b));
            battle.AdvanceSimulation(1.1);
            Assert.That(audio.LastNotice, Does.Contain("побеждает враг"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeaderShowsWorkingIncomeAndItsLossDuringContestAndPause()
        {
            Unit tank = lifecycle.Slots[0].Unit, enemy = lifecycle.Slots[4].Unit;
            Vector3 center = battle.Relays[0].transform.position;
            Assert.That(tank.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(center), Is.True);
            battle.AdvanceSimulation(5.1);
            hud.Refresh();
            Assert.That(Field<Text>(hud, "playerScore").text, Does.Contain("+1 очк/с"));
            Assert.That(Field<Text[]>(hud, "relayLabels")[0].text, Does.Contain("приносит очки"));
            Assert.That(enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(center + Vector3.right * 0.8f), Is.True);
            battle.AdvanceSimulation(0.1);
            hud.Refresh();
            Assert.That(Field<Text>(hud, "playerScore").text, Does.Contain("+0 очк/с"));
            Assert.That(Field<Text[]>(hud, "relayLabels")[0].text, Does.Contain("эффект остановлен"));
            Assert.That(battle.Relays[0].GetComponentInChildren<TextMesh>().text, Is.EqualTo("A\nСПОР"));
            battle.SetPaused(true);
            hud.Refresh();
            Assert.That(Field<Text[]>(hud, "relayLabels")[0].text, Does.Contain("ПАУЗА"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepairCardShowsTheHealingEffectWhenAnOwnerOccupiesIt()
        {
            Unit tank = lifecycle.Slots[0].Unit;
            int damaged = tank.Health.Max - 40;
            tank.Health.TakeDamage(40);
            Assert.That(tank.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(battle.Relays[2].transform.position), Is.True);
            battle.AdvanceSimulation(6.1);
            hud.Refresh();
            Assert.That(tank.Health.Current, Is.GreaterThan(damaged));
            Assert.That(Field<Text[]>(hud, "relayLabels")[2].text, Does.Contain("+6 HP/с").And.Contain("ремонтирует"));
            Assert.That(battle.Relays[2].GetComponentInChildren<TextMesh>().text, Is.EqualTo("C\nремонт"));
            Assert.That(Field<Text>(hud, "playerScore").text, Does.Contain("+0 очк/с"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SafeAreaKeepsHudResultButtonsAndArenaInsideSimulatedNotchInsets()
        {
            var safe = hud.GetComponentInParent<SafeAreaLayout>();
            Assert.That(safe, Is.Not.Null);
            safe.enabled = false; // Simulate device insets without the next Screen.safeArea overwriting them.
            Rect area = new Rect(80, 24, Screen.width - 140, Screen.height - 40);
            safe.Apply(area, new Vector2(Screen.width, Screen.height));
            Canvas.ForceUpdateCanvases();
            yield return null;
            var result = Object.FindFirstObjectByType<BattleResultView>();
            result.ShowLoadError(() => { }, () => { });
            Canvas.ForceUpdateCanvases();
            Vector3[] corners = new Vector3[4];
            foreach (Button button in safe.GetComponentsInChildren<Button>(true))
            {
                button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corner);
                    Assert.That(screen.x, Is.InRange(area.xMin - 1, area.xMax + 1), button.name);
                    Assert.That(screen.y, Is.InRange(area.yMin - 1, area.yMax + 1), button.name);
                }
            }
            Rect viewport = Camera.main.pixelRect;
            Assert.That(viewport.xMin, Is.EqualTo(area.xMin).Within(1));
            Assert.That(viewport.xMax, Is.EqualTo(area.xMax).Within(1));
            Assert.That(viewport.yMin, Is.GreaterThan(area.yMin));
            Assert.That(viewport.yMax, Is.LessThan(area.yMax));
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
            Vector3[] corners = new Vector3[4];
            ((RectTransform)hud.transform.Find("Relays")).GetWorldCorners(corners);
            Assert.That(Camera.main.pixelRect.yMax, Is.LessThanOrEqualTo(corners[0].y), "Arena must end below relay controls.");
        }
    }
}
