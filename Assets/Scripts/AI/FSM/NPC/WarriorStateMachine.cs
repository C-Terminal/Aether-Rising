using System;
using System.Collections.Generic;
using System.Linq;
using AI.FSM.NPC.States;
using AI.FSM.Utility;
using AI.FSM.Warrior.States;
using AI.NPC.DebugTools;
using AI.NPC.Movement;
using AI.NPC.Sensing;
using AI.NPC.Sensing.Vision;
using Animation.AnimControllers;
using Characters.NPC;
using Combat.DamageSystem.Health;
using UnityEngine;
using UnityEngine.AI;

namespace AI.FSM.NPC
{
    public class 
        WarriorStateMachine : StateMachineNew, IPerceptionAwareFSM
    {
        // Configuration
        [SerializeField] private NPCPerceptionConfig perceptionConfig;
        
        // Add near other properties:
        private Transform _playerInTriggerZoneCache; // Player transform from detector
        public Action<IState, IState> OnStateChanged; // Event for state changes

        private IState startingState;

        // State & Component References
        public List<IState> states = new(); // Holds all state components attached
        
        // Core dependencies
        private INavAgent agent;
        private ICharacterAnimator animator;
        private INPCController controller;
        private IHealth health;
        private IVisionSensor vision;
        // private ITargetingSensor targeting;
        // private IZoneDetector zone;
        public override NPCController NpcController { get; set; } // Reference to NPCController
        public override Transform Player { get; set; }

        public override NavMeshAgent Agent { get; set; }

        // public Animator Anim { get; private set; }
        public override CharacterAnimator CharAnim { get; set; } // New - assuming CharacterAnimator.cs exists
        public override NPCController NpcCtrl { get; }
        public override Health NpcHealth { get; set; }
        public Health WarriorHealth { get; private set; } // Reference to the Health component

        private NPCPerceptionCoordinator _perceptionCoordinator;
        
        public AIMovementController MovementController { get; private set; }
        public new IState CurrentState { get; private set; }

        // State Tracking
        public float CirclingTime { get; set; } // Used by CirclingState and potentially NPCManager
        public bool HasSpottedPlayer { get; set; } // Flag set by NPCManager/PlayerDetector
        private new bool IsPlayerDead { get; set; } // Flag set based on Player health events

        public bool IsDead => WarriorHealth != null && WarriorHealth.IsDead;

        private new void Awake()
        {
            // TryAutoInjectDependencies();
            
            if (GetComponent<NPCMemoryComponent>() == null)
                gameObject.AddComponent<NPCMemoryComponent>();
            
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
        
            MovementController = GetComponent<AIMovementController>();
            if (MovementController == null)
                Debug.LogError($"[{gameObject.name}] WarriorStateMachine: AIMovementController component not found!", this);
            
            _perceptionCoordinator = GetComponentInChildren<NPCPerceptionCoordinator>();
            if (_perceptionCoordinator == null)
                Debug.LogError($"[{gameObject.name}] WarriorStateMachine: NPCPerceptionCoordinator component not found!", this);
            
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
                else if (state is W_RecoverState recoverState)
                    recoverState.InitReferences(this);
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

        private void TryAutoInjectDependencies()
        {
            {
                // Fallback for scene-based gameplay
                if (agent == null && TryGetComponent<NavMeshAgent>(out var realAgent))
                    agent = new NavMeshAgentWrapper(realAgent);

                if (animator == null && TryGetComponent<CharacterAnimator>(out var a))
                    animator = a;

                if (controller == null && TryGetComponent<NPCController>(out var c))
                    controller = c;

                if (health == null && TryGetComponent<Health>(out var h))
                    health = h;

                if (vision == null && TryGetComponent<VisionSensor>(out var v))
                    vision = v;

                // if (targeting == null && TryGetComponent<TargetingSensor>(out var t))
                //     targeting = t;
                //
                // if (zone == null && TryGetComponent<ZoneDetector>(out var z))
                //     zone = z;
            }
        }

        // public void InjectDependencies(
        //     INavAgent agent,
        //     ICharacterAnimator animator,
        //     INPCController controller,
        //     IHealth health,
        //     IVisionSensor vision,
        //     ITargetingSensor targeting,
        //     IZoneDetector zone)
        // {
        //     this.agent = agent;
        //     this.animator = animator;
        //     this.controller = controller;
        //     this.health = health;
        //     this.vision = vision;
        //     this.targeting = targeting;
        //     this.zone = zone;
        // }

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
            
            NPCRegistry.Instance?.Register(this);
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
            
            NPCRegistry.Instance?.Unregister(this);
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

        
        // Modified SwitchState method to fire events
        public override void SwitchState(IState newState)
        {
            IState oldState = CurrentState;
            base.SwitchState(newState);
    
            //Set the current state
            CurrentState = newState;

            // Fire state change event
            OnStateChanged?.Invoke(oldState, newState);
        }

        public NPCManager.AIAlertLevel CurrentAlertLevel { get; private set; }

        public void SetAlertLevel(NPCManager.AIAlertLevel level)
        {
            if (CurrentAlertLevel == level) return;

            CurrentAlertLevel = level;
            // Optional: Change state, animation, or perception behavior
        }
        
        // --- Public Helper Methods ---

        // Checks if the player is within the defined viewing angle
        // Modify IsPlayerVisible to potentially use the cached player transform
        // if its main Player property isn't set or to ensure it's checking the correct target.
        public override bool IsPlayerVisible() // Consider adding: Transform targetToCheckw
        {
            //TODO: figure out later
            var target = _playerInTriggerZoneCache ?? null; // Prioritize detector's cache if available

            // if (target == null || IsPlayerDead) return false;
            
            if (IsPlayerDead) return false;
            return _perceptionCoordinator?.IsPlayerCurrentlyVisible() ?? false;

        }

        public void Disengage()
        {
            if (NPCManager.Instance.IsPlayerGloballySpotted)
            {
                // transition to search state, go to lastKnownPlayerPosition
            }

        }

        // Add this to WarriorStateMachine.cs
        public override float GetMaxEngagementDistance()
        {
            // Base engagement distance is the attack distance plus some buffer
            // This gives NPCs some room to maneuver before breaking engagement
            var baseDistance = perceptionConfig.actionRange * 2.5f;

            // Optionally adjust based on weapon type
            if (NpcController != null)
            {
                var arsenalItem = NpcController.GetCurrentArsenalItem();
                if (arsenalItem.HasValue)
                {
                    // Weapons with longer reach might have larger engagement distances
                    if (arsenalItem.Value.name.Contains("Spear") || arsenalItem.Value.name.Contains("Polearm"))
                        baseDistance *= 1.2f; // 20% more for long weapons
                    else if (arsenalItem.Value.name.Contains("Bow") || arsenalItem.Value.name.Contains("Crossbow"))
                        baseDistance *= 1.5f; // 50% more for ranged weapons
                }
            }

            return baseDistance;
        }

        public override bool IsPlayerAttackable()
        {
            if (Player == null) return false;
            var npcToPlayerDir = Player.position - transform.position;
            return npcToPlayerDir.magnitude < perceptionConfig.actionRange;
        }

        // Smoothly rotates the NPC to face the player's position on the horizontal plane
        public override void RotateToFacePlayer()
        {
            this.MovementController.RotateToward(Player);
        }

        // Finds a state component of a specific type T attached to this GameObject
        public new T FindState<T>() where T : class, IState
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
        ///     Called by NPCPerceptionCoordinator when the player enters or exits its trigger volume.
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
                //TODO: mak Current state might react to this, e.g., an Idle state might become more alert.
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

// Helper method to check if in engaged state
        private bool IsInEngagedState()
        {
            return CurrentState is ChaseState ||
                   CurrentState is AttackState ||
                   CurrentState is W_PrepareAttackState ||
                   CurrentState is W_StrikeState ||
                   CurrentState is W_RecoverState ||
                   CurrentState is W_CirclingState;
        }


        public void SetGroupAwareness(Vector3 alertPosition)
        {
            if (IsDead || HasSpottedPlayer) return;

            // Optional: store memory of the last seen position
            var gizmos = GetComponent<NPCPerceptionGizmos>();
            if (gizmos != null)
                gizmos.MarkPlayerPosition(alertPosition);

            // Optional: notify perception logic (e.g., FSM states)
            SetAlertLevel(NPCManager.AIAlertLevel.Alert);

            // Optional: transition to an alert/search state
            var alertState = FindState<W_AlertState>();
            if (alertState != null)
                SwitchState(alertState);
        }

        
        /// <summary>
        ///     Called by PlayerDetector's coroutine when detailed visibility check confirms player is visible.
        ///     This is where the FSM decides to fully engage.
        /// </summary>
        public void ConfirmPlayerVisibilityAndEngage()
        {
            if (IsPlayerDead || IsDead) return;

            // Register with NPCManager if not already registered
            if (HasSpottedPlayer && NPCManager.Instance != null)
            {
                NPCManager.Instance.RegisterEngagedNPC(this);
            }
            
            // If not already actively engaging (chasing, attacking, circling etc.)
            if (!IsInEngagedState())
            {
                Debug.Log(
                    $"[{gameObject.name}] WarriorStateMachine: Player confirmed visible. Engaging - Switching to ChaseState.");
                HasSpottedPlayer = true; // Mark self as spotted (NPCManager also sets this via Register)
                // This flag is useful for states to know if initial contact was made.
                // TODO: STOP THE VISIBILITY COROUTINE SINCE WE'RE NOW ENGAGING
                // StopVisibilityChecks();
                AlertNearbyNPCs(); // Notify NPCManager and other NPCs 

                IState chaseState = FindState<ChaseState>();
                if (chaseState != null)
                    SwitchState(chaseState);
                else
                    Debug.LogError($"[{gameObject.name}] WarriorStateMachine: ChaseState not found to engage player!",
                        this);
            }
            
            if (IsPlayerAttackable())
            {
                Debug.Log(
                    $"[{gameObject.name}] WarriorStateMachine: Player in attack range on initial visibility. Skipping chase and preparing attack.");

                // Check if we can attack (via NPCManager)
                if (NPCManager.Instance.RequestAttackPermission(this))
                {
                    IState prepareAttackState = FindState<W_PrepareAttackState>();
                    if (prepareAttackState != null)
                        SwitchState(prepareAttackState);
                    else
                        Debug.LogError(
                            $"[{gameObject.name}] WarriorStateMachine: W_PrepareAttackState not found for immediate attack!",
                            this);
                }
                else
                {
                    // If can't attack yet (another NPC is attacking), go to circling state
                    IState circlingState = FindState<W_CirclingState>();
                    if (circlingState != null)
                        SwitchState(circlingState);
                    else
                        Debug.LogError(
                            $"[{gameObject.name}] WarriorStateMachine: W_CirclingState not found for immediate engagement!",
                            this);
                }
            }
        }

        private void StopVisibilityChecks()
        {
            // throw new NotImplementedException();
        }

        /// <summary>
        ///     Called by PlayerDetector's coroutine if player is in trigger zone but NOT visible (e.g., LoS broken).
        /// </summary>
        public void NotifyPlayerLostSight()
        {
            Debug.Log($"[{gameObject.name}] WarriorStateMachine: Lost sight of player.");

            HasSpottedPlayer = false;

            if (IsInEngagedState())
            {
                IState fallback = FindFirstAvailableState(typeof(WanderState), typeof(IdleState));
                if (fallback != null)
                    SwitchState(fallback);
                else
                    Debug.LogWarning($"[{gameObject.name}] No fallback state found after losing sight.");
            }
        }

        public void ReceiveAttackInvitation()
        {
            Debug.Log($"[{name}] Received attack invitation.");

            if (IsDead || IsPlayerDead)
            {
                Debug.LogWarning($"[{name}] Ignoring invitation: dead or player is dead.");
                return;
            }

            if (CurrentState is W_StrikeState ||
                // CurrentState is W_RecoverState ||
                CurrentState is W_PrepareAttackState ||
                CurrentState is W_RetreatState ||
                CurrentState is DeathState)
            {
                Debug.LogWarning($"[{name}] Ignoring attack invitation due to current state: {CurrentState?.GetType().Name}");
                return;
            }

            // Optional: Skip if morale too low
            var memory = GetComponent<NPCMemoryComponent>();
            if (memory != null && memory.morale < 0.25f)
            {
                Debug.Log($"[{name}] Skipping invitation due to low morale: {memory.morale:F2}");
                return;
            }

            // Optional: Only accept if currently circling or idle-like
            if (CurrentState is not W_CirclingState &&
                CurrentState is not IdleState &&
                CurrentState is not WanderState &&
                CurrentState is not ChaseState)
            {
                Debug.Log($"[{name}] Received invitation, but in transitional state: {CurrentState?.GetType().Name}");
                return;
            }

            // All clear — transition
            var prepareAttackState = FindState<W_PrepareAttackState>();
            if (prepareAttackState != null)
            {
                Debug.Log($"[{name}] Accepting invitation. Switching to PrepareAttackState.");
                SwitchState(prepareAttackState);
            }
        }

    }
}