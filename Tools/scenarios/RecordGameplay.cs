using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Player;
using SectorLab.Presentation;
using SectorLab.Units;
using Unity.Collections;
using UnityEngine;

// Loaded into a disposable QA player by Pipeline. Never imported into the game project.
public sealed class SectorLabDemoRecorder : MonoBehaviour
{
    public static string Folder, Encoder;
    public static int LimitSeconds;
    private const int Fps = 30;
    private BattleController battle;
    private UnitLifecycle lifecycle;
    private UnitCommander commander;
    private System.Diagnostics.Process encoder;
    private BinaryWriter audio;
    private Texture2D pixels;
    private RenderTexture capture;
    private int frame, channels = 2, sampleRate, orders, playerDeaths, enemyDeaths;
    private double nextOrders, resultAt = -1;
    private bool done, audioStarted;
    private string failure;
    private readonly Dictionary<Unit, RelayPoint> destinations = new Dictionary<Unit, RelayPoint>();

    public static object Launch()
    {
        new GameObject("Gameplay recording").AddComponent<SectorLabDemoRecorder>();
        return "recording scheduled";
    }

    private IEnumerator Start()
    {
        try { Begin(); } catch (Exception e) { failure = e.ToString(); done = true; }
        var end = new WaitForEndOfFrame();
        while (!done)
        {
            yield return end;
            try { CaptureFrame(); } catch (Exception e) { failure = e.ToString(); done = true; }
        }
        Finish();
    }

