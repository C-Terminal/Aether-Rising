using System.Collections;
using AI.FSM.NPC;
using AI.FSM.NPC.States;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace AI.FSM.Warrior.States
{
    /// <summary>
    /// State where the warrior retreats from the player to reposition or recover
    /// </summary>
    [RequireComponent(typeof(StateMachineNew))]
    public class W_RetreatState : MonoBehaviour, IState
    {
        [Header("Retreat Settings")]
        [Tooltip("Minimum distance to retreat from player")]
        [SerializeField] private float minRetreatDistance = 10f;
        
        [Tooltip("Maximum distance to retreat from player")]
        [SerializeField] private float maxRetreatDistance = 15f;
        
        [Tooltip("Duration in seconds to stay in retreat before re-evaluating")]
        [SerializeField] private float retreatDuration = 4f;
        
        [Tooltip("Chance to strafe while retreating (0-1)")]
        [SerializeField] private float strafeProbability = 0.6f;
        
        [Tooltip("Speed multiplier while retreating")]
        [SerializeField] private float speedMultiplier = 1.2f;

        private StateMachineNew _stateMachineNew;
        private NavMeshAgent agent;
        private Transform player;
        private float originalSpeed;
        private float retreatTimer;
        private bool isRetreating = false;
        private Vector3 retreatPosition;
        private Coroutine retreatCoroutine;

        private void Awake()
        {
            _stateMachineNew = GetComponent<StateMachineNew>();
        }

        public void OnStateEnter()
        {
            Debug.Log($"[W_RetreatState - {gameObject.name}]: Entering retreat state");
            
            // Cache references
            agent = _stateMachineNew.Agent;
            player = _stateMachineNew.Player;
            
            // Cache original speed to restore later
            originalSpeed = agent.speed;
            agent.speed *= speedMultiplier;
            
            // Set animation parameters
            _stateMachineNew.CharAnim.SetLocomotionBlend(1.0f);  // Full speed animation
            _stateMachineNew.CharAnim.SetBackwardMovement(true); // Backward movement animation
            
            // Reset the retreat timer
            retreatTimer = 0f;
            isRetreating = true;
            
            // Start retreating
            retreatCoroutine = StartCoroutine(RetreatBehavior());
        }

        public void OnStateExit()
        {
            Debug.Log($"[W_RetreatState - {gameObject.name}]: Exiting retreat state");
            
            // Reset agent properties
            if (agent != null)
            {
                agent.speed = originalSpeed;
                agent.isStopped = false;
            }
            
            // Reset animation parameters
            _stateMachineNew.CharAnim.SetBackwardMovement(false);
            
            // Stop any ongoing retreat coroutine
            if (retreatCoroutine != null)
            {
                StopCoroutine(retreatCoroutine);
                retreatCoroutine = null;
            }
            
            isRetreating = false;
        }

        public void OnStateUpdate(float deltaTime)
        {
            // Increment retreat timer
            retreatTimer += Time.deltaTime;
            
            // If we're still retreating, occasionally face the player
            if (isRetreating && Random.value > 0.8f)
            {
                _stateMachineNew.RotateToFacePlayer();
            }
            
            // Check if it's time to transition to another state
            CheckTransitions();
        }

        private IEnumerator RetreatBehavior()
        {
            while (isRetreating)
            {
                // Find a retreat position
                if (!agent.hasPath || agent.remainingDistance < 1f)
                {
                    FindRetreatPosition();
                }
                
                // If we should strafe occasionally
                if (Random.value < strafeProbability && retreatTimer > 1f)
                {
                    yield return StartCoroutine(PerformStrafe());
                }
                
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void FindRetreatPosition()
        {
            if (player == null || agent == null) return;
            
            // Calculate direction away from player
            Vector3 directionFromPlayer = transform.position - player.position;
            directionFromPlayer.y = 0; // Keep on the same Y level
            
            if (directionFromPlayer.magnitude < 0.1f)
            {
                // If somehow on top of player, pick a random direction
                directionFromPlayer = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
            }
            else
            {
                directionFromPlayer.Normalize();
            }
            
            // Add some randomness to the retreat direction
            float angleVariation = Random.Range(-45f, 45f);
            directionFromPlayer = Quaternion.Euler(0, angleVariation, 0) * directionFromPlayer;
            
            // Calculate retreat distance
            float retreatDistance = Random.Range(minRetreatDistance, maxRetreatDistance);
            
            // Calculate potential retreat position
            Vector3 potentialRetreatPos = transform.position + directionFromPlayer * retreatDistance;
            
            // Try to find a valid position on the NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(potentialRetreatPos, out hit, retreatDistance, NavMesh.AllAreas))
            {
                retreatPosition = hit.position;
                agent.SetDestination(retreatPosition);
                Debug.Log($"[W_RetreatState - {gameObject.name}]: Retreating to position {retreatPosition}");
            }
            else
            {
                Debug.LogWarning($"[W_RetreatState - {gameObject.name}]: Could not find valid retreat position");
                // As fallback, try a shorter distance
                if (NavMesh.SamplePosition(transform.position + directionFromPlayer * (retreatDistance / 2f), 
                    out hit, retreatDistance / 2f, NavMesh.AllAreas))
                {
                    retreatPosition = hit.position;
                    agent.SetDestination(retreatPosition);
                }
            }
        }

        private IEnumerator PerformStrafe()
        {
            // Save current destination
            Vector3 originalDestination = agent.destination;
            
            // Calculate strafe direction (perpendicular to direction to player)
            Vector3 dirToPlayer = player.position - transform.position;
            dirToPlayer.y = 0;
            Vector3 strafeDir = Vector3.Cross(dirToPlayer.normalized, Vector3.up);
            
            // 50% chance to strafe left vs right
            if (Random.value > 0.5f)
            {
                strafeDir = -strafeDir;
            }
            
            // Calculate strafe position
            Vector3 strafePos = transform.position + strafeDir * Random.Range(3f, 5f);
            
            // Try to find valid NavMesh position
            NavMeshHit hit;
            if (NavMesh.SamplePosition(strafePos, out hit, 5f, NavMesh.AllAreas))
            {
                // Set destination to strafe position
                agent.SetDestination(hit.position);
                
                // Wait while strafing
                float strafeDuration = Random.Range(0.5f, 1f);
                yield return new WaitForSeconds(strafeDuration);
                
                // Return to original retreat destination
                agent.SetDestination(originalDestination);
            }
            
            yield return null;
        }

        private void CheckTransitions()
        {
            // If warrior is dead, switch to death state
            if (_stateMachineNew.IsSelfDead)
            {
                var deathState = _stateMachineNew.FindState<DeathState>();
                if (deathState != null)
                {
                    _stateMachineNew.SwitchState(deathState);
                    return;
                }
            }
            
            // If player is dead, switch to wander state
            if (_stateMachineNew.IsPlayerDead)
            {
                var wanderState = _stateMachineNew.FindState<WanderState>();
                if (wanderState != null)
                {
                    _stateMachineNew.SwitchState(wanderState);
                    return;
                }
            }
            
            // If retreat timer is up, evaluate next state
            if (retreatTimer >= retreatDuration)
            {
                // When done retreating, we want to either:
                // 1. If health is very low, search for cover
                if (_stateMachineNew.NpcHealth.CurrentHealth < 30)
                {
                    var coverState = _stateMachineNew.FindState<CoverState>();
                    if (coverState != null)
                    {
                        _stateMachineNew.SwitchState(coverState);
                        return;
                    }
                }
                
                // 2. If player is in range and we can circle, start circling
                float distanceToPlayer = Vector3.Distance(transform.position, player.position);
                if (distanceToPlayer < 10f)
                {
                    var circlingState = _stateMachineNew.FindState<W_CirclingState>();
                    if (circlingState != null)
                    {
                        _stateMachineNew.SwitchState(circlingState);
                        return;
                    }
                }
                
                // 3. Otherwise, go back to chase state to re-engage
                var chaseState = _stateMachineNew.FindState<ChaseState>();
                if (chaseState != null)
                {
                    _stateMachineNew.SwitchState(chaseState);
                    return;
                }
            }
        }
    }
}