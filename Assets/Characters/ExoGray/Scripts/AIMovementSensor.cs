using System;
using Animation.AnimControllers;
using UnityEngine;
using UnityEngine.AI;

// Required for Action delegate (events)

namespace Characters.ExoGray.Scripts
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class AIMovementSensor : MonoBehaviour
    {
        [Header("Targeting")] [Tooltip("The primary target for sensing proximity and facing. Can be null.")]
        public Transform currentTarget; // States can set this

        [Header("Sensing")]
        [Tooltip("The distance threshold considered 'in range' for actions like attacking.")]
        [SerializeField]
        private float actionRange = 2.0f;

        [Header("Component References")]
        [Tooltip("Reference to the CharacterAnimator component for speed updates.")]
        [SerializeField]
        private CharacterAnimator characterAnimator;

        private NavMeshAgent _agent;
        private bool _wasTargetInActionRange;

        // Public property to check range status externally if needed
        public bool IsTargetInActionRange { get; private set; }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();

            if (characterAnimator == null)
            {
                Debug.LogWarning($"{GetType().Name}: CharacterAnimator not set. Attempting to get it.", this);
                characterAnimator = GetComponent<CharacterAnimator>();
                if (characterAnimator == null)
                    Debug.LogError(
                        $"{GetType().Name}: CharacterAnimator component not found. Animation speed updates will fail.",
                        this);
                // Not disabling the whole script, as sensing might still be useful
            }
        }

        private void Update()
        {
            // Update Animator with current speed
            if (characterAnimator != null && _agent != null && _agent.isOnNavMesh)
                characterAnimator.SetMovementSpeed(_agent.velocity.magnitude);

            // Proximity Sensing Logic
            if (currentTarget != null)
            {
                var distance = Vector3.Distance(transform.position, currentTarget.position);
                IsTargetInActionRange = distance <= actionRange;

                if (IsTargetInActionRange != _wasTargetInActionRange)
                {
                    OnTargetInRangeStatusChanged?.Invoke(IsTargetInActionRange);
                    _wasTargetInActionRange = IsTargetInActionRange;
                }
            }
            else // No target
            {
                IsTargetInActionRange = false;
                if (IsTargetInActionRange != _wasTargetInActionRange) // If it *was* in range and target became null
                {
                    OnTargetInRangeStatusChanged?.Invoke(false);
                    _wasTargetInActionRange = false;
                }
            }
        }

        private void OnDisable()
        {
            // Reset animation speed
            if (characterAnimator != null) characterAnimator.SetMovementSpeed(0f);

            // If target was in range, notify it's no longer (or if target exists)
            if (_wasTargetInActionRange || (currentTarget != null && IsTargetInActionRange))
                OnTargetInRangeStatusChanged?.Invoke(false);
            _wasTargetInActionRange = false;
            IsTargetInActionRange = false;
        }

        // Event fired when the currentTarget enters or leaves the 'actionRange'.
        public event Action<bool> OnTargetInRangeStatusChanged;

        /// <summary>
        ///     Utility method to make the agent face its currentTarget (on the horizontal plane).
        ///     States can call this when they want the NPC to orient towards the target.
        /// </summary>
        public void FaceCurrentTarget()
        {
            if (currentTarget == null || _agent == null) return;

            var direction = (currentTarget.position - transform.position).normalized;
            if (direction == Vector3.zero) return; // Already at target or invalid direction

            var lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z));

            // Use NavMeshAgent's angularSpeed for consistent rotation behavior if it's steering
            // Otherwise, use a Slerp like before or agent.updateRotation = true and let it handle.
            if (_agent.updateRotation)
            {
                // If agent is handling rotation, this might not be strictly needed unless for instant snap or fine-tuning
                // For now, let's assume states might want finer control or to call this when agent's updateRotation is false.
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation,
                Time.deltaTime * _agent.angularSpeed * Mathf.Deg2Rad);
            // Alternatively, if agent.updateRotation = true, and you just set a destination, it might face it.
            // But for standing and facing, this explicit rotation is good.
        }

        /// <summary>
        ///     Sets the primary target for this sensor. Called by FSM states.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            if (currentTarget != newTarget)
            {
                currentTarget = newTarget;
                // Reset range status immediately as target changed
                _wasTargetInActionRange = false; // Force re-evaluation in Update
                IsTargetInActionRange = false; // To avoid stale data before next Update
                if (currentTarget == null) OnTargetInRangeStatusChanged?.Invoke(false); // Notify if target removed
                // Immediate check and event fire could also happen here if needed
            }
        }
        
            
        /// <summary>
        ///     Simple method to move to target position.
        ///     Call this from your state machine to move to the target position.
        /// </summary>
        public void MoveToTarget()
        {
            if (currentTarget == null || _agent == null) return;

            _agent.SetDestination(currentTarget.position);
        }
    }

}