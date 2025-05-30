using AI.FSM;
using AI.FSM.NPC;
using UnityEngine;

namespace AI.NPC.Sensing
{
    /// <summary>
    /// Coordinates perception sensors and integrates with the refined NPCManager.
    /// Handles the bridge between perception events and FSM state changes.
    /// </summary>
    public class NPCPerceptionCoordinator : MonoBehaviour
    {
        [Header("Sensors")]
        [SerializeField] private TargetingSensor targetingSensor;
        [SerializeField] private ZoneDetector zoneDetector;
        [SerializeField] private VisionSensor visionSensor;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        // Cached components and state
        private IPerceptionAwareFSM _fsm;
        private WarriorStateMachine _warriorFSM;
        private Transform _player;
        private bool _playerCurrentlyVisible = false;
        private bool _playerInDetectionZone = false;

        // Events for decoupled communication
        public event System.Action<Transform> OnTargetAcquired;
        public event System.Action OnTargetLost;
        public event System.Action<bool> OnVisibilityChanged;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void InitializeComponents()
        {
            _fsm = GetComponentInParent<IPerceptionAwareFSM>();
            _warriorFSM = GetComponentInParent<WarriorStateMachine>();

            if (_fsm == null)
            {
                Debug.LogError($"{nameof(NPCPerceptionCoordinator)}: FSM interface not found in parent.");
                return;
            }

            if (_warriorFSM == null)
            {
                Debug.LogWarning($"{nameof(NPCPerceptionCoordinator)}: WarriorStateMachine not found. Some features may not work.");
            }

            ValidateSensorReferences();
        }

        private void ValidateSensorReferences()
        {
            if (targetingSensor == null)
                Debug.LogError($"{nameof(NPCPerceptionCoordinator)}: TargetingSensor reference missing!");
            
            if (zoneDetector == null)
                Debug.LogError($"{nameof(NPCPerceptionCoordinator)}: ZoneDetector reference missing!");
            
            if (visionSensor == null)
                Debug.LogError($"{nameof(NPCPerceptionCoordinator)}: VisionSensor reference missing!");
        }

        #endregion

        #region Event Management

        private void SubscribeToEvents()
        {
            if (zoneDetector != null)
            {
                zoneDetector.OnPlayerEnter += HandlePlayerEnterZone;
                zoneDetector.OnPlayerExit += HandlePlayerExitZone;
            }

            if (visionSensor != null)
            {
                visionSensor.OnVisibilityChanged += HandleVisibilityChanged;
            }

            // Subscribe to NPCManager events for coordination
            NPCManager.OnNPCRegistered += HandleNPCRegistered;
            NPCManager.OnNPCUnregistered += HandleNPCUnregistered;
        }

        private void UnsubscribeFromEvents()
        {
            if (zoneDetector != null)
            {
                zoneDetector.OnPlayerEnter -= HandlePlayerEnterZone;
                zoneDetector.OnPlayerExit -= HandlePlayerExitZone;
            }

            if (visionSensor != null)
            {
                visionSensor.OnVisibilityChanged -= HandleVisibilityChanged;
            }

            // Unsubscribe from NPCManager events
            NPCManager.OnNPCRegistered -= HandleNPCRegistered;
            NPCManager.OnNPCUnregistered -= HandleNPCUnregistered;
        }

        #endregion

        #region Event Handlers

        private void HandlePlayerEnterZone(Transform player)
        {
            _player = player;
            _playerInDetectionZone = true;

            Log($"Player entered detection zone");

            // Configure targeting sensor
            if (targetingSensor != null)
            {
                targetingSensor.SetTarget(player);
            }

            // Configure vision sensor with line-of-sight evaluator
            if (visionSensor != null)
            {
                visionSensor.VisibilityEvaluator = EvaluatePlayerVisibility;
                visionSensor.StartChecking();
            }

            // Notify FSM about zone entry
            _fsm?.NotifyPlayerInDetectionZone(true, player);
            OnTargetAcquired?.Invoke(player);
        }

