using System;
using UnityEngine;

namespace SectorLab.Units
{
    // Entry point of a unit for other systems: team, health, movement, attack and selection state.
    [RequireComponent(typeof(UnitMovement))]
    public class Unit : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Player;
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField] private GameObject selectionIndicator;

        private UnitAttack attack;

        public Team Team => team;
        public Health Health { get; private set; }
        public bool IsAlive => !Health.IsDead;
        public UnitMovement Movement { get; private set; }
        // Optional component, looked up lazily so it can be added after Unit.
        public UnitAttack Attack => attack != null ? attack : attack = GetComponent<UnitAttack>();
        public bool IsSelected { get; private set; }

        public event Action<Unit> Died;

        private void Awake()
        {
            Movement = GetComponent<UnitMovement>();
            Health = new Health(maxHealth);
            Health.Died += OnDied;
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selectionIndicator != null)
                selectionIndicator.SetActive(selected);
        }

        public bool IsHostileTo(Unit other)
        {
            return other != null && other.team != team;
        }

        private void OnDied()
        {
            SetSelected(false);
            Died?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
