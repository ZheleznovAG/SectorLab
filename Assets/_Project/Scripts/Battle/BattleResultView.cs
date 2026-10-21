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
        private Button exitButton;
        public bool IsShowingLoadError { get; private set; }

        private void Awake()
        {
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 24;
            title.resizeTextMaxSize = title.fontSize;
            panel.SetActive(false);
        }

        public void Show(BattleOutcome outcome, UnityAction onRestart, double playerScore = 0, double enemyScore = 0)
        {
            IsShowingLoadError = false;
            restartButton.gameObject.SetActive(true);
            restartButton.GetComponentInChildren<Text>().text = "Ещё бой";
            if (exitButton != null) exitButton.gameObject.SetActive(false);
            string result = outcome == BattleOutcome.Victory ? "Победа" : outcome == BattleOutcome.Defeat ? "Поражение" : "Ничья";
            title.text = $"{result}\n{playerScore:0} : {enemyScore:0}";
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(onRestart);
            panel.SetActive(true);
        }

        public void ShowLoading()
        {
            title.text = "Подготовка матча…";
            restartButton.gameObject.SetActive(false);
            if (exitButton != null) exitButton.gameObject.SetActive(false);
            panel.SetActive(true);
        }

        // A blocking notice without buttons, e.g. while a network host waits for its opponent.
        public void ShowMessage(string message)
        {
            title.text = message;
            restartButton.gameObject.SetActive(false);
            if (exitButton != null) exitButton.gameObject.SetActive(false);
            panel.SetActive(true);
        }

        public bool IsShowingMessage => panel.activeSelf && !restartButton.gameObject.activeSelf;

        public void Hide() => panel.SetActive(false);

        public void ShowLoadError(UnityAction retry, UnityAction exit)
        {
            IsShowingLoadError = true;
            title.text = "Не удалось подготовить матч.\nПовторите запуск.";
            restartButton.gameObject.SetActive(true);
            restartButton.GetComponentInChildren<Text>().text = "Повторить";
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(retry);
            if (exitButton == null)
            {
                exitButton = Instantiate(restartButton, restartButton.transform.parent);
                exitButton.name = "ExitAfterLoadError";
                var rect = exitButton.GetComponent<RectTransform>();
                rect.anchoredPosition -= Vector2.up * (rect.rect.height + 25);
            }
            exitButton.GetComponentInChildren<Text>().text = "Выйти";
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(exit);
            exitButton.gameObject.SetActive(true);
            panel.SetActive(true);
        }
    }
}
