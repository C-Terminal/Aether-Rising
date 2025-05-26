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
    public class WarriorStateMachine : StateMachineNew // Assumes a base StateMachine class exists
    {
        // Configuration
        [Tooltip("Warrior's Field Of View for initiating chase")] [SerializeField]
        private float visibleChaseAngle = 180f;

        [Tooltip("Rotation speed when facing the player")] [SerializeField]
        private float rotSpeed = 2f;
        [Tooltip("NPC's attack distance to Player")]
        [SerializeField] private float attackDistance = 3f;

        // Add near other properties:
        private Transform _playerInTriggerZoneCache; // Player transform from detector

        private IState startingState;

        // State & Component References
        public List<IState> states = new(); // Holds all state components attached
        public override NPCController NpcController { get;  set; } // Reference to NPCController
        public override Transform Player { get;  set; }

        public override NavMeshAgent Agent { get;  set; }

        // public Animator Anim { get; private set; }
        public override CharacterAnimator CharAnim { get;  set; } // New - assuming CharacterAnimator.cs exists
        public override NPCController NpcCtrl { get; }
        public override Health NpcHealth { get; set; }
        public Health WarriorHealth { get; private set; } // Reference to the Health component

        public IState CurrentState { get; private set; }

        // State Tracking
        public float CirclingTime { get; set; } // Used by CirclingState and potentially NPCManager
        public bool HasSpottedPlayer { get; set; } // Flag set by NPCManager/PlayerDetector
        private bool IsPlayerDead { get; set; } // Flag set based on Player health events

        public bool IsDead => WarriorHealth != null && WarriorHealth.IsDead;

        private void Awake()
        {
            // Cache essential components
            Player = GameObject.FindWithTag("Player")?.transform;
            if (Player == null)
                Debug.LogError($"[{gameObject.name}] WarriorStateMachine: Player not found! Tag Player correctly.",
                    this);

            Agent = GetComponent<NavMeshAgent>();
            if (Agent == null)
                Debug.LogError($"[{gameObject.name}] WarriorStateMachine: NavMeshAgent component not found!", this);

            CharAnim = GetComponent<CharacterAnimator>();
            if (CharAnim == null)
                Debug.LogError($"[{gameObject.name}] WarriorStateMachine: CharacterAnimator component not found!",
                    this);

            NpcController = GetComponent<NPCController>(); // Get the NPCController
            if (NpcController == null)
                Debug.LogError($"[{gameObject.name}] WarriorStateMachine: NPCController component not found!", this);

            WarriorHealth = GetComponent<Health>();
            if (WarriorHealth == null)
                Debug.LogError($"[{gameObject.name}] WarriorStateMachine: Health component not found!", this);

            // Find and cache all IState components attached to this GameObject
            states = GetComponents<IState>().ToList();
            if (states == null || states.Count == 0)
            {
                Debug.LogError($"[{gameObject.name}] WarriorStateMachine: No IState components found!", this);
                enabled = false; // Disable if no states
                return;
            }
            base.Awake();
            //TODO: Call InitReferences on states that need it, AFTER all core components on StateMachine are cached.
            foreach (var state in states)
                if (state is W_StrikeState strikeState) // Example for W_StrikeState
                    //TODO: maybe add this to the interface
                    strikeState.InitReferences(this);
                else if (state is W_PrepareAttackState prepareState)
                    prepareState.InitReferences(this);
            // Add similar blocks if other states need an InitReferences method
            // e.g., if (state is W_PrepareAttackState prepareState) { prepareState.InitReferences(this); }
            // Subscribe to Player death event (assuming Player also has a Health component)
            if (Player != null)
            {
                var playerHealth = Player.GetComponent<Health>();
                if (playerHealth != null)
                    playerHealth.OnPlayerDeath += HandlePlayerDeath;
                else
                    Debug.LogWarning(
                        $"[{gameObject.name}] WarriorStateMachine: Player GameObject does not have a Health component for death subscription.",
                        this);
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

        private void HandlePlayerDeath()
        {
            Debug.Log($"[{gameObject.name}] WarriorStateMachine: Player Died event received.");
            IsPlayerDead = true;
            // Logic to switch to non-aggressive state
            if (CurrentState is ChaseState || CurrentState is AttackState || CurrentState is W_CirclingState ||
                CurrentState is W_PrepareAttackState || CurrentState is W_StrikeState || CurrentState is W_RecoverState)
            {
                var nextState = FindFirstAvailableState(typeof(WanderState), typeof(IdleState));
                if (nextState != null)
                    SwitchState(nextState);
                else
                    Debug.LogError(
                        $"[{gameObject.name}] WarriorStateMachine: No fallback state found after player death.");
            }
        }

        // Events
        public static event Action<Vector3> OnPlayerSpotted; // Event for alerting NPCManager

        // --- Public Helper Methods ---

        // Checks if the player is within the defined viewing angle
// Modify IsPlayerVisible to potentially use the cached player transform
// if its main Player property isn't set or to ensure it's checking the correct target.
        public override bool IsPlayerVisible() // Consider adding: Transform targetToCheck
        {
            var target = _playerInTriggerZoneCache ?? null; // Prioritize detector's cache if available

            if (target == null || IsPlayerDead) return false;

            var directionToTarget = target.position - transform.position;
            var angle = Vector3.Angle(transform.forward, directionToTarget.normalized);

            if (angle < visibleChaseAngle / 2f)
            {
                // Line of Sight Check
                var distanceToTarget = directionToTarget.magnitude;
                // Ensure raycast doesn't hit self by starting slightly in front or using a layer mask
                var rayStart = transform.position + transform.up * Agent.height / 2f; // Approx eye level
                RaycastHit hit;
                // TODO: Define an obstacleLayerMask in WarriorStateMachine and pass it here
                // For now, assuming default raycast behavior.
                if (Physics.Raycast(rayStart, directionToTarget.normalized, out hit,
                        distanceToTarget /*, obstacleLayerMask*/))
                {
                    if (hit.transform == target ||
                        hit.transform.IsChildOf(target)) // Check if hit is player or part of player
                        return true; // Direct line of sight
                    // Debug.Log($"[{gameObject.name}] IsPlayerVisible: LOS blocked by {hit.collider.name}");
                    return false; // Blocked by an obstacle
                }

                return true; // No obstacles in the way (should ideally only happen if distanceToTarget is very small)
            }

            return false;
        }

        // Add this to WarriorStateMachine.cs
        override public  float GetMaxEngagementDistance()
        {
            // Base engagement distance is the attack distance plus some buffer
            // This gives NPCs some room to maneuver before breaking engagement
            float baseDistance = attackDistance * 2.5f;
    
            // Optionally adjust based on weapon type
            if (NpcController != null)
            {
                var arsenalItem = NpcController.GetCurrentArsenalItem();
                if (arsenalItem.HasValue)
                {
                    // Weapons with longer reach might have larger engagement distances
                    if (arsenalItem.Value.name.Contains("Spear") || arsenalItem.Value.name.Contains("Polearm"))
                    {
                        baseDistance *= 1.2f; // 20% more for long weapons
                    }
                    else if (arsenalItem.Value.name.Contains("Bow") || arsenalItem.Value.name.Contains("Crossbow"))
                    {
                        baseDistance *= 1.5f; // 50% more for ranged weapons
                    }
                }
            }
    
            return baseDistance;
        }
        
        public override bool IsPlayerAttackable()
        {
            if (Player == null) return false;
            Vector3 npcToPlayerDir = Player.position - this.transform.position;
            return npcToPlayerDir.magnitude < attackDistance;
        }

        // Smoothly rotates the NPC to face the player's position on the horizontal plane
        public override void RotateToFacePlayer()
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
            if (IsDead || CurrentState is DeathState) return;

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

        // New methods to be called by PlayerDetector or its coroutine:

        /// <summary>
        ///     Called by PlayerDetector when the player enters or exits its trigger volume.
        /// </summary>
        public void NotifyPlayerInDetectionZone(bool isInZone, Transform playerTransformIfInZone)
        {
            // This method primarily informs the StateMachine about the player's presence
            // in the wider detection area. The visibility check is separate.
            if (isInZone)
            {
                _playerInTriggerZoneCache = playerTransformIfInZone;
                // If the Player property was null, this is a good time to set it globally for the FSM
                if (Player == null && playerTransformIfInZone != null)
                {
                    Player = playerTransformIfInZone; // Assuming Player property can be set
                    Debug.Log(
                        $"[{gameObject.name}] WarriorStateMachine: Player reference set via PlayerDetector to {Player.name}.");
                }
                // Current state might react to this, e.g., an Idle state might become more alert.
            }
            else
            {
                _playerInTriggerZoneCache = null;
                // If HasSpottedPlayer was true, it's reset by PlayerDetector calling NPCManager.Unregister...
                // The FSM might transition to Wander or Idle if it was chasing and player exits zone.
                if (CurrentState is ChaseState || CurrentState is W_CirclingState ||
                    CurrentState is W_PrepareAttackState || CurrentState is W_StrikeState)
                {
                    Debug.Log(
                        $"[{gameObject.name}] WarriorStateMachine: Player left detection zone. Reverting to non-aggressive state.");
                    SwitchState(FindFirstAvailableState(typeof(WanderState), typeof(IdleState)));
                }
            }
        }

        /// <summary>
        ///     Called by PlayerDetector's coroutine when detailed visibility check confirms player is visible.
        ///     This is where the FSM decides to fully engage.
        /// </summary>
        public void ConfirmPlayerVisibilityAndEngage()
        {
            if (IsPlayerDead || IsDead) return;

            // If not already actively engaging (chasing, attacking, circling etc.)
            if (!(CurrentState is ChaseState ||
                  CurrentState is AttackState || // Old attack state, if still used
                  CurrentState is W_PrepareAttackState ||
                  CurrentState is W_StrikeState ||
                  CurrentState is W_RecoverState ||
                  CurrentState is W_CirclingState))
            {
                Debug.Log(
                    $"[{gameObject.name}] WarriorStateMachine: Player confirmed visible. Engaging - Switching to ChaseState.");
                HasSpottedPlayer = true; // Mark self as spotted (NPCManager also sets this via Register)
                // This flag is useful for states to know if initial contact was made.

                AlertNearbyNPCs(); // Notify NPCManager and other NPCs

                IState chaseState = FindState<ChaseState>();
                if (chaseState != null)
                    SwitchState(chaseState);
                else
                    Debug.LogError($"[{gameObject.name}] WarriorStateMachine: ChaseState not found to engage player!",
                        this);
            }
            //TODO: flesh out paths
            if (IsPlayerAttackable())
            {
                Debug.Log($"[{gameObject.name}] WarriorStateMachine: Player in attack range on initial visibility. Skipping chase and preparing attack.");
    
                // Check if we can attack (via NPCManager)
                if (NPCManager.Instance.RequestAttackPermission(this))
                {
                    IState prepareAttackState = FindState<W_PrepareAttackState>();
                    if (prepareAttackState != null)
                        SwitchState(prepareAttackState);
                    else
                        Debug.LogError($"[{gameObject.name}] WarriorStateMachine: W_PrepareAttackState not found for immediate attack!", this);
                }
                else
                {
                    // If can't attack yet (another NPC is attacking), go to circling state
                    IState circlingState = FindState<W_CirclingState>();
                    if (circlingState != null)
                        SwitchState(circlingState);
                    else
                        Debug.LogError($"[{gameObject.name}] WarriorStateMachine: W_CirclingState not found for immediate engagement!", this);
                }
            }
        }

        /// <summary>
        ///     Called by PlayerDetector's coroutine if player is in trigger zone but NOT visible (e.g., LoS broken).
        /// </summary>
        public void NotifyPlayerLostSight()
        {
            if (IsPlayerDead || IsDead) return;

            // If currently in an active chase/attack/circle state, might switch to a "Search" or "Wander" state
            // For now, let's assume if LOS is broken while in trigger, it might revert to Wander or a specific Search state.
            if (CurrentState is ChaseState || CurrentState is W_CirclingState)
            {
                Debug.Log(
                    $"[{gameObject.name}] WarriorStateMachine: Player sight lost (still in trigger). Switching to Wander/Search.");
                // TODO: Implement a W_SearchState that moves towards last known player position
                // For now, fallback to Wander.
                IState wanderState = FindState<WanderState>();
                if (wanderState != null)
                    SwitchState(wanderState);
                else
                    SwitchState(FindState<IdleState>()); // Fallback
            }
            // HasSpottedPlayer remains true because the NPC is still "aware" and in combat mode,
            // just lost immediate sight. NPCManager registration also remains.
        }
    }
}