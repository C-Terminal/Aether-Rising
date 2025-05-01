using System;
using UnityEngine;
using UnityEngine.AI;

// Required for Action delegate (events)

namespace Characters.ExoGray.Scripts
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class AICharacterMovement : MonoBehaviour
    {
        [Header("Targeting")]
        [Tooltip("The target the NPC should move towards.")]
        public Transform target;

        [Header("Movement")]
        [Tooltip("The distance at which the NPC stops moving towards the target.")]
        [SerializeField] private float movementStopDistance = 1.5f;
        [Tooltip("The distance threshold considered 'in range' for potential actions (e.g., attacking).")]
        [SerializeField] private float actionRange = 2.0f; // Renamed for clarity

        [Header("Component References")]
        [Tooltip("Reference to the CharacterAnimator component.")]
        [SerializeField] private Character.CharacterAnimator characterAnimator; // Reference via Inspector

        // --- Component References ---
        private NavMeshAgent _agent;

        // --- Events ---
        /// <summary>
        /// Event fired when the target enters or leaves the 'actionRange'.
        /// Passes 'true' if the target is now in range, 'false' otherwise.
        /// </summary>
        public event Action<bool> OnTargetInRangeStatusChanged;

        // --- State ---
        private bool _wasTargetInActionRange = false;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();

            // Basic validation for required components/references
            if (characterAnimator == null)
            {
                Debug.LogWarning("AICharacterMovement: CharacterAnimator reference not set.", this);
                // Optionally try to find it on the same GameObject
                characterAnimator = GetComponent<Character.CharacterAnimator>();
                if (characterAnimator == null)
                {
                    Debug.LogError("AICharacterMovement: CharacterAnimator component not found.", this);
                    enabled = false; // Disable script if critical component missing
                    return;
                }
            }
        }

        void Update()
        {
            // --- Target Handling ---
            if (target == null)
            {
                HandleNoTarget();
                return;
            }

            // --- Distance & State Calculation ---
            float distance = Vector3.Distance(transform.position, target.position);
            bool isTargetInActionRange = (distance <= actionRange);
            bool shouldMove = (distance > movementStopDistance);

            // --- Update NavMeshAgent ---
            if (shouldMove)
            {
                _agent.isStopped = false;
                _agent.SetDestination(target.position);
            }
            else
            {
                _agent.isStopped = true;
                // Optional: Face target when stopped (can be its own component too!)
                FaceTarget();
            }

            // --- Update Animator ---
            // Use agent's velocity magnitude for blend trees, or a fixed value if using bools
            float currentSpeed = _agent.velocity.magnitude;
            characterAnimator.SetMovementSpeed(currentSpeed); // Tell animator about speed

            // --- Update Proximity Status & Fire Event ---
            // Only fire the event if the status *changes*
            if (isTargetInActionRange != _wasTargetInActionRange)
            {
                OnTargetInRangeStatusChanged?.Invoke(isTargetInActionRange); // Fire event
                _wasTargetInActionRange = isTargetInActionRange;
            }
        }

        private void HandleNoTarget()
        {
            if (_agent.hasPath)
            {
                _agent.ResetPath();
                _agent.isStopped = true;
            }
            characterAnimator.SetMovementSpeed(0f); // Ensure animator shows idle

            // If target was previously in range, notify listeners it's no longer in range
            if (_wasTargetInActionRange)
            {
                OnTargetInRangeStatusChanged?.Invoke(false);
                _wasTargetInActionRange = false;
            }
        }

        private void FaceTarget()
        {
            if (target == null) return;
            Vector3 direction = (target.position - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * _agent.angularSpeed * Mathf.Deg2Rad);
        }

        // Ensure state is reset if target was in range when disabled
        private void OnDisable() {
            if (_wasTargetInActionRange)
            {
                // Notify listeners that target is no longer in range upon disabling
                OnTargetInRangeStatusChanged?.Invoke(false);
                _wasTargetInActionRange = false;
            }
            // Optional: Stop agent and reset animation on disable
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
                _agent.isStopped = true;
            }
            if(characterAnimator != null)
            {
                characterAnimator.SetMovementSpeed(0f);
            }
        }
    }
}