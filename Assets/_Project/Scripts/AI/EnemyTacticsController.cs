using System;
using System.Collections.Generic;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;

namespace SectorLab.AI
{
    // One decision per game second. Orders use the same movement/attack components as the player.
    public sealed class EnemyTacticsController : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        private readonly List<Unit> actors = new List<Unit>(3);
        private readonly Dictionary<Unit, RelayPoint> assignments = new Dictionary<Unit, RelayPoint>(3);
        private readonly List<Unit> staleAssignments = new List<Unit>(3);
        private RelayOption[] options;
        private Vector3[] approaches;
        private int[] claimed;
        private double decisionAccumulator;
        private bool initialized;
        private bool subscribed;

        public int DecisionCount { get; private set; }
        public int MoveOrdersIssued { get; private set; }
        public RelayPoint GetAssignedRelay(Unit unit) => assignments.TryGetValue(unit, out RelayPoint relay) ? relay : null;

        private void Start()
        {
            if (!initialized) Initialize(battle);
        }

        [VContainer.Inject]
        public void Initialize(BattleController controller)
        {
            Unsubscribe();
            battle = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            options = new RelayOption[battle.Relays.Count];
            approaches = new Vector3[battle.Relays.Count];
            claimed = new int[battle.Relays.Count];
            initialized = true;
            decisionAccumulator = 0;
            assignments.Clear();
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() { if (initialized) Subscribe(); }
        private void OnDisable()
        {
            Unsubscribe();
            actors.Clear();
            assignments.Clear();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            battle.Ticked += OnTick;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (battle != null) battle.Ticked -= OnTick;
            subscribed = false;
        }

        private void OnTick(double seconds)
        {
            if (!battle.State.IsRunning) return;
            decisionAccumulator += seconds;
            if (decisionAccumulator < 1 - 1e-6) return;
            decisionAccumulator -= 1;
            Think();
        }

        private static int CompareActors(Unit a, Unit b)
        {
            if (a == b) return 0;
            int speed = b.Movement.Speed.CompareTo(a.Movement.Speed);
            if (speed != 0) return speed;
            int role = string.CompareOrdinal(a.Stats != null ? a.Stats.name : "", b.Stats != null ? b.Stats.name : "");
            if (role != 0) return role;
            // Registration order is reproducible for equal roles and does not depend on Unity entity IDs.
            for (int i = 0; i < Unit.All.Count; i++)
            {
                if (Unit.All[i] == a) return -1;
                if (Unit.All[i] == b) return 1;
            }
            return 0;
        }

        private void Think()
        {
            DecisionCount++;
            actors.Clear();
            for (int i = 0; i < Unit.All.Count; i++)
                if (Unit.All[i].Team == Team.Enemy && Unit.All[i].IsAlive) actors.Add(Unit.All[i]);
            actors.Sort(CompareActors);
            staleAssignments.Clear();
            foreach (var entry in assignments)
                if (entry.Key == null || !entry.Key.IsAlive || !entry.Key.isActiveAndEnabled) staleAssignments.Add(entry.Key);
            for (int i = 0; i < staleAssignments.Count; i++) assignments.Remove(staleAssignments[i]);
            Array.Clear(claimed, 0, claimed.Length);

            for (int actorIndex = 0; actorIndex < actors.Count; actorIndex++)
            {
                Unit unit = actors[actorIndex];
                for (int i = 0; i < options.Length; i++)
                {
                    RelayPoint relay = battle.Relays[i];
                    bool reachable = relay.TryGetApproachPoint(unit, out approaches[i]);
                    bool threatened = relay.PlayersPresent > 0 && (relay.State.Owner == RelayOwner.Enemy || relay.State.Progress < 0);
                    float cost = Vector3.Distance(unit.transform.position, relay.transform.position) + claimed[i] * 8;
                    options[i] = new RelayOption(relay.Config.kind, relay.State.Owner, threatened, reachable, cost);
                }
                Unit nearbyHostile = FindInterference(unit, null);
                EnemyGoal goal = EnemyDecisionRules.Choose(options, unit.Health.Fraction, nearbyHostile != null);
                if (goal.Kind == EnemyGoalKind.Attack)
                {
                    assignments.Remove(unit);
                    Attack(unit, nearbyHostile);
                    continue;
                }
                if (goal.RelayIndex < 0) continue;
                int index = goal.RelayIndex;
                claimed[index]++;
                RelayPoint target = battle.Relays[index];
                assignments[unit] = target;
                Unit blocker = FindInterference(unit, target);
                if (blocker != null)
                {
                    Attack(unit, blocker);
                    continue;
                }
                if (target.Contains(unit))
                {
                    unit.Movement.Stop();
                    if (goal.Kind == EnemyGoalKind.Repair) unit.Attack?.ClearTarget();
                    continue;
                }
                // Keep an accepted unchanged path; do not send SetDestination every decision/frame.
                unit.Attack?.ClearTarget();
                if (unit.Movement.IsMoving && (unit.Movement.Destination - approaches[index]).sqrMagnitude < 0.01f) continue;
                if (unit.Movement.MoveTo(approaches[index], target.GetArrivalDistance(approaches[index]))) MoveOrdersIssued++;
            }
        }

        private static void Attack(Unit unit, Unit target)
        {
            if (target != null && unit.Attack != null && unit.Attack.Target != target) unit.Attack.SetTarget(target);
        }

        private static Unit FindInterference(Unit actor, RelayPoint relay)
        {
            if (actor.Attack == null) return null;
            float maxDistance = actor.Attack.Range + 0.5f;
            Unit nearest = null;
            float nearestDistance = maxDistance * maxDistance;
            for (int i = 0; i < Unit.All.Count; i++)
            {
                Unit unit = Unit.All[i];
                if (!unit.IsAlive || unit.Team != Team.Player || relay != null && !relay.Contains(unit)) continue;
                Vector3 offset = unit.transform.position - actor.transform.position;
                offset.y = 0;
                float distance = offset.sqrMagnitude;
                if (distance < nearestDistance) { nearest = unit; nearestDistance = distance; }
            }
            return nearest;
        }
    }
}
