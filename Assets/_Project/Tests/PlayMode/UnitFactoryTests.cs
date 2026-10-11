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
            Object.Destroy(root);
            yield return null;
            Assert.That(AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero);
            Assert.That(Unit.All.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AddressableTankSpawnsOnNavMeshAndDeathReleasesItsReferenceOnce()
        {
            var provider = new AddressableUnitPrefabProvider();
            factory = new UnitFactory(provider);
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
            Assert.That(provider.ReleasedHandles, Is.EqualTo(1));
            factory.Dispose();
            yield return null;
            Assert.That(provider.ReleasedHandles, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InvalidNavMeshSpawnReleasesLoadedPrefabWithoutCreatingUnit()
        {
            var provider = new AddressableUnitPrefabProvider();
            factory = new UnitFactory(provider);
            System.Exception failure = null;
            yield return factory.SpawnAsync("units/Scout", Vector3.one * 1000, root.transform, CancellationToken.None)
                .ToCoroutine(exceptionHandler: error => failure = error);
            Assert.That(failure, Is.TypeOf<System.InvalidOperationException>());
            Assert.That(factory.ActiveInstances, Is.Zero);
            Assert.That(provider.ReleasedHandles, Is.EqualTo(1));
        }
    }
}
