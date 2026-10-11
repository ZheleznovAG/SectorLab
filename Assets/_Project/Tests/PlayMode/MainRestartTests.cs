using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using SectorLab.AI;
using SectorLab.Battle;
using SectorLab.Infrastructure;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace SectorLab.Tests
{
    public class MainRestartTests
    {
        private UnitPrefabLease delayedLease;
        private DelayedProvider delayedProvider;

        private sealed class MissingKeyProvider : IUnitPrefabProvider
        {
            private readonly AddressableUnitPrefabProvider actual = new AddressableUnitPrefabProvider();
            public UniTask<UnitPrefabLease> LoadAsync(string key, CancellationToken token) =>
                actual.LoadAsync(key == "units/Ranger" ? "units/Missing-W5-Main" : key, token);
        }

        private sealed class DelayedProvider : IUnitPrefabProvider
        {
            public readonly UniTaskCompletionSource<UnitPrefabLease> Completion = new UniTaskCompletionSource<UnitPrefabLease>();
            public CancellationToken Token;
            public int Requests;
            public UniTask<UnitPrefabLease> LoadAsync(string key, CancellationToken token)
            {
                Token = token;
                Requests++;
                return Completion.Task;
            }
        }

        [UnitySetUp]
        public IEnumerator LoadMain()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return MainLifecycleTests.WaitForReady();
            FreezeFrameSimulation();
        }

        private static void FreezeFrameSimulation()
        {
            Object.FindFirstObjectByType<BattleController>().enabled = false;
            Object.FindFirstObjectByType<EnemyTacticsController>().enabled = false;
            foreach (Unit unit in Unit.All) { unit.Attack.enabled = false; unit.Movement.Stop(); }
        }

        private static T Field<T>(Object target, string name) => (T)target.GetType()
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(target);

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (delayedProvider != null && delayedLease != null) delayedProvider.Completion.TrySetResult(delayedLease);
            delayedLease?.Dispose();
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("AfterRestarts");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
            yield return null;
            yield return null;
            Assert.That(Unit.All.Count, Is.Zero);
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero);
            Time.timeScale = 1;
        }

        [UnityTest]
        public IEnumerator TenRealRestartsDisposeOldFactoriesAndResetSlotsScoresClockAndRelays()
        {
            string report = "utc,restart,old_factory_instances,new_units,owned_handles,clock,player_score,enemy_score\n";
            for (int i = 1; i <= 10; i++)
            {
                var oldScope = Object.FindFirstObjectByType<GameLifetimeScope>();
                var oldFactory = oldScope.Container.Resolve<UnitFactory>();
                var oldState = oldScope.Container.Resolve<MatchState>();
                var oldLifecycle = Object.FindFirstObjectByType<UnitLifecycle>();
                var battle = Object.FindFirstObjectByType<BattleController>();
                oldLifecycle.Slots[i % 6].Unit.Health.TakeDamage(oldLifecycle.Slots[i % 6].Unit.Health.Max);
                battle.AdvanceSimulation(3);
                battle.Relays[0].State.Step(3, 1, 0); // Seed partial progress explicitly for the reset assertion.
                oldState.Tick(1, 3, 2);
                Assert.That(oldState.PlayerScore, Is.GreaterThan(0));
                battle.Restart();
                yield return MainLifecycleTests.WaitForReady();
                yield return null;
                FreezeFrameSimulation();
                var scope = Object.FindFirstObjectByType<GameLifetimeScope>();
                var state = scope.Container.Resolve<MatchState>();
                var current = Object.FindFirstObjectByType<UnitLifecycle>();
                Assert.That(state, Is.Not.SameAs(oldState));
                Assert.That(state.Clock, Is.Not.SameAs(oldState.Clock));
                Assert.That(oldFactory.ActiveInstances, Is.Zero);
                Assert.That(Unit.All.Count, Is.EqualTo(6));
                Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.EqualTo(6));
                Assert.That(state.PlayerScore, Is.Zero);
                Assert.That(state.EnemyScore, Is.Zero);
                Assert.That(state.Clock.ElapsedSeconds, Is.LessThan(0.5));
                foreach (UnitSlotState slot in current.Slots)
                {
                    Assert.That(slot.Unit, Is.Not.Null);
                    Assert.That(slot.Respawn.IsWaiting, Is.False);
                    Assert.That(slot.IsSpawning, Is.False);
                }
                foreach (var relay in Object.FindFirstObjectByType<BattleController>().Relays)
                {
                    Assert.That(relay.State.Owner, Is.EqualTo(SectorLab.Objectives.RelayOwner.Neutral));
                    Assert.That(relay.State.Progress, Is.EqualTo(0).Within(0.05));
                }
                report += System.DateTime.UtcNow.ToString("o") + "," + i + "," + oldFactory.ActiveInstances + ","
                    + Unit.All.Count + "," + AddressableUnitPrefabProvider.OutstandingHandles + ","
                    + state.Clock.ElapsedSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",0,0\n";
#if UNITY_EDITOR
                string task = System.Environment.GetEnvironmentVariable("SECTORLAB_VERIFICATION_TASK") ?? "manual";
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath,
                    "../docs/agent_execution/evidence/" + task + "-main-restarts.csv"), report);
