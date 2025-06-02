using System;
using UnityEngine;
using UnityEngine.AI;

namespace AI.NPC.Sensing
{
    public class TargetingSensor : MonoBehaviour
    {
        // [SerializeField] private float actionRange = 2f;
        // [SerializeField] private float visibleChaseAngle = 90f;
        [SerializeField] private LayerMask obstacleLayerMask = -1;
        
        [SerializeField] private NPCPerceptionConfig perceptionConfig;
        public Transform CurrentTarget { get; private set; }

        public event Action<bool> OnTargetRangeChanged;

        private bool _wasInRange;

        private void Update()
        {
            if (CurrentTarget == null)
            {
                UpdateRange(false);
                return;
            }

            float distance = Vector3.Distance(transform.position, CurrentTarget.position);
            UpdateRange(distance <= perceptionConfig.actionRange);
        }

        public void SetTarget(Transform newTarget)
        {
            CurrentTarget = newTarget;
            _wasInRange = false; // Force re-evaluation
        }
        
        public bool HasLineOfSight()
        {
            if (CurrentTarget == null) return false;

            var directionToTarget = CurrentTarget.position - transform.position;
            var angle = Vector3.Angle(transform.forward, directionToTarget.normalized);

            if (angle < perceptionConfig.fieldOfView / 2f)
            {
                var distanceToTarget = directionToTarget.magnitude;
                var rayStart = transform.position + transform.up * GetComponent<NavMeshAgent>().height / 2f;
        
                if (Physics.Raycast(rayStart, directionToTarget.normalized, out RaycastHit hit,
                        distanceToTarget, obstacleLayerMask))
                {
                    return hit.transform == CurrentTarget || hit.transform.IsChildOf(CurrentTarget);
                }
                return true;
            }
            return false;
        }

        private void UpdateRange(bool isInRange)
        {
            if (_wasInRange != isInRange)
            {
                _wasInRange = isInRange;
                OnTargetRangeChanged?.Invoke(isInRange);
            }
        }

        private void OnDisable()
        {
            OnTargetRangeChanged?.Invoke(false);
            _wasInRange = false;
        }
    }
}