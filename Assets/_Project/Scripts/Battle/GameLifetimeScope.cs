using System;
using SectorLab.AI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace SectorLab.Battle
{
    // Scene-owned composition root. Gameplay components retain injected references, never resolve per frame.
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private EnemyTacticsController enemyTactics;

        protected override void Configure(IContainerBuilder builder)
        {
            if (battle == null || enemyTactics == null)
                throw new InvalidOperationException("Main requires BattleController and EnemyTacticsController registrations.");
            builder.Register<MatchClock>(Lifetime.Scoped);
            builder.Register<MatchState>(Lifetime.Scoped);
            builder.RegisterComponent(battle);
            builder.RegisterComponent(enemyTactics);
        }
    }
}
