using System;
using System.Collections.Generic;

namespace SectorLab.Units
{
    // Owned by one MatchState. Registration order is stable; queries never cross a match boundary.
    public sealed class UnitRegistry
    {
        private readonly List<Unit> units = new List<Unit>(6);
        public IReadOnlyList<Unit> All => units;
        internal void Add(Unit unit) { if (!units.Contains(unit)) units.Add(unit); }
        internal void Remove(Unit unit) => units.Remove(unit);

        public Unit Find(int id)
        {
            if (id < 0) return null;
            Unit dead = null;
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit == null || unit.Id != id) continue;
                if (unit.IsAlive) return unit;
                dead = unit;
            }
            return dead;
        }

        internal void ValidateId(Unit unit, int id)
        {
            Unit existing = Find(id);
            if (existing != null && existing != unit && existing.IsAlive)
                throw new InvalidOperationException("A living roster id is already registered in this match.");
        }
    }
}
