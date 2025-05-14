// NPCStateMachine.cs (Excerpt from Listing 3-6 - showing key parts)
// This class inherits from StateMachine and is specific to NPC logic.

using System.Collections.Generic;
using System.Linq;
using AI.FSM.NPC.States;
using Combat.DamageSystem.Health;
using UnityEngine;

// For Action

namespace AI.FSM.NPC
{
    public class NPCStateMachine : StateMachine
    {
        [Tooltip("NPC's distance from Player to begin Chase.")]
        [SerializeField] private float visibleChaseDistance = 15f;
        [Tooltip("NPC's visibility angle for initiating chase")]
        [SerializeField] private float visibleChaseAngle = 70f;
        [Tooltip("NPC's attack distance to Player")]
        [SerializeField] private float attackDistance = 3f;
    
        public List<IState> states = new List<IState>(); // Holds all attached state components
        private IState startingState;
        private Transform player; // Reference to the Player transform
        private float rotSpeed = 1.5f;
        // private bool isPlayerDead; // Example of shared data

        // Properties for states to access
        public Transform Player => player;
        public Health NpcHealth { get; private set; } // Assuming Health component is present
        public IState CurrentState { get; private set; }

        // Event subscriptions for health changes
        void OnEnable()
        {
            NpcHealth = GetComponent<Health>();
            if (NpcHealth != null) {
                NpcHealth.OnNpcDeath += NpcDead;
                NpcHealth.OnHealthDepleted += HandleDamageTaken; // Renamed for clarity
            }
        }

        void OnDisable()
        {
            if (NpcHealth != null) {
                NpcHealth.OnNpcDeath -= NpcDead;
                NpcHealth.OnHealthDepleted -= HandleDamageTaken;
            }
        }

        private void Awake()
        {
            // Populate the list of states from components attached to this GameObject
            states = GetComponents<IState>().ToList();
            if (states.Count == 0)
            {
                Debug.LogError($"[NPCStateMachine {gameObject.name}] : No states found.");
                return;
            }

            player = GameObject.FindWithTag("Player").transform;
        
            if (player == null) Debug.LogError($"[NPCStateMachine {gameObject.name}] : Player not found.");
        
            // Cache other components like NavMeshAgent, Animator, etc.
        
        }

        void Start()
        {
            // Selects Idle state as default starting state
            startingState = states.Find(s => s.GetType() == typeof(IdleState));
            // if (!isPlayerDead) // Example condition
            if (startingState != null) SwitchState(startingState);
            else Debug.LogError("Starting state (IdleState) not found.");
        }

        // Commonly used Methods that can be accessed from any State
        public bool IsPlayerVisible()
        {
            if (player == null) return false;
            Vector3 npcToPlayerDir = player.position - this.transform.position;
            float angle = Vector3.Angle(npcToPlayerDir, this.transform.forward);
            if (npcToPlayerDir.magnitude < visibleChaseDistance && angle < visibleChaseAngle)
            {
                // Optional: Add a Raycast check for line of sight here
                return true;
            }
            return false;
        }

        public bool IsPlayerAttackable()
        {
            if (player == null) return false;
            Vector3 npcToPlayerDir = player.position - this.transform.position;
            return npcToPlayerDir.magnitude < attackDistance;
        }

        public void RotateToFacePlayer()
        {
            if (player == null) return;
            Vector3 npcToPlayerDir = player.position - this.transform.position;
            npcToPlayerDir.y = 0; // Keep rotation horizontal
            if (npcToPlayerDir == Vector3.zero) return; // Avoid LookRotation error if at same position
            this.transform.rotation =
                Quaternion.Slerp(this.transform.rotation, Quaternion.LookRotation(npcToPlayerDir), Time.deltaTime * rotSpeed);
        }

        private void NpcDead()
        {
            IState death = states.Find(s => s.GetType() == typeof(DeathState));
            if (death != null) SwitchState(death);
        }

        // Renamed from TakeDamage in source to avoid confusion with Health.TakeHealth
        private void HandleDamageTaken(string victimTag, string attackerTag) // objTag parameter from original Health.OnHealthDepleted
        {
            if (victimTag == gameObject.tag) // Ensure this event is for this NPC
            {
                if (NpcHealth.CurrentHealth < 50 && NpcHealth.CurrentHealth > 0) // Example threshold
                {
                    Debug.Log("NPC Health < 50% - Taking Cover");
                    IState takeCover = states.Find(s => s.GetType() == typeof(CoverState));
                    if (takeCover != null) SwitchState(takeCover);
                }
                else if (NpcHealth.CurrentHealth > 0) // Not dead, but health > 50%
                {
                    Debug.Log("NPC Has been Hit");
                    IState hit = states.Find(s => s.GetType() == typeof(HitState));
                    if (hit != null) SwitchState(hit);
                }
            }
        }
    }
}

