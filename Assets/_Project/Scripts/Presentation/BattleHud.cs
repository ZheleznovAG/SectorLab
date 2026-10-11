using System;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Player;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.UI;

namespace SectorLab.Presentation
{
    public sealed class BattleHud : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private UnitLifecycle lifecycle;
        [SerializeField] private UnitCommander commander;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Text playerScore, enemyScore, timer, selection;
        [SerializeField] private Text[] relayLabels, unitLabels;
        [SerializeField] private Button[] relayButtons, unitButtons;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Text pauseLabel;
        private readonly int[] relayKeys = { int.MinValue, int.MinValue, int.MinValue };
        private readonly int[] unitKeys = { int.MinValue, int.MinValue, int.MinValue };
        private int shownPlayer = -1, shownEnemy = -1, shownSeconds = -1, selectedHp = -1;
        private Unit shownSelection;
        private float nextRefresh;
        private RectTransform layoutRoot;
        private float shownHeight = -1;
        private SafeAreaLayout safeLayout;
        private Rect shownArea;
        private TutorialView tutorial;
        private static readonly Color Blue = new Color(0.20f, 0.58f, 0.92f);
        private static readonly Color Red = new Color(0.92f, 0.34f, 0.29f);
        private static readonly Color Neutral = new Color(0.16f, 0.20f, 0.26f);

        private void Awake()
        {
            layoutRoot = GetComponent<RectTransform>();
            safeLayout = GetComponentInParent<SafeAreaLayout>();
            tutorial = GetComponent<TutorialView>();
            for (int i = 0; i < 3; i++)
            {
                int role = i;
                unitButtons[i].onClick.AddListener(() => SelectRole(role));
                relayButtons[i].onClick.AddListener(() => commander.OrderSelectedToRelay(battle.Relays[role]));
            }
            pauseButton.onClick.AddListener(() => battle.SetPaused(!battle.State.Clock.IsPaused));
        }

        private void Start() => Refresh(); // All relay Awake methods must finish before reading their states.

        private void LateUpdate()
        {
            float height = layoutRoot.rect.height;
            Rect area = safeLayout != null ? safeLayout.NormalizedArea : new Rect(0, 0, 1, 1);
            if (height <= 0 || (Mathf.Approximately(height, shownHeight) && area == shownArea)) return;
            shownHeight = height;
            shownArea = area;
            // Keep the arena between the 225-unit header and the 175-unit squad panel.
            worldCamera.rect = ArenaViewport(area, height);
        }

        public static Rect ArenaViewport(Rect safeArea, float layoutHeight) =>
            new Rect(safeArea.x, safeArea.y + safeArea.height * 191 / layoutHeight,
                safeArea.width, safeArea.height * Mathf.Max(0.1f, 1 - 432 / layoutHeight));

        private UnitSlotState PlayerSlot(int role)
        {
            for (int i = 0; i < lifecycle.Slots.Count; i++)
            {
                UnitSlotState slot = lifecycle.Slots[i];
                if (slot.Config.team == Team.Player && (int)slot.Config.role == role) return slot;
            }
            return null;
        }

        private void SelectRole(int role)
        {
            UnitSlotState slot = PlayerSlot(role);
            if (slot != null) commander.SelectFriendly(slot.Unit);
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            Refresh();
        }

        public void Refresh()
        {
            if (battle.State == null) return;
            int blue = (int)Math.Floor(battle.State.PlayerScore + MatchRules.Epsilon);
            int red = (int)Math.Floor(battle.State.EnemyScore + MatchRules.Epsilon);
            int seconds = (int)Math.Ceiling(battle.State.Clock.RemainingSeconds - MatchRules.Epsilon);
            if (blue != shownPlayer) { shownPlayer = blue; playerScore.text = "СИНИЕ  " + blue; }
            if (red != shownEnemy) { shownEnemy = red; enemyScore.text = red + "  КРАСНЫЕ"; }
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                timer.text = $"{seconds / 60:00}:{seconds % 60:00}  •  до 45 очков";
            }
            bool running = lifecycle.IsReady && battle.State.IsRunning;
            pauseButton.interactable = lifecycle.IsReady && (tutorial == null || !tutorial.IsShowing);
            string pause = battle.State.Clock.IsPaused ? "Продолжить" : "Пауза";
            if (pauseLabel.text != pause) pauseLabel.text = pause;
            for (int i = 0; i < 3; i++)
            {
                RelayPoint relay = battle.Relays[i];
                int progress = (int)Math.Round(Math.Abs(relay.State.Progress) * 100);
                int side = relay.State.Progress > MatchRules.Epsilon ? 1 : relay.State.Progress < -MatchRules.Epsilon ? 2 : 0;
                int key = progress + side * 101 + (relay.State.IsContested ? 404 : 0);
                if (relayKeys[i] != key)
                {
                    relayKeys[i] = key;
                    string owner = side == 1 ? "Синие" : side == 2 ? "Красные" : "Свободен";
                    string effect = relay.Config.kind == RelayKind.Score ? $"+{relay.Config.scorePerSecond:0}/с" : $"+{relay.Config.healingPerSecond:0} HP/с";
                    relayLabels[i].text = $"{relay.Config.label}  {effect}\n{owner} {progress}%{(relay.State.IsContested ? " • СПОР" : "")}";
                    relayButtons[i].GetComponent<Image>().color = relay.State.IsContested ? new Color(0.56f, 0.42f, 0.10f)
                        : side == 1 ? Blue : side == 2 ? Red : Neutral;
                }
                relayButtons[i].interactable = running && commander.Selected != null;
                UnitSlotState slot = PlayerSlot(i);
                Unit unit = slot?.Unit;
                bool alive = unit != null && unit.IsAlive;
                int hp = alive ? unit.Health.Current : -1;
                int respawn = slot != null && slot.Respawn.IsWaiting ? (int)Math.Ceiling(slot.Respawn.RemainingSeconds - MatchRules.Epsilon) : -1;
                int unitKey = alive ? hp : slot == null ? -200 : slot.IsSpawning ? -100 : -2 - Math.Max(0, respawn);
                if (unitKeys[i] != unitKey)
                {
                    unitKeys[i] = unitKey;
                    string role = ((UnitRole)i).ToString();
                    unitLabels[i].text = alive ? $"{role}\n{hp}/{unit.Health.Max} HP"
                        : slot == null ? role : slot.IsSpawning ? role + "\nВозвращается…"
                        : slot.Respawn.IsWaiting ? role + $"\nВозврат: {Math.Max(0, respawn)} с" : role + "\nОжидание";
                }
                unitButtons[i].interactable = running && alive;
                unitButtons[i].GetComponent<Image>().color = alive && commander.Selected == unit ? Blue : Neutral;
            }
            Unit selected = commander.Selected;
            int currentHp = selected != null ? selected.Health.Current : -1;
            if (shownSelection != selected || selectedHp != currentHp || selection.text.Length == 0)
            {
                shownSelection = selected;
                selectedHp = currentHp;
                selection.text = selected == null ? "Выберите бойца на поле или снизу. Тап по узлу — занять, по врагу — атаковать."
                    : $"{selected.Stats.name} • {currentHp}/{selected.Health.Max} HP • урон {selected.Stats.damage} • дальность {selected.Stats.attackRange:0.#} • скорость {selected.Stats.speed:0.#}";
            }
        }
    }
}
