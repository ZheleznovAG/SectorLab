using SectorLab.Units;
using UnityEngine;

namespace SectorLab.Tests
{
    // Synthetic arenas have no BattleController; this stands in for its tick so combat advances
    // with game time (a paused Time.timeScale gives zero seconds).
    public sealed class CombatTicker : MonoBehaviour
    {
        public static CombatTicker AddTo(GameObject root) => root.AddComponent<CombatTicker>();

        private void Update()
        {
            for (int i = 0; i < Unit.All.Count; i++)
            {
                UnitAttack attack = Unit.All[i].Attack;
                if (attack != null) attack.Simulate(Time.deltaTime);
            }
        }
    }
}
