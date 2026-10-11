using System;
using SectorLab.Units;
using UnityEngine;

namespace SectorLab.Infrastructure
{
    // A clone keeps its prefab reference until Unity actually destroys the clone, including deferred Destroy.
    public sealed class UnitPrefabOwnership : MonoBehaviour
    {
        private UnitPrefabLease lease;
        private Unit unit;
        private Action<Unit> destroyed;

        public void Initialize(Unit ownedUnit, UnitPrefabLease ownedLease, Action<Unit> onDestroyed)
        {
            unit = ownedUnit;
            lease = ownedLease;
            destroyed = onDestroyed;
        }

        private void OnDestroy()
        {
            lease?.Dispose();
            lease = null;
            destroyed?.Invoke(unit);
            destroyed = null;
        }
    }
}
