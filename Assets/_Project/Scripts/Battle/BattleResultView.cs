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
            panel.SetActive(false);
        }

        public void Show(BattleOutcome outcome, UnityAction onRestart)
        {
            title.text = outcome == BattleOutcome.Victory ? "Victory" : "Defeat";
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(onRestart);
            panel.SetActive(true);
        }
    }
}
