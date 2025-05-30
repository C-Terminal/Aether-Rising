using AI.FSM.NPC;
using AI.FSM.NPC.States;
using Animation.AnimControllers;
using UnityEngine;
using UnityEngine.AI;

namespace AI.FSM.Warrior.States
{
    public class W_CirclingState : MonoBehaviour, IState
    {
        private StateMachineNew _stateMachineNew;
        private NavMeshAgent agent;
        private CharacterAnimator charAnim;
        private Transform player;

        [Header("Circling Parameters")]
        [SerializeField] private float circlingSpeed = 2.0f;
        [SerializeField] private float desiredCirclingRadius = 5.0f;
        [SerializeField] private float radiusTolerance = 1.0f;
        [SerializeField] private float strafePointRecalculateTime = 2.0f;
        [SerializeField] private float maxEngagementDistance = 15f;
        [SerializeField] private float minCirclingRadius = 3.0f;
        [SerializeField] private float maxCirclingRadius = 8.0f;

        [Header("Behavioral Parameters")]
        [SerializeField] private float playerLostSightTimeout = 5.0f; // How long to circle if player is not visible
        [SerializeField] private float directionChangeChance = 0.3f; // Chance to change direction each recalculation
        [SerializeField] private bool enableAdaptiveRadius = true; // Adjust radius based on circumstances

        private float _currentStrafeTimer;
        private float _playerLostSightTimer;
        private Vector3 _currentTargetStrafePoint;
        private int _currentStrafeDirection = 1;
        private bool _wasPlayerVisibleLastFrame = true;
        private Vector3 _lastKnownPlayerPosition;
        private int _consecutiveNavMeshFailures = 0;
        private const int MAX_NAVMESH_FAILURES = 3;

        // Events for state communication
        public System.Action OnCirclingStarted;
        public System.Action OnCirclingEnded;
        public System.Action OnLostPlayerWhileCircling;

        void Awake()
        {
            _stateMachineNew = GetComponent<StateMachineNew>();
            if (_stateMachineNew == null) 
                Debug.LogError($"[{gameObject.name}] W_CirclingState: StateMachine not found!");
        }

        public void OnStateEnter()
        {
            // Cache components
            agent = _stateMachineNew.Agent;
            charAnim = _stateMachineNew.CharAnim;
            player = _stateMachineNew.Player;

            if (!ValidateComponents())
            {
                Debug.LogError($"[{gameObject.name}] W_CirclingState: Critical components missing. Switching to safe state.");
                _stateMachineNew.SwitchState(_stateMachineNew.FindState<IdleState>());
                return;
            }

            Debug.Log($"[{_stateMachineNew.gameObject.name}] Entering CirclingState.");

            InitializeCirclingBehavior();
            OnCirclingStarted?.Invoke();
        }

        public void OnStateUpdate(float deltaTime)
        {
            if (!ValidateComponents()) return;

            // Always try to face the player (or last known position)
            Vector3 targetPosition = _wasPlayerVisibleLastFrame ? player.position : _lastKnownPlayerPosition;
            _stateMachineNew.RotateTowardPosition(targetPosition);

            // Check player visibility
            HandlePlayerVisibility(deltaTime);

            // Check for state transitions
            if (ShouldTransitionToAttack()) return;
            if (ShouldTransitionToChase()) return;
            if (ShouldTransitionToSearch()) return;

            // Handle circling movement
            UpdateCirclingMovement(deltaTime);

            // Update animations
            UpdateAnimations();
        }

        public void OnStateExit()
        {
            Debug.Log($"[{_stateMachineNew.gameObject.name}] Exiting CirclingState.");
            
            if (agent != null && agent.enabled)
            {
                agent.ResetPath();
            }

            // Reset timers and flags
            _currentStrafeTimer = 0f;
            _playerLostSightTimer = 0f;
            _consecutiveNavMeshFailures = 0;
            
            OnCirclingEnded?.Invoke();
        }

        private bool ValidateComponents()
        {
            return agent != null && charAnim != null && player != null && agent.enabled;
        }

        private void InitializeCirclingBehavior()
        {
            agent.speed = circlingSpeed;
            agent.isStopped = false;
            agent.stoppingDistance = 0.5f;

            // Intelligent direction selection
            DetermineInitialStrafeDirection();

            // Reset timers
            _currentStrafeTimer = 0f;
            _playerLostSightTimer = 0f;
            _wasPlayerVisibleLastFrame = true;
            _lastKnownPlayerPosition = player.position;
            _consecutiveNavMeshFailures = 0;

            // Calculate first strafe point
            CalculateAndSetNewStrafeDestination();

            // Set initial animations
            charAnim.SetMovementSpeed(circlingSpeed);
            //TODO: consider making aiming into a single frame and use
            // charAnim.SetAiming(true);
        }