        private void HandlePlayerExitZone()
        {
            Log($"Player exited detection zone");

            _playerInDetectionZone = false;

            // Clear targeting
            if (targetingSensor != null)
            {
                targetingSensor.SetTarget(null);
            }

            // Stop vision checking
            if (visionSensor != null)
            {
                visionSensor.VisibilityEvaluator = null;
                visionSensor.StopChecking();
            }

            // Handle visibility loss if currently visible
            if (_playerCurrentlyVisible)
            {
                HandleVisibilityLost();
            }

            // Notify FSM about zone exit
            _fsm?.NotifyPlayerInDetectionZone(false, null);
            OnTargetLost?.Invoke();

            _player = null;
        }

        private void HandleVisibilityChanged(bool visible)
        {
            if (_playerCurrentlyVisible == visible)
                return; // No change

            _playerCurrentlyVisible = visible;
            Log($"Player visibility changed to: {visible}");

            if (visible)
            {
                HandleVisibilityGained();
            }
            else
            {
                HandleVisibilityLost();
            }

            OnVisibilityChanged?.Invoke(visible);
        }

        private void HandleVisibilityGained()
        {
            Log("Player became visible - registering with NPCManager");

            // Register with NPCManager for engagement coordination
            if (_warriorFSM != null && NPCManager.Instance != null)
            {
                NPCManager.Instance.RegisterEngagedNPC(_warriorFSM);
            }

            // Notify FSM to engage
            _fsm?.ConfirmPlayerVisibilityAndEngage();
        }

        private void HandleVisibilityLost()
        {
            Log("Player lost from sight - unregistering from NPCManager");

            // Unregister from NPCManager
            if (_warriorFSM != null && NPCManager.Instance != null)
            {
                NPCManager.Instance.UnregisterEngagedNPC(_warriorFSM);
            }

            // Notify FSM about sight loss
            _fsm?.NotifyPlayerLostSight();
        }

        private void HandleNPCRegistered(WarriorStateMachine npc)
        {
            if (npc == _warriorFSM)
            {
                Log("This NPC was registered for engagement");
            }
        }

        private void HandleNPCUnregistered(WarriorStateMachine npc)
        {
            if (npc == _warriorFSM)
            {
                Log("This NPC was unregistered from engagement");
            }
        }

        #endregion

        #region Visibility Evaluation

        /// <summary>
        /// Evaluates whether the player is currently visible based on targeting sensor.
        /// </summary>
        private bool EvaluatePlayerVisibility()
        {
            if (targetingSensor == null || _player == null)
                return false;

            // Player must be in detection zone AND have line of sight
            return _playerInDetectionZone && 
                   targetingSensor.CurrentTarget != null && 
                   targetingSensor.HasLineOfSight();
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Gets the current target transform.
        /// </summary>
        public Transform GetCurrentTarget()
        {
            return _player;
        }

        /// <summary>
        /// Checks if the player is currently visible.
        /// </summary>
        public bool IsPlayerCurrentlyVisible()
        {
            return _playerCurrentlyVisible;
        }

        /// <summary>
        /// Checks if the player is in the detection zone.
        /// </summary>
        public bool IsPlayerInDetectionZone()
        {
            return _playerInDetectionZone;
        }

        /// <summary>
        /// Forces a visibility check (useful for debugging or manual triggers).
        /// </summary>
        [ContextMenu("Force Visibility Check")]
        public void ForceVisibilityCheck()
        {
            if (visionSensor != null && visionSensor.IsChecking)
            {
                visionSensor.ForceCheck();
            }
        }

        /// <summary>
        /// Manually triggers player spotted behavior (for external systems).
        /// </summary>
        public void TriggerPlayerSpotted()
        {
            if (_player != null && !_playerCurrentlyVisible)
            {
                Log("Manually triggering player spotted");
                HandleVisibilityChanged(true);
            }
        }

        #endregion

        #region Utilities

        private void Log(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[{gameObject.name} - PerceptionCoordinator] {message}");
            }
        }

        #endregion

        #region Debug & Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
                return;

            // Draw connection to current target
            if (_player != null && _playerCurrentlyVisible)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, _player.position);
                Gizmos.DrawWireSphere(_player.position, 0.5f);
            }
            else if (_player != null && _playerInDetectionZone)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, _player.position);
                Gizmos.DrawWireSphere(_player.position, 0.3f);
            }
        }

        #endregion
    }
}