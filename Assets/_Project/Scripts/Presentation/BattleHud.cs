using System;
using R3;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Player;
using SectorLab.Units;
using UnityEngine;
using Unit = SectorLab.Units.Unit;
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
        private readonly int[] unitKeys = { int.MinValue, int.MinValue, int.MinValue };
        private int selectedHp = -1;
        private bool running;
        private TeamPerspective perspective = new TeamPerspective(Team.Player);
        private readonly CompositeDisposable subscriptions = new CompositeDisposable();
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
            pauseButton.onClick.AddListener(() => battle.SetPaused(!battle.IsPausedBy(PauseReason.User)));
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

        // Cards read the same model in local and remote matches; scene units only locate selection.
        private void Start()
        {
            battle.PublishModel(); // All relay Awake methods have finished by now.
            Bind(battle.Model);
            Refresh();
        }

        private void OnDestroy() => subscriptions.Dispose();

        // Re-reads the local team (a network session may assign it after connecting) and resubscribes.
        public void Rebind()
        {
            subscriptions.Clear();
            for (int i = 0; i < unitKeys.Length; i++) unitKeys[i] = int.MinValue;
            Bind(battle.Model);
            Refresh();
        }

        private void Bind(MatchModel model)
        {
            perspective = battle.Perspective;
            perspective.MyScore(model).Select(MatchModel.WholePoints).DistinctUntilChanged()
                .CombineLatest(perspective.MyRate(model), (points, rate) => (points, rate))
                .Subscribe(v => playerScore.text = $"ВЫ  {v.points}/{MatchRules.ScoreLimit:0}\n+{v.rate:0.#} очк/с")
                .AddTo(subscriptions);
            perspective.TheirScore(model).Select(MatchModel.WholePoints).DistinctUntilChanged()
                .CombineLatest(perspective.TheirRate(model), (points, rate) => (points, rate))
                .Subscribe(v => enemyScore.text = $"ВРАГ  {v.points}/{MatchRules.ScoreLimit:0}\n+{v.rate:0.#} очк/с")
                .AddTo(subscriptions);
            model.RemainingSeconds.Subscribe(seconds => timer.text = $"{seconds / 60:00}:{seconds % 60:00}").AddTo(subscriptions);
            model.Paused.Subscribe(paused => pauseLabel.text = paused ? "Продолжить" : "Пауза").AddTo(subscriptions);
            model.Running.Subscribe(value => running = value).AddTo(subscriptions);
            model.Forecast.Subscribe(forecast => ShowForecast(perspective.Outcome(forecast))).AddTo(subscriptions);
            for (int i = 0; i < model.Relays.Length && i < relayLabels.Length; i++)
            {
                int index = i;
                model.Relays[i].CombineLatest(model.Paused, model.Outcome, (view, paused, outcome) => (view, paused, outcome))
                    .Subscribe(v => ShowRelay(index, v.view, !v.paused && v.outcome == BattleOutcome.InProgress, v.paused))
                    .AddTo(subscriptions);
            }
        }

        private void ShowForecast(BattleOutcome forecast)
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

        private void ShowRelay(int index, RelayView view, bool isRunning, bool paused)
        {
            // A replaced relay set (scene fixtures) leaves the old pipelines alive until the HUD is destroyed.
            if (index >= battle.Relays.Count) return;
            RelayConfig config = battle.Relays[index].Config;
            int side = perspective.Side(view.Side);
            string owner = side == 1 ? "Ваш" : side == 2 ? "Враг" : "Свободен";
            string status = !isRunning ? paused ? "ПАУЗА" : "Бой окончен"
                : view.Contested ? "СПОР · эффект остановлен"
                : view.Owner == RelayOwner.Neutral ? view.Percent == 0 ? $"Свободен · захват {config.captureSeconds:0.#} с" : $"{owner} · захват {view.Percent}%"
                : config.kind == RelayKind.Score ? owner + " · приносит очки"
                : view.OwnerPresent ? owner + " · ремонтирует" : owner + " · ждёт бойца";
            relayLabels[index].text = $"{config.label}  {RelayText.Effect(config)}\n{status}";
            relayImages[index].color = view.Contested ? new Color(0.56f, 0.42f, 0.10f)
                : side == 1 ? Blue : side == 2 ? Red : Neutral;
        }

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
                if (slot.Config.team == perspective.Local && (int)slot.Config.role == role) return slot;
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
            bool isRunning = lifecycle.IsReady && running;
            // A network client does not own the clock; its local pause would also stop its orders.
            pauseButton.interactable = lifecycle.IsReady && (tutorial == null || !tutorial.IsShowing)
                && SectorLab.Network.NetworkLaunch.Role != SectorLab.Network.SessionRole.Client;
            for (int i = 0; i < 3; i++)
            {
                relayButtons[i].interactable = isRunning && commander.Selected != null;
                UnitSlotState slot = PlayerSlot(i);
                Unit unit = slot?.Unit;
                bool known = battle.Model.TryGetRole(perspective.Local, (UnitRole)i, out UnitView view);
                bool alive = known && view.Alive;
                int hp = alive ? view.Health : -1;
                int respawn = known && view.Life == UnitLife.Respawning ? (int)Math.Ceiling(view.RespawnSeconds - MatchRules.Epsilon) : -1;
                int unitKey = HashCode.Combine(known, view.Life, hp, view.MaxHealth, respawn);
                if (unitKeys[i] != unitKey)
                {
                    unitKeys[i] = unitKey;
                    string role = ((UnitRole)i).ToString();
                    unitLabels[i].text = alive ? $"{role} · {RolePurpose((UnitRole)i)}\n{hp}/{view.MaxHealth} HP"
                        : !known ? role : view.Life == UnitLife.Spawning ? role + "\nВозвращается…"
                        : view.Life == UnitLife.Respawning ? role + $"\nВозврат: {Math.Max(0, respawn)} с" : role + "\nОжидание";
                }
                unitButtons[i].interactable = isRunning && alive;
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
