using System;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Random = UnityEngine.Random;

namespace AI.FSM.NPC.States
{
    public class WanderState : MonoBehaviour, IState, IInitializableState
    {
        [Tooltip("Speed at which NPC moves while wandering")]
        [SerializeField] private float npcSpeed = 2f;
        [Tooltip("Radius around starting position to wander")]
        [SerializeField] private float wanderRadius = 10f;
        [Tooltip("Min time between picking new wander targets")]
        [SerializeField] private float minWanderTime = 5f;
        [Tooltip("Max time between picking new wander targets")]
        [SerializeField] private float maxWanderTime = 12f;
        [Tooltip("Time to wait when reaching a wander point")]
        [SerializeField] private float waitTimeAtPoint = 3f;
        [Tooltip("Chance to go idle after a wander cycle")]
        [Range(0, 1)]
        [SerializeField] private float idleChanceAfterWander = 0.4f;
        [Tooltip("Max tries to find valid wander point")]
        [SerializeField] private int maxPositionAttempts = 5;

        private StateMachineNew _ownerStateMachine;
        private NavMeshAgent agent;
        private Vector3 startingPosition;
        private float wanderTimer;
        private float currentWanderTime;
        private bool isWaiting = false;
        private float waitTimer = 0f;
        private Coroutine wanderCoroutine;

        void Awake()
        {
            _ownerStateMachine = GetComponent<StateMachineNew>();
            if (_ownerStateMachine == null) Debug.LogError($"[WanderState - {gameObject.name}] : NPCStateMachine not found.");
            
            agent = GetComponent<NavMeshAgent>();
            if (agent == null) Debug.LogError($"[WanderState - {gameObject.name}] : No NavMesh Agent found.");
            
            // Store starting position for wander radius reference
            startingPosition = transform.position;
        }

        public void OnStateEnter()
        {
            OnNpcWander?.Invoke();
            
            Debug.Log("WanderState: Enter");
            
            if (agent != null && agent.enabled)
            {
                agent.speed = npcSpeed;
                agent.isStopped = false;
            }
            
            // Update starting position when entering wander state
            startingPosition = transform.position;
            
            // Initialize wander parameters
            currentWanderTime = Random.Range(minWanderTime, maxWanderTime);
            wanderTimer = 0f;
            isWaiting = false;
            waitTimer = 0f;
            
            // Start the wandering process
            FindAndMoveToWanderPoint();
        }

        public void OnStateUpdate(float deltaTime)
        {
            // Check if player became visible
            if (_ownerStateMachine.IsPlayerVisible())
            {
                Debug.Log("WanderState: Player spotted, switching to Chase");
                IState chase = _ownerStateMachine.states.Find(s => s.GetType() == typeof(ChaseState));
                if (chase != null) _ownerStateMachine.SwitchState(chase);
                return;
            }

            if (isWaiting)
            {
                // Update wait timer
                waitTimer += deltaTime;
                
                // Look around occasionally while waiting
                if (waitTimer % 2 < 0.1f)
                {
                    LookRandomDirection();
                }
                
                // Resume wandering after wait time
                if (waitTimer >= waitTimeAtPoint)
                {
                    isWaiting = false;
                    
                    // Decide whether to continue wandering or go idle
                    if (Random.value < idleChanceAfterWander)
                    {
                        Debug.Log("WanderState: Finished waiting, transitioning to Idle");
                        TransitionToIdle();
                    }
                    else
                    {
                        Debug.Log("WanderState: Finished waiting, finding new wander point");
                        FindAndMoveToWanderPoint();
                    }
                }
            }
            else
            {
                // Update wander timer
                wanderTimer += deltaTime;
                
                // Check if we've reached destination
                if (agent != null && agent.enabled && !agent.pathPending && 
                    agent.remainingDistance <= agent.stoppingDistance)
                {
                    Debug.Log("WanderState: Reached wander point, waiting");
                    isWaiting = true;
                    waitTimer = 0f;
                }
                
                // Pick a new destination if current wander time is up
                if (wanderTimer >= currentWanderTime)
                {
                    Debug.Log("WanderState: Wander time up, finding new wander point");
                    FindAndMoveToWanderPoint();
                }
            }
        }

        public void OnStateExit()
        {
            Debug.Log("WanderState: Exit");
            
            // Clean up any ongoing coroutines
            if (wanderCoroutine != null)
            {
                StopCoroutine(wanderCoroutine);
                wanderCoroutine = null;
            }
        }

        public void Initialize(StateMachineNew ownerMachineNew)
        {
            _ownerStateMachine = ownerMachineNew;
            if (_ownerStateMachine == null)
            {
                Debug.LogError($"[{gameObject.name}] WanderState: Owner StateMachine is null in Initialize!", this);
                enabled = false; return;
            }
        }



        private void FindAndMoveToWanderPoint()
        {
            if (agent == null || !agent.enabled) return;
            
            wanderTimer = 0f;
            currentWanderTime = Random.Range(minWanderTime, maxWanderTime);
            
            // Try to find a valid wander position
            Vector3 newWanderPoint = FindRandomWanderPoint();
            
            // Set destination to the new wander point
            agent.SetDestination(newWanderPoint);
            Debug.Log($"WanderState: Moving to new wander point {newWanderPoint}");
        }
        
        private Vector3 FindRandomWanderPoint()
        {
            Vector3 finalPosition = transform.position;
            bool foundPosition = false;
            int attempts = 0;
            
            while (!foundPosition && attempts < maxPositionAttempts)
            {
                // Get random direction from NPC position
                Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
                randomDirection += startingPosition;
                randomDirection.y = transform.position.y; // Keep on same y-level
                
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
                {
                    // Check if point is within wander radius of starting position
                    if (Vector3.Distance(hit.position, startingPosition) <= wanderRadius)
                    {
                        finalPosition = hit.position;
                        foundPosition = true;
                    }
                }
                
                attempts++;
            }
            
            return finalPosition;
        }
        
        private void LookRandomDirection()
        {
            // Look in a random direction within 180 degrees
            float randomAngle = Random.Range(-90f, 90f);
            Vector3 lookDir = Quaternion.Euler(0, randomAngle, 0) * transform.forward;
            
            // Gradually rotate to the new direction
            StartCoroutine(RotateToDirection(lookDir));
        }
        
        private IEnumerator RotateToDirection(Vector3 direction)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            float rotationSpeed = 2.0f;
            float rotationTime = 0;
            float rotationDuration = 0.5f;
            
            while (rotationTime < rotationDuration)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationTime / rotationDuration);
                rotationTime += Time.deltaTime * rotationSpeed;
                yield return null;
            }
        }
        
        private void TransitionToIdle()
        {
            IState idle = _ownerStateMachine.states.Find(s => s.GetType() == typeof(IdleState));
            if (idle != null)
            {
                _ownerStateMachine.SwitchState(idle);
            }
        }
        
        // Optional: Helper function for editor to visualize wander radius
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(startingPosition, wanderRadius);
            }
            else
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, wanderRadius);
            }
        }

        public event Action OnNpcWander;
    }
}