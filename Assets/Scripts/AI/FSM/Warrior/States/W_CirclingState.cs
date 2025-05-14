using AI.FSM.NPC;
using AI.FSM.NPC.States;
using Animation.AnimControllers;
using UnityEngine;
using UnityEngine.AI;

// Explicit for Random.Range

namespace AI.FSM.Warrior.States // Assuming your namespace
{
    public class W_CirclingState : MonoBehaviour, IState
    {
        private WarriorStateMachine stateMachine;
        private NavMeshAgent agent;
        private CharacterAnimator charAnim; // From WarriorStateMachine
        private Transform player;       // From WarriorStateMachine

        [Header("Circling Parameters")]
        [SerializeField] private float circlingSpeed = 2.0f;
        [SerializeField] private float desiredCirclingRadius = 5.0f;
        [SerializeField] private float radiusTolerance = 1.0f; // How much deviation from desiredRadius is acceptable
        [SerializeField] private float strafePointRecalculateTime = 2.0f; // How often to pick a new point on the circle
        [SerializeField] private float maxEngagementDistance = 15f; // If player is further than this, chase

        private float _currentStrafeTimer;
        private Vector3 _currentTargetStrafePoint;
        private int _currentStrafeDirection = 1; // 1 for clockwise (e.g., player's right), -1 for counter-clockwise

        // From WarriorStateMachine to use its events if needed (conceptual)
        // public event System.Action OnNpcCircle; 


        void Awake()
        {
            stateMachine = GetComponent<WarriorStateMachine>();
            if (stateMachine == null) Debug.LogError($"[{gameObject.name}] W_CirclingState: WarriorStateMachine not found!");
        }

        public void OnStateEnter()
        {
            // Cache components from stateMachine for convenience (they should be initialized in WarriorStateMachine.Awake)
            agent = stateMachine.Agent;
            charAnim = stateMachine.CharAnim;
            player = stateMachine.Player;

            if (agent == null || charAnim == null || player == null)
            {
                Debug.LogError($"[{gameObject.name}] W_CirclingState: Critical component missing from StateMachine. Disabling state behavior.");
                // Potentially switch to a fail-safe state like Idle
                stateMachine.SwitchState(stateMachine.FindState<IdleState>());
                return;
            }

            Debug.Log($"[{stateMachine.gameObject.name}] Entering CirclingState.");

            agent.speed = circlingSpeed;
            agent.isStopped = false;
            agent.stoppingDistance = 0.5f; // Stop close to the strafe point

            // Decide initial strafe direction (e.g., based on which side of player NPC is, or random)
            Vector3 toPlayer = (player.position - transform.position).normalized;
            Vector3 npcRight = transform.right;
            _currentStrafeDirection = (Vector3.Dot(npcRight, toPlayer) > 0) ? -1 : 1; // Simple heuristic: strafe away from player initially
            // Or simply Random.value > 0.5f ? 1 : -1;

            _currentStrafeTimer = 0f; // Calculate first point immediately
            CalculateAndSetNewStrafeDestination();

            // Animation: Indicate moving at circling speed.
            // Might also set a bool like "IsStrafing" if you have specific strafe animations.
            charAnim.SetMovementSpeed(circlingSpeed); // Or a normalized value if your blend tree expects that
            charAnim.SetAiming(true); // Usually NPCs aim/are combat-ready while circling
        }

        public void OnStateUpdate(float deltaTime)
        {
            if (player == null || agent == null || !agent.enabled) return; // Safety check

            stateMachine.RotateToFacePlayer(); // Always face the player

            // 1. Check for attack opportunity (PRIMARY exit condition for action)
            if (NPCManager.Instance != null && NPCManager.Instance.GetAttackingNPC() == stateMachine)
            {
                Debug.Log($"[{stateMachine.gameObject.name}] CirclingState: My turn to attack! Switching to PrepareAttackState.");
                stateMachine.SwitchState(stateMachine.FindState<W_PrepareAttackState>());
                return;
            }

            // 2. Check if player is too far, then chase
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer > maxEngagementDistance)
            {
                Debug.Log($"[{stateMachine.gameObject.name}] CirclingState: Player too far ({distanceToPlayer}m). Switching to ChaseState.");
                charAnim.SetAiming(false); // Stop aiming if chasing
                stateMachine.SwitchState(stateMachine.FindState<ChaseState>());
                return;
            }

            // 3. Manage circling movement
            _currentStrafeTimer += deltaTime;

            // If reached destination OR timer is up to pick a new point OR path is invalid
            if (_currentStrafeTimer >= strafePointRecalculateTime ||
                (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance) ||
                !agent.hasPath)
            {
                CalculateAndSetNewStrafeDestination();
            }

            // Ensure animation speed is set correctly (in case it was changed by another system)
            charAnim.SetMovementSpeed(agent.velocity.magnitude > 0.1f ? circlingSpeed : 0f);
            charAnim.SetAiming(true); // Keep aiming
        }

        public void OnStateExit()
        {
            Debug.Log($"[{stateMachine.gameObject.name}] Exiting CirclingState.");
            if (agent != null && agent.enabled)
            {
                // agent.isStopped = true; // Next state will control this
                // agent.ResetPath(); // Usually good practice
            }
            // charAnim.SetAiming(false); // Next state (like PrepareAttack or Chase) will set this.
        }

        private void CalculateAndSetNewStrafeDestination()
        {
            if (player == null || agent == null) return;

            _currentStrafeTimer = 0f; // Reset timer

            // Calculate a point on a circle around the player
            // The angle determines how "wide" the strafe step is.
            // More sophisticated logic could try to find tactically advantageous spots.
            float angleOffset = 45.0f * _currentStrafeDirection; // Strafe about 45 degrees each time
            Vector3 directionToPlayer = (transform.position - player.position).normalized;
            Vector3 pointOnCircleDirection = Quaternion.Euler(0, angleOffset, 0) * directionToPlayer;
            
            Vector3 desiredPoint = player.position + pointOnCircleDirection * desiredCirclingRadius;

            // Try to find a valid NavMesh point near the desired circling point
            NavMeshHit hit;
            if (NavMesh.SamplePosition(desiredPoint, out hit, radiusTolerance * 2, NavMesh.AllAreas))
            {
                _currentTargetStrafePoint = hit.position;
                agent.SetDestination(_currentTargetStrafePoint);
                // Debug.Log($"[{stateMachine.gameObject.name}] Circling: New target point {_currentTargetStrafePoint}");
            }
            else
            {
                // Could not find a point, maybe try reversing direction or a smaller step
                // For simplicity, we just log it. NPC might pause until next recalculation.
                // Debug.LogWarning($"[{stateMachine.gameObject.name}] Circling: Could not find NavMesh sample position near {desiredPoint}. Trying opposite direction.");
                _currentStrafeDirection *= -1; // Try other direction next time
                 // Optionally, try a point closer to current position but still lateral
                Vector3 fallbackDir = (Quaternion.Euler(0, 90 * _currentStrafeDirection,0) * transform.forward).normalized;
                if(NavMesh.SamplePosition(transform.position + fallbackDir * 2f, out hit, 2f, NavMesh.AllAreas))
                {
                    _currentTargetStrafePoint = hit.position;
                    agent.SetDestination(_currentTargetStrafePoint);
                } else {
                    // agent.isStopped = true; // If truly stuck
                }
            }
        }
    }
}