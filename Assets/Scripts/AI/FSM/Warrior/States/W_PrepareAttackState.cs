using System;
using System.Threading;
using AI.FSM.NPC;
using AI.FSM.NPC.States;
using AI.FSM.Utility;
using AI.NPC.Sensing;
using Animation.AnimControllers;
using Characters.NPC;
using Core.Events;
using Core.Events.Combat;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

// Add this for perception system

namespace AI.FSM.Warrior.States
{
    public class W_PrepareAttackState : MonoBehaviour, IState
    {
        
        private NPCAttackIntent _attackIntent;
        private NPCIntentController _intentController;
        private NPCMemoryComponent _memory;
        private WarriorStateMachine _warriorFSM;
        // Remove timer-based telegraph - now purely animation driven
        private bool _isTelegraphing;


        // Perception system reference
        private NPCPerceptionCoordinator _perceptionCoordinator;
        private float _safetyTimeoutDuration = 1f; // Safety fallback timeout

        // Cancellation token for safety timeout (optional fallback)
        private CancellationTokenSource _safetyTimeoutTokenSource;
        // private StateMachineNew _warriorFSM;
        private NavMeshAgent agent;
        private Vector3 attackPosition; // Position to move to before telegraphing
        private CharacterAnimator charAnim; // Assuming this is used
        private NPCController npcController;

        // private void Awake()
        // {
        //     throw new NotImplementedException();
        // }

        private void OnEnable()
        {
            // Subscribe to telegraph complete event
            EventManager.AddListener<AttackTelegraphCompleteEventData>(OnTelegraphComplete);
        }

        private void OnDisable()
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
            Debug.Log($"[{_warriorFSM.gameObject.name}] Entering PrepareAttackState - Animation Driven Mode.");

            _isTelegraphing = true;

            //TODO: Decide attack position
            // Potentially move to an optimal attack spot if not already there
            // attackPosition = CalculateOptimalAttackPosition();
            // agent.SetDestination(attackPosition);
            // agent.isStopped = false;
            // For now, assume in position or handled by Chase/Circle

            agent.isStopped = true; // Stop to telegraph
            _warriorFSM.RotateToFacePlayer();

            npcController = _warriorFSM.NpcController;
            if (npcController != null)
            {
                // Start the telegraph action - this should trigger the telegraph animation
                npcController.StartTelegraphAction();

                var arsenalItem = npcController.GetCurrentArsenalItem();
                var telegraphDuration = 0.3f; // Default fallback

                if (arsenalItem.HasValue && arsenalItem.Value.telegraphDuration > 0)
                {
                    telegraphDuration = arsenalItem.Value.telegraphDuration;
                    Debug.Log($"[{_warriorFSM.gameObject.name}] Telegraph duration set to: {telegraphDuration}");
                }
                else
                {
                    Debug.Log(
                        $"[{_warriorFSM.gameObject.name}] Using default telegraph duration: {telegraphDuration}");
                }

                // Update safety timeout duration based on telegraph duration
                _safetyTimeoutDuration = telegraphDuration + 0.3f; // Add buffer time
                // Get current target from perception system instead of direct Player reference
                var currentTarget = GetCurrentTarget();
                // Raise event for additional telegraph effects
                // Trigger the telegraph event for VFX Manager to handle
                EventManager.TriggerEvent(new AttackTelegraphEventData
                {
                    AttackerTransform = transform,
                    WeaponType = arsenalItem?.name ?? "Unknown",
                    Duration = telegraphDuration,
                    TargetTransform = _warriorFSM.Player,
                    EffectIntensity = 1.0f
                });
            }

            // Start safety timeout as a fallback in case animation event never fires
            StartSafetyTimeout();
        }

        public void OnStateUpdate(float deltaTime)
        {
            if (!_isTelegraphing) return;

            _warriorFSM.RotateToFacePlayer();

            if (!IsTargetInRange() || !CanSeeTarget())
            {
                var reason = !IsTargetInRange() ? "TargetOutOfRange" : "TargetNotVisible";
                Debug.Log(
                    $"[{_warriorFSM.gameObject.name}] Target lost during telegraph ({reason}). Aborting to Chase.");

                AbortTelegraphAndTransitionTo<ChaseState>();
            }
            
            // NEW: Check if we should seek cover or fallback due to morale/health
            if (_intentController.ShouldSeekCover())
            {
                AbortTelegraphAndTransitionTo<CoverState>();
                return;
            }

            if (_intentController.ShouldFallback())
            {
                AbortTelegraphAndTransitionTo<W_RetreatState>();
                return;
            }
        }


        public void OnStateExit()
        {
            Debug.Log($"[{_warriorFSM.gameObject.name}] Exiting PrepareAttackState.");

            _isTelegraphing = false;

            // Cancel the safety timeout if it's running
            if (_safetyTimeoutTokenSource != null)
            {
                if (!_safetyTimeoutTokenSource.Token.IsCancellationRequested)
                {
                    _safetyTimeoutTokenSource.Cancel();
                    Debug.Log($"[{_warriorFSM.gameObject.name}] Cancelled safety timeout on state exit.");
                }

                _safetyTimeoutTokenSource.Dispose();
                _safetyTimeoutTokenSource = null;
            }

            charAnim.SetTelegraphing(false); // Or reset bool
        }

        /// <summary>
        ///     Get the current target from the perception system
        /// </summary>
        private Transform GetCurrentTarget()
        {
            if (_perceptionCoordinator != null)
                // Assuming you add a method to get current target from perception coordinator
                return _perceptionCoordinator.GetCurrentTarget();

            // Fallback to state machine's player reference if perception system not available
            return _warriorFSM.Player;
        }

