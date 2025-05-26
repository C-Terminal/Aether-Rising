using UnityEngine;
using System.Collections;
using System;
using Random = UnityEngine.Random;

namespace AI.FSM.NPC.States
{
    public class HitState : MonoBehaviour, IState
    {
        [Tooltip("Duration NPC remains in hit reaction")]
        [SerializeField] private float hitReactionDuration = 0.65f;
        [Tooltip("Chance to flee when health is low")]
        [Range(0, 1)]
        [SerializeField] private float fleeChanceWhenLowHealth = 0.3f;
        [Tooltip("Health threshold to consider 'low health'")]
        [SerializeField] private float lowHealthThreshold = 30f;
        [Tooltip("Chance to trigger aggressive response after being hit")]
        [Range(0, 1)]
        [SerializeField] private float aggressiveResponseChance = 0.6f;

        // Events for external systems like animation/audio
        public event Action OnNpcHit;
        public event Action OnNpcHitRecover;

        private FSM.StateMachineNew _stateMachineNew;
        private IState returnState; // The state to return to after hit reaction
        private float hitTimer = 0f;

        void Awake()
        {
            _stateMachineNew = GetComponent<FSM.StateMachineNew>();
            if (_stateMachineNew == null) Debug.LogError($"[HitState - {gameObject.name}] : NPCStateMachine not found.");
        }

