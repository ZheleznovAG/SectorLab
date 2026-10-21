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
        public const float TopBarHeight = 84;
        public const float RelayBarHeight = 72;
        public const float SquadBarHeight = 175;
        public const float ArenaPadding = 16;
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
        private double shownPlayerRate = -1, shownEnemyRate = -1;
        private Unit shownSelection;
        // A frame around the field in the forecast winner's colour: readable from the corner of the eye.
        public const float LeadFrameThickness = 10;
        private Image[] leadEdges;
        private Image[] relayImages, unitImages;
        private BattleOutcome shownForecast = BattleOutcome.InProgress;
        public BattleOutcome ShownForecast => shownForecast;
        public bool LeadFrameVisible => leadEdges[0].gameObject.activeSelf;
        public Color LeadFrameColor => leadEdges[0].color;
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
            relayImages = new Image[3];
            unitImages = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                int role = i;
                relayImages[i] = relayButtons[i].GetComponent<Image>();
                unitImages[i] = unitButtons[i].GetComponent<Image>();
                unitButtons[i].onClick.AddListener(() => SelectRole(role));
                relayButtons[i].onClick.AddListener(() => commander.OrderSelectedToRelay(battle.Relays[role]));
            }
            pauseButton.onClick.AddListener(() => battle.SetPaused(!battle.State.Clock.IsPaused));
            CreateLeadFrame();
        }

        private void CreateLeadFrame()
        {
            const float t = LeadFrameThickness;
            float bottom = SquadBarHeight + ArenaPadding, top = TopBarHeight + RelayBarHeight + ArenaPadding;
            // bottom, top, left, right edges of the arena viewport
            Vector2[] min = { new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0) };
            Vector2[] max = { new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(1, 1) };
            Vector2[] offsetMin = { new Vector2(0, bottom), new Vector2(0, -top - t), new Vector2(0, bottom), new Vector2(-t, bottom) };
            Vector2[] offsetMax = { new Vector2(0, bottom + t), new Vector2(0, -top), new Vector2(t, -top), new Vector2(0, -top) };
            leadEdges = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var edge = new GameObject("LeadFrame" + i, typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)edge.transform;
                rect.SetParent(layoutRoot, false);
                rect.SetAsFirstSibling(); // Behind panels, notices and the tutorial.
                rect.anchorMin = min[i]; rect.anchorMax = max[i];
                rect.offsetMin = offsetMin[i]; rect.offsetMax = offsetMax[i];
                leadEdges[i] = edge.GetComponent<Image>();
                leadEdges[i].raycastTarget = false;
                edge.SetActive(false);
            }
        }

        private void Start() => Refresh(); // All relay Awake methods must finish before reading their states.

        private void LateUpdate()
        {
            float height = layoutRoot.rect.height;
            Rect area = safeLayout != null ? safeLayout.NormalizedArea : new Rect(0, 0, 1, 1);
            if (height <= 0 || (Mathf.Approximately(height, shownHeight) && area == shownArea)) return;
            shownHeight = height;
            shownArea = area;
            // Scene setup uses these same panel dimensions.
            worldCamera.rect = ArenaViewport(area, height);
        }

        public static Rect ArenaViewport(Rect safeArea, float layoutHeight) =>
            new Rect(safeArea.x, safeArea.y + safeArea.height * (SquadBarHeight + ArenaPadding) / layoutHeight,
                safeArea.width, safeArea.height * Mathf.Max(0.1f,
                    1 - (TopBarHeight + RelayBarHeight + SquadBarHeight + 2 * ArenaPadding) / layoutHeight));

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
            bool running = lifecycle.IsReady && battle.State.IsRunning;
            double blueRate = 0, redRate = 0;
            if (running) battle.CurrentRates(out blueRate, out redRate);
            BattleOutcome forecast = lifecycle.IsReady ? battle.Forecast() : BattleOutcome.Draw;
            if (forecast != shownForecast)
            {
                shownForecast = forecast;
                bool visible = forecast == BattleOutcome.Victory || forecast == BattleOutcome.Defeat;
                Color color = forecast == BattleOutcome.Victory ? Blue : Red;
                color.a = 0.9f;
                for (int i = 0; i < leadEdges.Length; i++)
                {
                    leadEdges[i].color = color;
                    leadEdges[i].gameObject.SetActive(visible);
                }
            }
            if (blue != shownPlayer || blueRate != shownPlayerRate)
            {
                shownPlayer = blue; shownPlayerRate = blueRate;
                playerScore.text = $"ВЫ  {blue}/{MatchRules.ScoreLimit:0}\n+{blueRate:0.#} очк/с";
            }
            if (red != shownEnemy || redRate != shownEnemyRate)
            {
                shownEnemy = red; shownEnemyRate = redRate;
                enemyScore.text = $"ВРАГ  {red}/{MatchRules.ScoreLimit:0}\n+{redRate:0.#} очк/с";
            }
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                timer.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }
            pauseButton.interactable = lifecycle.IsReady && (tutorial == null || !tutorial.IsShowing);
            string pause = battle.State.Clock.IsPaused ? "Продолжить" : "Пауза";
            if (pauseLabel.text != pause) pauseLabel.text = pause;
            for (int i = 0; i < 3; i++)
            {
                RelayPoint relay = battle.Relays[i];
                int progress = (int)Math.Round(Math.Abs(relay.State.Progress) * 100);
                int side = relay.State.Progress > MatchRules.Epsilon ? 1 : relay.State.Progress < -MatchRules.Epsilon ? 2 : 0;
                bool repairOccupied = relay.Config.kind == RelayKind.Repair
                    && (relay.State.Owner == RelayOwner.Player ? relay.PlayersPresent > 0 : relay.EnemiesPresent > 0);
                int key = progress + side * 101 + (relay.State.IsContested ? 404 : 0)
                    + (int)relay.State.Owner * 1000 + (repairOccupied ? 4000 : 0) + (running ? 8000 : 0);
                if (relayKeys[i] != key)
                {
                    relayKeys[i] = key;
                    string owner = side == 1 ? "Ваш" : side == 2 ? "Враг" : "Свободен";
                    string effect = RelayText.Effect(relay.Config);
                    string status = !running ? battle.State.Clock.IsPaused ? "ПАУЗА" : "Бой окончен"
                        : relay.State.IsContested ? "СПОР · эффект остановлен"
                        : relay.State.Owner == RelayOwner.Neutral ? progress == 0 ? $"Свободен · захват {relay.Config.captureSeconds:0.#} с" : $"{owner} · захват {progress}%"
                        : relay.Config.kind == RelayKind.Score ? owner + " · приносит очки"
                        : repairOccupied ? owner + " · ремонтирует" : owner + " · ждёт бойца";
                    relayLabels[i].text = $"{relay.Config.label}  {effect}\n{status}";
                    relayImages[i].color = relay.State.IsContested ? new Color(0.56f, 0.42f, 0.10f)
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
                    unitLabels[i].text = alive ? $"{role} · {RolePurpose((UnitRole)i)}\n{hp}/{unit.Health.Max} HP"
                        : slot == null ? role : slot.IsSpawning ? role + "\nВозвращается…"
                        : slot.Respawn.IsWaiting ? role + $"\nВозврат: {Math.Max(0, respawn)} с" : role + "\nОжидание";
                }
                unitButtons[i].interactable = running && alive;
                unitImages[i].color = alive && commander.Selected == unit ? Blue : Neutral;
            }
            Unit selected = commander.Selected;
            int currentHp = selected != null ? selected.Health.Current : -1;
            if (shownSelection != selected || selectedHp != currentHp || selection.text.Length == 0)
            {
                shownSelection = selected;
                selectedHp = currentHp;
                selection.text = selected == null ? "Выберите бойца на поле или снизу. Тап по узлу — занять, по врагу — атаковать."
                    : $"{selected.Stats.role} · {RoleAdvice(selected.Stats.role)}\n{currentHp}/{selected.Health.Max} HP · удар {selected.Stats.damage} / {selected.Stats.attackCooldown:0.#} с · дальность {selected.Stats.attackRange:0.#} · скорость {selected.Stats.speed:0.#}";
            }
        }

        private static string RolePurpose(UnitRole role) => role == UnitRole.Tank ? "защита"
            : role == UnitRole.Scout ? "манёвр" : "дальний бой";

        private static string RoleAdvice(UnitRole role) => role == UnitRole.Tank ? "живучий: удерживайте узел"
            : role == UnitRole.Scout ? "быстрый, но хрупкий: обходите бой"
            : "стреляет издалека: прикрывайте союзника";
    }
}
