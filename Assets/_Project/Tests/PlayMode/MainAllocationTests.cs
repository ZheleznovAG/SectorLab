using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    // Editor measurement, not a device profile. The Editor and test runner allocate a steady, scene-dependent
    // amount every frame, so the running match is compared with the same Main after every SectorLab script
    // is disabled. Medians ignore one-off frames (a label change, a capture) and catch garbage made each frame.
    public class MainAllocationTests
    {
        public const long BudgetBytesPerFrame = 512;
        private const int WarmupFrames = 120, MatchFrames = 600, BaselineFrames = 120;

        [UnityTest]
        public IEnumerator RunningMatchAddsNoSteadyGarbagePerFrame()
        {
            var result = new Measurement();
            yield return Measure(result, null);
            Assert.That(result.Added, Is.LessThanOrEqualTo(BudgetBytesPerFrame), result.Summary);
        }

        [UnityTest]
        public IEnumerator BudgetCatchesGarbageMadeEveryFrame()
        {
            var result = new Measurement();
            yield return Measure(result, typeof(GarbageEveryFrame));
            Assert.That(result.Added, Is.GreaterThan(BudgetBytesPerFrame), result.Summary);
        }

        private sealed class Measurement
        {
            public long MatchMedian, BaselineMedian;
            public int Frames;
            public long Added => MatchMedian - BaselineMedian;
            public string Summary => $"match median={MatchMedian} B/frame over {Frames} frames, scripts-off median={BaselineMedian} B/frame, added={Added} B/frame";
        }

        private sealed class GarbageEveryFrame : MonoBehaviour
        {
            public static object Sink;
            private void Update() => Sink = new byte[2048];
        }

        private static IEnumerator Measure(Measurement result, Type injected)
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return MainLifecycleTests.WaitForReady();
            var battle = UnityEngine.Object.FindAnyObjectByType<BattleController>();
            Assert.That(battle, Is.Not.Null);
            // Send the squad to A so movement, combat, capture and HUD updates are all exercised.
            for (int i = 0; i < Unit.All.Count; i++)
            {
                Unit unit = Unit.All[i];
                if (unit.Team != Team.Player) continue;
                RelayPoint relay = battle.Relays[0];
                Assert.That(relay.TryGetApproachPoint(unit, out Vector3 point), Is.True);
                unit.Movement.MoveTo(point, relay.GetArrivalDistance(point));
            }
            if (injected != null) new GameObject("Injected garbage", injected);
            for (int i = 0; i < WarmupFrames; i++) yield return null;

            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var match = new long[MatchFrames];
            int measured = 0;
            yield return null;
            while (measured < MatchFrames && battle.Outcome == BattleOutcome.InProgress)
            {
                yield return null;
                match[measured++] = recorder.LastValue;
            }
            Assert.That(measured, Is.GreaterThan(MatchFrames / 2), "The match ended before enough frames were measured.");

            var disabled = new List<MonoBehaviour>();
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                string space = behaviour.GetType().Namespace;
                if (behaviour.enabled && space != null && space.StartsWith("SectorLab", StringComparison.Ordinal))
                {
                    behaviour.enabled = false;
                    disabled.Add(behaviour);
                }
            }
            for (int i = 0; i < 30; i++) yield return null;
            var baseline = new long[BaselineFrames];
            for (int i = 0; i < BaselineFrames; i++) { yield return null; baseline[i] = recorder.LastValue; }

            result.Frames = measured;
            result.MatchMedian = Median(match, measured);
            result.BaselineMedian = Median(baseline, BaselineFrames);
            Debug.Log($"SectorLab GC allocations ({(injected == null ? "production" : injected.Name)}, {disabled.Count} scripts off for baseline): {result.Summary}");
#if UNITY_EDITOR
            string task = Environment.GetEnvironmentVariable("SECTORLAB_VERIFICATION_TASK");
            if (!string.IsNullOrEmpty(task) && injected == null)
            {
                var csv = new StringBuilder("phase,frame,gc_bytes\n");
                for (int i = 0; i < measured; i++) csv.Append("match,").Append(i).Append(',').Append(match[i]).Append('\n');
                for (int i = 0; i < BaselineFrames; i++) csv.Append("scripts_off,").Append(i).Append(',').Append(baseline[i]).Append('\n');
                string path = System.IO.Path.Combine(Application.dataPath, "../docs/evidence/" + task + "-gc-frames.csv");
                System.IO.File.WriteAllText(path, csv.ToString());
            }
#endif
        }

        private static long Median(long[] values, int count)
        {
            var sorted = new long[count];
            Array.Copy(values, sorted, count);
            Array.Sort(sorted);
            return sorted[count / 2];
        }
    }
}
