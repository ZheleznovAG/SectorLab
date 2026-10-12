using System.Collections.Generic;
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
        private readonly List<Unit> observed = new List<Unit>(6);
        private RelayOwner[] owners;
        private AudioSource source;
        private AudioClip shot, capture, victory, defeat;
        private float nextShot;
        public bool Muted { get; private set; }
        public int PlayedShots { get; private set; }
        public int PlayedCaptures { get; private set; }
        public int PlayedResults { get; private set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0; source.volume = 0.5f;
            shot = Tone("SectorLab shot", 180, 80, 0.06f);
            capture = Tone("SectorLab capture", 540, 900, 0.18f);
            victory = Tone("SectorLab victory", 440, 880, 0.45f);
            defeat = Tone("SectorLab defeat", 440, 180, 0.45f);
            SetMuted(PlayerPrefs.GetInt(MutedPreference, 0) != 0, false);
            muteButton.onClick.AddListener(() => SetMuted(!Muted));
        }

        private void OnEnable()
        {
            owners = new RelayOwner[battle.Relays.Count];
            lifecycle.Spawned += Observe;
            battle.Ticked += OnTick;
            battle.Finished += OnFinished;
            for (int i = 0; i < Unit.All.Count; i++) Observe(Unit.All[i]);
        }

        private void Observe(Unit unit)
        {
            if (observed.Contains(unit)) return;
            observed.Add(unit);
            unit.Attack.Fired += OnShot;
            unit.Died += OnDied;
        }

        private void OnDied(Unit unit)
        {
            unit.Attack.Fired -= OnShot;
            unit.Died -= OnDied;
            observed.Remove(unit);
        }

        private void OnShot(Unit unit)
        {
            if (Muted || Time.unscaledTime < nextShot) return;
            nextShot = Time.unscaledTime + 0.04f;
            source.PlayOneShot(shot, 0.3f);
            PlayedShots++;
        }

        private void OnTick(double seconds)
        {
            for (int i = 0; i < battle.Relays.Count; i++)
            {
                RelayOwner current = battle.Relays[i].State.Owner;
                if (current == owners[i]) continue;
                owners[i] = current;
                if (current == RelayOwner.Neutral || Muted) continue;
                source.PlayOneShot(capture, 0.6f); PlayedCaptures++;
            }
        }

        private void OnFinished(BattleOutcome outcome)
        {
            if (Muted) return;
            source.PlayOneShot(outcome == BattleOutcome.Victory ? victory : defeat, 0.8f);
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
            lifecycle.Spawned -= Observe; battle.Ticked -= OnTick; battle.Finished -= OnFinished;
            for (int i = observed.Count - 1; i >= 0; i--)
                if (observed[i] != null) { observed[i].Attack.Fired -= OnShot; observed[i].Died -= OnDied; }
            observed.Clear();
            if (source != null) source.Stop();
        }

        private void OnDestroy() { Destroy(shot); Destroy(capture); Destroy(victory); Destroy(defeat); }

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
