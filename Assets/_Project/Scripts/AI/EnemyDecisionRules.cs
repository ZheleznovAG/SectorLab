using SectorLab.Objectives;

namespace SectorLab.AI
{
    public enum EnemyGoalKind { Hold, Defend, Capture, Repair, Attack }

    public readonly struct RelayOption
    {
        public readonly RelayKind Kind;
        public readonly RelayOwner Owner;
        public readonly bool Threatened;
        public readonly bool Reachable;
        public readonly float Cost;
        public RelayOption(RelayKind kind, RelayOwner owner, bool threatened, bool reachable, float cost)
        {
            Kind = kind; Owner = owner; Threatened = threatened; Reachable = reachable; Cost = cost;
        }
    }

    public readonly struct EnemyGoal
    {
        public readonly EnemyGoalKind Kind;
        public readonly int RelayIndex;
        public EnemyGoal(EnemyGoalKind kind, int relayIndex) { Kind = kind; RelayIndex = relayIndex; }
    }

    // Stable array order breaks ties; no random seed, clock or Unity objects are needed here.
    public static class EnemyDecisionRules
    {
        public static EnemyGoal Choose(RelayOption[] relays, float healthFraction, bool hasInterference)
        {
            int best = Best(relays, EnemyGoalKind.Defend);
            if (best >= 0) return new EnemyGoal(EnemyGoalKind.Defend, best);
            best = Best(relays, EnemyGoalKind.Capture);
            if (best >= 0) return new EnemyGoal(EnemyGoalKind.Capture, best);
            if (healthFraction <= 0.35f)
            {
                best = Best(relays, EnemyGoalKind.Repair);
                if (best >= 0) return new EnemyGoal(EnemyGoalKind.Repair, best);
            }
            if (hasInterference) return new EnemyGoal(EnemyGoalKind.Attack, -1);
            // Once both scoring relays are secure, acquire a repair base or guard a scoring relay.
            for (int i = 0; i < relays.Length; i++)
                if (relays[i].Reachable && relays[i].Kind == RelayKind.Repair && relays[i].Owner != RelayOwner.Enemy)
                    return new EnemyGoal(EnemyGoalKind.Capture, i);
            return new EnemyGoal(EnemyGoalKind.Hold, Best(relays, EnemyGoalKind.Hold));
        }

        private static int Best(RelayOption[] relays, EnemyGoalKind kind)
        {
            int best = -1;
            float cost = float.PositiveInfinity;
            for (int i = 0; i < relays.Length; i++)
            {
                RelayOption relay = relays[i];
                if (!relay.Reachable) continue;
                bool eligible = kind switch
                {
                    EnemyGoalKind.Defend => relay.Kind == RelayKind.Score && relay.Threatened,
                    EnemyGoalKind.Capture => relay.Kind == RelayKind.Score && relay.Owner != RelayOwner.Enemy,
                    EnemyGoalKind.Repair => relay.Kind == RelayKind.Repair && relay.Owner == RelayOwner.Enemy,
                    _ => relay.Kind == RelayKind.Score && relay.Owner == RelayOwner.Enemy
                };
                if (eligible && relay.Cost < cost) { best = i; cost = relay.Cost; }
            }
            return best;
        }
    }
}