        public void OnStateEnter()
        {
            Debug.Log("HitState: Enter");

            // Store the previous state to return to after hit reaction
            // If previous state was another Hit or Death, don't go back to those
            if (_stateMachineNew.PreviousState != null && 
                _stateMachineNew.PreviousState.GetType() != typeof(HitState) &&
                _stateMachineNew.PreviousState.GetType() != typeof(DeathState))
            {
                returnState = _stateMachineNew.PreviousState;
            }
            else
            {
                // Default to IdleState if no valid previous state
                returnState = _stateMachineNew.states.Find(s => s.GetType() == typeof(IdleState));
            }
            
            // Trigger hit animation/effect
            PlayNpcHitAnim?.Invoke();
            
            hitTimer = 0f;
            
            // Stop any movement
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        public void OnStateUpdate(float deltaTime)
        {
            hitTimer += deltaTime;
            
            // Only leave the hit state once the hit reaction is complete
            if (hitTimer >= hitReactionDuration)
            {
                RecoverFromHit();
            }
            
            // While recovering, still check if player is attacking
            if (_stateMachineNew.IsPlayerAttackable() && hitTimer >= hitReactionDuration * 0.5f)
            {
                float chance = _stateMachineNew.NpcHealth.CurrentHealth < lowHealthThreshold ? 
                    aggressiveResponseChance * 0.5f : aggressiveResponseChance;
                    
                if (Random.value < chance)
                {
                    // Interrupt hit reaction to counter-attack
                    RecoverAndAttack();
                }
            }
        }

        public void OnStateExit()
        {
            Debug.Log("HitState: Exit");
            
            // Signal recovery from hit
            OnNpcHitRecover?.Invoke();
        }
        
        private void RecoverFromHit()
        {
            // Decision making based on health level
            if (_stateMachineNew.NpcHealth != null && 
                _stateMachineNew.NpcHealth.CurrentHealth < lowHealthThreshold)
            {
                // Low health behavior
                if (Random.value < fleeChanceWhenLowHealth)
                {
                    // Try to find cover
                    IState coverState = _stateMachineNew.states.Find(s => s.GetType() == typeof(CoverState));
                    if (coverState != null)
                    {
                        Debug.Log("HitState: Low health, taking cover");
                        _stateMachineNew.SwitchState(coverState);
                        return;
                    }
                    
                    // Fallback to fleeing if cover state doesn't exist
                    Debug.Log("HitState: Low health, fleeing (no cover state found)");
                    FleeFromPlayer();
                    return;
                }
            }
            
            // If player is visible, decide between attacking or chasing
            if (_stateMachineNew.IsPlayerVisible())
            {
                if (_stateMachineNew.IsPlayerAttackable())
                {
                    // Player is in attack range
                    IState attackState = _stateMachineNew.states.Find(s => s.GetType() == typeof(AttackState));
                    if (attackState != null)
                    {
                        Debug.Log("HitState: Player in range, counter-attacking");
                        _stateMachineNew.SwitchState(attackState);
                        return;
                    }
                }
                else
                {
                    // Player is visible but not in attack range
                    IState chaseState = _stateMachineNew.states.Find(s => s.GetType() == typeof(ChaseState));
                    if (chaseState != null)
                    {
                        Debug.Log("HitState: Player spotted, giving chase");
                        _stateMachineNew.SwitchState(chaseState);
                        return;
                    }
                }
            }
            
            // If no special case was triggered, return to previous state
            if (returnState != null)
            {
                Debug.Log($"HitState: Returning to previous state {returnState.GetType().Name}");
                _stateMachineNew.SwitchState(returnState);
            }
            else
            {
                // Fallback to Idle as a last resort
                IState idleState = _stateMachineNew.states.Find(s => s.GetType() == typeof(IdleState));
                if (idleState != null)
                {
                    Debug.Log("HitState: No return state, reverting to Idle");
                    _stateMachineNew.SwitchState(idleState);
                }
            }
        }
        
        private void RecoverAndAttack()
        {
            Debug.Log("HitState: Interrupting hit reaction to counter-attack");
            
            // Force immediate recovery and counterattack
            IState attackState = _stateMachineNew.states.Find(s => s.GetType() == typeof(AttackState));
            if (attackState != null)
            {
                _stateMachineNew.SwitchState(attackState);
            }
            else
            {
                // Fallback to chase if attack isn't available
                IState chaseState = _stateMachineNew.states.Find(s => s.GetType() == typeof(ChaseState));
                if (chaseState != null)
                {
                    _stateMachineNew.SwitchState(chaseState);
                }
            }
        }
        
        private void FleeFromPlayer()
        {
            // Simple fleeing implementation - could be expanded to a full FleeState
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && _stateMachineNew.Player != null)
            {
                // Calculate direction away from player
                Vector3 fleeDirection = transform.position - _stateMachineNew.Player.position;
                fleeDirection.y = 0; // Keep horizontal
                fleeDirection = fleeDirection.normalized;
                
                // Find a point to flee to
                Vector3 fleeTarget = transform.position + fleeDirection * 10f;
                
                // Try to find a valid NavMesh position
                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(fleeTarget, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    Debug.Log("HitState: Fleeing from player");
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                    
                    // Switch to idle after reaching flee point
                    StartCoroutine(SwitchToIdleAfterFleeing(agent));
                }
                else
                {
                    // If can't find flee point, return to idle
                    IState idleState = _stateMachineNew.states.Find(s => s.GetType() == typeof(IdleState));
                    if (idleState != null)
                    {
                        Debug.Log("HitState: Cannot find flee path, reverting to Idle");
                        _stateMachineNew.SwitchState(idleState);
                    }
                }
            }
        }
        
        private IEnumerator SwitchToIdleAfterFleeing(UnityEngine.AI.NavMeshAgent agent)
        {
            // Wait until path is complete or time runs out
            float timeout = 5f;
            float elapsed = 0f;
            
            while (elapsed < timeout && agent != null && 
                  agent.enabled && !agent.pathPending && 
                  agent.remainingDistance > agent.stoppingDistance)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Return to idle state after fleeing
            IState idleState = _stateMachineNew.states.Find(s => s.GetType() == typeof(IdleState));
            if (idleState != null && _stateMachineNew.CurrentState == this)
            {
                Debug.Log("HitState: Flee complete, returning to Idle");
                _stateMachineNew.SwitchState(idleState);
            }
        }

        public event Action PlayNpcHitAnim;
    }
}