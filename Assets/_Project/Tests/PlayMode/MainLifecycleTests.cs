using System.Collections;
using NUnit.Framework;
using SectorLab.AI;
using SectorLab.Battle;
using SectorLab.Infrastructure;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class MainLifecycleTests
    {
        private BattleController battle;
        private UnitLifecycle lifecycle;
        private GameObject blocker;

        public static IEnumerator WaitForReady(bool dismissTutorial = true)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            UnitLifecycle current;
            do
            {
                yield return null;
                current = Object.FindFirstObjectByType<UnitLifecycle>();
                Assert.That(current, Is.Not.Null);
                Assert.That(current.Phase, Is.Not.EqualTo(UnitLifecyclePhase.Failed), current.Failure);
            } while (!current.IsReady && Time.realtimeSinceStartup < deadline);
            Assert.That(current.IsReady, Is.True, "Main must finish its real local prefab loads.");
            if (dismissTutorial)
            {
                // Close via the production action while preserving the human's first-launch preference.
                var tutorial = Object.FindFirstObjectByType<SectorLab.Presentation.TutorialView>();
                string key = SectorLab.Presentation.TutorialView.SeenPreference;
                bool existed = PlayerPrefs.HasKey(key); int previous = PlayerPrefs.GetInt(key);
                if (tutorial != null && tutorial.IsShowing) tutorial.Dismiss();
                if (existed) PlayerPrefs.SetInt(key, previous); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [UnitySetUp]
        public IEnumerator LoadRealMain()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return WaitForReady();
            battle = Object.FindFirstObjectByType<BattleController>();
            lifecycle = Object.FindFirstObjectByType<UnitLifecycle>();
            battle.enabled = false; // Drive the production fixed ticks explicitly, without frame timing drift.
            var hud = Object.FindFirstObjectByType<SectorLab.Presentation.BattleHud>();
            if (hud != null) hud.enabled = false;
            battle.InitializeRelays(System.Array.Empty<SectorLab.Objectives.RelayPoint>());
            // Isolate countdowns from relay scoring; full MainMatchTests keep actual objectives/combat.
            Object.FindFirstObjectByType<EnemyTacticsController>().enabled = false;
            lifecycle.Spawned += DisableCombat;
            for (int i = 0; i < Unit.All.Count; i++) DisableCombat(Unit.All[i]);
        }

        private static void DisableCombat(Unit unit)
        {
            unit.Attack.enabled = false;
            unit.Movement.Stop();
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (blocker != null) Object.Destroy(blocker);
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("AfterLifecycle");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
            yield return null;
            Assert.That(Unit.All.Count, Is.Zero);
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero);
            Time.timeScale = 1;
        }

        [UnityTest]
        public IEnumerator EveryRoleOfBothTeamsRespawnsOnceAtTenGameSecondsWithFullHealth()
        {
            foreach (UnitSlotState slot in lifecycle.Slots)
            {
                Unit old = slot.Unit;
                old.Health.TakeDamage(old.Health.Max);
                old.Health.TakeDamage(old.Health.Max); // A repeated lethal damage cannot schedule a duplicate.
                yield return null;
                Assert.That(slot.Unit == null, Is.True);
                Assert.That(slot.Respawn.RemainingSeconds, Is.EqualTo(10));
                Assert.That(Unit.All.Count, Is.EqualTo(5));
                battle.AdvanceSimulation(9.9);
                Assert.That(slot.Unit == null, Is.True);
                battle.AdvanceSimulation(0.1);
                float deadline = Time.realtimeSinceStartup + 5;
                while (slot.Unit == null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(slot.Unit, Is.Not.Null, $"{slot.Config.team}/{slot.Config.role}: waiting={slot.Respawn.RemainingSeconds}, spawning={slot.IsSpawning}, phase={lifecycle.Phase}, safe={UnitSpawnPositions.TryFind(slot.Config.position, slot.SpawnRadius, out _)}");
                Assert.That(slot.Unit, Is.Not.SameAs(old));
                Assert.That(slot.Unit.Team, Is.EqualTo(slot.Config.team));
                Assert.That(slot.Unit.Stats.name, Is.EqualTo(slot.Config.role.ToString()));
                Assert.That(slot.Unit.Health.Current, Is.EqualTo(slot.Unit.Health.Max));
                Assert.That(slot.Unit.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh, Is.True);
                Assert.That(slot.Respawn.IsWaiting, Is.False);
                Assert.That(Unit.All.Count, Is.EqualTo(6));
                Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.EqualTo(6));
            }
        }

        [UnityTest]
        public IEnumerator PauseFreezesRespawnAndTeamWipeDoesNotFinishTheMatch()
        {
            foreach (UnitSlotState slot in lifecycle.Slots)
                if (slot.Config.team == Team.Player) slot.Unit.Health.TakeDamage(slot.Unit.Health.Max);
            yield return null;
            Assert.That(Unit.All.Count, Is.EqualTo(3));
            battle.AdvanceSimulation(4);
            battle.SetPaused(true);
            double elapsed = battle.State.Clock.ElapsedSeconds;
            battle.AdvanceSimulation(100);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(battle.State.Clock.ElapsedSeconds, Is.EqualTo(elapsed));
            foreach (UnitSlotState slot in lifecycle.Slots)
                if (slot.Config.team == Team.Player) Assert.That(slot.Respawn.RemainingSeconds, Is.EqualTo(6).Within(0.00001));
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.InProgress));
            battle.SetPaused(false);
            battle.AdvanceSimulation(6);
            float deadline = Time.realtimeSinceStartup + 5;
            while (Unit.All.Count != 6 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Unit.All.Count, Is.EqualTo(6));
        }

        [UnityTest]
        public IEnumerator OccupiedBaseDefersRespawnUntilSpaceBecomesSafe()
        {
            UnitSlotState slot = lifecycle.Slots[0];
            slot.Unit.Health.TakeDamage(slot.Unit.Health.Max);
            yield return null;
            blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = slot.Config.position + Vector3.up;
            blocker.transform.localScale = new Vector3(5, 3, 5);
            Physics.SyncTransforms();
            battle.AdvanceSimulation(10);
            yield return null;
            Assert.That(slot.Unit == null, Is.True);
            Assert.That(slot.IsSpawning, Is.False);
            Assert.That(slot.Respawn.IsWaiting, Is.True);
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.EqualTo(5));
            Object.Destroy(blocker);
            yield return null;
            Physics.SyncTransforms();
            battle.AdvanceSimulation(0.1);
            float deadline = Time.realtimeSinceStartup + 5;
            while (slot.Unit == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(slot.Unit, Is.Not.Null);
            Assert.That(Unit.All.Count, Is.EqualTo(6));
        }

        [UnityTest]
        public IEnumerator FinishedMatchCancelsWaitingRespawns()
        {
            lifecycle.Slots[0].Unit.Health.TakeDamage(lifecycle.Slots[0].Unit.Health.Max);
            yield return null;
            // Reach the real controller's terminal tick; no relay is occupied in this fixture.
            battle.State.Tick(179, 0, 0);
            battle.AdvanceSimulation(1);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Draw));
            Assert.That(lifecycle.Phase, Is.EqualTo(UnitLifecyclePhase.Closed));
            battle.AdvanceSimulation(20);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(Unit.All.Count, Is.EqualTo(5));
            Assert.That(lifecycle.Slots[0].Respawn.IsWaiting, Is.False);
        }
    }
}
