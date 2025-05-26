// --- Base StateMachine.cs (Conceptual) ---

using System.Collections.Generic;
using System.Linq;
using Animation.AnimControllers;
using Characters.NPC;
using Combat.DamageSystem.Health;
using UnityEngine;
using UnityEngine.AI;

namespace AI.FSM
{
    // For CharacterAnimator

    public abstract class StateMachineNew : MonoBehaviour
    {
        // Properties that states will need, to be implemented/cached by derived classes
        public abstract Transform Player { get; set; }
        public abstract NavMeshAgent Agent { get; set; }
        public abstract CharacterAnimator CharAnim { get; set; }
        public abstract NPCController NpcCtrl { get; } // Renamed for clarity
        public abstract Health NpcHealth { get;  set;  }
        
        public abstract NPCController NpcController { get;  set;  }

        public List<IState> states = new List<IState>();
        public IState CurrentState { get; protected set; }
        public IState PreviousState { get; protected set; }

        public bool IsPlayerDead { get; set; }
        public bool IsSelfDead => NpcHealth != null && NpcHealth.IsDead;


        protected virtual void Awake()
        {
            // Derived classes will cache their specific components first
            // Then, we initialize states
            InitializeStates();
        }

        // public virtual void NotifyPlayerInDetectionZone(bool isInZone, Transform playerTransformIfInZone)
        // {
        //     
        // }

        protected void InitializeStates()
        {
            states = GetComponents<IState>().ToList();
            if (states.Count == 0)
            {
                Debug.LogError($"[{gameObject.name}] StateMachine: No IState components found!", this);
                return;
            }

            foreach (var state in states)
            {
                // Using a common interface for states that need initialization
                if (state is IInitializableState initializable)
                {
                    initializable.Initialize(this);
                }
            }
        }

        public virtual void SwitchState(IState newState)
        {
            if (newState == null || newState == CurrentState) return;
            CurrentState?.OnStateExit();
            PreviousState = CurrentState;
            CurrentState = newState;
            Debug.Log($"[{gameObject.name}] Switched State: {PreviousState?.GetType().Name} -> {CurrentState?.GetType().Name}");
            CurrentState.OnStateEnter();
        }

        protected virtual void Update()
        {
            CurrentState?.OnStateUpdate(Time.deltaTime);
        }

        public T FindState<T>() where T : class, IState
        {
            return states.FirstOrDefault(s => s is T) as T;
        }
        
        // --- Abstract methods for common AI functionalities ---
        /// <summary>
        /// Checks if the player is currently considered visible by this state machine.
        /// Implementation will be specific to the derived state machine (e.g., WarriorStateMachine).
        /// </summary>
        public abstract bool IsPlayerVisible();

        /// <summary>
        /// Checks if the player is currently within attackable range for this state machine.
        /// </summary>
        public abstract bool IsPlayerAttackable();

        /// <summary>
        /// Commands this state machine's character to rotate towards the player.
        /// </summary>
        public abstract void RotateToFacePlayer();
    }

// Interface for states that need initialization from their owner
    public interface IInitializableState : IState // IState is your existing interface for OnStateEnter/Update/Exit
    {
        void Initialize(StateMachineNew ownerMachineNew);
    }
}