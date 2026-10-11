using UnityEngine;

namespace SectorLab.Units
{
    // Simple melee-range attack: walk up to the target, then hit it every cooldown until it dies.
    [RequireComponent(typeof(Unit))]
    public class UnitAttack : MonoBehaviour
    {
        [SerializeField, Min(0)] private int damage = 20;
        [SerializeField, Min(0f)] private float range = 1.5f;
        [SerializeField, Min(0.01f)] private float cooldown = 1f;

        private Unit self;
        private float nextAttackTime;

        public Unit Target { get; private set; }
        public float Range => range;

        private void Awake()
        {
            self = GetComponent<Unit>();
        }

        public void SetTarget(Unit target)
        {
            if (!self.IsHostileTo(target))
                return;

            Target = target;
        }

        public void ClearTarget()
        {
            Target = null;
        }

        public bool IsInRange(Unit target)
        {
            Vector3 offset = target.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= range * range;
        }

        private void Update()
        {
            // A destroyed target compares equal to null in Unity.
            if (Target == null || !Target.IsAlive)
            {
                Target = null;
                return;
            }

            if (!IsInRange(Target))
            {
                self.Movement.MoveTo(Target.transform.position);
                return;
            }

            self.Movement.Stop();

            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + cooldown;
                Target.Health.TakeDamage(damage);
            }
        }
    }
}
