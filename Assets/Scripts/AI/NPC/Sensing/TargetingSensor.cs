using System;
using UnityEngine;

namespace AI.Sensing.NPC
{
    public class TargetingSensor : MonoBehaviour
    {
        [SerializeField] private float actionRange = 2f;
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
            UpdateRange(distance <= actionRange);
        }

        public void SetTarget(Transform newTarget)
        {
            CurrentTarget = newTarget;
            _wasInRange = false; // Force re-evaluation
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