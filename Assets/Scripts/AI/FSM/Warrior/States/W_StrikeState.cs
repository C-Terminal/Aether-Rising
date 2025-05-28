using System;
using AI.FSM.NPC;
using AI.FSM.NPC.States;
using Animation.AnimControllers;
using Characters.NPC;
using Combat.Weapons.Melee;
using Core.Events;
using Core.Events.Combat;
using Cysharp.Threading.Tasks;
using System.Threading;
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
        private bool _isStrikeCompleted; // Flag to track if strike was completed via event
        
        // Add cancellation token source for UniTask
        private CancellationTokenSource _strikeCancellationTokenSource;

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
            _isStrikeCompleted = false; // Reset completion flag
            _machineNew.RotateToFacePlayer();
            _agent.isStopped = true; // Stop movement for the strike

            // Subscribe to the strike complete event
            EventManager.AddListener<AttackStrikeCompleteEventData>(OnStrikeComplete);

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
            
            // Start the strike UniTask
            if (_strikeCancellationTokenSource != null)
            {
                _strikeCancellationTokenSource.Cancel();
                _strikeCancellationTokenSource.Dispose();
            }
            _strikeCancellationTokenSource = new CancellationTokenSource();
            
            // Fire and forget the UniTask
            StrikeTask(_strikeCancellationTokenSource.Token).Forget();
        }
        
        // UniTask to handle strike timing
        private async UniTaskVoid StrikeTask(CancellationToken cancellationToken)
        {
            try
            {
                float elapsedTime = 0f;
                
                Debug.Log($"[{_machineNew.gameObject.name}] Starting strike UniTask. Duration: {_strikeAnimDurationEstimate}s");
                
                while (elapsedTime < _strikeAnimDurationEstimate)
                {
                    // Check for cancellation
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    elapsedTime += Time.deltaTime;
                    _timer = elapsedTime; // Update the timer variable for consistency
                    
                    // Log progress periodically
                    if (Mathf.Floor(elapsedTime * 2) > Mathf.Floor((elapsedTime - Time.deltaTime) * 2))
                    {
                        Debug.Log($"[{_machineNew.gameObject.name}] Strike progress: {elapsedTime:F2}/{_strikeAnimDurationEstimate:F2}");
                    }
                }
                
                Debug.Log($"[{_machineNew.gameObject.name}] Strike complete after {elapsedTime:F2} seconds");
                FinishStrikeSequence();
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{_machineNew.gameObject.name}] Strike UniTask was cancelled.");
                // Check if cancellation was due to animation event completion
                if (_isStrikeCompleted)
                {
                    Debug.Log($"[{_machineNew.gameObject.name}] Strike UniTask cancelled due to animation event completion.");
                }
                // Don't call FinishStrikeSequence() when cancelled - let the event handler manage it
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[{_machineNew.gameObject.name}] Strike UniTask error: {ex.Message}");
                // Handle error gracefully - could transition to safe state
                if (_machineNew != null)
                {
                    FinishStrikeSequence();
                }
            }
        }

        public void OnStateUpdate(float deltaTime)
        {
            if (_machineNew == null) return;

            // Keep facing player during strike if desired (some games allow slight tracking)
            // _machineNew.RotateToFacePlayer(); 
            
            // We don't need to update the timer or check for transition here anymore
            // The UniTask handles that independently
            
            // Any other state-specific logic that needs to run every frame can go here
        }
        
        // Event handler for animation-driven strike completion
        private void OnStrikeComplete(AttackStrikeCompleteEventData obj)
        {
            if (obj.AttackerTransform.gameObject == _machineNew.gameObject) // Only respond to events from this NPC
            {
                Debug.Log($"[{_machineNew.gameObject.name}] Strike completion event received from animation.");
                
                _isStrikeCompleted = true; // Mark as completed via event
                
                // Cancel the UniTask if it's still running
                if (_strikeCancellationTokenSource != null && !_strikeCancellationTokenSource.Token.IsCancellationRequested)
                {
                    _strikeCancellationTokenSource.Cancel();
                    Debug.Log($"[{_machineNew.gameObject.name}] Strike UniTask cancelled by animation event.");
                }
                
                // Let NPCController handle its cleanup
                var npcController = _machineNew.NpcController;
                if (npcController != null) 
                {
                    npcController.FinishStrikeAction();
                }
                
                // Clear attacker slot if not already done
                if (!_hasClearedAttackerSlot && NPCManager.Instance != null)
                {
                    NPCManager.Instance.ClearAttackingNPC(_machineNew);
                    _hasClearedAttackerSlot = true;
                }
                
                // Proceed with FSM transition logic
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
        
        // In W_StrikeState.cs
        public void HandleStrikeComplete()
        {
            // This method is kept for backwards compatibility but may not be needed
            // if you're using the event system exclusively
            Debug.LogWarning($"[{_machineNew.gameObject.name}] HandleStrikeComplete() called - consider using event system instead.");
            
            // Cancel the UniTask if it's still running
            if (_strikeCancellationTokenSource != null && !_strikeCancellationTokenSource.Token.IsCancellationRequested)
            {
                _strikeCancellationTokenSource.Cancel();
                Debug.Log($"[{_machineNew.gameObject.name}] Strike UniTask cancelled by HandleStrikeComplete.");
            }
            
            FinishStrikeSequence();
        }

        public void OnStateExit()
        {
            if (_machineNew == null) return;
            Debug.Log($"[{_machineNew.gameObject.name}] Exiting StrikeState.");
            
            // Unsubscribe from the strike complete event
            EventManager.RemoveListener<AttackStrikeCompleteEventData>(OnStrikeComplete);
            
            // Cancel the strike UniTask if it's running
            if (_strikeCancellationTokenSource != null)
            {
                if (!_strikeCancellationTokenSource.Token.IsCancellationRequested)
                {
                    _strikeCancellationTokenSource.Cancel();
                    Debug.Log($"[{_machineNew.gameObject.name}] Cancelled strike UniTask on state exit.");
                }
                _strikeCancellationTokenSource.Dispose();
                _strikeCancellationTokenSource = null;
            }

            // Ensure NPCController cleans up its strike state (e.g., SetAttacking(false))
            // Only call this if the strike wasn't completed via animation event
            if (!_isStrikeCompleted)
            {
                _npcController?.FinishStrikeAction();
            }

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