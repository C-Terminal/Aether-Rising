using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;
using Combat.DamageSystem.Health;
using Random = UnityEngine.Random;

namespace AI.FSM.NPC.States
{
    public class AttackState : MonoBehaviour, IState
    {
        [Tooltip("Speed at which NPC moves while attacking (0 for stationary attacks)")]
        [SerializeField] private float npcSpeed = 0f;
        [Tooltip("Distance beyond which NPC will chase rather than attack")]
        [SerializeField] private float maxAttackDistance = 3.5f;
        [Tooltip("Time between attacks")]
        [SerializeField] private float attackCooldown = 1.5f;
        [Tooltip("Amount of damage each attack deals")]
        [SerializeField] private int attackDamage = 10;
        [Tooltip("Max number of consecutive attacks before forced repositioning")]
        [SerializeField] private int maxConsecutiveAttacks = 3;
        [Tooltip("Chance to back away after attack sequence")]
        [Range(0, 1)]
        [SerializeField] private float repositionChance = 0.4f;

        // Events for external systems like animation/audio
        public event Action OnNpcAttack;
        public event Action OnNpcAttackHit;
        public event Action OnNpcAttackMiss;

        private NPCStateMachine stateMachine;
        private NavMeshAgent agent;
        private float attackTimer = 0f;
        private bool canAttack = true;
        private int consecutiveAttacks = 0;
        private Coroutine attackCoroutine;

        void Awake()
        {
            stateMachine = GetComponent<NPCStateMachine>();
            if (stateMachine == null) Debug.LogError($"[AttackState - {gameObject.name}] : NPCStateMachine not found.");
            
            agent = GetComponent<NavMeshAgent>();
            if (agent == null) Debug.LogError($"[AttackState - {gameObject.name}] : No NavMesh Agent found.");
        }

        public void OnStateEnter()
        {
            Debug.Log("AttackState: Enter");
            
            if (agent != null && agent.enabled)
            {
                agent.speed = npcSpeed;
                if (npcSpeed == 0)
                {
                    agent.isStopped = true;
                }
                else
                {
                    agent.isStopped = false;
                }
            }
            
            attackTimer = 0f;
            canAttack = true;
            consecutiveAttacks = 0;
        }

        public void OnStateUpdate(float deltaTime)
        {
            // First check if player is out of attack range but still visible
            if (!stateMachine.IsPlayerAttackable() && stateMachine.IsPlayerVisible())
            {
                Debug.Log("AttackState: Player out of attack range, switching to Chase");
                IState chase = stateMachine.states.Find(s => s.GetType() == typeof(ChaseState));
                if (chase != null) stateMachine.SwitchState(chase);
                return;
            }
            
            // If player is no longer visible at all, return to idle
            if (!stateMachine.IsPlayerVisible())
            {
                Debug.Log("AttackState: Player no longer visible, switching to Idle");
                IState idle = stateMachine.states.Find(s => s.GetType() == typeof(IdleState));
                if (idle != null) stateMachine.SwitchState(idle);
                return;
            }

            // Always face the player when attacking
            stateMachine.RotateToFacePlayer();
            
            // Update attack cooldown
            if (!canAttack)
            {
                attackTimer += deltaTime;
                
                if (attackTimer >= attackCooldown)
                {
                    canAttack = true;
                }
            }
            
            // Perform attack if ready
            if (canAttack && stateMachine.IsPlayerAttackable())
            {
                PerformAttack();
            }
            else if (npcSpeed > 0 && agent != null && agent.enabled)
            {
                // Move toward player if we're not a stationary attacker
                agent.SetDestination(stateMachine.Player.position);
            }
            
            // Check if we should reposition after multiple attacks
            if (consecutiveAttacks >= maxConsecutiveAttacks)
            {
                if (Random.value < repositionChance)
                {
                    Debug.Log("AttackState: Repositioning after attack sequence");
                    Reposition();
                }
                else
                {
                    consecutiveAttacks = 0;
                }
            }
        }

        public void OnStateExit()
        {
            Debug.Log("AttackState: Exit");
            
            // Clean up any ongoing attack animations or coroutines
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }
        }
        
        private void PerformAttack()
        {
            Debug.Log("AttackState: Performing attack");
            canAttack = false;
            attackTimer = 0f;
            consecutiveAttacks++;
            
            // Trigger attack animation/sound
            OnNpcAttack?.Invoke();
            
            // Start attack sequence coroutine
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
            }
            attackCoroutine = StartCoroutine(AttackSequence());
        }
        
        private IEnumerator AttackSequence()
        {
            // Delay for attack windup animation
            yield return new WaitForSeconds(0.3f);
            
            // Calculate hit
            bool hitSuccessful = CalculateHit();
            
            if (hitSuccessful)
            {
                // Deal damage to player
                DealDamageToPlayer();
                OnNpcAttackHit?.Invoke();
                Debug.Log("AttackState: Attack hit player");
            }
            else
            {
                OnNpcAttackMiss?.Invoke();
                Debug.Log("AttackState: Attack missed player");
            }
            
            // Recovery delay
            yield return new WaitForSeconds(0.5f);
            
            attackCoroutine = null;
        }
        
        private bool CalculateHit()
        {
            // Simple hit calculation - could be expanded with dodge mechanics, etc.
            if (stateMachine.Player != null && stateMachine.IsPlayerAttackable())
            {
                // Line of sight check
                RaycastHit hit;
                Vector3 dirToPlayer = stateMachine.Player.position - transform.position;
                if (Physics.Raycast(transform.position + Vector3.up, dirToPlayer.normalized, out hit, maxAttackDistance))
                {
                    return hit.transform == stateMachine.Player;
                }
            }
            return false;
        }
        
        private void DealDamageToPlayer()
        {
            // Find player's health component
            Health playerHealth = stateMachine.Player.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage, gameObject.tag);
            }
        }
        
        private void Reposition()
        {
            consecutiveAttacks = 0;
            
            // Choose to either back off or circle around
            if (Random.value < 0.5f && agent != null && agent.enabled)
            {
                // Back away
                Vector3 backDir = -transform.forward * 3f;
                Vector3 backPos = transform.position + backDir;
                
                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(backPos, path))
                {
                    agent.SetPath(path);
                }
            }
            
            // Let the chase state take over temporarily
            IState chase = stateMachine.states.Find(s => s.GetType() == typeof(ChaseState));
            if (chase != null) stateMachine.SwitchState(chase);
        }
    }
}