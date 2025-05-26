using System;
using UnityEngine;
using System.Collections;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace AI.FSM.NPC.States
{
    public class IdleState : MonoBehaviour, IState
    {
        [Tooltip("Min time NPC will remain idle")]
        [SerializeField] private float minIdleTime = 2f;
        [Tooltip("Max time NPC will remain idle")]
        [SerializeField] private float maxIdleTime = 5f;
        [Tooltip("Chance of transitioning to patrol instead of wander")]
        [Range(0, 1)]
        [SerializeField] private float patrolChance = 0.7f;
        
        private FSM.StateMachineNew _stateMachineNew;
        private NavMeshAgent agent;
        private float idleTimer;
        private float currentIdleTime;
        private bool isPlayerInArea = false;
        private Coroutine lookAroundCoroutine;

        void Awake()
        {
            _stateMachineNew = GetComponent<FSM.StateMachineNew>();
            if (_stateMachineNew == null) Debug.LogError($"[IdleState - {gameObject.name}] : NPCStateMachine not found.");
            
            agent = GetComponent<NavMeshAgent>();
            if (agent == null) Debug.LogError($"[IdleState - {gameObject.name}] : No NavMesh Agent found.");
        }

        public void OnStateEnter()
        {
            Debug.Log("IdleState: Enter");
            
            // Stop the agent when entering idle state
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            OnNpcIdle?.Invoke();
            
            // Pick a random idle time
            currentIdleTime = Random.Range(minIdleTime, maxIdleTime);
            idleTimer = 0;
            
            // Start looking around occasionally
            if (lookAroundCoroutine != null)
                StopCoroutine(lookAroundCoroutine);
                
            lookAroundCoroutine = StartCoroutine(LookAround());
        }

        public void OnStateUpdate(float deltaTime)
        {
            // Check if player became visible
            if (_stateMachineNew.IsPlayerVisible())
            {
                Debug.Log("IdleState: Player spotted, switching to Chase");
                IState chase = _stateMachineNew.states.Find(s => s.GetType() == typeof(ChaseState));
                if (chase != null) _stateMachineNew.SwitchState(chase);
                return;
            }

            // Update idle timer
            idleTimer += deltaTime;
            
            // If idle time is up, transition to patrol or wander
            if (idleTimer >= currentIdleTime)
            {
                TransitionToNextState();
            }
        }

        public void OnStateExit()
        {
            Debug.Log("IdleState: Exit");
            
            // Stop the look around coroutine if it's running
            if (lookAroundCoroutine != null)
            {
                StopCoroutine(lookAroundCoroutine);
                lookAroundCoroutine = null;
            }
        }
        
        private void TransitionToNextState()
        {
            // Randomly decide between patrol and wander based on patrolChance
            if (Random.value < patrolChance)
            {
                // Transition to patrol
                IState patrol = _stateMachineNew.states.Find(s => s.GetType() == typeof(PatrolState));
                if (patrol != null) 
                {
                    Debug.Log("IdleState: Transitioning to Patrol");
                    _stateMachineNew.SwitchState(patrol);
                }
                else
                {
                    // Fallback to wander if patrol doesn't exist
                    IState wander = _stateMachineNew.states.Find(s => s.GetType() == typeof(WanderState));
                    if (wander != null)
                    {
                        Debug.Log("IdleState: Patrol not found, transitioning to Wander");
                        _stateMachineNew.SwitchState(wander);
                    }
                }
            }
            else
            {
                // Transition to wander
                IState wander = _stateMachineNew.states.Find(s => s.GetType() == typeof(WanderState));
                if (wander != null)
                {
                    Debug.Log("IdleState: Transitioning to Wander");
                    _stateMachineNew.SwitchState(wander);
                }
                else
                {
                    // Fallback to patrol if wander doesn't exist
                    IState patrol = _stateMachineNew.states.Find(s => s.GetType() == typeof(PatrolState));
                    if (patrol != null)
                    {
                        Debug.Log("IdleState: Wander not found, transitioning to Patrol");
                        _stateMachineNew.SwitchState(patrol);
                    }
                }
            }
        }

        private IEnumerator LookAround()
        {
            while (true)
            {
                // Wait a short time before looking around
                yield return new WaitForSeconds(Random.Range(1f, 3f));
                
                // Randomly select a look direction
                float randomAngle = Random.Range(-120f, 120f);
                Vector3 lookDir = Quaternion.Euler(0, randomAngle, 0) * transform.forward;
                
                // Smoothly rotate to look in that direction
                float rotationSpeed = 1.0f;
                Quaternion targetRotation = Quaternion.LookRotation(lookDir);
                float rotationTime = 0;
                float rotationDuration = Random.Range(0.5f, 1.5f);
                
                while (rotationTime < rotationDuration)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationTime / rotationDuration);
                    rotationTime += Time.deltaTime * rotationSpeed;
                    yield return null;
                }
                
                // Wait a bit in the new direction
                yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
            }
        }

        public event Action OnNpcIdle;
    }
}