using UnityEngine;
using UnityEngine.UI;

namespace SectorLab.Battle
{
    // Top-of-screen "3 vs 3" counter: how many units each side still has.
    public class ForceCounterView : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private Text label;
        [SerializeField] private Color playerColor = new Color(0.30f, 0.61f, 1f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.42f, 0.37f);

        private int shownPlayers = -1;
        private int shownEnemies = -1;

        private void Update()
        {
            if (battle.PlayerUnitsAlive == shownPlayers && battle.EnemyUnitsAlive == shownEnemies)
                return;

            shownPlayers = battle.PlayerUnitsAlive;
            shownEnemies = battle.EnemyUnitsAlive;
            label.text = Format(shownPlayers, shownEnemies, playerColor, enemyColor);
        }

        public static string Format(int players, int enemies, Color playerColor, Color enemyColor)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(playerColor)}>{players}</color>  vs  <color=#{ColorUtility.ToHtmlStringRGB(enemyColor)}>{enemies}</color>";
        }
    }
}
