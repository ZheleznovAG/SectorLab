using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SectorLab.Infrastructure
{
    public sealed class AddressableUnitPrefabProvider : IUnitPrefabProvider
    {
        // Diagnostics count this adapter's references, including pending loads, across scene restarts.
        public static int OutstandingHandles { get; private set; }
        public int ReleasedHandles { get; private set; }

        public async UniTask<UnitPrefabLease> LoadAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Unit prefab key is empty.", nameof(key));
            // Missing configuration is a normal recoverable failure. Resolve first to avoid Unity's
            // InvalidKeyException logger, which can produce delayed errors while formatting a stack trace.
            var locations = Addressables.LoadResourceLocationsAsync(key, typeof(GameObject));
            OutstandingHandles++;
            try
            {
                await locations.ToUniTask();
                cancellationToken.ThrowIfCancellationRequested();
                if (locations.Result.Count == 0)
                    throw new InvalidOperationException("Unit prefab not found: " + key);
            }
            finally
            {
                Release(locations);
            }
            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(key);
            OutstandingHandles++;
            bool transferred = false;
            try
            {
                // AssetBundle IO may finish after cancellation. Keep ownership until the physical result exists.
                await handle.ToUniTask();
                cancellationToken.ThrowIfCancellationRequested();
                var lease = new UnitPrefabLease(handle.Result, () => Release(handle));
                transferred = true;
                return lease;
            }
            finally
            {
                if (!transferred) Release(handle);
            }
        }

        private void Release<T>(AsyncOperationHandle<T> handle)
        {
            if (!handle.IsValid()) throw new InvalidOperationException("Unit prefab handle lost its owner before release.");
            Addressables.Release(handle);
            OutstandingHandles--;
            ReleasedHandles++;
        }
    }
}
