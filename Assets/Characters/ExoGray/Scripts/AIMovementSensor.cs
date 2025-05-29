// using System;
// using Animation.AnimControllers;
// using UnityEngine;
// using UnityEngine.AI;
//
// // Required for Action delegate (events)
//
// namespace Characters.ExoGray.Scripts
// {
//     [RequireComponent(typeof(NavMeshAgent))]
//     public class AIMovementSensor : MonoBehaviour
//     {
//         [Header("Targeting")] [Tooltip("The primary target for sensing proximity and facing. Can be null.")]
//         public Transform currentTarget; // States can set this
//
//         [Header("Sensing")]
//         [Tooltip("The distance threshold considered 'in range' for actions like attacking.")]
//         [SerializeField]
//         private float actionRange = 2.0f;
//
//         [Header("Component References")]
//         [Tooltip("Reference to the CharacterAnimator component for speed updates.")]
//         [SerializeField]
//         private CharacterAnimator characterAnimator;
//
//         private NavMeshAgent _agent;
//         private bool _wasTargetInActionRange;
//
//         // Public property to check range status externally if needed
//         public bool IsTargetInActionRange { get; private set; }
//
//         private void Awake()
//         {
//             _agent = GetComponent<NavMeshAgent>();
//
//             if (characterAnimator == null)
//             {
//                 Debug.LogWarning($"{GetType().Name}: CharacterAnimator not set. Attempting to get it.", this);
//                 characterAnimator = GetComponent<CharacterAnimator>();
//                 if (characterAnimator == null)
//                     Debug.LogError(
//                         $"{GetType().Name}: CharacterAnimator component not found. Animation speed updates will fail.",
//                         this);
//                 // Not disabling the whole script, as sensing might still be useful
//             }
//         }
//
//         private void Update()
//         {
//             // Update Animator with current speed
//             if (characterAnimator != null && _agent != null && _agent.isOnNavMesh)
//                 characterAnimator.SetMovementSpeed(_agent.velocity.magnitude);
//
//             // Proximity Sensing Logic
//             if (currentTarget != null)
//             {
//                 var distance = Vector3.Distance(transform.position, currentTarget.position);
//                 IsTargetInActionRange = distance <= actionRange;
//
//                 if (IsTargetInActionRange != _wasTargetInActionRange)
//                 {
//                     OnTargetInRangeStatusChanged?.Invoke(IsTargetInActionRange);
//                     _wasTargetInActionRange = IsTargetInActionRange;
//                 }
//             }
//             else // No target
//             {
//                 IsTargetInActionRange = false;
//                 if (IsTargetInActionRange != _wasTargetInActionRange) // If it *was* in range and target became null
//                 {
//                     OnTargetInRangeStatusChanged?.Invoke(false);
//                     _wasTargetInActionRange = false;
//                 }
//             }
//         }
//
//         private void OnDisable()
//         {
//             // Reset animation speed
//             if (characterAnimator != null) characterAnimator.SetMovementSpeed(0f);
//
//             // If target was in range, notify it's no longer (or if target exists)
//             if (_wasTargetInActionRange || (currentTarget != null && IsTargetInActionRange))
//                 OnTargetInRangeStatusChanged?.Invoke(false);
//             _wasTargetInActionRange = false;
//             IsTargetInActionRange = false;
//         }
//
//         // Event fired when the currentTarget enters or leaves the 'actionRange'.
//         public event Action<bool> OnTargetInRangeStatusChanged;
//
//         /// <summary>
//         ///     Rotates the NPC to face a target on the horizontal plane with optional speed and smoothing.
//         /// </summary>
//         /// <param name="target">The target to face.</param>
//         /// <param name="rotationSpeed">Override for rotation speed. If negative, uses NavMeshAgent.angularSpeed.</param>
//         /// <param name="snapThreshold">Minimum angle before snapping (in degrees). Set 0 to disable snapping.</param>
//         /// <param name="useSmoothDamp">Whether to use SmoothDamp-like interpolation for more natural ease-in/out.</param>
//         public void FaceTarget(Transform target, float rotationSpeed = -1f, float snapThreshold = 1f,
//             bool useSmoothDamp = false)
//         {
//             if (target == null || _agent == null) return;
//
//             var direction = target.position - transform.position;
//             direction.y = 0f;
//
//             if (direction == Vector3.zero) return;
//
//             var targetRotation = Quaternion.LookRotation(direction.normalized);
//             var angle = Quaternion.Angle(transform.rotation, targetRotation);
//
//             // Snap if within threshold
//             if (snapThreshold > 0f && angle < snapThreshold)
//             {
//                 transform.rotation = targetRotation;
//                 return;
//             }
//
//             var effectiveSpeed = rotationSpeed > 0f ? rotationSpeed : _agent.angularSpeed * Mathf.Deg2Rad;
//
//             if (_agent.updateRotation)
//                 // Agent is handling rotation, but we're overriding it manually here
//                 _agent.updateRotation = false;
//
//             if (useSmoothDamp)
//                 transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
//                     effectiveSpeed * Time.deltaTime * angle);
//             else
//                 transform.rotation =
//                     Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * effectiveSpeed);
//         }
//
//
//         /// <summary>
//         ///     Sets the primary target for this sensor. Called by FSM states.
//         /// </summary>
//         public void SetTarget(Transform newTarget)
//         {
//             if (currentTarget != newTarget)
//             {
//                 currentTarget = newTarget;
//                 // Reset range status immediately as target changed
//                 _wasTargetInActionRange = false; // Force re-evaluation in Update
//                 IsTargetInActionRange = false; // To avoid stale data before next Update
//                 if (currentTarget == null) OnTargetInRangeStatusChanged?.Invoke(false); // Notify if target removed
//                 // Immediate check and event fire could also happen here if needed
//             }
//         }
//
//
//         /// <summary>
//         ///     Simple method to move to target position.
//         ///     Call this from your state machine to move to the target position.
//         /// </summary>
//         public void MoveToTarget()
//         {
//             if (currentTarget == null || _agent == null) return;
//
//             _agent.SetDestination(currentTarget.position);
//         }
//     }
// }