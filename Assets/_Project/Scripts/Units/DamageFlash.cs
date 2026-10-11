using UnityEngine;

namespace SectorLab.Units
{
    // Briefly tints the unit's body when it takes damage, so hits read on a small screen.
    // Uses a MaterialPropertyBlock to avoid creating a material copy per unit.
    [RequireComponent(typeof(Unit))]
    public class DamageFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer body;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField, Min(0f)] private float duration = 0.1f;

        private Unit unit;
        private MaterialPropertyBlock block;
        private float endTime;
        private int previousHealth;

        public bool IsFlashing { get; private set; }

        private void Awake()
        {
            unit = GetComponent<Unit>();
            if (body == null)
                body = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        private void Start()
        {
            previousHealth = unit.Health.Current;
            unit.Health.Changed += OnHealthChanged;
        }

        private void OnDestroy()
        {
            if (unit != null && unit.Health != null)
                unit.Health.Changed -= OnHealthChanged;
        }

        private void OnHealthChanged()
        {
            if (unit.Health.Current < previousHealth) Flash();
            previousHealth = unit.Health.Current;
        }

        private void Flash()
        {
            block.SetColor(BaseColorId, flashColor);
            body.SetPropertyBlock(block);
            endTime = Time.time + duration;
            IsFlashing = true;
        }

        private void Update()
        {
            if (IsFlashing && Time.time >= endTime)
            {
                // An empty block restores the material's own colour.
                body.SetPropertyBlock(null);
                IsFlashing = false;
            }
        }
    }
}
