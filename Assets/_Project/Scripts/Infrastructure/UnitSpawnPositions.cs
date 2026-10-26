using SectorLab.Units;
using UnityEngine;
using UnityEngine.AI;

namespace SectorLab.Infrastructure
{
    public static class UnitSpawnPositions
    {
        private static readonly Vector3[] offsets = {
            Vector3.zero, new Vector3(0,0,-0.8f), new Vector3(0,0,0.8f),
            new Vector3(-0.8f,0,0), new Vector3(0.8f,0,0),
            new Vector3(-1.1f,0,-1.1f), new Vector3(1.1f,0,-1.1f),
            new Vector3(-1.1f,0,1.1f), new Vector3(1.1f,0,1.1f)
        };

        public static bool TryFind(UnitRegistry units, Vector3 preferred, float radius, out Vector3 position)
        {
            for (int i = 0; i < offsets.Length; i++)
            {
                if (!NavMesh.SamplePosition(preferred + offsets[i], out NavMeshHit hit, 0.35f, NavMesh.AllAreas)) continue;
                Vector3 deviation = hit.position - preferred;
                deviation.y = 0;
                if (deviation.sqrMagnitude > 1.6f * 1.6f) continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * (radius + 0.05f),
                    hit.position + Vector3.up * 1.5f, radius, 1, QueryTriggerInteraction.Ignore)) continue;
                bool occupied = false;
                for (int j = 0; j < units.All.Count; j++)
                {
                    Unit unit = units.All[j];
                    if (!unit.IsAlive) continue;
                    var agent = unit.GetComponent<NavMeshAgent>();
                    float otherRadius = agent != null ? agent.radius * Mathf.Max(unit.transform.lossyScale.x, unit.transform.lossyScale.z) : 0.7f;
                    Vector3 separation = hit.position - unit.transform.position;
                    separation.y = 0;
                    float clearance = radius + otherRadius + 0.15f;
                    if (separation.sqrMagnitude < clearance * clearance) { occupied = true; break; }
                }
                if (occupied) continue;
                position = hit.position;
                return true;
            }
            position = default;
            return false;
        }
    }

    public sealed class UnitSpawnBlockedException : System.InvalidOperationException
    {
        public UnitSpawnBlockedException() : base("No safe NavMesh unit spawn is available near the team's base.") { }
    }
}
