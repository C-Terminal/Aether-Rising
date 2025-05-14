using System;
using System.Collections.Generic;
using System.Linq;
using AI.FSM.NPC.States;
using AI.FSM.Warrior.States;
using Animation.AnimControllers;
using Characters.NPC;
using Combat.DamageSystem.Health;
using UnityEngine;
using UnityEngine.AI;

namespace AI.FSM.NPC
{
    public class WarriorStateMachine : StateMachine // Assumes a base StateMachine class exists
    {
        // Configuration
        [Tooltip("Warrior's Field Of View for initiating chase")] [SerializeField]
        private float visibleChaseAngle = 180f;

        [Tooltip("Rotation speed when facing the player")] [SerializeField]
        private float rotSpeed = 2f;

        private IState startingState;

        // State & Component References
        public List<IState> states = new(); // Holds all state components attached
        public NPCController NpcController { get; private set; } // Reference to NPCController
        public Transform Player { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        // public Animator Anim { get; private set; }
        public CharacterAnimator CharAnim { get; private set; } // New - assuming CharacterAnimator.cs exists
        public Health WarriorHealth { get; private set; } // Reference to the Health component

        public IState CurrentState { get; private set; }

        // State Tracking
        public float CirclingTime { get; set; } // Used by CirclingState and potentially NPCManager
        public bool HasSpottedPlayer { get; set; } // Flag set by NPCManager/PlayerDetector
        public bool IsPlayerDead { get; set; } // Flag set based on Player health events

    void Awake()
    {
        // Cache essential components
        Player = GameObject.FindWithTag("Player")?.transform;
        if (Player == null) Debug.LogError($"[{gameObject.name}] WarriorStateMachine: Player not found! Tag Player correctly.", this);

        Agent = GetComponent<NavMeshAgent>();
        if (Agent == null) Debug.LogError($"[{gameObject.name}] WarriorStateMachine: NavMeshAgent component not found!", this);

        CharAnim = GetComponent<CharacterAnimator>();
        if (CharAnim == null) Debug.LogError($"[{gameObject.name}] WarriorStateMachine: CharacterAnimator component not found!", this);

        NpcController = GetComponent<NPCController>(); // Get the NPCController
        if (NpcController == null) Debug.LogError($"[{gameObject.name}] WarriorStateMachine: NPCController component not found!", this);
        
        WarriorHealth = GetComponent<Health>();
        if (WarriorHealth == null) Debug.LogError($"[{gameObject.name}] WarriorStateMachine: Health component not found!", this);

        // Find and cache all IState components attached to this GameObject
        states = GetComponents<IState>().ToList();
        if (states == null || states.Count == 0)
        {
            Debug.LogError($"[{gameObject.name}] WarriorStateMachine: No IState components found!", this);
            enabled = false; // Disable if no states
            return;
        }

        // Call InitReferences on states that need it, AFTER all core components on StateMachine are cached.
        foreach (var state in states)
        {
            if (state is W_StrikeState strikeState) // Example for W_StrikeState
            {
                strikeState.InitReferences(this);
            }
            // Add similar blocks if other states need an InitReferences method
            // e.g., if (state is W_PrepareAttackState prepareState) { prepareState.InitReferences(this); }
        }


        // Subscribe to Player death event (assuming Player also has a Health component)
        if (Player != null)
        {
            Health playerHealth = Player.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.OnPlayerDeath += HandlePlayerDeath;
            }
            else Debug.LogWarning($"[{gameObject.name}] WarriorStateMachine: Player GameObject does not have a Health component for death subscription.", this);
        }
    }

    private void HandlePlayerDeath()
    {
        Debug.Log($"[{gameObject.name}] WarriorStateMachine: Player Died event received.");
        IsPlayerDead = true;
        // Logic to switch to non-aggressive state
        if (CurrentState is ChaseState || CurrentState is AttackState || CurrentState is W_CirclingState || CurrentState is W_PrepareAttackState || CurrentState is W_StrikeState || CurrentState is W_RecoverState)
        {
            var nextState = FindFirstAvailableState(typeof(WanderState), typeof(IdleState));
            if (nextState != null)
                SwitchState(nextState);
            else
                Debug.LogError($"[{gameObject.name}] WarriorStateMachine: No fallback state found after player death.");
        }
    }

    private void Start()
        {
            // Find the Idle state as the default starting state
            startingState = FindState<IdleState>(); // Use generic FindState
            if (startingState == null)
                startingState = FindState<WanderState>(); // Fallback to Wander if Idle isn't present
            if (startingState != null && !IsPlayerDead)
            {
                SwitchState(startingState);
            }
            else if (startingState == null)
            {
                /* Error Log */
            }
        }

