using UnityEngine;

namespace SectorLab.Units
{
    // Entry point of a unit for other systems: its movement and whether the player has it selected.
    [RequireComponent(typeof(UnitMovement))]
    public class Unit : MonoBehaviour
    {
        [SerializeField] private GameObject selectionIndicator;

        public UnitMovement Movement { get; private set; }
        public bool IsSelected { get; private set; }

        private void Awake()
        {
            Movement = GetComponent<UnitMovement>();
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selectionIndicator != null)
                selectionIndicator.SetActive(selected);
        }
    }
}
