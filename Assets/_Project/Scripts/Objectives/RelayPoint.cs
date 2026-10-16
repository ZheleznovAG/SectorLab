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
        [Tooltip("Unlit material for the capture ring around the area; no ring without it.")]
        [SerializeField] private Material ringMaterial;
        private const int RingSegments = 64;
        private LineRenderer ring;
        private readonly Vector3[] ringPoints = new Vector3[RingSegments + 1];
        private MaterialPropertyBlock colors;
        private Camera viewCamera;
        private int visualKey = int.MinValue;
        private readonly System.Collections.Generic.Dictionary<Unit, double> healingFractions = new System.Collections.Generic.Dictionary<Unit, double>(6);
        private readonly System.Collections.Generic.List<Unit> staleHealing = new System.Collections.Generic.List<Unit>(6);

        public RelayConfig Config => config;
        public RelayState State { get; private set; }
        public int PlayersPresent { get; private set; }
        public int EnemiesPresent { get; private set; }
        // Share of the ring drawn: capture progress toward the side currently holding it.
        public float CaptureRingFill { get; private set; }
        public Color CaptureRingColor { get; private set; }

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
            healingFractions.Clear();
            visualKey = int.MinValue;
            UpdateVisual();
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
            HealOccupants();
            UpdateVisual();
        }

        private bool Owns(Unit unit) => State.Owner == RelayOwner.Player && unit.Team == Team.Player
            || State.Owner == RelayOwner.Enemy && unit.Team == Team.Enemy;

        private void HealOccupants()
        {
            if (config.kind != RelayKind.Repair)
            {
                healingFractions.Clear();
                return;
            }
            staleHealing.Clear();
            foreach (var entry in healingFractions)
                if (!Contains(entry.Key) || !Owns(entry.Key) || entry.Key.Health.Current == entry.Key.Health.Max)
                    staleHealing.Add(entry.Key);
            for (int i = 0; i < staleHealing.Count; i++) healingFractions.Remove(staleHealing[i]);
            if (State.EffectiveSeconds <= 0) return;
            for (int i = 0; i < Unit.All.Count; i++)
            {
                Unit unit = Unit.All[i];
                if (!Contains(unit) || !Owns(unit) || unit.Health.Current == unit.Health.Max) continue;
                healingFractions.TryGetValue(unit, out double fraction);
                fraction += State.EffectiveSeconds * config.healingPerSecond;
                int whole = (int)System.Math.Min(unit.Health.Max - unit.Health.Current, System.Math.Floor(fraction + 1e-6));
                unit.Health.Heal(whole);
                if (unit.Health.Current == unit.Health.Max) healingFractions.Remove(unit);
                else healingFractions[unit] = System.Math.Max(0, fraction - whole);
            }
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

        public float GetArrivalDistance(Vector3 approach)
        {
            Vector3 offset = approach - transform.position;
            offset.y = 0;
            // The largest existing agent has a 0.575 m world radius. Stopping too close
            // to the circle's edge lets the first arrivals block the following squadmate.
            return Mathf.Max(0.1f, config.radius - offset.magnitude - 0.6f);
        }

        private void LateUpdate()
        {
            if (statusLabel != null && viewCamera != null)
                statusLabel.transform.rotation = viewCamera.transform.rotation;
        }

        private void UpdateVisual()
        {
            if (config == null) return;
            int percent = (int)System.Math.Round(System.Math.Abs(State.Progress) * 100);
            int side = State.Progress > 0 ? 1 : State.Progress < 0 ? 2 : 0;
            bool repairOccupied = config.kind == RelayKind.Repair
                && (State.Owner == RelayOwner.Player ? PlayersPresent > 0 : EnemiesPresent > 0);
            int key = percent + side * 101 + (State.IsContested ? 404 : 0)
                + (int)State.Owner * 1000 + (repairOccupied ? 4000 : 0);
            if (key == visualKey) return;
            visualKey = key;
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
            Color sideColor = State.IsContested ? new Color(1f, 0.76f, 0.18f)
                : side == 1 ? new Color(0.35f, 0.68f, 1f) : side == 2 ? new Color(1f, 0.42f, 0.38f) : Color.clear;
            UpdateRing(percent / 100f, sideColor);
            // Details live in the HUD cards; the field keeps what a glance can read.
            if (statusLabel != null)
                statusLabel.text = State.IsContested ? config.label + "\nСПОР"
                    : State.Owner == RelayOwner.Neutral ? percent == 0 ? config.label : $"{config.label}\n{percent}%"
                    : repairOccupied ? config.label + "\nремонт" : config.label;
        }

        private void UpdateRing(float fill, Color color)
        {
            CaptureRingFill = fill;
            CaptureRingColor = color;
            if (ringMaterial == null) return;
            if (ring == null)
            {
                var child = new GameObject("CaptureRing");
                child.transform.SetParent(transform, false);
                ring = child.AddComponent<LineRenderer>();
                ring.sharedMaterial = ringMaterial;
                ring.useWorldSpace = false;
                ring.widthMultiplier = 0.16f;
                ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ring.receiveShadows = false;
            }
            int segments = Mathf.CeilToInt(RingSegments * Mathf.Clamp01(fill));
            ring.enabled = segments > 0;
            if (segments == 0) return;
            float radius = config.radius + 0.1f;
            // Starts at the far side (top of the screen) and fills clockwise as seen from above.
            for (int i = 0; i <= segments; i++)
            {
                float angle = 2 * Mathf.PI * fill * i / segments;
                ringPoints[i] = new Vector3(Mathf.Sin(angle) * radius, 0.07f, Mathf.Cos(angle) * radius);
            }
            ring.positionCount = segments + 1;
            ring.SetPositions(ringPoints);
            if (colors == null) colors = new MaterialPropertyBlock();
            colors.SetColor("_BaseColor", color);
            ring.SetPropertyBlock(colors);
        }
    }
}
