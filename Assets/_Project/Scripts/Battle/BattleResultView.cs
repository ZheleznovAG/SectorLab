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
            IsShowingMessage = false;
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
            IsShowingMessage = false;
            title.text = "Подготовка матча…";
            restartButton.gameObject.SetActive(false);
            if (exitButton != null) exitButton.gameObject.SetActive(false);
            panel.SetActive(true);
        }

        // A blocking notice, e.g. while a network host waits for its opponent. With an action the
        // panel's button offers a way out, so a lost connection never leaves the player stuck.
        public void ShowMessage(string message, string actionLabel = null, UnityAction action = null)
        {
            IsShowingLoadError = false;
            title.text = message;
            bool hasAction = action != null;
            restartButton.gameObject.SetActive(hasAction);
            restartButton.onClick.RemoveAllListeners();
            if (hasAction)
            {
                restartButton.GetComponentInChildren<Text>().text = actionLabel;
                restartButton.onClick.AddListener(action);
            }
            if (exitButton != null) exitButton.gameObject.SetActive(false);
            panel.SetActive(true);
            IsShowingMessage = true;
        }

        public bool IsShowingMessage { get; private set; }
        public bool IsVisible => panel.activeSelf;
        public string ActionLabel => restartButton.gameObject.activeSelf ? restartButton.GetComponentInChildren<Text>().text : null;

        public void Hide()
        {
            panel.SetActive(false);
            IsShowingMessage = false;
        }

        public void ShowLoadError(UnityAction retry, UnityAction exit)
        {
            IsShowingLoadError = true;
            IsShowingMessage = false;
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