        private void OnEnable()
        {
            // Subscribe to own health events
            if (WarriorHealth != null)
            {
                WarriorHealth.OnNpcDeath += NpcDead;
                WarriorHealth.OnHealthDepleted += HandleDamageTaken; // Renamed for clarity
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from own health events
            if (WarriorHealth != null)
            {
                WarriorHealth.OnNpcDeath -= NpcDead;
                WarriorHealth.OnHealthDepleted -= HandleDamageTaken;
            }

            // Unsubscribe from player death event
            if (Player != null)
            {
                var playerHealth = Player.GetComponent<Health>();
                if (playerHealth != null) playerHealth.OnPlayerDeath -= PlayerDead;
            }
        }

        // Events
        public static event Action<Vector3> OnPlayerSpotted; // Event for alerting NPCManager

        // --- Public Helper Methods ---

        // Checks if the player is within the defined viewing angle
        public bool IsPlayerVisible()
        {
            if (Player == null) return false;
            var npcToPlayerDir = Player.position - transform.position;
            var angle = Vector3.Angle(transform.forward, npcToPlayerDir);
            // Check if within the half-angle on either side
            return angle < visibleChaseAngle / 2f;
            // Potential Improvement: Add a Raycast check for line-of-sight obstacles
        }

        // Smoothly rotates the NPC to face the player's position on the horizontal plane
        public void RotateToFacePlayer()
        {
            if (Player == null) return;
            var npcToPlayerDir = Player.position - transform.position;
            npcToPlayerDir.y = 0; // Ignore vertical difference
            if (npcToPlayerDir == Vector3.zero) return; // Avoid zero vector rotation
            var targetRotation = Quaternion.LookRotation(npcToPlayerDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotSpeed);
        }

        // Finds a state component of a specific type T attached to this GameObject
        public T FindState<T>() where T : class, IState
        {
            // Efficiently finds the state using LINQ FirstOrDefault
            return states.FirstOrDefault(s => s is T) as T;
        }

        // Finds the first available state from a list of state types
        public IState FindFirstAvailableState(params Type[] stateTypes)
        {
            foreach (var stateType in stateTypes)
            {
                var state = states.FirstOrDefault(s => s.GetType() == stateType);
                if (state != null)
                    return state;
            }
            Debug.LogWarning($"[{gameObject.name}] WarriorStateMachine: None of the requested state types were found.");
            return null;
        }

        // Overload to find state by instance type (less common)
        public IState FindState(IState stateInstance)
        {
            return states.Find(s => s.GetType() == stateInstance.GetType());
        }

        // Triggers the OnPlayerSpotted event for NPCManager to react
        public void AlertNearbyNPCs()
        {
            Debug.Log($"[WarriorStateMachine - {gameObject.name}]: Alerting nearby NPCs.");
            OnPlayerSpotted?.Invoke(transform.position); // Invoke static event with current position
        }

        // Called by NPCManager to force this NPC to chase the player
        public void InitiateAttackOnPlayer() // Removed npc parameter as it's implicit
        {
            Debug.Log($"[WarriorStateMachine - {gameObject.name}]: Initiating chase on Player");
            IState chase = FindState<ChaseState>();
            if (chase != null) SwitchState(chase);
            /* Error Log: Chase state missing */
        }

        // --- Event Handlers ---

        // Handles the OnNpcDeath event from the Health component
        private void NpcDead()
        {
            Debug.Log($"[WarriorStateMachine - {gameObject.name}]: NPC Died. Switching to Death State.");
            IState death = FindState<DeathState>();
            if (death != null) SwitchState(death);
            /* Error Log: Death state missing */
        }

        // Handles the OnPlayerDeath event from the Player's Health component
        private void PlayerDead()
        {
            IsPlayerDead = true;
            Debug.Log(
                $"[WarriorStateMachine - {gameObject.name}]: Player is Dead. Current State: {CurrentState?.GetType().Name}");
            // If currently in an aggressive state, switch to a non-aggressive one (e.g., Wander or Idle)
            if (CurrentState is ChaseState || CurrentState is AttackState || CurrentState is W_CirclingState ||
                CurrentState is W_RetreatState)
            {
                IState wander = FindState<WanderState>();
                if (wander != null)
                {
                    SwitchState(wander);
                }
                else
                {
                    IState idle = FindState<IdleState>();
                    if (idle != null) SwitchState(idle);
                }
            }
        }

        // Handles the OnHealthDepleted event from the Health component
        private void HandleDamageTaken(string victimTag, string attackerTag) // Added originTag for context
        {
            // Ignore if already dead or if damage is from self/another NPC (handled in MeleeWeaponDamage)
            if (WarriorHealth.IsDead || CurrentState is DeathState) return;

            Debug.Log(
                $"[WarriorStateMachine - {gameObject.name}]: Took damage. Current Health: {WarriorHealth?.CurrentHealth}");
            // If health is low, prioritize taking cover
            if (WarriorHealth != null && WarriorHealth.CurrentHealth < 50)
            {
                IState cover = FindState<CoverState>();
                if (cover != null && !(CurrentState is CoverState)) // Don't switch if already in cover
                {
                    Debug.Log(
                        $"[WarriorStateMachine - {gameObject.name}]: Health low ({WarriorHealth.CurrentHealth}), switching to Cover State.");
                    SwitchState(cover);
                    return; // Exit after switching to cover
                }

                if (cover == null)
                {
                    /* Error Log: Cover state missing */
                }
            }

            // Otherwise, play a hit reaction (if not already in hit state)
            IState hit = FindState<HitState>();
            if (hit != null && !(CurrentState is HitState))
            {
                Debug.Log($"[WarriorStateMachine - {gameObject.name}]: Switching to Hit State.");
                SwitchState(hit);
            }
            else if (hit == null)
            {
                /* Error Log: Hit state missing */
            }
        }

        // Base StateMachine class might provide these:
        // public IState CurrentState { get; private set; }
        // public IState PreviousState { get; private set; }
        public bool IsDead { get { return WarriorHealth != null && WarriorHealth.IsDead; } } // Or use Health's isDead flag
        // public virtual void SwitchState(IState newState) { ... implementation ... }
    }
}