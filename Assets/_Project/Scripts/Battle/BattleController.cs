using System;
using SectorLab.Player;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SectorLab.Battle
{
    // Watches the units on the field, decides the outcome once, then stops player input and shows the result.
    public class BattleController : MonoBehaviour
    {
        [SerializeField] private UnitCommander commander;
        [SerializeField] private BattleResultView resultView;

        public BattleOutcome Outcome { get; private set; } = BattleOutcome.InProgress;
        public int PlayerUnitsAlive { get; private set; }
        public int EnemyUnitsAlive { get; private set; }

        public event Action<BattleOutcome> Finished;

        private void Update()
        {
            if (Outcome != BattleOutcome.InProgress)
                return;

            int players = 0;
            int enemies = 0;
            // Index loop: foreach over an IReadOnlyList boxes its enumerator and allocates every frame.
            for (int i = 0; i < Unit.All.Count; i++)
            {
                Unit unit = Unit.All[i];
                if (!unit.IsAlive)
                    continue;
                if (unit.Team == Team.Player)
                    players++;
                else
                    enemies++;
            }

            PlayerUnitsAlive = players;
            EnemyUnitsAlive = enemies;

            BattleOutcome outcome = BattleRules.Evaluate(players, enemies);
            if (outcome == BattleOutcome.InProgress)
                return;

            Outcome = outcome;
            if (commander != null)
                commander.enabled = false;
            if (resultView != null)
                resultView.Show(outcome, Restart);
            Finished?.Invoke(outcome);
        }

        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
