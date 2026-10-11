using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using SectorLab.Infrastructure;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class UnitFactoryTests
    {
        private GameObject root;
        private UnitFactory factory;
        private GameObject delayedPrefab;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("FactoryArena");
            TestArena.CreateGround(root.transform);
            TestArena.Bake(root);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            factory?.Dispose();
            if (delayedPrefab != null) Object.Destroy(delayedPrefab);
            Object.Destroy(root);
            yield return null;
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero);
            Assert.That(Unit.All.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AddressableTankSpawnsOnNavMeshAndDeathReleasesItsReferenceOnce()
        {
            var provider = new AddressableUnitPrefabProvider();
            factory = new UnitFactory(provider, new SectorLab.Battle.MatchState(new SectorLab.Battle.MatchClock()));
            Unit spawned = null;
            yield return factory.SpawnAsync("units/Tank", Vector3.zero, root.transform, CancellationToken.None)
                .ToCoroutine(unit => spawned = unit);
            Assert.That(spawned, Is.Not.Null);
            Assert.That(spawned.Stats.name, Is.EqualTo("Tank"));
            Assert.That(spawned.Team, Is.EqualTo(Team.Player));
            Assert.That(spawned.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh, Is.True);
            Assert.That(factory.ActiveInstances, Is.EqualTo(1));
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.EqualTo(1));
            spawned.Health.TakeDamage(spawned.Health.Max);
            yield return null;
            yield return null;
            Assert.That(factory.ActiveInstances, Is.Zero);
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero);
            Assert.That(provider.ReleasedHandles, Is.EqualTo(2));
            factory.Dispose();
            yield return null;
            Assert.That(provider.ReleasedHandles, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator InvalidNavMeshSpawnReleasesLoadedPrefabWithoutCreatingUnit()
        {
            var provider = new AddressableUnitPrefabProvider();
            factory = new UnitFactory(provider, new SectorLab.Battle.MatchState(new SectorLab.Battle.MatchClock()));
            System.Exception failure = null;
            yield return factory.SpawnAsync("units/Scout", Vector3.one * 1000, root.transform, CancellationToken.None)
                .ToCoroutine(exceptionHandler: error => failure = error);
            Assert.That(failure, Is.InstanceOf<System.InvalidOperationException>());
            Assert.That(factory.ActiveInstances, Is.Zero);
            Assert.That(provider.ReleasedHandles, Is.EqualTo(2));
        }

        private sealed class DelayedProvider : IUnitPrefabProvider
        {
            public readonly UniTaskCompletionSource<UnitPrefabLease> Completion = new UniTaskCompletionSource<UnitPrefabLease>();
            public int Requests { get; private set; }
            public CancellationToken Token { get; private set; }
            public UniTask<UnitPrefabLease> LoadAsync(string key, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests++;
                Token = cancellationToken;
                // Intentionally finish later even when token is cancelled: this models physical IO.
                return Completion.Task;
            }
        }

        [UnityTest]
        public IEnumerator CancelDuringLoadDisposesLateResultWithoutCreatingAGhost()
        {
            var provider = new DelayedProvider();
            factory = new UnitFactory(provider, new SectorLab.Battle.MatchState(new SectorLab.Battle.MatchClock()));
            using var cancellation = new CancellationTokenSource();
            System.Exception failure = null;
            var operation = factory.SpawnAsync("delayed", Vector3.zero, root.transform, cancellation.Token)
                .ToCoroutine(exceptionHandler: error => failure = error);
            Assert.That(provider.Requests, Is.EqualTo(1));
            cancellation.Cancel();
            Assert.That(provider.Token.IsCancellationRequested, Is.True);
            delayedPrefab = new GameObject("DelayedPrefab");
            int released = 0;
            var lease = new UnitPrefabLease(delayedPrefab, () => released++);
            provider.Completion.TrySetResult(lease);
            yield return operation;
            Assert.That(failure, Is.InstanceOf<System.OperationCanceledException>());
            Assert.That(factory.ActiveInstances, Is.Zero);
            Assert.That(Unit.All.Count, Is.Zero);
            Assert.That(released, Is.EqualTo(1));
            lease.Dispose();
            Assert.That(released, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisposeDuringLoadCancelsTheScopeAndReleasesLateResultOnce()
        {
            var provider = new DelayedProvider();
            factory = new UnitFactory(provider, new SectorLab.Battle.MatchState(new SectorLab.Battle.MatchClock()));
            System.Exception failure = null;
            var operation = factory.SpawnAsync("delayed", Vector3.zero, root.transform, CancellationToken.None)
                .ToCoroutine(exceptionHandler: error => failure = error);
            factory.Dispose();
            factory.Dispose();
            Assert.That(provider.Token.IsCancellationRequested, Is.True);
            delayedPrefab = new GameObject("LatePrefab");
            int released = 0;
            provider.Completion.TrySetResult(new UnitPrefabLease(delayedPrefab, () => released++));
            yield return operation;
            Assert.That(failure, Is.InstanceOf<System.OperationCanceledException>());
            Assert.That(released, Is.EqualTo(1));
            Assert.That(factory.ActiveInstances, Is.Zero);
            System.Exception rejected = null;
            yield return factory.SpawnAsync("delayed", Vector3.zero, root.transform, CancellationToken.None)
                .ToCoroutine(exceptionHandler: error => rejected = error);
            Assert.That(rejected, Is.TypeOf<System.ObjectDisposedException>());
            Assert.That(provider.Requests, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AlreadyCancelledRequestDoesNotStartAnAddressableOperation()
        {
            var provider = new AddressableUnitPrefabProvider();
            factory = new UnitFactory(provider, new SectorLab.Battle.MatchState(new SectorLab.Battle.MatchClock()));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            System.Exception failure = null;
            yield return factory.SpawnAsync("units/Tank", Vector3.zero, root.transform, cancellation.Token)
                .ToCoroutine(exceptionHandler: error => failure = error);
            Assert.That(failure, Is.InstanceOf<System.OperationCanceledException>());
            Assert.That(provider.ReleasedHandles, Is.Zero);
            Assert.That(factory.ActiveInstances, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MissingAddressableKeyFailsAndReleasesFailedHandle()
        {
            var provider = new AddressableUnitPrefabProvider();
            factory = new UnitFactory(provider, new SectorLab.Battle.MatchState(new SectorLab.Battle.MatchClock()));
            System.Exception failure = null;
            yield return factory.SpawnAsync("units/Missing-W5-asset", Vector3.zero, root.transform, CancellationToken.None)
                .ToCoroutine(exceptionHandler: error => failure = error);
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Message, Does.Contain("units/Missing-W5-asset"));
            Assert.That(provider.ReleasedHandles, Is.EqualTo(1));
            Assert.That(factory.ActiveInstances, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LoadedRespawnWaitsForResumeBeforeCreatingItsClone()
        {
            var provider = new AddressableUnitPrefabProvider();
            var state = new SectorLab.Battle.MatchState(new SectorLab.Battle.MatchClock());
            state.SetPaused(true);
            factory = new UnitFactory(provider, state);
            Unit unit = null;
            var task = factory.SpawnAsync("units/Scout", Vector3.zero, root.transform, CancellationToken.None, UnitSpawnReason.Respawn);
            float deadline = Time.realtimeSinceStartup + 5;
            while (provider.ReleasedHandles < 1 && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            yield return null;
            Assert.That(provider.ReleasedHandles, Is.EqualTo(1));
            Assert.That(factory.ActiveInstances, Is.Zero);
            Assert.That(Unit.All.Count, Is.Zero);
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.EqualTo(1));
            state.SetPaused(false);
            yield return task.ToCoroutine(value => unit = value);
            Assert.That(unit, Is.Not.Null);
            Assert.That(factory.ActiveInstances, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisposingFactoryDestroysLiveCloneAndReleasesActualHandle()
        {
            var provider = new AddressableUnitPrefabProvider();
            factory = new UnitFactory(provider, new SectorLab.Battle.MatchState(new SectorLab.Battle.MatchClock()));
            Unit unit = null;
            yield return factory.SpawnAsync("units/EnemyRanger", Vector3.zero, root.transform, CancellationToken.None)
                .ToCoroutine(value => unit = value);
            Assert.That(unit.Team, Is.EqualTo(Team.Enemy));
            Assert.That(provider.ReleasedHandles, Is.EqualTo(1), "Location lookup is released; prefab remains owned by its clone.");
            factory.Dispose();
            yield return null;
            yield return null;
            Assert.That(unit == null, Is.True);
            Assert.That(factory.ActiveInstances, Is.Zero);
            Assert.That(provider.ReleasedHandles, Is.EqualTo(2));
        }
    }
}
