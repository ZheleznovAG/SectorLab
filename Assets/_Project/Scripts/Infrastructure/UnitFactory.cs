using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using SectorLab.Battle;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.AI;

namespace SectorLab.Infrastructure
{
    public enum UnitSpawnReason { Initial, Respawn }

    public sealed class UnitFactory : IDisposable
    {
        private readonly IUnitPrefabProvider provider;
        private readonly MatchState state;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly Dictionary<Unit, UnitPrefabOwnership> instances = new Dictionary<Unit, UnitPrefabOwnership>(6);
        private bool disposed;
        public int ActiveInstances => instances.Count;

        public UnitFactory(IUnitPrefabProvider provider, MatchState state)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public async UniTask<Unit> SpawnAsync(string key, Vector3 position, Transform parent, CancellationToken cancellationToken,
            UnitSpawnReason reason = UnitSpawnReason.Initial)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UnitFactory));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
            UnitPrefabLease lease = await provider.LoadAsync(key, linked.Token);
            GameObject instance = null;
            bool transferred = false;
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                if (reason == UnitSpawnReason.Respawn)
                    await UniTask.WaitUntil(() => state.IsRunning, cancellationToken: linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                if (state.Outcome != BattleOutcome.InProgress) throw new OperationCanceledException(linked.Token);
                if (lease.Prefab.GetComponent<Unit>() == null || lease.Prefab.GetComponent<NavMeshAgent>() == null)
                    throw new InvalidOperationException("Unit prefab requires Unit and NavMeshAgent.");
                var prefabAgent = lease.Prefab.GetComponent<NavMeshAgent>();
                float radius = prefabAgent.radius * Mathf.Max(lease.Prefab.transform.localScale.x, lease.Prefab.transform.localScale.z);
                if (!UnitSpawnPositions.TryFind(state.Units, position, radius, out Vector3 spawn)) throw new UnitSpawnBlockedException();
                instance = UnityEngine.Object.Instantiate(lease.Prefab, spawn + Vector3.up * prefabAgent.baseOffset, Quaternion.identity, parent);
                var unit = instance.GetComponent<Unit>();
                unit.BindRegistry(state.Units);
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

        public void CancelPendingOperations() { if (!disposed) lifetime.Cancel(); }

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
