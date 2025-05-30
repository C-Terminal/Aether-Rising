using System.Collections.Generic;
using System.Linq;
using AI.FSM.NPC.States;
using AI.FSM.Warrior.States;
using UnityEngine;

namespace AI.FSM.NPC
{
    /// <summary>
    /// Centralized manager for NPC coordination, attack timing, and group behaviors.
    /// Integrates with the new perception system for improved NPC management.
    /// </summary>
    public class NPCManager : MonoBehaviour
    {
        [Header("Attack Coordination")]
        [Tooltip("Range within which NPCs will be alerted by others")]
        [SerializeField] private float alertRange = 15f;
        
        [Tooltip("Time range (min, max) in seconds before a new NPC attacks Player")]
        [SerializeField] private Vector2 attackTimeRange = new Vector2(2.0f, 4.0f);
        
        [Tooltip("Maximum number of NPCs that can attack simultaneously")]
        [SerializeField] private int maxSimultaneousAttackers = 1;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        // Event system for decoupled communication
        public static event System.Action<WarriorStateMachine> OnNPCRegistered;
        public static event System.Action<WarriorStateMachine> OnNPCUnregistered;
        public static event System.Action<WarriorStateMachine> OnAttackerSelected;
        public static event System.Action<WarriorStateMachine> OnAttackerCleared;

        // NPC tracking with better encapsulation
        private readonly HashSet<WarriorStateMachine> _npcsInRange = new HashSet<WarriorStateMachine>();
        private readonly List<WarriorStateMachine> _npcsInLevel = new List<WarriorStateMachine>();
        
        // Attack coordination with support for multiple attackers
        private readonly HashSet<WarriorStateMachine> _currentAttackers = new HashSet<WarriorStateMachine>();
        private float _attackTimer;

