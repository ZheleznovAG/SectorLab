using UnityEngine;

namespace SectorLab.Units
{
    // Moves the unit in a straight line to a destination and stops exactly on it.
    // No pathfinding or obstacle avoidance yet (see docs/03_ROADMAP.md, R1).
    public class UnitMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 3f;

        private Vector3 destination;

        public bool IsMoving { get; private set; }
        public Vector3 Destination => destination;

        public void MoveTo(Vector3 point)
        {
            // The unit keeps its own height; only XZ comes from the order.
            destination = new Vector3(point.x, transform.position.y, point.z);
            IsMoving = true;
        }

        public void Stop()
        {
            IsMoving = false;
        }

        private void Update()
        {
            if (!IsMoving)
                return;

            // MoveTowards never steps past the target, so a long frame can't overshoot.
            transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);

            if (transform.position == destination)
                IsMoving = false;
        }
    }
}
