using System;
using AI.FSM.NPC.States;
using Animation.AnimControllers;
using Characters.NPC;
using Cysharp.Threading.Tasks;
using System.Threading;
using Core.Events;
using Core.Events.Combat;
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

        // Remove timer-based recovery - now purely animation driven
        private bool _isRecovering = false;
        
        // Cancellation token for safety timeout (optional fallback)
        private CancellationTokenSource _safetyTimeoutTokenSource;
        private float _safetyTimeoutDuration = 5f; // Safety fallback timeout

        void Awake()
        {
            _machineNew = GetComponent<StateMachineNew>();
            _charAnim = _machineNew.CharAnim;
        }

        void OnEnable()
        {
            // Subscribe to recovery complete event
            EventManager.AddListener<AttackRecoveryCompleteEventData>(HandleRecoveryComplete);
        }

        void OnDisable()
        {
            // Unsubscribe from recovery complete event
            EventManager.RemoveListener<AttackRecoveryCompleteEventData>(HandleRecoveryComplete);
            
            // Clean up safety timeout
            if (_safetyTimeoutTokenSource != null)
            {
                _safetyTimeoutTokenSource.Cancel();
                _safetyTimeoutTokenSource.Dispose();
                _safetyTimeoutTokenSource = null;
            }
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
        }

        public void OnStateEnter()
        {
            if (_machineNew == null || _npcController == null || _charAnim == null || _agent == null)
            {
                Debug.LogError($"[{gameObject.name ?? "W_RecoverState"}] Critical reference missing in OnStateEnter. State cannot execute. Forcing Idle.");
                _machineNew?.SwitchState(_machineNew.FindState<IdleState>()); // Failsafe
                return;
            }

            Debug.Log($"[{_machineNew.gameObject.name}] Entering RecoverState - Animation Driven Mode.");
            
            _isRecovering = true;
            _agent.isStopped = true; // Stay stopped during recovery

            // Execute the recovery action - this should trigger the recovery animation
            _npcController.ExecuteRecoveryAction();
            
            // Start safety timeout as a fallback in case animation event never fires
            StartSafetyTimeout();
        }

        /// <summary>
        /// Safety timeout to prevent getting stuck if animation event doesn't fire
        /// </summary>
        private void StartSafetyTimeout()
        {
            if (_safetyTimeoutTokenSource != null)
            {
                _safetyTimeoutTokenSource.Cancel();
                _safetyTimeoutTokenSource.Dispose();
            }
            
            _safetyTimeoutTokenSource = new CancellationTokenSource();
            SafetyTimeoutTask(_safetyTimeoutTokenSource.Token).Forget();
        }

        private async UniTaskVoid SafetyTimeoutTask(CancellationToken cancellationToken)
        {
            try
            {
                Debug.Log($"[{_machineNew.gameObject.name}] Safety timeout started ({_safetyTimeoutDuration}s)");
                
                await UniTask.Delay(TimeSpan.FromSeconds(_safetyTimeoutDuration), cancellationToken: cancellationToken);
                
                // If we reach here, the animation event didn't fire within the timeout
                if (_isRecovering)
                {
                    Debug.LogWarning($"[{_machineNew.gameObject.name}] Recovery animation event didn't fire within {_safetyTimeoutDuration}s. Forcing completion.");
                    FinishRecoverySequence();
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{_machineNew.gameObject.name}] Safety timeout cancelled (recovery completed normally).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{_machineNew.gameObject.name}] Safety timeout error: {ex.Message}");
                // Force completion on error
                if (_isRecovering && _machineNew != null)
                {
                    FinishRecoverySequence();
                }
            }
        }

        public void OnStateUpdate(float deltaTime)
        {
            if (_machineNew == null || !_isRecovering) return;

            // Keep facing player during recovery if desired
            // _machineNew.RotateToFacePlayer(); 
            
            // The state now waits purely for animation events
            // Any other state-specific logic that needs to run every frame can go here
        }
        
        /// <summary>
        /// Called by EventManager when AttackRecoveryCompleteEventData is fired
        /// This should be triggered by an animation event
        /// </summary>
        public void HandleRecoveryComplete(AttackRecoveryCompleteEventData eventData)
        {
            // Verify this event is for our character (if eventData contains character reference)
            // if (eventData.character != _npcController) return;
            //TODO: verify why this never syncs up 
            if (!_isRecovering)
            {
                Debug.Log($"[{_machineNew.gameObject.name}] Received recovery complete event but not currently recovering. Ignoring.");
                return;
            }
            
            Debug.Log($"[{_machineNew.gameObject.name}] Recovery complete event received from animation.");
            
            // Cancel the safety timeout since we received the proper event
            if (_safetyTimeoutTokenSource != null && !_safetyTimeoutTokenSource.Token.IsCancellationRequested)
            {
                _safetyTimeoutTokenSource.Cancel(); 
                Debug.Log($"[{_machineNew.gameObject.name}] Safety timeout cancelled by animation event.");
            }
            
            FinishRecoverySequence();
        }

        public void OnStateExit()
        {
            if (_machineNew == null) return;
            Debug.Log($"[{_machineNew.gameObject.name}] Exiting RecoverState.");
            
            _isRecovering = false;
            
            // Cancel the safety timeout if it's running
            if (_safetyTimeoutTokenSource != null)
            {
                if (!_safetyTimeoutTokenSource.Token.IsCancellationRequested)
                {
                    _safetyTimeoutTokenSource.Cancel();
                    Debug.Log($"[{_machineNew.gameObject.name}] Cancelled safety timeout on state exit.");
                }
                _safetyTimeoutTokenSource.Dispose();
                _safetyTimeoutTokenSource = null;
            }

            // Ensure NPCController cleans up its recovery state
            _npcController?.FinishRecoveryAction();
        }

        /// <summary>
        /// Called when the recovery sequence is considered finished (by animation event or safety timeout).
        /// </summary>
        private void FinishRecoverySequence()
        {
            if (_machineNew == null || !_isRecovering) return;
            
            _isRecovering = false;
            
            Debug.Log($"[{_machineNew.gameObject.name}] Finishing recovery sequence.");
            
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