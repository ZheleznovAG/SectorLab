using SectorLab.Battle;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.UI;
using R3;

namespace SectorLab.Presentation
{
    public sealed class TutorialView : MonoBehaviour
    {
        public const string SeenPreference = "SectorLab.Tutorial.v1";
        [SerializeField] private BattleController battle;
        [SerializeField] private UnitLifecycle lifecycle;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text title, body, nextLabel;
        [SerializeField] private Button nextButton, skipButton, helpButton;
        private int step;
        private System.IDisposable outcomeSubscription;
        public bool IsShowing => panel.activeSelf;
        private static readonly string[] Titles = { "Захватите A и B", "Отдавайте приказы", "Меняйте тактику" };
        private static readonly string[] Bodies = {
            "A и B дают по 1 очку/с. Победа — 45 очков или лучший счёт через 3 минуты. C ремонтирует своих: +6 HP/с. При споре узел не работает.",
            "Выберите своего бойца карточкой снизу или на поле. Тап по кругу узла, даже занятому союзником, — занять; по врагу — атаковать; по земле — идти; по своему бойцу — выбрать его. Свайп двигает камеру; два пальца меняют масштаб.",
            "Tank держит позицию, Scout быстро захватывает, Ranger стреляет издалека. Павший боец вернётся через 10 игровых секунд. Пауза останавливает бой и все таймеры."
        };

        private void Awake()
        {
            panel.SetActive(false);
            nextButton.onClick.AddListener(Next);
            skipButton.onClick.AddListener(Dismiss);
            helpButton.onClick.AddListener(Open);
        }
        private void OnEnable()
        {
            lifecycle.Ready += OnReady;
            outcomeSubscription = battle.Model.Outcome.Subscribe(OnFinished);
        }
        private void OnDisable()
        {
            lifecycle.Ready -= OnReady;
            outcomeSubscription?.Dispose();
            if (battle != null) battle.SetPaused(PauseReason.Tutorial, false);
        }
        private void OnReady() { if (PlayerPrefs.GetInt(SeenPreference, 0) == 0) Open(); }
        private void OnFinished(BattleOutcome outcome)
        {
            if (outcome == BattleOutcome.InProgress) return;
            panel.SetActive(false);
            battle.SetPaused(PauseReason.Tutorial, false);
        }
        private void Update() => helpButton.interactable = lifecycle.IsReady && battle.Model.Outcome.Value == BattleOutcome.InProgress && !IsShowing;

        public void Open()
        {
            if (!lifecycle.IsReady || battle.Model.Outcome.Value != BattleOutcome.InProgress || IsShowing) return;
            step = 0;
            battle.SetPaused(PauseReason.Tutorial, true);
            panel.SetActive(true);
            Render();
        }
        private void Next() { if (++step >= Titles.Length) Dismiss(); else Render(); }
        private void Render()
        {
            title.text = $"{step + 1}/3  •  {Titles[step]}";
            body.text = Bodies[step];
            nextLabel.text = step == Titles.Length - 1 ? "Играть" : "Дальше";
        }
        public void Dismiss()
        {
            if (!IsShowing) return;
            panel.SetActive(false);
            PlayerPrefs.SetInt(SeenPreference, 1);
            PlayerPrefs.Save();
            battle.SetPaused(PauseReason.Tutorial, false);
        }
    }
}
