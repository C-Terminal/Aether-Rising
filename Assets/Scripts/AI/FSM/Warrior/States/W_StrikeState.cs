using AI.FSM.NPC;
using AI.FSM.NPC.States;
using Animation.AnimControllers;
using Characters.NPC;
using Combat.Weapons.Melee;
using Core.Events;
using Core.Events.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace AI.FSM.Warrior.States
{
    public class W_StrikeState : MonoBehaviour, IState
    {
        private StateMachineNew _machineNew;
        private NavMeshAgent _agent;
        private CharacterAnimator _charAnim;
        private NPCController _npcController; // Corrected type and name
        private MeleeWeaponDamage _meleeWeapon;

        // Configurable or obtained from NPCController.GetCurrentArsenalItem()
        private float _strikeAnimDurationEstimate = 1.2f; // Fallback if not from ArsenalItem
        private float _timer;
        private bool _hasClearedAttackerSlot;

        void Awake()
        {
            _machineNew = GetComponent<StateMachineNew>();
            _charAnim = _machineNew.CharAnim;
            // meleeWeapon = GetComponentInChildren<MeleeWeaponDamage>(); // Or get via NPCController if it manages weapon instances
        }
       
        /// <summary>
        /// Call this from WarriorStateMachine.Awake() AFTER it has cached its own components.
        /// </summary>
        public void InitReferences(StateMachineNew machineNew)
        {
            _machineNew = machineNew;
            if (_machineNew == null)
            {
                Debug.LogError($"[{gameObject.name}] W_StrikeState: WarriorStateMachine reference not passed during InitReferences!", this);
                enabled = false; return;
            }

            _agent = _machineNew.Agent;
            _charAnim = _machineNew.CharAnim;
            _npcController = _machineNew.NpcController; // Get NPCController from StateMachine

            if (_agent == null) Debug.LogError($"[{_machineNew.gameObject.name}] W_StrikeState: NavMeshAgent not found via StateMachine!", this);
            if (_charAnim == null) Debug.LogError($"[{_machineNew.gameObject.name}] W_StrikeState: CharacterAnimator not found via StateMachine!", this);
            if (_npcController == null) Debug.LogError($"[{_machineNew.gameObject.name}] W_StrikeState: NPCController not found via StateMachine!", this);

            // Get MeleeWeaponDamage from the NPCController's current weapon
            if (_npcController != null)
            {
                _meleeWeapon = _npcController.GetCurrentMeleeDamageDealer();
                var currentArsenalItem = _npcController.GetCurrentArsenalItem();
                if(currentArsenalItem.HasValue && currentArsenalItem.Value.strikeDuration > 0)
                {
                    _strikeAnimDurationEstimate = currentArsenalItem.Value.strikeDuration;
                }
            }
            
            if (_meleeWeapon == null)
            {
                 // Fallback if NPCController didn't provide it (e.g. unarmed, or error)
                _meleeWeapon = GetComponentInChildren<MeleeWeaponDamage>(); // Less ideal, direct dependency
                if (_meleeWeapon == null) Debug.LogWarning($"[{_machineNew.gameObject.name}] W_StrikeState: MeleeWeaponDamage not found via NPCController or as child. Hit detection might fail.", this);
            }
        }

        public void OnStateEnter()
        {
            if (_machineNew == null || _npcController == null || _charAnim == null || _agent == null)
            {
                Debug.LogError($"[{gameObject.name ?? "W_StrikeState"}] Critical reference missing in OnStateEnter. State cannot execute. Forcing Idle.");
                _machineNew?.SwitchState(_machineNew.FindState<IdleState>()); // Failsafe
                return;
            }

            Debug.Log($"[{_machineNew.gameObject.name}] Entering StrikeState.");
            _timer = 0f;
            _hasClearedAttackerSlot = false;
            _machineNew.RotateToFacePlayer();
            _agent.isStopped = true; // Stop movement for the strike

            // Execute the strike animation
            _npcController.ExecuteStrikeAction();
    
            // Get weapon info for strike-specific effects
            var arsenalItem = _npcController.GetCurrentArsenalItem();
            if (arsenalItem.HasValue)
            {
                // Trigger strike event for VFX/SFX
                EventManager.TriggerEvent(new AttackStrikeEventData
                {
                    AttackerTransform = transform,
                    WeaponType = arsenalItem.Value.name,
                    TargetTransform = _machineNew.Player,
                    StrikePower = 1.0f // Could be variable based on NPC state/weapon
                });
        
                // Update strike duration from arsenal item if available
                if (arsenalItem.Value.strikeDuration > 0)
                {
                    _strikeAnimDurationEstimate = arsenalItem.Value.strikeDuration;
                }
            }
        }

        public void OnStateUpdate(float deltaTime)
        {
            if (_machineNew == null) return;

            // Keep facing player during strike if desired (some games allow slight tracking)
            // _stateMachine.RotateToFacePlayer(); 
            _timer += deltaTime;

            // Transition based on timer (estimate) or ideally an Animation Event
            // that calls a method on WarriorStateMachine, which then calls a method on this current state.
            // e.g., public void HandleAnimationEvent(string eventName) { if (eventName == "StrikeComplete") ... }
            if (_timer >= _strikeAnimDurationEstimate)
            {
                FinishStrikeSequence();
            }
        }
        
        // In W_StrikeState.cs
        public void HandleStrikeComplete()
        {
            // This can be called by an animation event through WarriorAnimationEvents
            FinishStrikeSequence();
        }

        public void OnStateExit()
        {
            if (_machineNew == null) return;
            Debug.Log($"[{_machineNew.gameObject.name}] Exiting StrikeState.");

            // Ensure NPCController cleans up its strike state (e.g., SetAttacking(false))
            _npcController?.FinishStrikeAction();

            // Failsafe: Ensure the attack slot is cleared if not done by timer/event
            if (!_hasClearedAttackerSlot && NPCManager.Instance != null)
            {
                NPCManager.Instance.ClearAttackingNPC(_machineNew);
                _hasClearedAttackerSlot = true; // Mark as cleared
                Debug.LogWarning($"[{_machineNew.gameObject.name}] W_StrikeState: Cleared attacking NPC slot in OnStateExit (failsafe).");
            }
        }

        /// <summary>
        /// Called when the strike sequence is considered finished (by timer or animation event).
        /// </summary>
        public void FinishStrikeSequence() // Could be called by Animation Event via StateMachine
        {
            if (_machineNew == null) return;

            if (!_hasClearedAttackerSlot && NPCManager.Instance != null)
            {
                NPCManager.Instance.ClearAttackingNPC(_machineNew);
                _hasClearedAttackerSlot = true;
            }
            
            // NPCController's FinishStrikeAction should have been called by now if anim event driven,
            // or call it here if this method is the primary completion point.
            _npcController?.FinishStrikeAction();

            // Transition to Recover or Circle
            IState recoverState = _machineNew.FindState<W_RecoverState>();
            if (recoverState != null)
            {
                _machineNew.SwitchState(recoverState);
            }
            else
            {
                _machineNew.SwitchState(_machineNew.FindState<W_CirclingState>()); // Fallback
            }
        }
    }
}