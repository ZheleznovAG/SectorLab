using System;

namespace SectorLab.Units
{
    // Plain C# health rules, kept out of MonoBehaviour so they can be unit-tested without a scene.
    public class Health
    {
        public int Max { get; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public float Fraction => (float)Current / Max;

        public event Action Changed;
        public event Action Died;

        public Health(int max)
        {
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max), "Max health must be positive.");

            Max = max;
            Current = max;
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead)
                return;

            Current = Math.Max(0, Current - amount);
            Changed?.Invoke();

            if (IsDead)
                Died?.Invoke();
        }

        // A proxy mirrors health, including zero, without starting local death/respawn logic.
        public void SetReplicated(int current)
        {
            int value = Math.Max(0, Math.Min(Max, current));
            if (value == Current) return;
            Current = value;
            Changed?.Invoke();
        }

        // Returns actual healing. Adding only the missing amount also avoids integer overflow.
        public int Heal(int amount)
        {
            if (amount <= 0 || IsDead) return 0;
            int healed = Math.Min(amount, Max - Current);
            if (healed == 0) return 0;
            Current += healed;
            Changed?.Invoke();
            return healed;
        }
    }
}
