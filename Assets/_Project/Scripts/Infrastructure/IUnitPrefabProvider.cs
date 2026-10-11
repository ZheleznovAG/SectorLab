using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SectorLab.Infrastructure
{
    // The asynchronous resource boundary is the only abstraction needed by lifecycle tests.
    public interface IUnitPrefabProvider
    {
        UniTask<UnitPrefabLease> LoadAsync(string key, CancellationToken cancellationToken);
    }

    public sealed class UnitPrefabLease : IDisposable
    {
        public GameObject Prefab { get; }
        private Action release;

        public UnitPrefabLease(GameObject prefab, Action release)
        {
            Prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            this.release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public void Dispose()
        {
            Action ownedRelease = release;
            release = null;
            ownedRelease?.Invoke();
        }
    }
}
