using AI.FSM.NPC;
using AI.FSM.NPC.States;
using Animation.AnimControllers;
using Characters.NPC;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace AI.FSM.Warrior.States
{
    public class W_RecoverState : MonoBehaviour, IState
    {
        private StateMachineNew _machineNew;
        private NavMeshAgent _agent;
        private CharacterAnimator _charAnim;
        private NPCController _npcController;

        // Configurable or obtained from NPCController.GetCurrentArsenalItem()
        private float _recoveryDurationEstimate = 0.5f; // Fallback if not from ArsenalItem
        private float _timer;
        
        // Add coroutine reference
        private Coroutine _recoveryCoroutine;

        void Awake()
        {
            _machineNew = GetComponent<StateMachineNew>();
            _charAnim = _machineNew.CharAnim;
        }
       
        /// <summary>
        /// Call this from WarriorStateMachine.Awake() AFTER it has cached its own components.
        /// </summary>
        public void InitReferences(StateMachineNew machineNew)
        {
            _machineNew = machineNew;
            if (_machineNew == null)
            {
                Debug.LogError($"[{gameObject.name}] W_RecoverState: WarriorStateMachine reference not passed during InitReferences!", this);
                enabled = false; return;
            }

            _agent = _machineNew.Agent;
            _charAnim = _machineNew.CharAnim;
            _npcController = _machineNew.NpcController; // Get NPCController from StateMachine

            if (_agent == null) Debug.LogError($"[{_machineNew.gameObject.name}] W_RecoverState: NavMeshAgent not found via StateMachine!", this);
            if (_charAnim == null) Debug.LogError($"[{_machineNew.gameObject.name}] W_RecoverState: CharacterAnimator not found via StateMachine!", this);
            if (_npcController == null) Debug.LogError($"[{_machineNew.gameObject.name}] W_RecoverState: NPCController not found via StateMachine!", this);

            // Get recovery duration from the NPCController's current weapon if available
            if (_npcController != null)
            {
                var currentArsenalItem = _npcController.GetCurrentArsenalItem();
                if(currentArsenalItem.HasValue && currentArsenalItem.Value.recoveryDuration > 0)
                {
                    _recoveryDurationEstimate = currentArsenalItem.Value.recoveryDuration;
                }
            }
        }

        public void OnStateEnter()
        {
            if (_machineNew == null || _npcController == null || _charAnim == null || _agent == null)
            {
                Debug.LogError($"[{gameObject.name ?? "W_RecoverState"}] Critical reference missing in OnStateEnter. State cannot execute. Forcing Idle.");
                _machineNew?.SwitchState(_machineNew.FindState<IdleState>()); // Failsafe
                return;
            }

            Debug.Log($"[{_machineNew.gameObject.name}] Entering RecoverState.");
            _timer = 0f;
            _agent.isStopped = true; // Stay stopped during recovery

            // Execute the recovery action
            _npcController.ExecuteRecoveryAction();
    
            // Get weapon info for recovery-specific duration
            var arsenalItem = _npcController.GetCurrentArsenalItem();
            if (arsenalItem.HasValue)
            {
                // Update recovery duration from arsenal item if available
                if (arsenalItem.Value.recoveryDuration > 0)
                {
                    _recoveryDurationEstimate = arsenalItem.Value.recoveryDuration;
                }
            }
            
            // Start the recovery coroutine
            if (_recoveryCoroutine != null)
            {
                StopCoroutine(_recoveryCoroutine);
            }
            _recoveryCoroutine = StartCoroutine(RecoveryCoroutine());
        }
        
        // Coroutine to handle recovery timing
        private IEnumerator RecoveryCoroutine()
        {
            float elapsedTime = 0f;
            
            Debug.Log($"[{_machineNew.gameObject.name}] Starting recovery coroutine. Duration: {_recoveryDurationEstimate}s");
            
            while (elapsedTime < _recoveryDurationEstimate)
            {
                elapsedTime += Time.deltaTime;
                _timer = elapsedTime; // Update the timer variable for consistency
                
                // Log progress periodically (less frequent than strike for shorter duration)
                if (Mathf.Floor(elapsedTime * 4) > Mathf.Floor((elapsedTime - Time.deltaTime) * 4))
                {
                    Debug.Log($"[{_machineNew.gameObject.name}] Recovery progress: {elapsedTime:F2}/{_recoveryDurationEstimate:F2}");
                }
                
                yield return null;
            }
            
            Debug.Log($"[{_machineNew.gameObject.name}] Recovery complete after {elapsedTime:F2} seconds");
            FinishRecoverySequence();
        }

        public void OnStateUpdate(float deltaTime)
        {
            if (_machineNew == null) return;

            // Keep facing player during recovery if desired
            // _machineNew.RotateToFacePlayer(); 
            
            // We don't need to update the timer or check for transition here anymore
            // The coroutine handles that independently
            
            // Any other state-specific logic that needs to run every frame can go here
        }
        
        // In W_RecoverState.cs
        public void HandleRecoveryComplete()
        {
            // This can be called by an animation event through WarriorAnimationEvents
            
            // Stop the coroutine if it's still running
            if (_recoveryCoroutine != null)
            {
                StopCoroutine(_recoveryCoroutine);
                _recoveryCoroutine = null;
                Debug.Log($"[{_machineNew.gameObject.name}] Recovery coroutine stopped by animation event.");
            }
            
            FinishRecoverySequence();
        }

        public void OnStateExit()
        {
            if (_machineNew == null) return;
            Debug.Log($"[{_machineNew.gameObject.name}] Exiting RecoverState.");
            
            // Stop the recovery coroutine if it's running
            if (_recoveryCoroutine != null)
            {
                StopCoroutine(_recoveryCoroutine);
                _recoveryCoroutine = null;
                Debug.Log($"[{_machineNew.gameObject.name}] Stopped recovery coroutine on state exit.");
            }

            // Ensure NPCController cleans up its recovery state
            _npcController?.FinishRecoveryAction();
        }

        /// <summary>
        /// Called when the recovery sequence is considered finished (by timer or animation event).
        /// </summary>
        public void FinishRecoverySequence() // Could be called by Animation Event via StateMachine
        {
            if (_machineNew == null) return;
            
            // NPCController's FinishRecoveryAction should have been called by now if anim event driven,
            // or call it here if this method is the primary completion point.
            _npcController?.FinishRecoveryAction();

            // Transition to Circling or Idle
            IState circlingState = _machineNew.FindState<W_CirclingState>();
            if (circlingState != null)
            {
                _machineNew.SwitchState(circlingState);
            }
            else
            {
                _machineNew.SwitchState(_machineNew.FindState<IdleState>()); // Fallback
            }
        }
    }
}