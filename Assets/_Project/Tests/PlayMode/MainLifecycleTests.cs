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
                current = Object.FindAnyObjectByType<UnitLifecycle>();
                Assert.That(current, Is.Not.Null);
                Assert.That(current.Phase, Is.Not.EqualTo(UnitLifecyclePhase.Failed), current.Failure);
            } while (!current.IsReady && Time.realtimeSinceStartup < deadline);
            Assert.That(current.IsReady, Is.True, "Main must finish its real local prefab loads.");
            if (dismissTutorial)
            {
                // Close via the production action while preserving the human's first-launch preference.
                var tutorial = Object.FindAnyObjectByType<SectorLab.Presentation.TutorialView>();
                string key = SectorLab.Presentation.TutorialView.SeenPreference;
                bool existed = PlayerPrefs.HasKey(key); int previous = PlayerPrefs.GetInt(key);
                if (tutorial != null && tutorial.IsShowing) tutorial.Dismiss();
                if (existed) PlayerPrefs.SetInt(key, previous); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        public static IEnumerator WaitForReleasedLoads()
        {
            // Cancellation retains each reference until its physical Addressables operation ends.
            // A real match may finish on the same frame that a respawn load starts.
            float deadline = Time.realtimeSinceStartup + 5;
            do
            {
                yield return null;
                Assert.That(Unit.All.Count, Is.Zero, "Cancelled loads must never create a unit in the next scene.");
            } while (AddressableUnitPrefabProvider.OutstandingHandles != 0 && Time.realtimeSinceStartup < deadline);
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero, "All physical loads must release within the bounded cleanup wait.");
        }

        [UnitySetUp]
        public IEnumerator LoadRealMain()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return WaitForReady();
            battle = Object.FindAnyObjectByType<BattleController>();
            lifecycle = Object.FindAnyObjectByType<UnitLifecycle>();
            battle.enabled = false; // Drive the production fixed ticks explicitly, without frame timing drift.
            var hud = Object.FindAnyObjectByType<SectorLab.Presentation.BattleHud>();
            if (hud != null) hud.enabled = false;
            battle.InitializeRelays(System.Array.Empty<SectorLab.Objectives.RelayPoint>());
            // Isolate countdowns from relay scoring; full MainMatchTests keep actual objectives/combat.
            Object.FindAnyObjectByType<EnemyTacticsController>().enabled = false;
            lifecycle.Spawned += DisableCombat;
            for (int i = 0; i < Unit.All.Count; i++) DisableCombat(Unit.All[i]);
        }

        private static void DisableCombat(Unit unit)
        {
            unit.Attack.enabled = false;
            unit.Movement.Stop();
        }

        // Shared role assets edited by a test. Restored in teardown: a [UnityTest] aborted by an
        // unexpected log is never disposed, so its finally block would not run.
        private readonly System.Collections.Generic.Dictionary<UnitStats, (string name, UnitRole role)> editedStats =
            new System.Collections.Generic.Dictionary<UnitStats, (string, UnitRole)>();

        private void RememberStats(UnitStats stats)
        {
            if (stats != null && !editedStats.ContainsKey(stats)) editedStats.Add(stats, (stats.name, stats.role));
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            foreach (var entry in editedStats)
                if (entry.Key != null) { entry.Key.name = entry.Value.name; entry.Key.role = entry.Value.role; }
            editedStats.Clear();
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
                int id = old.Id;
                Assert.That(id, Is.EqualTo(System.Array.IndexOf((UnitSlotState[])lifecycle.Slots, slot)), "Ids are roster slot indices.");
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
                Assert.That(slot.Unit, Is.Not.Null, $"{slot.Config.team}/{slot.Config.role}: waiting={slot.Respawn.RemainingSeconds}, spawning={slot.IsSpawning}, phase={lifecycle.Phase}, safe={UnitSpawnPositions.TryFind(battle.Units, slot.Config.position, slot.SpawnRadius, out _)}");
                Assert.That(slot.Unit, Is.Not.SameAs(old));
                Assert.That(slot.Unit.Id, Is.EqualTo(id), "A respawned unit keeps its slot's id for network messages.");
                Assert.That(slot.Unit.Team, Is.EqualTo(slot.Config.team));
                Assert.That(slot.Unit.Stats.role, Is.EqualTo(slot.Config.role));
                Assert.That(slot.Unit.Health.Current, Is.EqualTo(slot.Unit.Health.Max));
                Assert.That(slot.Unit.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh, Is.True);
                Assert.That(slot.Respawn.IsWaiting, Is.False);
                Assert.That(Unit.All.Count, Is.EqualTo(6));
                Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.EqualTo(6));
            }
        }

        [UnityTest]
        public IEnumerator RenamedRoleAssetsStillRespawnEveryRosterSlot()
        {
            foreach (UnitSlotState slot in lifecycle.Slots)
            {
                UnitStats stats = slot.Unit.Stats;
                if (editedStats.ContainsKey(stats)) continue;
                RememberStats(stats);
                stats.name = "Renamed profile " + editedStats.Count;
            }
            foreach (UnitSlotState slot in lifecycle.Slots)
            {
                slot.Unit.Health.TakeDamage(slot.Unit.Health.Max);
                yield return null;
                battle.AdvanceSimulation(10);
                float deadline = Time.realtimeSinceStartup + 5;
                while (slot.Unit == null && lifecycle.IsReady && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(lifecycle.Phase, Is.EqualTo(UnitLifecyclePhase.Ready), lifecycle.Failure);
                Assert.That(slot.Unit, Is.Not.Null, slot.Config.team + "/" + slot.Config.role);
                Assert.That(slot.Unit.Stats.role, Is.EqualTo(slot.Config.role));
                Assert.That(slot.Unit.Health.Current, Is.EqualTo(slot.Unit.Health.Max));
                Assert.That(Unit.All.Count, Is.EqualTo(6));
                Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.EqualTo(6));
            }
        }

        [UnityTest]
        public IEnumerator WrongRoleDataStillFailsRespawnAndReleasesAllInstances()
        {
            UnitSlotState slot = lifecycle.Slots[0];
            UnitStats stats = slot.Unit.Stats;
            UnitRole previous = stats.role;
            RememberStats(stats);
            stats.role = previous == UnitRole.Scout ? UnitRole.Tank : UnitRole.Scout;
            slot.Unit.Health.TakeDamage(slot.Unit.Health.Max);
            yield return null;
            battle.AdvanceSimulation(10);
            float deadline = Time.realtimeSinceStartup + 5;
            while (lifecycle.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(lifecycle.Phase, Is.EqualTo(UnitLifecyclePhase.Failed));
            Assert.That(lifecycle.Failure, Does.Contain("team/role"));
            Assert.That(battle.State.Clock.IsPaused, Is.True);
            yield return WaitForReleasedLoads();
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
