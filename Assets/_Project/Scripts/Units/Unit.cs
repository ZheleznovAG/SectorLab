using System;
using System.Collections.Generic;
using UnityEngine;

namespace SectorLab.Units
{
    // Entry point of a unit for other systems: team, health, movement, attack and selection state.
    [RequireComponent(typeof(UnitMovement))]
    public class Unit : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Player;
        [Tooltip("Role numbers. When set, they override maxHealth here and the movement/attack fields.")]
        [SerializeField] private UnitStats stats;
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField] private GameObject selectionIndicator;

        private UnitAttack attack;

        public Team Team => team;
        // Stable address for orders and network messages: the roster slot, kept across respawns.
        // -1 until the match lifecycle (or a test fixture) assigns it.
        public int Id { get; private set; } = -1;
        public uint Generation { get; internal set; }
        public UnitStats Stats => stats;
        public Health Health { get; private set; }
        public bool IsAlive => !Health.IsDead;
        public UnitMovement Movement { get; private set; }
        // Optional component, looked up lazily so it can be added after Unit.
        public UnitAttack Attack => attack != null ? attack : attack = GetComponent<UnitAttack>();
        public bool IsSelected { get; private set; }

        public event Action<Unit> Died;

        public UnitRegistry Registry { get; private set; }
        public void BindRegistry(UnitRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (Registry != null && Registry != registry)
                throw new InvalidOperationException("A unit belongs to one match for its lifetime.");
            registry.ValidateId(this, Id);
            Registry = registry;
            if (isActiveAndEnabled) Registry.Add(this);
        }

#if UNITY_EDITOR
        // Editor leak diagnostics only. Runtime gameplay never reads this aggregate.
        private static readonly List<Unit> all = new List<Unit>();
        public static IReadOnlyList<Unit> All => all;
#endif

        public void AssignId(int id)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (Id >= 0 && Id != id) throw new InvalidOperationException("A unit keeps its id for its whole life.");
            Registry?.ValidateId(this, id);
            Id = id;
        }

        private void OnEnable()
        {
            Registry?.Add(this);
#if UNITY_EDITOR
            all.Add(this);
#endif
        }

        private void OnDisable()
        {
            Registry?.Remove(this);
#if UNITY_EDITOR
            all.Remove(this);
#endif
        }

        private void Awake()
        {
            Movement = GetComponent<UnitMovement>();
            if (stats != null)
            {
                maxHealth = stats.maxHealth;
                Movement.Configure(stats.speed);
                if (Attack != null)
                    Attack.Configure(stats);
            }
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
            return other != null && Registry != null && other.Registry == Registry && other.team != team;
        }

        private void OnDied()
        {
            SetSelected(false);
            Died?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
