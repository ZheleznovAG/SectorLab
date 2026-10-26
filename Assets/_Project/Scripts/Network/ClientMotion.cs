using System;
using SectorLab.Battle;
using UnityEngine;

namespace SectorLab.Network
{
    // A short visual delay, bounded to the newest known position. Never extrapolates through walls.
    public sealed class ClientMotion
    {
        public const double DelaySeconds = 0.1;
        private struct Track
        {
            public bool Alive;
            public uint Generation;
            public Vector2 From, To;
            public double StartedAt;
        }
        private readonly Track[] tracks = new Track[UnitsSnapshot.MaxUnits];
        private ulong match;

        public void Push(MatchModel model, double now)
        {
            if (match != model.MatchId || !model.IsSynchronized)
            {
                Array.Clear(tracks, 0, tracks.Length);
                match = model.MatchId;
            }
            if (!model.IsSynchronized) return;
            uint present = 0;
            foreach (UnitView view in model.Units)
            {
                int id = view.Id;
                if (id < 0 || id >= tracks.Length) continue;
                present |= 1u << id;
                ref Track track = ref tracks[id];
                Vector2 next = new Vector2(view.X, view.Z);
                bool snap = !track.Alive || !view.Alive || track.Generation != view.Generation || model.Paused.Value;
                if (snap)
                {
                    track.From = track.To = next;
                    track.StartedAt = now;
                }
                else if (track.To != next)
                {
                    track.From = Sample(track, now);
                    track.To = next;
                    track.StartedAt = now;
                }
                track.Alive = view.Alive;
                track.Generation = view.Generation;
            }
            for (int i = 0; i < tracks.Length; i++) if ((present & (1u << i)) == 0) tracks[i].Alive = false;
        }

        public bool TryPosition(int id, double now, out Vector2 position)
        {
            position = default;
            if (id < 0 || id >= tracks.Length || !tracks[id].Alive) return false;
            position = Sample(tracks[id], now);
            return true;
        }

        private static Vector2 Sample(in Track track, double now) =>
            Vector2.Lerp(track.From, track.To, Mathf.Clamp01((float)((now - track.StartedAt) / DelaySeconds)));
    }
}
