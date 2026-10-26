using R3;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.UI;

namespace SectorLab.Presentation
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class MatchAudio : MonoBehaviour
    {
        public const string MutedPreference = "SectorLab.AudioMuted.v1";
        [SerializeField] private BattleController battle;
        [SerializeField] private UnitLifecycle lifecycle;
        [SerializeField] private Button muteButton;
        [SerializeField] private Text muteLabel;
        [SerializeField] private Text noticeLabel;
        private readonly UnitView[] previousUnits = new UnitView[16];
        private readonly bool[] seenUnits = new bool[16];
        private RelayView[] previousRelays = System.Array.Empty<RelayView>();
        private ulong observedMatch;
        private double observedSeconds;
        private bool baseline;
        private System.IDisposable outcomeSubscription;
        private AudioSource source;
        private AudioClip tankShot, scoutShot, rangerShot, capture, warning, victory, defeat;
        private readonly float[] nextShot = new float[3];
        private float hideNoticeAt;
        public bool Muted { get; private set; }
        public int PlayedShots { get; private set; }
        public int PlayedCaptures { get; private set; }
        public int PlayedAlerts { get; private set; }
        public int PlayedResults { get; private set; }
        public string LastSound { get; private set; }
        public string LastNotice { get; private set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0; source.volume = 0.5f;
            tankShot = Tone("SectorLab Tank shot", 140, 55, 0.12f);
            scoutShot = Tone("SectorLab Scout shot", 1100, 750, 0.035f);
            rangerShot = Tone("SectorLab Ranger shot", 1800, 220, 0.16f);
            capture = Tone("SectorLab own capture", 540, 1100, 0.28f);
            warning = Tone("SectorLab relay warning", 650, 180, 0.32f);
            victory = Tone("SectorLab victory", 440, 880, 0.45f);
            defeat = Tone("SectorLab defeat", 440, 180, 0.45f);
            SetMuted(PlayerPrefs.GetInt(MutedPreference, 0) != 0, false);
            muteButton.onClick.AddListener(() => SetMuted(!Muted));
            if (noticeLabel != null) noticeLabel.transform.parent.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            baseline = false;
            battle.Model.Updated += OnModel;
            outcomeSubscription = battle.Model.Outcome.Subscribe(outcome =>
            {
                if (outcome != BattleOutcome.InProgress) OnFinished(outcome);
            });
        }

        private void Start() => battle.PublishModel();

        private void OnModel()
        {
            MatchModel model = battle.Model;
            if (!model.IsSynchronized || observedMatch != model.MatchId)
            {
                baseline = false; observedMatch = model.MatchId;
                System.Array.Clear(seenUnits, 0, seenUnits.Length);
                System.Array.Clear(nextShot, 0, nextShot.Length);
                announcedForecast = pendingForecast = BattleOutcome.Draw;
                pendingSeconds = 0;
                if (!model.IsSynchronized) return;
            }
            if (previousRelays.Length != model.Relays.Length)
            {
                previousRelays = new RelayView[model.Relays.Length];
                baseline = false;
            }
            for (int i = 0; i < model.Relays.Length; i++)
            {
                RelayView current = model.Relays[i].Value;
                if (baseline && i < battle.Relays.Count)
                    OnRelayChanged(battle.Relays[i].Config, previousRelays[i], current);
                previousRelays[i] = current;
            }
            foreach (UnitView current in model.Units)
            {
                int id = current.Id;
                if (id < 0 || id >= previousUnits.Length) continue;
                UnitView previous = previousUnits[id];
                if (baseline && seenUnits[id] && current.Alive && previous.Alive
                    && current.Generation == previous.Generation && current.Shots > previous.Shots)
                    OnShot(current.Role, current.Team);
                previousUnits[id] = current; seenUnits[id] = true;
            }
            double delta = model.GameSeconds - observedSeconds;
            if (baseline && delta > 0 && !model.Paused.Value && model.Outcome.Value == BattleOutcome.InProgress)
                AnnounceForecast(delta);
            observedSeconds = model.GameSeconds;
            baseline = true;
        }

        private void OnShot(UnitRole role, Team team)
        {
            int index = (int)role;
            if (Muted || Time.unscaledTime < nextShot[index]) return;
            nextShot[index] = Time.unscaledTime + 0.08f;
            Play(role == UnitRole.Tank ? tankShot : role == UnitRole.Scout ? scoutShot : rangerShot,
                team == battle.Perspective.Local ? 0.3f : 0.18f);
            PlayedShots++;
        }

        private void OnRelayChanged(RelayConfig config, RelayView previous, RelayView current)
        {
            // A contest matters when it threatens our relay or our capture in progress.
            TeamPerspective view = battle.Perspective;
            if (current.Contested && !previous.Contested && (view.IsMine(previous.Owner) || view.Side(current.Side) == 1))
            {
                Notify(config.label + ": СПОР · эффект остановлен");
                Alert();
            }
            if (current.Owner == previous.Owner) return;
            if (view.IsMine(current.Owner))
            {
                string effect = config.kind == RelayKind.Score ? RelayText.Effect(config) : "ремонт " + RelayText.Effect(config);
                Notify($"Вы захватили {config.label} · {effect}");
                if (!Muted) { Play(capture, 0.8f); PlayedCaptures++; }
            }
            else if (view.IsTheirs(current.Owner))
            {
                Notify("Враг захватил " + config.label);
                if (!Muted) { Play(warning, 0.8f); PlayedCaptures++; }
            }
            else if (view.IsMine(previous.Owner))
            {
                Notify(config.label + ": контроль потерян");
                Alert();
            }
        }

        // A new forecast must hold for a moment, so a brief contest does not flip announcements.
        public const double ForecastSettleSeconds = 1;
        private BattleOutcome announcedForecast = BattleOutcome.Draw, pendingForecast = BattleOutcome.Draw;
        private double pendingSeconds;
        public int PlayedLeadChanges { get; private set; }

        private void AnnounceForecast(double seconds)
        {
            BattleOutcome forecast = battle.Perspective.Outcome(battle.Model.Forecast.CurrentValue);
            if (forecast != pendingForecast) { pendingForecast = forecast; pendingSeconds = 0; }
            pendingSeconds += seconds;
            if (pendingSeconds < ForecastSettleSeconds - MatchRules.Epsilon || forecast == announcedForecast) return;
            announcedForecast = forecast;
            if (forecast == BattleOutcome.Victory)
            {
                Notify("Сейчас вы побеждаете");
                if (!Muted) { Play(capture, 0.8f); PlayedLeadChanges++; }
            }
            else if (forecast == BattleOutcome.Defeat)
            {
                Notify("Сейчас побеждает враг: отбейте узел");
                if (!Muted) { Play(warning, 0.8f); PlayedLeadChanges++; }
            }
        }

        private void Alert() { if (!Muted) { Play(warning, 0.8f); PlayedAlerts++; } }

        private void Play(AudioClip clip, float volume)
        {
            source.PlayOneShot(clip, volume);
            LastSound = clip.name;
        }

        public void ShowNetworkNotice(string message) => Notify(message);

        private void Notify(string message)
        {
            LastNotice = message;
            hideNoticeAt = Time.unscaledTime + 3.5f;
            if (noticeLabel == null) return;
            noticeLabel.text = message;
            noticeLabel.transform.parent.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (noticeLabel != null && noticeLabel.transform.parent.gameObject.activeSelf && Time.unscaledTime >= hideNoticeAt)
                noticeLabel.transform.parent.gameObject.SetActive(false);
        }

        private void OnFinished(BattleOutcome outcome)
        {
            if (Muted) return;
            Play(battle.Perspective.Outcome(outcome) == BattleOutcome.Victory ? victory : defeat, 0.8f);
            PlayedResults++;
        }

        public void SetMuted(bool muted, bool save = true)
        {
            Muted = muted; source.mute = muted;
            if (muted) source.Stop();
            muteLabel.text = muted ? "Звук: выкл" : "Звук: вкл";
            if (save) { PlayerPrefs.SetInt(MutedPreference, muted ? 1 : 0); PlayerPrefs.Save(); }
        }

        private void OnDisable()
        {
            battle.Model.Updated -= OnModel;
            outcomeSubscription?.Dispose();
            outcomeSubscription = null;
            if (source != null) source.Stop();
        }

        private void OnDestroy()
        {
            Destroy(tankShot); Destroy(scoutShot); Destroy(rangerShot); Destroy(capture); Destroy(warning); Destroy(victory); Destroy(defeat);
        }

        private static AudioClip Tone(string name, float startFrequency, float endFrequency, float duration)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            float phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / samples.Length;
                phase += 2 * Mathf.PI * Mathf.Lerp(startFrequency, endFrequency, t) / rate;
                samples[i] = Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t) * (1 - t) * 0.35f;
            }
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
    }
}
