using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SectorLab.Battle
{
    // End-of-match panel: a title and a restart button. Hidden until the battle is decided.
    public class BattleResultView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text title;
        [SerializeField] private Button restartButton;

        private void Awake()
        {
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 24;
            title.resizeTextMaxSize = title.fontSize;
            panel.SetActive(false);
        }

        public void Show(BattleOutcome outcome, UnityAction onRestart, double playerScore = 0, double enemyScore = 0)
        {
            string result = outcome == BattleOutcome.Victory ? "Victory" : outcome == BattleOutcome.Defeat ? "Defeat" : "Draw";
            title.text = $"{result}\n{playerScore:0} : {enemyScore:0}";
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(onRestart);
            panel.SetActive(true);
        }
    }
}
