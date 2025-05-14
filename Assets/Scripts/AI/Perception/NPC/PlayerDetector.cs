using System.Collections;
using AI.FSM.NPC;
using UnityEngine;

// Required for IEnumerator

namespace AI.Perception.NPC
{
    /// <summary>
    /// Detects the player entering/exiting a trigger zone and informs the NPC's StateMachine.
    /// It then collaborates with the StateMachine to determine actual visibility and register/unregister
    /// with the NPCManager.
    /// Attach this to a child GameObject of the NPC, equipped with a trigger Collider.
    /// </summary>
    public class PlayerDetector : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("How often (in seconds) to perform detailed visibility checks when player is in the trigger zone.")]
        [SerializeField] private float visibilityCheckInterval = 0.2f;
        [Tooltip("Tag of the player GameObject.")]
        [SerializeField] private string playerTag = "Player";

        private WarriorStateMachine _stateMachine; // Reference to the parent NPC's StateMachine
        private Coroutine _visibilityCheckCoroutine;
        private bool _isPlayerInTriggerZone = false;
        private Transform _playerTransformCache; // Cache player transform when in zone

        void Awake()
        {
            _stateMachine = GetComponentInParent<WarriorStateMachine>();
            if (_stateMachine == null)
            {
                Debug.LogError($"[{gameObject.name}] PlayerDetector: WarriorStateMachine not found on parent or self! Detection will not work.", this);
                enabled = false;
                return;
            }

            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                Debug.LogError($"[{gameObject.name}] PlayerDetector: No Collider component found on this GameObject. Detection will not work.", this);
                enabled = false;
                return;
            }
            if (!col.isTrigger)
            {
                Debug.LogWarning($"[{gameObject.name}] PlayerDetector: Collider is not set to 'Is Trigger'. Please enable 'Is Trigger' for detection.", this);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                Debug.Log($"[{_stateMachine.gameObject.name}'s PlayerDetector]: Player '{other.name}' ENTERED trigger zone.");
                _isPlayerInTriggerZone = true;
                _playerTransformCache = other.transform; // Cache for visibility checks
                _stateMachine.NotifyPlayerInDetectionZone(true, _playerTransformCache);


                // Start checking for actual visibility if not already doing so
                if (_visibilityCheckCoroutine == null)
                {
                    _visibilityCheckCoroutine = StartCoroutine(CheckPlayerVisibilityCoroutine());
                }
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                Debug.Log($"[{_stateMachine.gameObject.name}'s PlayerDetector]: Player '{other.name}' EXITED trigger zone.");
                _isPlayerInTriggerZone = false;
                _playerTransformCache = null;
                _stateMachine.NotifyPlayerInDetectionZone(false, null);


                // If the NPC was registered as "in range" with NPCManager, unregister it.
                // This happens regardless of visibility, as they are out of the broad detection zone.
                if (NPCManager.Instance != null && _stateMachine.HasSpottedPlayer) // Check HasSpottedPlayer to see if it was ever registered
                {
                    NPCManager.Instance.UnregisterOutOfRangeNpc(_stateMachine);
                }
                _stateMachine.HasSpottedPlayer = false; // Reset this flag on the state machine

                // Stop the visibility check coroutine
                if (_visibilityCheckCoroutine != null)
                {
                    StopCoroutine(_visibilityCheckCoroutine);
                    _visibilityCheckCoroutine = null;
                }
            }
        }

        private IEnumerator CheckPlayerVisibilityCoroutine()
        {
            // Debug.Log($"[{_stateMachine.gameObject.name}'s PlayerDetector]: Starting visibility checks.");
            while (_isPlayerInTriggerZone)
            {
                if (_stateMachine.Player == null && _playerTransformCache != null)
                {
                    // If state machine's player ref is null (e.g. on first spot), set it.
                    // This assumes WarriorStateMachine.Player can be set or is primarily for read by states.
                    // A better approach might be for PlayerDetector to pass player transform to IsPlayerVisible.
                }

                // Ask the state machine to perform its detailed visibility check
                // The WarriorStateMachine.IsPlayerVisible() should ideally take the target as a parameter
                // or use its internally set Player transform (which PlayerDetector can help set).
                bool canSeePlayer = _stateMachine.IsPlayerVisible(); // This method should use _playerTransformCache or stateMachine.Player

                if (canSeePlayer)
                {
                    // If player is visible AND this NPC wasn't previously marked as spotting the player (for NPCManager registration)
                    if (!_stateMachine.HasSpottedPlayer && NPCManager.Instance != null)
                    {
                        NPCManager.Instance.RegisterInRangeNpc(_stateMachine);
                        // _stateMachine.HasSpottedPlayer is set to true by NPCManager.RegisterInRangeNpc
                        // No need to call stateMachine.AlertNearbyNPCs() here, that's usually done
                        // by the state machine itself once it decides to transition to an aggressive state (e.g. Chase)
                    }

                    // If the FSM isn't already in an aggressive state, it might decide to switch now.
                    // This logic is typically within the WarriorStateMachine's current state (e.g. Idle or Wander update)
                    // reacting to _stateMachine.HasSpottedPlayer or a direct notification.
                    // For example, WarriorStateMachine could have a method: OnPlayerConfirmedVisible()
                    _stateMachine.ConfirmPlayerVisibilityAndEngage();


                }
                else
                {
                    // If player was previously spotted (registered with NPCManager) but is no longer visible (e.g. behind obstacle)
                    if (_stateMachine.HasSpottedPlayer && NPCManager.Instance != null)
                    {
                        // Option: Do we unregister from NPCManager immediately if LOS is broken but still in trigger?
                        // For "free-flow" combat, often NPCs remain "in combat" and aware if player is in zone but temporarily hidden.
                        // They might switch to a "Search" state.
                        // Let's assume for now they remain registered with NPCManager as long as in trigger and initially spotted.
                        // Unregistration primarily happens on OnTriggerExit.
                        // However, the StateMachine itself should react (e.g. switch from Chase to Search/Wander)
                        _stateMachine.NotifyPlayerLostSight(); // Inform state machine player is not visible right now
                    }
                }
                yield return new WaitForSeconds(visibilityCheckInterval);
            }
            _visibilityCheckCoroutine = null; // Clear coroutine reference when loop exits
            // Debug.Log($"[{_stateMachine.gameObject.name}'s PlayerDetector]: Stopped visibility checks.");
        }

        void OnDisable() // Or OnDestroy
        {
            // Ensure coroutine is stopped if this object is disabled/destroyed
            if (_visibilityCheckCoroutine != null)
            {
                StopCoroutine(_visibilityCheckCoroutine);
                _visibilityCheckCoroutine = null;
            }
            // If it was tracking the player, ensure it's unregistered
            if (_isPlayerInTriggerZone && NPCManager.Instance != null && _stateMachine != null && _stateMachine.HasSpottedPlayer)
            {
                NPCManager.Instance.UnregisterOutOfRangeNpc(_stateMachine);
            }
        }
    }
}
