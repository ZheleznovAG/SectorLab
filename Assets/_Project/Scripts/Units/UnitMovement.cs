using UnityEngine;
using UnityEngine.AI;

namespace SectorLab.Units
{
    // Moves the unit along a NavMesh path, around obstacles and other agents.
    // A thin wrapper so the rest of the code only sees MoveTo / Stop / IsMoving.
    [RequireComponent(typeof(NavMeshAgent))]
    public class UnitMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 3f;
        [Tooltip("How close to the destination counts as arrived.")]
        [SerializeField, Min(0.01f)] private float arriveDistance = 0.1f;

        // Lower value = more important. An agent ignores less important agents in its own
        // avoidance, so a unit under orders walks on and a standing unit is pushed aside
        // instead of blocking a passage. The same rule applies to both teams.
        public const int MovingPriority = 30;
        public const int IdlePriority = 70;

        private NavMeshAgent agent;

        public bool IsMoving { get; private set; }
        public Vector3 Destination { get; private set; }
        public float Speed => speed;

        // Lazy, because Unit.Awake may configure us before our own Awake runs.
        private NavMeshAgent Agent => agent != null ? agent : agent = GetComponent<NavMeshAgent>();

        private void Awake()
        {
            Agent.speed = speed;
            Agent.avoidancePriority = IdlePriority;
        }

        public void Configure(float newSpeed)
        {
            speed = Mathf.Max(0f, newSpeed);
            Agent.speed = speed;
        }

        public bool MoveTo(Vector3 point, float stoppingDistance = 0f)
        {
            if (!Agent.isActiveAndEnabled || !Agent.isOnNavMesh)
            {
                Stop();
                return false;
            }

            Agent.stoppingDistance = Mathf.Max(arriveDistance, stoppingDistance);
            if (!Agent.SetDestination(point))
            {
                Stop();
                return false;
            }
            Destination = point;
            IsMoving = true;
            Agent.avoidancePriority = MovingPriority;
            return true;
        }

        public void Stop()
        {
            IsMoving = false;
            Agent.avoidancePriority = IdlePriority;
            if (Agent.isActiveAndEnabled && Agent.isOnNavMesh)
            {
                Agent.ResetPath();
                // Otherwise the agent would coast to a stop instead of halting on the spot.
                Agent.velocity = Vector3.zero;
            }
        }

        private void Update()
        {
            if (!IsMoving)
                return;

            if (!Agent.isActiveAndEnabled || !Agent.isOnNavMesh)
            {
                Stop();
                return;
            }
            if (Agent.pathPending)
                return;

            // A partial path may end short of Destination; IsMoving describes the agent,
            // not a promise that the requested point is reachable.
            if (!Agent.hasPath || Agent.pathStatus == NavMeshPathStatus.PathInvalid || Agent.remainingDistance <= Mathf.Max(arriveDistance, Agent.stoppingDistance))
                Stop();
        }
    }
}
