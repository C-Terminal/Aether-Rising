using AI.FSM.NPC;
using AI.FSM.NPC.States;
using Animation.AnimControllers;
using Core.Events;
using Core.Events.Combat;
using System.Collections;
using Characters.NPC;
using UnityEngine;
using UnityEngine.AI;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

namespace AI.FSM.Warrior.States
{
    public class W_PrepareAttackState : MonoBehaviour, IState
    {
        private StateMachineNew _stateMachineNew;
        private NavMeshAgent agent;
        private Vector3 attackPosition; // Position to move to before telegraphing
        private CharacterAnimator charAnim; // Assuming this is used
        
        // Remove timer-based telegraph - now purely animation driven
        private bool _isTelegraphing = false;
        
        // Cancellation token for safety timeout (optional fallback)
        private CancellationTokenSource _safetyTimeoutTokenSource;
        private float _safetyTimeoutDuration = 5f; // Safety fallback timeout

        void OnEnable()
        {
            // Subscribe to telegraph complete event
            EventManager.AddListener<AttackTelegraphCompleteEventData>(OnTelegraphComplete);
        }

        void OnDisable()
        {
            // Unsubscribe from telegraph complete event
            EventManager.RemoveListener<AttackTelegraphCompleteEventData>(OnTelegraphComplete);
            
            // Clean up safety timeout
            if (_safetyTimeoutTokenSource != null)
            {
                _safetyTimeoutTokenSource.Cancel();
                _safetyTimeoutTokenSource.Dispose();
                _safetyTimeoutTokenSource = null;
            }
        }

