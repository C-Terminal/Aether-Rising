using System;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace AI.FSM.NPC.States
{
    public class PatrolState : MonoBehaviour, IState
    {
        [Tooltip("Array of patrol points the NPC will move between")]
        [SerializeField] private Transform[] wayPoints;
        [Tooltip("Speed at which NavMeshAgent moves while patrolling")]
        [SerializeField] private float npcSpeed = 2.5f;
        [Tooltip("How close NPC needs to get to waypoint (in meters)")]
        [SerializeField] private float waypointTolerance = 1.0f;
        [Tooltip("Time to wait at each waypoint")]
        [SerializeField] private float waitTimeAtWaypoint = 2.0f;
        [Tooltip("Chance to go idle after completing a patrol circuit")]
        [Range(0, 1)]
        [SerializeField] private float idleChanceAfterCircuit = 0.3f;

        private NPCStateMachine stateMachine;
        private NavMeshAgent agent;
        private int currentWaypointIndex = 0;
        private float waitTimer = 0f;
        private bool isWaiting = false;
        private bool patrolCircuitCompleted = false;

        void Awake()
        {
            stateMachine = GetComponent<NPCStateMachine>();
            if (stateMachine == null) Debug.LogError($"[PatrolState - {gameObject.name}] : NPCStateMachine not found.");
            
            agent = GetComponent<NavMeshAgent>();
            if (agent == null) Debug.LogError($"[PatrolState - {gameObject.name}] : No NavMesh Agent found.");
        }

        public void OnStateEnter()
        
        {
            OnNpcPatrol?.Invoke();
            
            Debug.Log("PatrolState: Enter");
            
            if (wayPoints == null || wayPoints.Length == 0)
            {
                Debug.LogWarning($"[PatrolState - {gameObject.name}] : No waypoints assigned. Reverting to Idle.");
                ReturnToIdle();
                return;
            }

            if (agent != null && agent.enabled)
            {
                agent.speed = npcSpeed;
                agent.isStopped = false;
                
                // Set destination to current waypoint
                if (currentWaypointIndex >= wayPoints.Length)
                {
                    currentWaypointIndex = 0;
                }
                
                SetDestinationToCurrentWaypoint();
            }
            
            isWaiting = false;
            waitTimer = 0f;
            patrolCircuitCompleted = false;
        }

        public void OnStateUpdate(float deltaTime)
        {
            // Check if player became visible
            if (stateMachine.IsPlayerVisible())
            {
                Debug.Log("PatrolState: Player spotted, switching to Chase");
                IState chase = stateMachine.states.Find(s => s.GetType() == typeof(ChaseState));
                if (chase != null) stateMachine.SwitchState(chase);
                return;
            }

            if (agent == null || !agent.enabled || wayPoints.Length == 0) return;

            if (isWaiting)
            {
                // Wait at waypoint
                waitTimer += deltaTime;
                if (waitTimer >= waitTimeAtWaypoint)
                {
                    isWaiting = false;
                    
                    // Move to next waypoint
                    currentWaypointIndex = (currentWaypointIndex + 1) % wayPoints.Length;
                    
                    // Check if we've completed a full circuit
                    if (currentWaypointIndex == 0)
                    {
                        patrolCircuitCompleted = true;
                        
                        // Chance to go idle after completing circuit
                        if (Random.value < idleChanceAfterCircuit)
                        {
                            ReturnToIdle();
                            return;
                        }
                    }
                    
                    SetDestinationToCurrentWaypoint();
                }
            }
            else
            {
                // Check if we've reached the waypoint
                if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
                {
                    Debug.Log($"PatrolState: Reached waypoint {currentWaypointIndex}");
                    isWaiting = true;
                    waitTimer = 0f;
                }
            }
        }

        public void OnStateExit()
        {
            Debug.Log("PatrolState: Exit");
            
            // Agent will be controlled by the next state
        }
        
        private void SetDestinationToCurrentWaypoint()
        {
            if (agent != null && agent.enabled && currentWaypointIndex < wayPoints.Length && wayPoints[currentWaypointIndex] != null)
            {
                agent.SetDestination(wayPoints[currentWaypointIndex].position);
                Debug.Log($"PatrolState: Moving to waypoint {currentWaypointIndex}");
            }
            else
            {
                Debug.LogWarning($"[PatrolState - {gameObject.name}] : Invalid waypoint reference at index {currentWaypointIndex}");
            }
        }
        
        private void ReturnToIdle()
        {
            IState idle = stateMachine.states.Find(s => s.GetType() == typeof(IdleState));
            if (idle != null)
            {
                Debug.Log("PatrolState: Returning to Idle");
                stateMachine.SwitchState(idle);
            }
        }
        
        // Optional: Helper function for editor to visualize patrol path
        private void OnDrawGizmosSelected()
        {
            if (wayPoints == null || wayPoints.Length <= 1) return;
            
            Gizmos.color = Color.blue;
            
            // Draw lines between waypoints
            for (int i = 0; i < wayPoints.Length; i++)
            {
                if (wayPoints[i] == null) continue;
                
                // Draw line to next waypoint or back to first waypoint to complete the circuit
                int nextIndex = (i + 1) % wayPoints.Length;
                if (wayPoints[nextIndex] != null)
                {
                    Gizmos.DrawLine(wayPoints[i].position, wayPoints[nextIndex].position);
                }
                
                // Draw sphere at waypoint
                Gizmos.DrawSphere(wayPoints[i].position, 0.3f);
            }
        }

        public event Action OnNpcPatrol;
    }
}