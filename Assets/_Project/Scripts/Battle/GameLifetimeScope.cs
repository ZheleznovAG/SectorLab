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
        [SerializeField] private SectorLab.Units.UnitRoster roster;
        [SerializeField] private SectorLab.Units.UnitLifecycle unitLifecycle;

        protected override void Configure(IContainerBuilder builder)
        {
            if (battle == null || enemyTactics == null || roster == null || unitLifecycle == null)
                throw new InvalidOperationException("Main requires battle, enemy tactics, roster and unit lifecycle registrations.");
            builder.Register<MatchClock>(Lifetime.Scoped);
            builder.Register<MatchState>(Lifetime.Scoped);
            builder.RegisterComponent(battle);
            builder.RegisterComponent(enemyTactics);
            builder.Register<SectorLab.Infrastructure.AddressableUnitPrefabProvider>(Lifetime.Scoped)
                .As<SectorLab.Infrastructure.IUnitPrefabProvider>().AsSelf();
            builder.Register<SectorLab.Infrastructure.UnitFactory>(Lifetime.Scoped);
            builder.RegisterInstance(roster);
            builder.RegisterComponent(unitLifecycle);
        }
    }
}