        /// <summary>
        ///     Check if target is within engagement range using perception system
        /// </summary>
        private bool IsTargetInRange()
        {
            var target = GetCurrentTarget();
            if (target == null) return false;

            var distance = Vector3.Distance(transform.position, target.position);
            return distance <= _warriorFSM.GetMaxEngagementDistance();
        }

        /// <summary>
        ///     Check if we can still see the target
        /// </summary>
        private bool CanSeeTarget()
        {
            if (_perceptionCoordinator != null) return _perceptionCoordinator.IsPlayerCurrentlyVisible();

            // Fallback to state machine's visibility check
            return _warriorFSM.IsPlayerVisible();
        }


        /// <summary>
        ///     Safety timeout to prevent getting stuck if animation event doesn't fire
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
                Debug.Log(
                    $"[{_warriorFSM.gameObject.name}] Telegraph safety timeout started ({_safetyTimeoutDuration}s)");

                await UniTask.Delay(TimeSpan.FromSeconds(_safetyTimeoutDuration), cancellationToken: cancellationToken);

                // If we reach here, the animation event didn't fire within the timeout
                if (_isTelegraphing)
                {
                    Debug.LogWarning(
                        $"[{_warriorFSM.gameObject.name}] Telegraph animation event didn't fire within {_safetyTimeoutDuration}s. Forcing completion.");
                    TransitionToStrikeState();
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log(
                    $"[{_warriorFSM.gameObject.name}] Telegraph safety timeout cancelled (completed normally).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{_warriorFSM.gameObject.name}] Telegraph safety timeout error: {ex.Message}");
                // Force completion on error
                if (_isTelegraphing && _warriorFSM != null) TransitionToStrikeState();
            }
        }

        /// <summary>
        ///     Called by EventManager when AttackTelegraphCompleteEventData is fired
        ///     This should be triggered by an animation event
        /// </summary>
        private void OnTelegraphComplete(AttackTelegraphCompleteEventData eventData)
        {
            // Only respond to events from this NPC
            if (eventData.AttackerTransform.gameObject != _warriorFSM.gameObject) return;

            _memory?.MarkAttackRequest(true);
            
            if (!_isTelegraphing)
            {
                //TODO: maybe transition to a different state, OR change the conditions for exiting Locomotion cuz this is weird
                Debug.Log(
                    $"[{_warriorFSM.gameObject.name}] Received telegraph complete event but not currently telegraphing. Ignoring.");
                return;
            }

            Debug.Log($"[{_warriorFSM.gameObject.name}] Telegraph complete event received from animation.");

            // Cancel the safety timeout since we received the proper event
            if (_safetyTimeoutTokenSource != null && !_safetyTimeoutTokenSource.Token.IsCancellationRequested)
            {
                _safetyTimeoutTokenSource.Cancel();
                Debug.Log($"[{_warriorFSM.gameObject.name}] Safety timeout cancelled by animation event.");
            }

            var npcController = _warriorFSM.NpcController;
            if (npcController != null) npcController.EndTelegraphAction();

            
            
            // Proceed with FSM transition logic
            TransitionToStrikeState();
        }

        private void TransitionToStrikeState()
        {
            if (_attackIntent.HasAssignedAttackTurn())
            {
                // End telegraph
               if (npcController != null) npcController.EndTelegraphAction();
                _isTelegraphing = false;

                _attackIntent.CommitAttack(); // Switches to StrikeState
            }
            else
            {
                AbortTelegraphAndTransitionTo<W_CirclingState>();
            }
        }

        private void AbortTelegraphAndTransitionTo<T>() where T : class, IState
        {
            if (!_isTelegraphing) return;

            Debug.Log($"[{_warriorFSM.gameObject.name}] Aborting telegraph and transitioning to {typeof(T).Name}");

            _isTelegraphing = false;

            // Cancel safety timeout
            if (_safetyTimeoutTokenSource != null && !_safetyTimeoutTokenSource.Token.IsCancellationRequested)
                _safetyTimeoutTokenSource.Cancel();


            if (npcController != null) npcController.EndTelegraphAction();

            //TODO: have the event trigger storage of memory
            EventManager.TriggerEvent(new AttackTelegraphAbortEventData
            {
                AttackerTransform = transform,
                Reason = $"TransitionTo{typeof(T).Name}"
            });
            
            _memory?.MarkAttackRequest(false);

            
            
            _warriorFSM.SwitchState(_warriorFSM.FindState<T>());
        }


        public void InitReferences(StateMachineNew stateMachine)
        {
            
            _warriorFSM = stateMachine as WarriorStateMachine;

            if (_warriorFSM == null)
            {
                Debug.LogError($"[{gameObject.name}] W_PrepareAttackState: Invalid FSM passed.");
                enabled = false;
                return;
            }

            agent = _warriorFSM.Agent;
            charAnim = _warriorFSM.CharAnim;

            _memory = _warriorFSM.GetComponent<NPCMemoryComponent>();
            _attackIntent = new NPCAttackIntent(_warriorFSM);
            _intentController = new NPCIntentController(_warriorFSM);

            _perceptionCoordinator = _warriorFSM.GetComponentInChildren<NPCPerceptionCoordinator>();
            
            if (_perceptionCoordinator == null)
                Debug.LogWarning(
                    $"[{_warriorFSM.gameObject.name}] NPCPerceptionCoordinator not found. Using fallback methods.");
        }
            

           
        }
    }