#endif
            }
        }

        [UnityTest]
        public IEnumerator MissingKeyInRealMainShowsErrorAndRetryStartsFreshMatch()
        {
            // Two successful real loads precede the missing Ranger key and must also be cleaned up.
            var missing = new MissingKeyProvider();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Unit lifecycle failed: Unit prefab not found"));
            using (LifetimeScope.Enqueue(builder => builder.RegisterInstance<IUnitPrefabProvider>(missing)))
                yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            float deadline = Time.realtimeSinceStartup + 10;
            var lifecycle = Object.FindFirstObjectByType<UnitLifecycle>();
            while (lifecycle.Phase == UnitLifecyclePhase.Idle || lifecycle.Phase == UnitLifecyclePhase.Loading)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
            yield return null;
            yield return null;
            Assert.That(lifecycle.Phase, Is.EqualTo(UnitLifecyclePhase.Failed));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Unit.All.Count, Is.Zero);
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero);
            foreach (UnitSlotState slot in lifecycle.Slots)
            {
                Assert.That(slot.IsSpawning, Is.False);
                Assert.That(slot.Respawn.IsWaiting, Is.False);
            }
            var view = Object.FindFirstObjectByType<BattleResultView>();
            Assert.That(view.IsShowingLoadError, Is.True);
            Assert.That(Field<GameObject>(view, "panel").activeInHierarchy, Is.True);
            var retry = Field<Button>(view, "restartButton");
            var exit = Field<Button>(view, "exitButton");
            Assert.That(retry.gameObject.activeInHierarchy && exit.gameObject.activeInHierarchy, Is.True);
            retry.onClick.Invoke();
            yield return MainLifecycleTests.WaitForReady();
            Assert.That(Unit.All.Count, Is.EqualTo(6));
            Assert.That(Object.FindFirstObjectByType<BattleResultView>().IsShowingLoadError, Is.False);
        }

        [UnityTest]
        public IEnumerator RestartDuringMainLoadingCancelsOldRequestAndReleasesItsLatePrefab()
        {
            var provider = new AddressableUnitPrefabProvider();
            yield return provider.LoadAsync("units/Tank", CancellationToken.None).ToCoroutine(value => delayedLease = value);
            delayedProvider = new DelayedProvider();
            using (LifetimeScope.Enqueue(builder => builder.RegisterInstance<IUnitPrefabProvider>(delayedProvider)))
                yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            var oldFactory = Object.FindFirstObjectByType<GameLifetimeScope>().Container.Resolve<UnitFactory>();
            var lifecycle = Object.FindFirstObjectByType<UnitLifecycle>();
            Assert.That(lifecycle.Phase, Is.EqualTo(UnitLifecyclePhase.Loading));
            Assert.That(delayedProvider.Requests, Is.EqualTo(1));
            Assert.That(Unit.All.Count, Is.Zero);
            Object.FindFirstObjectByType<BattleController>().Restart();
            yield return MainLifecycleTests.WaitForReady();
            Assert.That(delayedProvider.Token.IsCancellationRequested, Is.True);
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.EqualTo(7));
            delayedProvider.Completion.TrySetResult(delayedLease);
            yield return null;
            yield return null;
            Assert.That(oldFactory.ActiveInstances, Is.Zero);
            Assert.That(Unit.All.Count, Is.EqualTo(6));
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.EqualTo(6));
            Assert.That(provider.ReleasedHandles, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator LoadErrorPanelWiresRetryAndExitActionsWithoutAccumulatingListeners()
        {
            var view = Object.FindFirstObjectByType<BattleResultView>();
            int retries = 0, exits = 0;
            view.ShowLoadError(() => retries++, () => exits++);
            view.ShowLoadError(() => retries++, () => exits++);
            Field<Button>(view, "restartButton").onClick.Invoke();
            Field<Button>(view, "exitButton").onClick.Invoke();
            Assert.That(retries, Is.EqualTo(1));
            Assert.That(exits, Is.EqualTo(1));
            yield return null;
        }
    }
}