        // Singleton with lazy initialization
        private static NPCManager _instance;
        public static NPCManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<NPCManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[NPCManager]");
                        _instance = go.AddComponent<NPCManager>();
                    }
                }
                return _instance;
            }
        }

        // Public read-only properties
        public IReadOnlyCollection<WarriorStateMachine> NPCsInRange => _npcsInRange;
        public IReadOnlyCollection<WarriorStateMachine> NPCsInLevel => _npcsInLevel;
        public IReadOnlyCollection<WarriorStateMachine> CurrentAttackers => _currentAttackers;
        public int ActiveNPCCount => _npcsInRange.Count;
        public bool HasAvailableAttackSlots => _currentAttackers.Count < maxSimultaneousAttackers;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
            ResetAttackTimer();
        }

        private void Start()
        {
            PopulateNPCsInLevel();
        }

        private void OnEnable()
        {
            WarriorStateMachine.OnPlayerSpotted += HandlePlayerSpotted;
        }

        private void OnDisable()
        {
            WarriorStateMachine.OnPlayerSpotted -= HandlePlayerSpotted;
        }

        private void Update()
        {
            UpdateAttackCoordination();
        }

        #endregion

        #region NPC Registration (Called by Perception System)

        /// <summary>
        /// Registers an NPC as actively engaging the player.
        /// Called by NPCPerceptionCoordinator when player becomes visible.
        /// </summary>
        public void RegisterEngagedNPC(WarriorStateMachine npc)
        {
            if (!IsValidNPC(npc))
            {
                LogWarning($"Attempted to register invalid NPC: {GetNPCName(npc)}");
                return;
            }

            if (_npcsInRange.Add(npc))
            {
                npc.HasSpottedPlayer = true;
                Log($"Registered {npc.name} as engaged. Total engaged: {_npcsInRange.Count}");
                OnNPCRegistered?.Invoke(npc);
            }
        }

        /// <summary>
        /// Unregisters an NPC from active engagement.
        /// Called by NPCPerceptionCoordinator when player is no longer visible.
        /// </summary>
        public void UnregisterEngagedNPC(WarriorStateMachine npc)
        {
            if (npc == null) return;

            if (_npcsInRange.Remove(npc))
            {
                npc.HasSpottedPlayer = false;
                
                // Clear from attackers if currently attacking
                if (_currentAttackers.Remove(npc))
                {
                    Log($"Cleared {npc.name} from attackers due to disengagement");
                    OnAttackerCleared?.Invoke(npc);
                    ResetAttackTimer(0.5f); // Allow new attacker sooner
                }

                Log($"Unregistered {npc.name} from engagement. Total engaged: {_npcsInRange.Count}");
                OnNPCUnregistered?.Invoke(npc);
            }
        }

        #endregion

        #region Attack Coordination

        /// <summary>
        /// Requests permission for an NPC to attack.
        /// More flexible approach supporting multiple attack patterns.
        /// </summary>
        public bool RequestAttackPermission(WarriorStateMachine npc)
        {
            if (!IsValidAttackRequest(npc))
                return false;

            // If already attacking, maintain permission
            if (_currentAttackers.Contains(npc))
                return true;

            // Check if attack slots are available
            if (!HasAvailableAttackSlots)
            {
                Log($"{npc.name} denied attack permission - no slots available");
                return false;
            }

            // Grant permission and add to attackers
            _currentAttackers.Add(npc);
            Log($"{npc.name} granted attack permission. Active attackers: {_currentAttackers.Count}");
            OnAttackerSelected?.Invoke(npc);
            return true;
        }

        /// <summary>
        /// Clears an NPC from the attacking slot.
        /// Called when attack completes or is interrupted.
        /// </summary>
        public void ReleaseAttackPermission(WarriorStateMachine npc)
        {
            if (npc != null && _currentAttackers.Remove(npc))
            {
                Log($"Released attack permission for {npc.name}. Active attackers: {_currentAttackers.Count}");
                OnAttackerCleared?.Invoke(npc);
                
                // Reset timer to allow new attackers
                if (_npcsInRange.Count > _currentAttackers.Count)
                {
                    ResetAttackTimer(0.5f);
                }
            }
        }

        /// <summary>
        /// Checks if any NPCs are currently attacking.
        /// </summary>
        public bool IsAnyNPCAttacking()
        {
            // Clean up invalid attackers
            _currentAttackers.RemoveWhere(npc => !IsValidNPC(npc) || !_npcsInRange.Contains(npc));
            return _currentAttackers.Count > 0;
        }

        /// <summary>
        /// Gets the primary attacker (for legacy compatibility).
        /// </summary>
        public WarriorStateMachine GetPrimaryAttacker()
        {
            return _currentAttackers.FirstOrDefault();
        }

        #endregion

        #region Alert System

        /// <summary>
        /// Handles player spotted alerts from the perception system.
        /// Improved to work with the new perception coordinator.
        /// </summary>
        private void HandlePlayerSpotted(Vector3 alertPosition)
        {
            Log($"Player spotted alert at {alertPosition}. Alerting nearby NPCs.");
            
            var nearbyNPCs = GetNPCsInRange(alertPosition, alertRange)
                .Where(npc => !npc.HasSpottedPlayer && !npc.IsDead)
                .ToList();

            foreach (var npc in nearbyNPCs)
            {
                Log($"Alerting {npc.name} to player presence");
                npc.InitiateAttackOnPlayer();
            }
        }

        /// <summary>
        /// Gets NPCs within a specific range of a position.
        /// </summary>
        private IEnumerable<WarriorStateMachine> GetNPCsInRange(Vector3 position, float range)
        {
            return _npcsInLevel.Where(npc => 
                npc != null && 
                npc.enabled && 
                Vector3.Distance(npc.transform.position, position) <= range);
        }

        #endregion

        #region Internal Logic

        private void InitializeSingleton()
        {
            if (_instance == null)
            {
                _instance = this;
                // Uncomment if manager should persist across scenes
                // DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateAttackCoordination()
        {
            if (_npcsInRange.Count == 0)
            {
                HandleNoEngagedNPCs();
                return;
            }

            // Clean up invalid attackers
            CleanupInvalidAttackers();

            // Manage attack timing if slots are available
            if (HasAvailableAttackSlots && !IsAttackTimerActive())
            {
                _attackTimer -= Time.deltaTime;
                
                if (_attackTimer <= 0)
                {
                    SelectNextAttacker();
                    ResetAttackTimer();
                }
            }
        }

        private void HandleNoEngagedNPCs()
        {
            if (_currentAttackers.Count > 0)
            {
                Log("No NPCs engaged - clearing all attackers");
                _currentAttackers.Clear();
            }
            ResetAttackTimer();
        }

        private void CleanupInvalidAttackers()
        {
            var invalidAttackers = _currentAttackers
                .Where(npc => !IsValidNPC(npc) || !_npcsInRange.Contains(npc))
                .ToList();

            foreach (var invalid in invalidAttackers)
            {
                _currentAttackers.Remove(invalid);
                Log($"Removed invalid attacker: {GetNPCName(invalid)}");
            }
        }

        private bool IsAttackTimerActive()
        {
            return _currentAttackers.Count >= maxSimultaneousAttackers;
        }

        private void SelectNextAttacker()
        {
            var eligibleNPCs = GetEligibleAttackers();
            
            if (eligibleNPCs.Count == 0)
            {
                Log("No eligible attackers available");
                ResetAttackTimer(0.5f);
                return;
            }

            var selectedNPC = eligibleNPCs[Random.Range(0, eligibleNPCs.Count)];
            
            // The NPC will request permission when ready to attack
            Log($"Selected {selectedNPC.name} as potential attacker");
        }

        private List<WarriorStateMachine> GetEligibleAttackers()
        {
            return _npcsInRange
                .Where(npc => IsEligibleForAttack(npc))
                .ToList();
        }

        private bool IsEligibleForAttack(WarriorStateMachine npc)
        {
            return IsValidNPC(npc) && 
                   !_currentAttackers.Contains(npc) &&
                   IsInAttackableState(npc);
        }

        private bool IsInAttackableState(WarriorStateMachine npc)
        {
            // return npc.CurrentState is W_CirclingState || 
            //        npc.CurrentState is ChaseState ||
            //     npc.CurrentState is IdleState;

            return true;
        }

        private void ResetAttackTimer(float specificTime = -1f)
        {
            _attackTimer = specificTime >= 0 ? specificTime : 
                Random.Range(attackTimeRange.x, attackTimeRange.y);
        }

        private void PopulateNPCsInLevel()
        {
            _npcsInLevel.Clear();
            
            var npcObjects = GameObject.FindGameObjectsWithTag("NPC");
            
            foreach (var npcObj in npcObjects)
            {
                var warrior = npcObj.GetComponent<WarriorStateMachine>();
                if (warrior != null)
                {
                    _npcsInLevel.Add(warrior);
                }
                else
                {
                    LogWarning($"GameObject {npcObj.name} tagged NPC but missing WarriorStateMachine");
                }
            }
            
            Log($"Found {_npcsInLevel.Count} NPCs in level");
        }

        #endregion

        #region Validation & Utilities

        private bool IsValidNPC(WarriorStateMachine npc)
        {
            return npc != null && npc.enabled && !npc.IsSelfDead;
        }

        private bool IsValidAttackRequest(WarriorStateMachine npc)
        {
            if (!IsValidNPC(npc))
            {
                LogWarning($"Invalid attack request from {GetNPCName(npc)}");
                return false;
            }

            if (!_npcsInRange.Contains(npc))
            {
                LogWarning($"Attack request from non-engaged NPC: {npc.name}");
                return false;
            }

            return true;
        }

        private string GetNPCName(WarriorStateMachine npc)
        {
            return npc != null ? npc.name : "null";
        }

        private void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[NPCManager] {message}");
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[NPCManager] {message}");
        }

        #endregion

        #region Legacy Support (For Backward Compatibility)

        [System.Obsolete("Use RegisterEngagedNPC instead")]
        public void RegisterInRangeNpc(WarriorStateMachine npc)
        {
            RegisterEngagedNPC(npc);
        }

        [System.Obsolete("Use UnregisterEngagedNPC instead")]
        public void UnregisterOutOfRangeNpc(WarriorStateMachine npc)
        {
            UnregisterEngagedNPC(npc);
        }

        [System.Obsolete("Use RequestAttackPermission instead")]
        public void SetAttackingNPC(StateMachineNew npc)
        {
            if (npc is WarriorStateMachine warrior)
                RequestAttackPermission(warrior);
        }

        [System.Obsolete("Use GetPrimaryAttacker instead")]
        public FSM.StateMachineNew GetAttackingNPC()
        {
            return GetPrimaryAttacker();
        }

        [System.Obsolete("Use ReleaseAttackPermission instead")]
        public void ClearAttackingNPC(FSM.StateMachineNew npc)
        {
            if (npc is WarriorStateMachine warrior)
                ReleaseAttackPermission(warrior);
        }

        #endregion
    }
}