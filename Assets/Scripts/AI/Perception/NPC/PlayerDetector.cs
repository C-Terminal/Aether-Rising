using System;
using System.Collections;
using AI.FSM;
using AI.FSM.NPC;
using AI.FSM.NPC.States;
using AI.FSM.Warrior.States;
using UnityEngine;

namespace AI.Perception.NPC
{
    /// <summary>
    /// Detects the player and manages visibility checks. Uses events to communicate with the StateMachine.
    /// The responsibility separation: PlayerDetector handles detection, StateMachine handles state decisions.
    /// </summary>
    public class PlayerDetector : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("How often (in seconds) to perform detailed visibility checks when player is in the trigger zone.")]
        [SerializeField] private float visibilityCheckInterval = 0.2f;
        [Tooltip("Tag of the player GameObject.")]
        [SerializeField] private string playerTag = "Player";

        // Events for communication with StateMachine
        public System.Action<bool> OnPlayerVisibilityChanged;
        public System.Action OnPlayerEnteredZone;
        public System.Action OnPlayerExitedZone;

        private WarriorStateMachine _stateMachine;
        private Coroutine _visibilityCheckCoroutine;
        private bool _isPlayerInTriggerZone = false;
        private bool _wasPlayerVisibleLastCheck = false;
        private Transform _playerTransformCache;
        
        // Control flags
        private bool _shouldPerformVisibilityChecks = true;
        private bool _hasInitiatedEngagement = false;

        void Awake()
        {
            _stateMachine = GetComponentInParent<WarriorStateMachine>();
            if (_stateMachine == null)
            {
                Debug.LogError($"[{gameObject.name}] PlayerDetector: WarriorStateMachine not found on parent or self!", this);
                enabled = false;
                return;
            }

            ValidateCollider();
            
            // Subscribe to state machine events
            _stateMachine.OnStateChanged += OnStateMachineStateChanged;
        }

        private void Start()
        {
            var circlingState = _stateMachine.FindState<W_CirclingState>();
            if (circlingState != null)
            {
                circlingState.OnLostPlayerWhileCircling += HandlePlayerLostDuringCircling;
            }
        }

        private void HandlePlayerLostDuringCircling()
        {
            // Maybe increase visibility check frequency temporarily
            // or trigger special search behavior
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            if (_stateMachine != null)
                _stateMachine.OnStateChanged -= OnStateMachineStateChanged;
        }

        private void ValidateCollider()
        {
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                Debug.LogError($"[{gameObject.name}] PlayerDetector: No Collider component found!", this);
                enabled = false;
                return;
            }
            if (!col.isTrigger)
            {
                Debug.LogWarning($"[{gameObject.name}] PlayerDetector: Collider should be set to 'Is Trigger'.", this);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                Debug.Log($"[{_stateMachine.gameObject.name}'s PlayerDetector]: Player entered trigger zone.");
                
                _isPlayerInTriggerZone = true;
                _playerTransformCache = other.transform;
                _hasInitiatedEngagement = false; // Reset engagement flag
                
                // Notify state machine
                _stateMachine.NotifyPlayerInDetectionZone(true, _playerTransformCache);
                OnPlayerEnteredZone?.Invoke();

                // Start visibility checks if we should be checking
                if (_shouldPerformVisibilityChecks && _visibilityCheckCoroutine == null)
                {
                    _visibilityCheckCoroutine = StartCoroutine(CheckPlayerVisibilityCoroutine());
                }
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                Debug.Log($"[{_stateMachine.gameObject.name}'s PlayerDetector]: Player exited trigger zone.");
                
                _isPlayerInTriggerZone = false;
                _playerTransformCache = null;
                _wasPlayerVisibleLastCheck = false;
                _hasInitiatedEngagement = false;
                
                // Notify state machine
                _stateMachine.NotifyPlayerInDetectionZone(false, null);
                OnPlayerExitedZone?.Invoke();

                // Handle NPCManager unregistration
                if (NPCManager.Instance != null && _stateMachine.HasSpottedPlayer)
                {
                    NPCManager.Instance.UnregisterOutOfRangeNpc(_stateMachine);
                }
                _stateMachine.HasSpottedPlayer = false;

                // Stop visibility checks
                StopVisibilityChecks();
            }
        }

        private IEnumerator CheckPlayerVisibilityCoroutine()
        {
            while (_isPlayerInTriggerZone && _shouldPerformVisibilityChecks)
            {
                // Don't check visibility if we're already engaged and don't need continuous checks
                if (_hasInitiatedEngagement && IsInEngagedState())
                {
                    yield return new WaitForSeconds(visibilityCheckInterval);
                    continue;
                }

                bool canSeePlayer = _stateMachine.IsPlayerVisible();

                // Only process visibility changes, not every frame
                if (canSeePlayer != _wasPlayerVisibleLastCheck)
                {
                    _wasPlayerVisibleLastCheck = canSeePlayer;
                    
                    if (canSeePlayer)
                    {
                        HandlePlayerBecameVisible();
                    }
                    else
                    {
                        HandlePlayerBecameInvisible();
                    }
                    
                    // Notify state machine of visibility change
                    OnPlayerVisibilityChanged?.Invoke(canSeePlayer);
                }

                yield return new WaitForSeconds(visibilityCheckInterval);
            }
            
            _visibilityCheckCoroutine = null;
        }

        private void HandlePlayerBecameVisible()
        {
            Debug.Log($"[{_stateMachine.gameObject.name}] PlayerDetector: Player became visible.");
            
            // Register with NPCManager if not already registered
            if (!_stateMachine.HasSpottedPlayer && NPCManager.Instance != null)
            {
                NPCManager.Instance.RegisterInRangeNpc(_stateMachine);
            }

            // Only initiate engagement if we haven't already done so
            if (!_hasInitiatedEngagement && !IsInEngagedState())
            {
                _hasInitiatedEngagement = true;
                _stateMachine.ConfirmPlayerVisibilityAndEngage();
            }
        }

        private void HandlePlayerBecameInvisible()
        {
            Debug.Log($"[{_stateMachine.gameObject.name}] PlayerDetector: Player became invisible.");
            
            // Notify state machine that player is no longer visible
            if (_stateMachine.HasSpottedPlayer)
            {
                _stateMachine.NotifyPlayerLostSight();
            }
        }

        private bool IsInEngagedState()
        {
            return _stateMachine.CurrentState is ChaseState ||
                   _stateMachine.CurrentState is AttackState ||
                   _stateMachine.CurrentState is W_PrepareAttackState ||
                   _stateMachine.CurrentState is W_StrikeState ||
                   _stateMachine.CurrentState is W_RecoverState ||
                   _stateMachine.CurrentState is W_CirclingState;
        }

        private void OnStateMachineStateChanged(IState oldState, IState newState)
        {
            // If transitioning from an engaged state back to patrol/idle, resume full visibility checks
            if (IsEngagedState(oldState) && !IsEngagedState(newState))
            {
                Debug.Log($"[{gameObject.name}] PlayerDetector: NPC disengaged, resuming full visibility checks.");
                _hasInitiatedEngagement = false;
                _shouldPerformVisibilityChecks = true;
                
                // Restart visibility checks if player is still in zone
                if (_isPlayerInTriggerZone && _visibilityCheckCoroutine == null)
                {
                    _visibilityCheckCoroutine = StartCoroutine(CheckPlayerVisibilityCoroutine());
                }
            }
        }

        private bool IsEngagedState(IState state)
        {
            return state is ChaseState ||
                   state is AttackState ||
                   state is W_PrepareAttackState ||
                   state is W_StrikeState ||
                   state is W_RecoverState ||
                   state is W_CirclingState;
        }

        public void StopVisibilityChecks()
        {
            if (_visibilityCheckCoroutine != null)
            {
                StopCoroutine(_visibilityCheckCoroutine);
                _visibilityCheckCoroutine = null;
                Debug.Log($"[{gameObject.name}] PlayerDetector: Stopped visibility checks.");
            }
        }

        public void ResumeVisibilityChecks()
        {
            _shouldPerformVisibilityChecks = true;
            if (_isPlayerInTriggerZone && _visibilityCheckCoroutine == null)
            {
                _visibilityCheckCoroutine = StartCoroutine(CheckPlayerVisibilityCoroutine());
            }
        }

        public void PauseVisibilityChecks()
        {
            _shouldPerformVisibilityChecks = false;
        }

        void OnDisable()
        {
            StopVisibilityChecks();
            
            // Clean up NPCManager registration
            if (_isPlayerInTriggerZone && NPCManager.Instance != null && 
                _stateMachine != null && _stateMachine.HasSpottedPlayer)
            {
                NPCManager.Instance.UnregisterOutOfRangeNpc(_stateMachine);
            }
        }

        // Public properties for debugging
        public bool IsPlayerInTriggerZone => _isPlayerInTriggerZone;
        public bool IsPlayerVisible => _wasPlayerVisibleLastCheck;
        public bool HasInitiatedEngagement => _hasInitiatedEngagement;
    }
}