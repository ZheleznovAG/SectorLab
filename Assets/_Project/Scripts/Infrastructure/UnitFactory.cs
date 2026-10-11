using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.AI;

namespace SectorLab.Infrastructure
{
    public sealed class UnitFactory : IDisposable
    {
        private readonly IUnitPrefabProvider provider;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly Dictionary<Unit, UnitPrefabOwnership> instances = new Dictionary<Unit, UnitPrefabOwnership>(6);
        private bool disposed;
        public int ActiveInstances => instances.Count;

        public UnitFactory(IUnitPrefabProvider provider) => this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

        public async UniTask<Unit> SpawnAsync(string key, Vector3 position, Transform parent, CancellationToken cancellationToken)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UnitFactory));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
            UnitPrefabLease lease = await provider.LoadAsync(key, linked.Token);
            GameObject instance = null;
            bool transferred = false;
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                if (lease.Prefab.GetComponent<Unit>() == null || lease.Prefab.GetComponent<NavMeshAgent>() == null)
                    throw new InvalidOperationException("Unit prefab requires Unit and NavMeshAgent.");
                if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 0.75f, NavMesh.AllAreas))
                    throw new InvalidOperationException("Unit spawn is outside the NavMesh.");
                float baseOffset = lease.Prefab.GetComponent<NavMeshAgent>().baseOffset;
                instance = UnityEngine.Object.Instantiate(lease.Prefab, hit.position + Vector3.up * baseOffset, Quaternion.identity, parent);
                var unit = instance.GetComponent<Unit>();
                var ownership = instance.AddComponent<UnitPrefabOwnership>();
                ownership.Initialize(unit, lease, OnDestroyed);
                instances.Add(unit, ownership);
                transferred = true;
                return unit;
            }
            finally
            {
                if (!transferred)
                {
                    if (instance != null) UnityEngine.Object.Destroy(instance);
                    lease.Dispose();
                }
            }
        }

        private void OnDestroyed(Unit unit) => instances.Remove(unit);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            foreach (var entry in instances)
                if (entry.Value != null) UnityEngine.Object.Destroy(entry.Value.gameObject);
            lifetime.Dispose();
        }
    }
}