        private void DetermineInitialStrafeDirection()
        {
            // More intelligent direction selection based on positioning
            Vector3 toPlayer = (player.position - transform.position).normalized;
            Vector3 npcRight = transform.right;
            
            // Consider nearby NPCs to avoid clustering
            if (NPCManager.Instance != null)
            {
                var nearbyNPCs = NPCManager.Instance.NPCsInRange;
                if (nearbyNPCs.Count > 1)
                {
                    // Spread out from other NPCs
                    Vector3 avgNPCPosition = Vector3.zero;
                    foreach (var npc in nearbyNPCs)
                    {
                        if (npc != _stateMachineNew)
                            avgNPCPosition += npc.transform.position;
                    }
                    avgNPCPosition /= (nearbyNPCs.Count - 1);
                    
                    Vector3 awayFromNPCs = (transform.position - avgNPCPosition).normalized;
                    _currentStrafeDirection = Vector3.Dot(transform.right, awayFromNPCs) > 0 ? 1 : -1;
                }
                else
                {
                    // Default behavior: strafe perpendicular to player direction
                    _currentStrafeDirection = (Vector3.Dot(npcRight, toPlayer) > 0) ? -1 : 1;
                }
            }
            else
            {
                _currentStrafeDirection = Random.value > 0.5f ? 1 : -1;
            }
        }

        private void HandlePlayerVisibility(float deltaTime)
        {
            bool canSeePlayer = _stateMachineNew.IsPlayerVisible();
            
            if (canSeePlayer)
            {
                _wasPlayerVisibleLastFrame = true;
                _lastKnownPlayerPosition = player.position;
                _playerLostSightTimer = 0f;
            }
            else
            {
                if (_wasPlayerVisibleLastFrame)
                {
                    Debug.Log($"[{_stateMachineNew.gameObject.name}] CirclingState: Lost sight of player.");
                    OnLostPlayerWhileCircling?.Invoke();
                }
                
                _wasPlayerVisibleLastFrame = false;
                _playerLostSightTimer += deltaTime;
            }
        }

