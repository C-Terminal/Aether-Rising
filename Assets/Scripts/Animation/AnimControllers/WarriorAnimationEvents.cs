using AI.FSM;
using AI.FSM.NPC;
using AI.FSM.Warrior.States;
using Characters.NPC;
using UnityEngine;
using VFX;

// Assuming NPCController is in this namespace
// using AI.FSM.NPC; // Assuming WarriorStateMachine is in this namespace if needed directly

namespace Animation.AnimControllers
{
    /// <summary>
    /// Receives events triggered from animation clips (Animation Events).
    /// This script acts as a bridge, calling appropriate methods on other components
    /// like NPCController (for hitboxes) or WarriorStateMachine (for FSM phase signals).
    /// Attach this to the same GameObject as the Animator and NPCController.
    /// </summary>
    public class WarriorAnimationEvents : MonoBehaviour
    {
        private NPCController _npcController;
        private WarriorStateMachine _stateMachineNewNew; // Optional, if anim events directly signal FSM

        void Awake()
        {
            // Cache the NPCController component
            _npcController = GetComponent<NPCController>();
            if (_npcController == null)
            {
                Debug.LogError($"[{gameObject.name}] WarriorAnimationEvents: NPCController component not found! Hitbox events will not work.", this);
            }

            // Cache the WarriorStateMachine (optional, if you want animation events to directly signal FSM states)
            _stateMachineNewNew = GetComponent<WarriorStateMachine>();
            if (_stateMachineNewNew == null)
            {
                // This might be okay if all FSM signaling is handled through NPCController or states themselves
                // Debug.LogWarning($"[{gameObject.name}] WarriorAnimationEvents: WarriorStateMachine component not found. Direct FSM signaling from anim events will not be available.", this);
            }
        }

        // --- Hitbox Control Events (Called from Attack Animations) ---

        /// <summary>
        /// Called by an Animation Event to enable the current weapon's hitbox.
        /// This method should be linked in the Animation window at the frame where the attack swing starts dealing damage.
        /// </summary>
        public void Animation_EnableHitbox()
        {
            if (_npcController != null)
            {
                _npcController.EnableHitbox();
            }
            else
            {
                Debug.LogError($"[{gameObject.name}] WarriorAnimationEvents: Attempted to call EnableHitbox, but NPCController is missing.", this);
            }
        }

        /// <summary>
        /// Called by an Animation Event to disable the current weapon's hitbox.
        /// This method should be linked in the Animation window at the frame where the attack swing's damage window ends.
        /// </summary>
        public void Animation_DisableHitbox()
        {
            if (_npcController != null)
            {
                _npcController.DisableHitbox();
            }
            else
            {
                Debug.LogError($"[{gameObject.name}] WarriorAnimationEvents: Attempted to call DisableHitbox, but NPCController is missing.", this);
            }
        }

        // --- Optional: FSM Phase Completion Events (Called from Animations) ---
        // These can help make FSM state transitions more animation-driven rather than purely timer-based.

        /// <summary>
        /// Called by an Animation Event when a telegraph animation phase is considered complete.
        /// </summary>
        public void Animation_TelegraphComplete()
        {
            // Debug.Log($"[{gameObject.name}] WarriorAnimationEvents: Telegraph Animation Complete.");
            // Option 1: Signal the NPCController, which might then inform the FSM or handle it.
            // _npcController?.SignalTelegraphComplete(); 

            // Option 2: Directly signal the WarriorStateMachine (if it has a method to handle this)
            // _warriorStateMachine?.HandleAnimationPhaseEvent("TelegraphComplete");

            // Option 3: The FSM state itself could subscribe to a C# event invoked here.
            // For now, let's assume the FSM state (e.g., W_PrepareAttackState) uses its own timer
            // or the NPCController handles this internally if it has more complex sequence logic.
            // This is a good place for a Debug.Log to confirm the event fires.
        }

        /// <summary>
        /// Called by an Animation Event when the main strike/attack animation phase is complete.
        /// </summary>
        public void Animation_StrikeComplete()
        {
            // Debug.Log($"[{gameObject.name}] WarriorAnimationEvents: Strike Animation Complete.");
            // Similar to TelegraphComplete, this can signal NPCController or WarriorStateMachine.
            // _warriorStateMachine?.HandleAnimationPhaseEvent("StrikeComplete");
        
            // If NPCController's ExecuteStrikeAction was a one-shot trigger, this event might signal the FSM
            // that the action is done and it's time to transition to RecoverState.
            // If NPCController's SetAttacking(bool) is used, the FSM might manage the end of the strike
            // based on its own logic or after NPCController.FinishStrikeAction() is called by the state.
        }

        /// <summary>
        /// Called by an Animation Event when a recovery animation phase is complete.
        /// </summary>
        public void Animation_RecoveryComplete()
        {
            // Debug.Log($"[{gameObject.name}] WarriorAnimationEvents: Recovery Animation Complete.");
            // _warriorStateMachine?.HandleAnimationPhaseEvent("RecoveryComplete");
        }


        // --- Other Utility Animation Events ---

        /// <summary>
        /// Called by an Animation Event, typically from walk/run cycles, to play a footstep sound.
        /// </summary>
        public void Animation_Footstep()
        {
            // Debug.Log($"[{gameObject.name}] WarriorAnimationEvents: Footstep Event.");
            // Add logic here to play a footstep sound effect.
            // Example: AudioManager.Instance.PlayFootstepSound(transform.position, groundMaterial);
        }

        /// <summary>
        /// A generic event that can be used for various purposes, passing a string parameter.
        /// </summary>
        public void Animation_GenericEvent(string eventName)
        {
            Debug.Log($"[{gameObject.name}] WarriorAnimationEvents: Generic Event - {eventName}");
            // _warriorStateMachine?.HandleAnimationPhaseEvent(eventName);
            // Or handle specific string eventName values here.
        }
        // In WarriorAnimationEvents.cs
        public void OnStrikeComplete()
        {
            // Find the state machine and pass the event
            var stateMachine = GetComponent<StateMachineNew>();
            if (stateMachine != null)
            {
                var strikeState = stateMachine.FindState<W_StrikeState>();
                if (strikeState != null)
                {
                    (strikeState as W_StrikeState)?.HandleStrikeComplete();
                }
            }
        }
        // In WarriorAnimationEvents.cs
        public void OnTelegraphStart()
        {
            // Get weapon info from NPCController
            var npcController = GetComponent<NPCController>();
            if (npcController != null)
            {
                var arsenalItem = npcController.GetCurrentArsenalItem();
                if (arsenalItem.HasValue)
                {
                    // Directly spawn the effect at the precise animation frame
                    VFXManager.Instance.SpawnTelegraphEffect(
                        arsenalItem.Value.name, 
                        transform.position, 
                        transform.rotation, 
                        arsenalItem.Value.telegraphDuration
                    );
                }
            }
        }
    }
}
