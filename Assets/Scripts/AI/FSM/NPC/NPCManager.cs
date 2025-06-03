using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AI.FSM.NPC
{
    public enum GlobalAlertState
    {
        Calm,
        Suspicious,
        Alert,
        Combat
    }

    public class NPCManager : MonoBehaviour
    {
        [Header("Attack Coordination")]
        [SerializeField] private float alertRange = 15f;
        [SerializeField] private Vector2 attackTimeRange = new Vector2(2.0f, 4.0f);
        [SerializeField] private int maxSimultaneousAttackers = 1;

        [Header("Global Alert State")]
        [SerializeField] private GlobalAlertState initialAlertState = GlobalAlertState.Calm;
        public GlobalAlertState CurrentGlobalAlertState { get; private set; }

        [SerializeField] private bool enableDebugLogs = true;

        // Shared player location memory
        private Vector3? _lastKnownPlayerPosition;
        public Vector3? LastKnownPlayerPosition => _lastKnownPlayerPosition;
        public bool IsPlayerGloballySpotted => _lastKnownPlayerPosition.HasValue;

        // NPC tracking
        private readonly HashSet<WarriorStateMachine> _npcsInRange = new();
        private readonly List<WarriorStateMachine> _npcsInLevel = new();
        private readonly HashSet<WarriorStateMachine> _currentAttackers = new();
        private readonly HashSet<WarriorStateMachine> _invitedAttackers = new();

        public static event Action<WarriorStateMachine> OnAttackerSelected;
        
        private float _attackTimer;

        // Singleton
        private static NPCManager _instance;
        public static NPCManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<NPCManager>();
                    if (_instance == null)
                        _instance = new GameObject("[NPCManager]").AddComponent<NPCManager>();
                }
                return _instance;
            }
        }

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
            SetGlobalAlertState(initialAlertState);
            ResetAttackTimer();
        }

        private void Start() => PopulateNPCsInLevel();

        private void OnEnable()
        {
            WarriorStateMachine.OnPlayerSpotted += HandlePlayerSpotted;
            NPCManager.OnAttackerSelected += HandleAttackerSelected;
        }
            

        private void OnDisable()
        {
            WarriorStateMachine.OnPlayerSpotted -= HandlePlayerSpotted;
            NPCManager.OnAttackerSelected -= HandleAttackerSelected;
        }
            
        

        private void Update() =>
            UpdateAttackCoordination();

        #endregion

        #region Alert & Shared Memory

        public void ReportPlayerSeen(Vector3 position)
        {
            _lastKnownPlayerPosition = position;

            Log($"[Global Alert] Player spotted at {position}");

            SetGlobalAlertState(GlobalAlertState.Alert);

            foreach (var npc in _npcsInLevel)
            {
                if (!npc.HasSpottedPlayer && !npc.IsDead)
                {
                    npc.SetGroupAwareness(position);
                }
            }
        }

        public void ClearGlobalPlayerMemory()
        {
            _lastKnownPlayerPosition = null;
            SetGlobalAlertState(GlobalAlertState.Calm);
        }

        public void SetGlobalAlertState(GlobalAlertState newState)
        {
            if (CurrentGlobalAlertState == newState) return;

            CurrentGlobalAlertState = newState;

            foreach (var npc in _npcsInLevel)
            {
                if (npc != null)
                    npc.SetAlertLevel(ConvertToNPCAlert(newState));
            }

            Log($"[Global Alert] State changed to {newState}");
        }
        public enum AIAlertLevel
        {
            Idle,
            Suspicious,
            Alert,
            Combat
        }
        

        private AIAlertLevel ConvertToNPCAlert(GlobalAlertState global)
        {
            return global switch
            {
                GlobalAlertState.Calm => AIAlertLevel.Idle,
                GlobalAlertState.Suspicious => AIAlertLevel.Suspicious,
                GlobalAlertState.Alert => AIAlertLevel.Alert,
                GlobalAlertState.Combat => AIAlertLevel.Combat,
                _ => AIAlertLevel.Idle
            };
        }

        private void HandlePlayerSpotted(Vector3 position)
        {
            ReportPlayerSeen(position);
        }

        #endregion

        #region Registration API

        public void RegisterEngagedNPC(WarriorStateMachine npc)
        {
            if (!IsValidNPC(npc)) return;

            if (_npcsInRange.Add(npc))
            {
                npc.HasSpottedPlayer = true;
                Log($"Registered: {npc.name} | Engaged: {_npcsInRange.Count}");
            }
        }

        public void UnregisterEngagedNPC(WarriorStateMachine npc)
        {
            if (npc == null) return;

            if (_npcsInRange.Remove(npc))
            {
                npc.HasSpottedPlayer = false;

                if (_currentAttackers.Remove(npc))
                {
                    Log($"Removed attacker: {npc.name}");
                    ResetAttackTimer(0.5f);
                }

                Log($"Unregistered: {npc.name} | Engaged: {_npcsInRange.Count}");
            }
        }

        #endregion

        #region Attack System

        public bool RequestAttackPermission(WarriorStateMachine npc)
        {
            if (!IsValidAttackRequest(npc)) return false;

            if (_currentAttackers.Contains(npc)) return true;
            if (!HasAvailableAttackSlots()) return false;

            _currentAttackers.Add(npc);
            _invitedAttackers.Remove(npc); // ← Clean up
            OnAttackerSelected?.Invoke(npc);

            Log($"Granted attack permission to {npc.name}");
            return true;
        }

        public void ReleaseAttackPermission(WarriorStateMachine npc)
        {
            if (_currentAttackers.Remove(npc))
            {
                Log($"Attack permission released: {npc.name}");
                ResetAttackTimer(0.5f);
            }
        }

        private bool HasAvailableAttackSlots() =>
            _currentAttackers.Count < maxSimultaneousAttackers;

        private void UpdateAttackCoordination()
        {
            if (_npcsInRange.Count == 0)
            {
                _currentAttackers.Clear();
                ResetAttackTimer();
                return;
            }

            _currentAttackers.RemoveWhere(n => !IsValidNPC(n) || !_npcsInRange.Contains(n));

            if (HasAvailableAttackSlots() && _attackTimer <= 0f)
            {
                SelectNextAttacker();
                ResetAttackTimer();
            }

            _attackTimer -= Time.deltaTime;
        }

        private void SelectNextAttacker()
        {
            var eligible = _npcsInRange
                .Where(n => IsValidNPC(n) && !_currentAttackers.Contains(n))
                .ToList();

            if (eligible.Count == 0) return;

            var npc = eligible[Random.Range(0, eligible.Count)];

            _invitedAttackers.Add(npc); // Mark as invited
            Log($"[NPCManager] Selected next attacker: {npc.name}");

            npc.ReceiveAttackInvitation(); // <-- NEW method you'll add
        }

        private void ResetAttackTimer(float overrideTime = -1f)
        {
            _attackTimer = overrideTime >= 0
                ? overrideTime
                : Random.Range(attackTimeRange.x, attackTimeRange.y);
        }

        private void HandleAttackerSelected(WarriorStateMachine selected)
        {
            if (selected != this) return;
            Debug.Log($"[{name}] Confirmed as next attacker.");
            // Optional: trigger FSM feedback or animation flag
        }
        
        #endregion

        #region NPC Initialization

        private void PopulateNPCsInLevel()
        {
            _npcsInLevel.Clear();

            foreach (var npc in GameObject.FindGameObjectsWithTag("NPC"))
            {
                var fsm = npc.GetComponent<WarriorStateMachine>();
                if (fsm != null)
                    _npcsInLevel.Add(fsm);
            }

            Log($"Found {_npcsInLevel.Count} NPCs in level.");
        }

        #endregion

        #region Utils

        private void InitializeSingleton()
        {
            if (_instance == null)
            {
                _instance = this;
                // Optional: DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Returns a list of currently engaged NPCs within a radius of the given position,
        /// excluding the requesting NPC (optional).
        /// </summary>
        public List<WarriorStateMachine> GetNearbyEngagedNPCs(Vector3 position, float radius, StateMachineNew exclude = null)
        {
            return _npcsInRange
                .Where(npc => npc != null && npc != exclude && !npc.IsDead && Vector3.Distance(npc.transform.position, position) <= radius)
                .ToList();
        }

        
        public WarriorStateMachine GetPrimaryAttacker()
        {
            if (_npcsInRange.Count == 0)
                return null;

            var player = GameObject.FindWithTag("Player")?.transform;
            if (player == null)
            {
                Debug.LogWarning("NPCManager: Player not found for GetPrimaryAttacker.");
                return null;
            }

            WarriorStateMachine closest = null;
            float closestDistance = Mathf.Infinity;

            foreach (var npc in _npcsInRange)
            {
                if (npc == null || npc.IsDead || !npc.IsPlayerVisible()) continue;

                float distance = Vector3.Distance(npc.transform.position, player.position);
                if (distance < closestDistance)
                {
                    closest = npc;
                    closestDistance = distance;
                }
            }

            return closest;
        }


        private bool IsValidNPC(WarriorStateMachine npc) =>
            npc != null && npc.enabled && !npc.IsDead;

        private bool IsValidAttackRequest(WarriorStateMachine npc) =>
            IsValidNPC(npc) && _npcsInRange.Contains(npc);

        private void Log(string msg)
        {
            if (enableDebugLogs)
                Debug.Log($"[NPCManager] {msg}");
        }
        
        

        #endregion
    }
}