        private bool ShouldTransitionToAttack()
        {
            // Check for attack opportunity
            if (NPCManager.Instance != null && NPCManager.Instance.GetAttackingNPC() == _stateMachineNew)
            {
                Debug.Log($"[{_stateMachineNew.gameObject.name}] CirclingState: My turn to attack! Switching to PrepareAttackState.");
                _stateMachineNew.SwitchState(_stateMachineNew.FindState<W_PrepareAttackState>());
                return true;
            }

            // Check if player is close enough for immediate attack (overrides queue)
            if (_wasPlayerVisibleLastFrame && _stateMachineNew.IsPlayerAttackable())
            {
                float distanceToPlayer = Vector3.Distance(transform.position, player.position);
                if (distanceToPlayer < desiredCirclingRadius * 0.7f) // Very close
                {
                    if (NPCManager.Instance.RequestAttackPermission(_stateMachineNew as WarriorStateMachine))
                    {
                        Debug.Log($"[{_stateMachineNew.gameObject.name}] CirclingState: Player very close, attacking immediately!");
                        _stateMachineNew.SwitchState(_stateMachineNew.FindState<W_PrepareAttackState>());
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ShouldTransitionToChase()
        {
            if (!_wasPlayerVisibleLastFrame) return false;

            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer > maxEngagementDistance)
            {
                Debug.Log($"[{_stateMachineNew.gameObject.name}] CirclingState: Player too far ({distanceToPlayer:F1}m). Switching to ChaseState.");
                // charAnim.SetAiming(false);
                _stateMachineNew.SwitchState(_stateMachineNew.FindState<ChaseState>());
                return true;
            }

            return false;
        }

        private bool ShouldTransitionToSearch()
        {
            // If lost sight of player for too long, switch to search
            if (_playerLostSightTimer > playerLostSightTimeout)
            {
                Debug.Log($"[{_stateMachineNew.gameObject.name}] CirclingState: Lost player for {playerLostSightTimeout}s. Switching to SearchState.");
                var searchState = _stateMachineNew.FindState<IdleState>();
                if (searchState != null)
                {
                    _stateMachineNew.SwitchState(searchState);
                    return true;
                }
            }

            return false;
        }

        private void UpdateCirclingMovement(float deltaTime)
        {
            _currentStrafeTimer += deltaTime;

            // Check if we need to recalculate destination
            bool shouldRecalculate = _currentStrafeTimer >= strafePointRecalculateTime ||
                                   (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance) ||
                                   !agent.hasPath ||
                                   _consecutiveNavMeshFailures >= MAX_NAVMESH_FAILURES;

            if (shouldRecalculate)
            {
                CalculateAndSetNewStrafeDestination();
            }

            // Handle stuck situations
            if (agent.velocity.magnitude < 0.1f && !agent.pathPending && agent.hasPath)
            {
                // Might be stuck, try a new destination
                CalculateAndSetNewStrafeDestination();
            }
        }

        private void UpdateAnimations()
        {
            float currentSpeed = agent.velocity.magnitude;
            charAnim.SetMovementSpeed(currentSpeed > 0.1f ? currentSpeed : 0f);
            // charAnim.SetAiming(_wasPlayerVisibleLastFrame);
        }

        private void CalculateAndSetNewStrafeDestination()
        {
            if (player == null || agent == null) return;

            _currentStrafeTimer = 0f;

            // Randomly change direction sometimes for unpredictability
            if (Random.value < directionChangeChance)
            {
                _currentStrafeDirection *= -1;
                Debug.Log($"[{_stateMachineNew.gameObject.name}] CirclingState: Changing strafe direction.");
            }

            // Use last known player position if not visible
            Vector3 referencePosition = _wasPlayerVisibleLastFrame ? player.position : _lastKnownPlayerPosition;
            
            // Calculate adaptive radius
            float currentRadius = CalculateAdaptiveRadius();
            
            // Calculate strafe point
            Vector3 desiredPoint = CalculateStrafePoint(referencePosition, currentRadius);

            // Try to find valid NavMesh position
            if (TrySetStrafeDestination(desiredPoint))
            {
                _consecutiveNavMeshFailures = 0;
            }
            else
            {
                HandleNavMeshFailure();
            }
        }

        private float CalculateAdaptiveRadius()
        {
            if (!enableAdaptiveRadius) return desiredCirclingRadius;

            float baseRadius = desiredCirclingRadius;
            
            // Adjust based on nearby NPCs
            if (NPCManager.Instance != null)
            {
                var nearbyNPCs = NPCManager.Instance.NPCsInRange;
                if (nearbyNPCs.Count > 2) // Too crowded, increase radius
                {
                    baseRadius = Mathf.Min(maxCirclingRadius, baseRadius + 1f);
                }
                else if (nearbyNPCs.Count == 1) // Just us, can get closer
                {
                    baseRadius = Mathf.Max(minCirclingRadius, baseRadius - 0.5f);
                }
            }

            return baseRadius;
        }

        private Vector3 CalculateStrafePoint(Vector3 referencePosition, float radius)
        {
            // More sophisticated point calculation
            float angleOffset = Random.Range(30f, 60f) * _currentStrafeDirection;
            Vector3 directionToPlayer = (transform.position - referencePosition).normalized;
            Vector3 pointOnCircleDirection = Quaternion.Euler(0, angleOffset, 0) * directionToPlayer;
            
            return referencePosition + pointOnCircleDirection * radius;
        }

        private bool TrySetStrafeDestination(Vector3 desiredPoint)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(desiredPoint, out hit, radiusTolerance * 2, NavMesh.AllAreas))
            {
                _currentTargetStrafePoint = hit.position;
                agent.SetDestination(_currentTargetStrafePoint);
                return true;
            }
            return false;
        }

        private void HandleNavMeshFailure()
        {
            _consecutiveNavMeshFailures++;
            Debug.LogWarning($"[{_stateMachineNew.gameObject.name}] CirclingState: NavMesh failure #{_consecutiveNavMeshFailures}");

            // Try alternative strategies
            if (_consecutiveNavMeshFailures < MAX_NAVMESH_FAILURES)
            {
                // Try opposite direction
                _currentStrafeDirection *= -1;
                
                // Try a fallback position closer to current location
                Vector3 fallbackDir = (Quaternion.Euler(0, 90 * _currentStrafeDirection, 0) * transform.forward).normalized;
                Vector3 fallbackPoint = transform.position + fallbackDir * 2f;
                
                if (TrySetStrafeDestination(fallbackPoint))
                {
                    _consecutiveNavMeshFailures = 0;
                }
            }
            else
            {
                // Too many failures, maybe transition to different state
                Debug.LogError($"[{_stateMachineNew.gameObject.name}] CirclingState: Too many NavMesh failures. Considering state change.");
                
                // Could transition to ChaseState or SearchState as fallback
                var chaseState = _stateMachineNew.FindState<ChaseState>();
                if (chaseState != null)
                {
                    _stateMachineNew.SwitchState(chaseState);
                }
            }
        }

        // Public methods for external control
        public void ForceDirectionChange()
        {
            _currentStrafeDirection *= -1;
            CalculateAndSetNewStrafeDestination();
        }

        public void SetCirclingRadius(float newRadius)
        {
            desiredCirclingRadius = Mathf.Clamp(newRadius, minCirclingRadius, maxCirclingRadius);
        }

        // Debug properties
        public Vector3 CurrentTargetPoint => _currentTargetStrafePoint;
        public int CurrentDirection => _currentStrafeDirection;
        public bool IsPlayerVisible => _wasPlayerVisibleLastFrame;
        public float TimeWithoutSight => _playerLostSightTimer;
    }
}