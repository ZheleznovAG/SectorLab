using SectorLab.Battle;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.AI;

namespace SectorLab.Network
{
    // Owns only proxy presentation. No movement, combat, capture or respawn simulation runs here.
    public sealed class ClientUnitView
    {
        private readonly Unit[] units;
        private readonly Renderer[][] renderers;
        private readonly Collider[][] colliders;
        private readonly MatchModel model;

        public ClientUnitView(UnitLifecycle lifecycle, MatchModel model)
        {
            this.model = model;
            int count = lifecycle.Slots.Count;
            units = new Unit[count]; renderers = new Renderer[count][]; colliders = new Collider[count][];
            for (int i = 0; i < count; i++)
            {
                Unit unit = units[i] = lifecycle.Slots[i].Unit;
                unit.GetComponent<NavMeshAgent>().enabled = false;
                unit.Movement.enabled = false;
                if (unit.Attack != null) unit.Attack.enabled = false;
                renderers[i] = unit.GetComponentsInChildren<Renderer>(true);
                colliders[i] = unit.GetComponentsInChildren<Collider>(true);
            }
            Apply();
        }

        public void Apply()
        {
            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];
                if (unit == null) continue;
                bool alive = model.IsSynchronized && model.TryGetUnit(unit.Id, out UnitView state) && state.Alive;
                // Definite assignment is separate from the short-circuited availability check.
                model.TryGetUnit(unit.Id, out state);
                unit.Health.SetReplicated(alive ? state.Health : 0);
                unit.Generation = state.Generation;
                if (alive) unit.transform.position = new Vector3(state.X, unit.transform.position.y, state.Z);
                foreach (Renderer renderer in renderers[i]) if (renderer != null) renderer.enabled = alive;
                foreach (Collider collider in colliders[i]) if (collider != null) collider.enabled = alive;
            }
        }
    }
}