    private void Begin()
    {
        battle = UnityEngine.Object.FindAnyObjectByType<BattleController>();
        lifecycle = UnityEngine.Object.FindAnyObjectByType<UnitLifecycle>();
        commander = UnityEngine.Object.FindAnyObjectByType<UnitCommander>();
        if (!lifecycle.IsReady) throw new InvalidOperationException("Wait for the six units before recording.");
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Time.captureFramerate = Fps;
        var config = AudioSettings.GetConfiguration();
        if (config.speakerMode != AudioSpeakerMode.Stereo) {
            config.speakerMode = AudioSpeakerMode.Stereo;
            if (!AudioSettings.Reset(config)) throw new InvalidOperationException("Cannot configure stereo capture.");
        }
        sampleRate = AudioSettings.outputSampleRate;
        audio = new BinaryWriter(File.Open(Path.Combine(Folder, "audio.f32"), FileMode.CreateNew));
        audioStarted = AudioRenderer.Start();
        if (!audioStarted) throw new InvalidOperationException("Cannot start Unity audio capture.");
        UnityEngine.Object.FindAnyObjectByType<MatchAudio>().SetMuted(false, false);
        capture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        capture.Create();
        // An invisible Windows swapchain has a black backbuffer. Render the actual game
        // camera and the same HUD into a texture instead, entirely inside this QA process.
        var camera = Camera.main;
        camera.targetTexture = capture;
        UnityEngine.Object.FindAnyObjectByType<TacticalCamera>().Zoom(22f / 18f);
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 1;
        }
        pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        var start = new System.Diagnostics.ProcessStartInfo {
            FileName = Encoder, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true,
            Arguments = "-hide_banner -loglevel error -f rawvideo -pixel_format rgb24 -video_size "
                + Screen.width + "x" + Screen.height + " -framerate 30 -i pipe:0 -vf vflip "
                + "-an -c:v libx264 -preset fast -crf 20 -pix_fmt yuv420p -movflags +faststart \""
                + Path.Combine(Folder, "video.mp4") + "\""
        };
        encoder = System.Diagnostics.Process.Start(start);
        lifecycle.Spawned += OnSpawned;
        foreach (var slot in lifecycle.Slots) if (slot.Unit != null) OnSpawned(slot.Unit);
        // Dismiss the tutorial while retaining the player's stored preference.
        string key = TutorialView.SeenPreference;
        bool existed = PlayerPrefs.HasKey(key);
        int previous = PlayerPrefs.GetInt(key, 0);
        UnityEngine.Object.FindAnyObjectByType<TutorialView>().Dismiss();
        if (existed) PlayerPrefs.SetInt(key, previous); else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        battle.SetPaused(true); // A brief view of the arena before the first real orders.
        WriteProgress();
    }

    private void OnSpawned(Unit unit) => unit.Died += OnDied;
    private void OnDied(Unit unit) { if (unit.Team == Team.Player) playerDeaths++; else enemyDeaths++; }

    private void Update()
    {
        if (done || battle == null || frame < 45) return;
        if (frame == 45) battle.SetPaused(false);
        if (battle.Outcome != BattleOutcome.InProgress) return;
        if (battle.State.Clock.ElapsedSeconds < nextOrders) return;
        nextOrders = battle.State.Clock.ElapsedSeconds + 1;
        foreach (var slot in lifecycle.Slots)
        {
            Unit unit = slot.Unit;
            if (slot.Config.team != Team.Player || unit == null || !unit.IsAlive) continue;
            // Cover B with Tank/Ranger; use Scout to intercept A, as in MainTacticsTests.
            RelayPoint target = battle.Relays[slot.Config.role == UnitRole.Scout ? 0 : 1];
            bool changed = !destinations.TryGetValue(unit, out var previous) || previous != target;
            bool idleAway = !unit.Movement.IsMoving && unit.Attack.Target == null && !target.Contains(unit);
            bool chasingAway = !target.Contains(unit) && unit.Attack.Target != null;
            if (!changed && !idleAway && !chasingAway) continue;
            if (commander.SelectFriendly(unit) && commander.OrderSelectedToRelay(target)) {
                destinations[unit] = target;
                orders++;
            }
        }
    }

    private void CaptureFrame()
    {
        var viewport = Camera.main.rect;
        Camera.main.rect = new Rect(0, 0, 1, 1);
        Canvas.ForceUpdateCanvases();
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,
            new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = capture });
        Camera.main.rect = viewport;
        var previous = RenderTexture.active;
        RenderTexture.active = capture;
        pixels.ReadPixels(new Rect(0, 0, capture.width, capture.height), 0, 0, false);
        RenderTexture.active = previous;
        byte[] bytes = pixels.GetRawTextureData<byte>().ToArray();
        encoder.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
        int count = AudioRenderer.GetSampleCountForCaptureFrame() * channels;
        using (var samples = new NativeArray<float>(count, Allocator.Temp)) {
            if (!AudioRenderer.Render(samples)) throw new InvalidOperationException("Audio frame capture failed.");
            for (int i = 0; i < count; i++) audio.Write(samples[i]);
        }
        frame++;
        if (frame % Fps == 0) WriteProgress();
        if (battle.Outcome != BattleOutcome.InProgress && resultAt < 0) resultAt = frame / (double)Fps;
        done = frame >= LimitSeconds * Fps || resultAt >= 0 && frame / (double)Fps >= resultAt + 4;
    }

    private void WriteProgress()
    {
        var record = new Recording {
            frames = frame, fps = Fps, width = Screen.width, height = Screen.height,
            sampleRate = sampleRate, channels = channels, orders = orders,
            playerDeaths = playerDeaths, enemyDeaths = enemyDeaths,
            gameSeconds = battle.State.Clock.ElapsedSeconds, playerScore = battle.State.PlayerScore,
            enemyScore = battle.State.EnemyScore, outcome = battle.Outcome.ToString(),
            resultAt = resultAt, completed = done, error = failure
        };
        File.WriteAllText(Path.Combine(Folder, "recording.json"), JsonUtility.ToJson(record, true));
    }

    private void Finish()
    {
        Time.captureFramerate = 0;
        if (audioStarted) AudioRenderer.Stop();
        audio?.Dispose();
        if (encoder != null) {
            encoder.StandardInput.Close();
            if (!encoder.WaitForExit(30000) || encoder.ExitCode != 0) failure = "FFmpeg encoder did not finish successfully.";
            encoder.Dispose(); encoder = null;
        }
        if (capture != null) { capture.Release(); Destroy(capture); }
        if (pixels != null) Destroy(pixels);
        done = true;
        WriteProgress();
    }

    [Serializable] private sealed class Recording {
        public int frames, fps, width, height, sampleRate, channels, orders, playerDeaths, enemyDeaths;
        public double gameSeconds, playerScore, enemyScore, resultAt;
        public string outcome, error;
        public bool completed;
    }
}
