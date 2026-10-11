using UnityEngine;

namespace SectorLab.Units
{
    // Numbers that define a unit role (tank, scout, ranger...). One asset per role, shared by both teams,
    // so roles can be compared and balanced in one place.
    [CreateAssetMenu(fileName = "UnitStats", menuName = "SectorLab/Unit Stats")]
    public class UnitStats : ScriptableObject
    {
        [Min(1)] public int maxHealth = 100;
        [Min(0f)] public float speed = 3f;
        [Min(0)] public int damage = 20;
        [Min(0f)] public float attackRange = 1.5f;
        [Min(0.01f)] public float attackCooldown = 1f;
    }
}
