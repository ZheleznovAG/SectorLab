using UnityEngine;

namespace SectorLab.Objectives
{
    public enum RelayKind { Score, Repair }

    [CreateAssetMenu(menuName = "SectorLab/Relay Config")]
    public sealed class RelayConfig : ScriptableObject
    {
        public string label = "A";
        public RelayKind kind;
        [Min(0.1f)] public float radius = 1.5f;
        [Min(0.1f)] public float captureSeconds = 5f;
        [Min(0f)] public float scorePerSecond = 1f;
        [Min(0f)] public float healingPerSecond = 6f;
    }
}