        public void OnStateEnter()
        {
            Debug.Log($"[{_stateMachineNew.gameObject.name}] Entering PrepareAttackState - Animation Driven Mode.");
            
            _isTelegraphing = true;
            
            //TODO: Decide attack position
            // Potentially move to an optimal attack spot if not already there
            // attackPosition = CalculateOptimalAttackPosition();
            // agent.SetDestination(attackPosition);
            // agent.isStopped = false;
            // For now, assume in position or handled by Chase/Circle

            agent.isStopped = true; // Stop to telegraph
            _stateMachineNew.RotateToFacePlayer();
            
            var npcController = _stateMachineNew.NpcController;
            if (npcController != null)
            {
                // Start the telegraph action - this should trigger the telegraph animation
                npcController.StartTelegraphAction();

                var arsenalItem = npcController.GetCurrentArsenalItem();
                float telegraphDuration = 0.3f; // Default fallback
                
                if (arsenalItem.HasValue && arsenalItem.Value.telegraphDuration > 0)
                {
                    telegraphDuration = arsenalItem.Value.telegraphDuration;
                    Debug.Log($"[{_stateMachineNew.gameObject.name}] Telegraph duration set to: {telegraphDuration}");
                }
                else
                {
                    Debug.Log($"[{_stateMachineNew.gameObject.name}] Using default telegraph duration: {telegraphDuration}");
                }

                // Update safety timeout duration based on telegraph duration
                _safetyTimeoutDuration = telegraphDuration + 2f; // Add buffer time

                // Raise event for additional telegraph effects
                // Trigger the telegraph event for VFX Manager to handle
                EventManager.TriggerEvent(new AttackTelegraphEventData
                {
                    AttackerTransform = transform,
                    WeaponType = arsenalItem?.name ?? "Unknown",
                    Duration = telegraphDuration,
                    TargetTransform = _stateMachineNew.Player,
                    EffectIntensity = 1.0f
                });
            }
            
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
                Debug.Log($"[{_stateMachineNew.gameObject.name}] Telegraph safety timeout started ({_safetyTimeoutDuration}s)");
                
                await UniTask.Delay(TimeSpan.FromSeconds(_safetyTimeoutDuration), cancellationToken: cancellationToken);
                
                // If we reach here, the animation event didn't fire within the timeout
                if (_isTelegraphing)
                {
                    Debug.LogWarning($"[{_stateMachineNew.gameObject.name}] Telegraph animation event didn't fire within {_safetyTimeoutDuration}s. Forcing completion.");
                    TransitionToStrikeState();
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{_stateMachineNew.gameObject.name}] Telegraph safety timeout cancelled (completed normally).");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[{_stateMachineNew.gameObject.name}] Telegraph safety timeout error: {ex.Message}");
                // Force completion on error
                if (_isTelegraphing && _stateMachineNew != null)
                {
                    TransitionToStrikeState();
                }
            }
        }

        /// <summary>
        /// Called by EventManager when AttackTelegraphCompleteEventData is fired
        /// This should be triggered by an animation event
        /// </summary>
        private void OnTelegraphComplete(AttackTelegraphCompleteEventData eventData)
        {
            // Only respond to events from this NPC
            if (eventData.AttackerTransform.gameObject != _stateMachineNew.gameObject)
            {
                return;
            }
            
            if (!_isTelegraphing)
            {
                //TODO: maybe transition to a different state, OR change the conditions for exiting Locomotion cuz this is weird
                Debug.Log($"[{_stateMachineNew.gameObject.name}] Received telegraph complete event but not currently telegraphing. Ignoring.");
                return;
            }
            
            Debug.Log($"[{_stateMachineNew.gameObject.name}] Telegraph complete event received from animation.");
            
            // Cancel the safety timeout since we received the proper event
            if (_safetyTimeoutTokenSource != null && !_safetyTimeoutTokenSource.Token.IsCancellationRequested)
            {
                _safetyTimeoutTokenSource.Cancel();
                Debug.Log($"[{_stateMachineNew.gameObject.name}] Safety timeout cancelled by animation event.");
            }
            
            var npcController = _stateMachineNew.NpcController;
            if (npcController != null)
            {
                npcController.EndTelegraphAction();
            }
            
            // Proceed with FSM transition logic
            TransitionToStrikeState();
        }

        public void OnStateUpdate(float deltaTime)
        {
            if (!_isTelegraphing) return;

            _stateMachineNew.RotateToFacePlayer();

            if (_stateMachineNew.Player != null &&
                Vector3.Distance(transform.position, _stateMachineNew.Player.position) > _stateMachineNew.GetMaxEngagementDistance())
            {
                AbortTelegraphAndTransitionTo<ChaseState>();
            }
        }


        public void OnStateExit()
        {
            Debug.Log($"[{_stateMachineNew.gameObject.name}] Exiting PrepareAttackState.");
            
            _isTelegraphing = false;
            
            // Cancel the safety timeout if it's running
            if (_safetyTimeoutTokenSource != null)
            {
                if (!_safetyTimeoutTokenSource.Token.IsCancellationRequested)
                {
                    _safetyTimeoutTokenSource.Cancel();
                    Debug.Log($"[{_stateMachineNew.gameObject.name}] Cancelled safety timeout on state exit.");
                }
                _safetyTimeoutTokenSource.Dispose();
                _safetyTimeoutTokenSource = null;
            }
            
            charAnim.SetTelegraphing(false); // Or reset bool
        }

        private void TransitionToStrikeState()
        {
            if (NPCManager.Instance.GetAttackingNPC() == _stateMachineNew)
            {
                _stateMachineNew.SwitchState(_stateMachineNew.FindState<W_StrikeState>());
            }
            else
            {
                AbortTelegraphAndTransitionTo<W_CirclingState>();
            }

        }
        
        private void AbortTelegraphAndTransitionTo<T>() where T : class, IState
        {
            if (!_isTelegraphing) return;

            Debug.Log($"[{_stateMachineNew.gameObject.name}] Aborting telegraph and transitioning to {typeof(T).Name}");

            _isTelegraphing = false;

            // Cancel safety timeout
            if (_safetyTimeoutTokenSource != null && !_safetyTimeoutTokenSource.Token.IsCancellationRequested)
            {
                _safetyTimeoutTokenSource.Cancel();
            }

            // End telegraph
            var npcController = _stateMachineNew.NpcController;
            if (npcController != null)
            {
                npcController.EndTelegraphAction();
            }

            // Trigger abort event (optional based on situation)
            EventManager.TriggerEvent(new AttackTelegraphAbortEventData
            {
                AttackerTransform = transform,
                Reason = $"TransitionTo{typeof(T).Name}"
            });

            _stateMachineNew.SwitchState(_stateMachineNew.FindState<T>());
        }


        public void InitReferences(StateMachineNew stateMachine)
        {
            _stateMachineNew = stateMachine;
            agent = _stateMachineNew.Agent;
            charAnim = _stateMachineNew.CharAnim;
        }
    }
}