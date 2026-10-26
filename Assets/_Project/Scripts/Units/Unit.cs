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

        // Units currently enabled in the scene; small enough battles to just loop over.
        private static readonly List<Unit> all = new List<Unit>();
        public static IReadOnlyList<Unit> All => all;

        public static Unit Find(int id)
        {
            if (id < 0) return null;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Id == id) return all[i];
            return null;
        }

        public void AssignId(int id)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (Id >= 0 && Id != id) throw new InvalidOperationException("A unit keeps its id for its whole life.");
            Id = id;
        }

        private void OnEnable()
        {
            all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
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
