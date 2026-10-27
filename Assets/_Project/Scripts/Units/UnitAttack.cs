using UnityEngine;

namespace SectorLab.Units
{
    // Walk until the target is within range, then hit it every cooldown until it dies.
    // Range decides melee vs ranged; an optional line flashes on each hit so ranged shots are readable.
    // Targeting and chasing follow frames (NavMesh); cooldown and damage advance only with the
    // match tick. Frame pacing still influences navigation and the moment of acquiring a target.
    [RequireComponent(typeof(Unit))]
    public class UnitAttack : MonoBehaviour
    {
        [SerializeField, Min(0)] private int damage = 20;
        [SerializeField, Min(0f)] private float range = 1.5f;
        [SerializeField, Min(0.01f)] private float cooldown = 1f;
        [Tooltip("An idle unit attacks the nearest hostile within this distance. 0 = only on order.")]
        [SerializeField, Min(0f)] private float autoAcquireRange;
        [SerializeField] private LineRenderer shotLine;
        [SerializeField, Min(0f)] private float shotLineDuration = 0.1f;
        [Tooltip("Solid arena geometry that blocks attacks. Default contains the current walls and blocks.")]
        [SerializeField] private LayerMask obstructionMask = 1;

        private const float RepathDistance = 0.5f;
        private const float RepathInterval = 0.5f;

        private Unit self;
        private double cooldownRemaining;
        private float hideShotLineTime;
        private float nextRepathTime;

        public Unit Target { get; private set; }
        public float Range => range;
        public int Damage => damage;
        public uint ShotsFired { get; private set; }
        public Vector3 LastShotEnd { get; private set; }
        public LineRenderer ShotLine => shotLine;
        public float ShotLineDuration => shotLineDuration;
        public float Cooldown => cooldown;
        public event System.Action<Unit> Fired;
        // True while the current target is in reach with a clear line; Simulate fires only then.
        public bool CanHitTarget { get; private set; }

        private void Awake()
        {
            self = GetComponent<Unit>();
        }

        private void OnDisable()
        {
            if (shotLine != null)
                shotLine.enabled = false;
        }

        public void Configure(UnitStats stats)
        {
            damage = stats.damage;
            range = stats.attackRange;
            cooldown = stats.attackCooldown;
        }

        public void SetTarget(Unit target)
        {
            if (!self.IsHostileTo(target))
                return;

            Target = target;
            nextRepathTime = 0f;
        }

        public void ClearTarget()
        {
            Target = null;
            CanHitTarget = false;
            nextRepathTime = 0f;
        }

        public bool IsInRange(Unit target)
        {
            Vector3 offset = target.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= range * range;
        }

        private void ShowShotLine(Vector3 targetPosition)
        {
            if (shotLine == null)
                return;

            shotLine.useWorldSpace = true;
            shotLine.positionCount = 2;
            shotLine.SetPosition(0, transform.position);
            shotLine.SetPosition(1, targetPosition);
            shotLine.enabled = true;
            hideShotLineTime = Time.time + shotLineDuration;
        }

        private Unit FindNearestHostile(float maxDistance)
        {
            if (maxDistance <= 0f || self.Registry == null)
                return null;

            Unit nearest = null;
            float nearestSqr = maxDistance * maxDistance;
            // Index loop: foreach over an IReadOnlyList boxes its enumerator and allocates every frame.
            for (int i = 0; i < self.Registry.All.Count; i++)
            {
                Unit other = self.Registry.All[i];
                if (!self.IsHostileTo(other) || !other.IsAlive)
                    continue;

                Vector3 offset = other.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= nearestSqr)
                {
                    nearestSqr = offset.sqrMagnitude;
                    nearest = other;
                }
            }
            return nearest;
        }

        private void Update()
        {
            if (shotLine != null && shotLine.enabled && Time.time >= hideShotLineTime)
                shotLine.enabled = false;
            // A paused match keeps every unit where it is.
            if (!self.IsAlive || Time.timeScale <= 0f)
                return;

            // A destroyed target compares equal to null in Unity.
            if (Target == null || !Target.IsAlive)
            {
                Target = null;
                CanHitTarget = false;

                // Only idle units pick fights, so a move order isn't hijacked on the way.
                if (self.Movement.IsMoving)
                    return;
                // Anything already within weapon reach is fair game, even past the usual alert distance.
                float alert = autoAcquireRange > 0f ? Mathf.Max(autoAcquireRange, range) : 0f;
                Target = FindNearestHostile(alert);
                if (Target == null)
                    return;
                nextRepathTime = 0f;
            }

            CanHitTarget = HasClearShot(Target);
            if (!CanHitTarget)
            {
                // Bound retries for failed/partial paths, and re-path for a moving target.
                Vector3 targetPosition = Target.transform.position;
                if (Time.time >= nextRepathTime && (!self.Movement.IsMoving || (self.Movement.Destination - targetPosition).sqrMagnitude > RepathDistance * RepathDistance))
                {
                    nextRepathTime = Time.time + RepathInterval;
                    self.Movement.MoveTo(targetPosition);
                }
                return;
            }

            self.Movement.Stop();
        }

        private bool HasClearShot(Unit target) => IsInRange(target)
            && !Physics.Linecast(transform.position, target.transform.position, obstructionMask, QueryTriggerInteraction.Ignore);

        // A standalone adapter for small fixtures. A match prepares every shot before applying damage.
        public void Simulate(double seconds)
        {
            if (TryPrepareShot(seconds, out PreparedShot shot)) shot.Apply();
        }

        internal bool TryPrepareShot(double seconds, out PreparedShot shot)
        {
            shot = default;
            if (seconds <= 0 || !isActiveAndEnabled || !self.IsAlive)
                return false;
            cooldownRemaining = System.Math.Max(0, cooldownRemaining - seconds);
            if (cooldownRemaining > 1e-6 || Target == null || !Target.IsAlive)
                return false;
            // Re-check here: the target may have moved since this frame's Update.
            if (!HasClearShot(Target))
            {
                CanHitTarget = false;
                return false;
            }
            cooldownRemaining = cooldown;
            LastShotEnd = Target.transform.position;
            ShowShotLine(LastShotEnd);
            ShotsFired++;
            shot = new PreparedShot(this, Target.Health, damage);
            return true;
        }

        // Health remains valid during the whole tick, even when an earlier shot kills its attacker.
        internal readonly struct PreparedShot
        {
            private readonly UnitAttack attack;
            private readonly Health target;
            private readonly int damage;

            public PreparedShot(UnitAttack attack, Health target, int damage)
            { this.attack = attack; this.target = target; this.damage = damage; }

            public void Apply()
            {
                target.TakeDamage(damage);
                attack.Fired?.Invoke(attack.self);
            }
        }
    }
}
