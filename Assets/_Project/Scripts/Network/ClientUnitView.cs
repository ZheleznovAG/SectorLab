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
        private readonly LineRenderer[] shotLines;
        private readonly double[] hideShotAt;
        private readonly UnitView[] previous;
        private ulong observedMatch;
        private readonly MatchModel model;
        private readonly ClientMotion motion = new ClientMotion();

        public ClientUnitView(UnitLifecycle lifecycle, MatchModel model)
        {
            this.model = model;
            int count = lifecycle.Slots.Count;
            units = new Unit[count]; renderers = new Renderer[count][]; colliders = new Collider[count][];
            shotLines = new LineRenderer[count]; hideShotAt = new double[count]; previous = new UnitView[count];
            for (int i = 0; i < count; i++)
            {
                Unit unit = units[i] = lifecycle.Slots[i].Unit;
                unit.GetComponent<NavMeshAgent>().enabled = false;
                unit.Movement.enabled = false;
                if (unit.Attack != null) unit.Attack.enabled = false;
                shotLines[i] = unit.Attack != null ? unit.Attack.ShotLine : null;
                renderers[i] = unit.GetComponentsInChildren<Renderer>(true);
                colliders[i] = unit.GetComponentsInChildren<Collider>(true);
            }
            Apply();
        }

        public void Apply()
        {
            motion.Push(model, Time.unscaledTimeAsDouble);
            bool sameMatch = observedMatch == model.MatchId && model.IsSynchronized;
            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];
                if (unit == null) continue;
                bool found = model.TryGetUnit(unit.Id, out UnitView state);
                bool alive = model.IsSynchronized && found && state.Alive;
                unit.Health.SetReplicated(alive ? state.Health : 0);
                unit.Generation = state.Generation;
                foreach (Renderer renderer in renderers[i])
                    if (renderer != null && (!alive || !(renderer is LineRenderer))) renderer.enabled = alive;
                foreach (Collider collider in colliders[i]) if (collider != null) collider.enabled = alive;
                LineRenderer line = shotLines[i];
                bool continuous = sameMatch && alive && previous[i].Alive && previous[i].Generation == state.Generation;
                if (line != null)
                {
                    if (!continuous) line.enabled = false;
                    if (continuous && state.Shots > previous[i].Shots)
                    {
                        line.useWorldSpace = true;
                        line.positionCount = 2;
                        line.SetPosition(1, new Vector3(state.ShotX, unit.transform.position.y, state.ShotZ));
                        line.enabled = true;
                        hideShotAt[i] = Time.unscaledTimeAsDouble + unit.Attack.ShotLineDuration;
                    }
                }
                previous[i] = model.IsSynchronized && found ? state : default;
            }
            observedMatch = model.MatchId;
            Render();
        }

        public void Render()
        {
            double now = Time.unscaledTimeAsDouble;
            for (int i = 0; i < units.Length; i++)
            {
                Unit unit = units[i];
                if (unit == null) continue;
                if (motion.TryPosition(unit.Id, now, out Vector2 point))
                    unit.transform.position = new Vector3(point.x, unit.transform.position.y, point.y);
                LineRenderer line = shotLines[i];
                if (line == null || !line.enabled) continue;
                if (now >= hideShotAt[i]) line.enabled = false;
                else line.SetPosition(0, unit.transform.position);
            }
        }
    }
}
