using SectorLab.Units;
using UnityEngine;
using UnityEngine.AI;

namespace SectorLab.Objectives
{
    // Occupancy is sampled on the match tick, including death/disable without trigger bookkeeping.
    public sealed class RelayPoint : MonoBehaviour
    {
        [SerializeField] private RelayConfig config;
        [SerializeField] private Renderer areaRenderer;
        [SerializeField] private TextMesh statusLabel;
        private MaterialPropertyBlock colors;
        private Camera viewCamera;

        public RelayConfig Config => config;
        public RelayState State { get; private set; }
        public int PlayersPresent { get; private set; }
        public int EnemiesPresent { get; private set; }

        private void Awake()
        {
            State = new RelayState(config != null ? config.captureSeconds : 5);
            colors = new MaterialPropertyBlock();
            viewCamera = Camera.main;
            UpdateVisual();
        }

        // Used by scene setup and small integration fixtures before simulation starts.
        public void Configure(RelayConfig data, Renderer area = null, TextMesh label = null)
        {
            config = data;
            areaRenderer = area;
            statusLabel = label;
            State = new RelayState(data.captureSeconds);
        }

        public bool Contains(Unit unit)
        {
            if (unit == null || !unit.IsAlive || !unit.isActiveAndEnabled)
                return false;
            Vector3 offset = unit.transform.position - transform.position;
            offset.y = 0;
            return offset.sqrMagnitude <= config.radius * config.radius;
        }

        public void Simulate(double seconds)
        {
            PlayersPresent = 0;
            EnemiesPresent = 0;
            for (int i = 0; i < Unit.All.Count; i++)
            {
                Unit unit = Unit.All[i];
                if (!Contains(unit)) continue;
                if (unit.Team == Team.Player) PlayersPresent++;
                else EnemiesPresent++;
            }
            State.Step(seconds, PlayersPresent, EnemiesPresent);
            UpdateVisual();
        }

        public bool TryGetApproachPoint(Unit unit, out Vector3 destination)
        {
            destination = default;
            if (unit == null || !unit.IsAlive || config == null)
                return false;
            var agent = unit.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
                return false;
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, config.radius - 0.1f, agent.areaMask))
                return false;
            Vector3 offset = hit.position - transform.position;
            offset.y = 0;
            if (offset.sqrMagnitude >= config.radius * config.radius)
                return false;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;
            destination = hit.position;
            return true;
        }

        private void LateUpdate()
        {
            if (statusLabel != null && viewCamera != null)
                statusLabel.transform.rotation = viewCamera.transform.rotation;
        }

        private void UpdateVisual()
        {
            Color color = State.IsContested ? new Color(1f, 0.76f, 0.18f)
                : State.Owner == RelayOwner.Player ? new Color(0.18f, 0.55f, 1f)
                : State.Owner == RelayOwner.Enemy ? new Color(0.94f, 0.3f, 0.27f)
                : new Color(0.45f, 0.49f, 0.58f);
            if (areaRenderer != null)
            {
                if (colors == null) colors = new MaterialPropertyBlock();
                colors.SetColor("_BaseColor", color);
                areaRenderer.SetPropertyBlock(colors);
            }
            if (statusLabel != null && config != null)
            {
                string side = State.Progress > 0 ? "BLUE" : State.Progress < 0 ? "RED" : "FREE";
                statusLabel.text = $"{config.label}  {(config.kind == RelayKind.Score ? "+1/s" : "+6 HP/s")}\n{side} {System.Math.Abs(State.Progress) * 100:0}%{(State.IsContested ? " !" : "")}";
            }
        }
    }
}
