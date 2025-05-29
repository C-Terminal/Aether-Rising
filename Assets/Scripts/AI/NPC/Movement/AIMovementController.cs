using UnityEngine;
using UnityEngine.AI;

namespace AI.NPC.Movement
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class AIMovementController : MonoBehaviour
    {
        private NavMeshAgent _agent;

        private void Awake() => _agent = GetComponent<NavMeshAgent>();

        public void MoveTo(Transform target)
        {
            if (target != null && _agent.isOnNavMesh)
                _agent.SetDestination(target.position);
        }

        public void RotateToward(Transform target, float speed = -1f, float snapThreshold = 1f, bool smooth = false)
        {
            if (target == null) return;

            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            if (direction == Vector3.zero) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            float angle = Quaternion.Angle(transform.rotation, targetRotation);
            float effectiveSpeed = speed > 0f ? speed : _agent.angularSpeed * Mathf.Deg2Rad;

            if (snapThreshold > 0 && angle < snapThreshold)
            {
                transform.rotation = targetRotation;
                return;
            }

            if (_agent.updateRotation)
                _agent.updateRotation = false;

            transform.rotation = smooth
                ? Quaternion.RotateTowards(transform.rotation, targetRotation, effectiveSpeed * Time.deltaTime * angle)
                : Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * effectiveSpeed);
        }

        public float GetSpeed() => _agent.velocity.magnitude;
    }
}