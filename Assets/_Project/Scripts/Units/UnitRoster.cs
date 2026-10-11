using System;
using System.Collections.Generic;
using UnityEngine;

namespace SectorLab.Units
{
    public enum UnitRole { Tank, Scout, Ranger }

    [Serializable]
    public sealed class UnitSpawnSlot
    {
        public Team team;
        public UnitRole role;
        public string prefabKey;
        public Vector3 position;
    }

    // Immutable during a match. Runtime units/countdowns live in UnitLifecycle, never in this asset.
    [CreateAssetMenu(fileName = "UnitRoster", menuName = "SectorLab/Unit Roster")]
    public sealed class UnitRoster : ScriptableObject
    {
        public UnitSpawnSlot[] slots = Array.Empty<UnitSpawnSlot>();

        public void Validate()
        {
            if (slots == null || slots.Length != 6) throw new InvalidOperationException("A match requires six unit slots.");
            var identities = new HashSet<int>();
            foreach (UnitSpawnSlot slot in slots)
            {
                if (slot == null || (int)slot.team < 0 || (int)slot.team > 1 || (int)slot.role < 0 || (int)slot.role > 2)
                    throw new InvalidOperationException("Invalid team/role in unit roster.");
                if (!identities.Add((int)slot.team * 3 + (int)slot.role))
                    throw new InvalidOperationException("Each team must have one unit of each role.");
                if (string.IsNullOrWhiteSpace(slot.prefabKey)) throw new InvalidOperationException("Unit prefab key is required.");
                if (!IsFinite(slot.position.x) || !IsFinite(slot.position.y) || !IsFinite(slot.position.z))
                    throw new InvalidOperationException("Unit spawn position must be finite.");
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
