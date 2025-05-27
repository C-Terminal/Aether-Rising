using AI.FSM.NPC;
using AI.FSM.NPC.States;
using Animation.AnimControllers;
using Core.Events;
using Core.Events.Combat;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace AI.FSM.Warrior.States
{
    public class W_PrepareAttackState : MonoBehaviour, IState
    {
        private StateMachineNew _stateMachineNew;
        private NavMeshAgent agent;
        private Vector3 attackPosition; // Position to move to before telegraphing
        private CharacterAnimator charAnim; // Assuming this is used
        private float telegraphDuration = 1.0f; // Example, configure this
        private float timer;
        
        // Add coroutine reference
        private Coroutine _telegraphCoroutine;

        public void OnStateEnter()
        {
            Debug.Log($"[{_stateMachineNew.gameObject.name}] Entering PrepareAttackState.");
            timer = 0f;
            //TODO: Decide attack position
            // Potentially move to an optimal attack spot if not already there
            // attackPosition = CalculateOptimalAttackPosition();
            // agent.SetDestination(attackPosition);
            // agent.isStopped = false;
            // For now, assume in position or handled by Chase/Circle

            agent.isStopped = true; // Stop to telegraph
            _stateMachineNew.RotateToFacePlayer();
            // charAnim.PlayTelegraphAnimation(); // Or set a bool/trigger
            var npcController = _stateMachineNew.NpcController;
            if (npcController != null)
            {
                npcController.StartTelegraphAction();

                var arsenalItem = npcController.GetCurrentArsenalItem();
                if (arsenalItem.HasValue && arsenalItem.Value.telegraphDuration > 0)
                {
                    telegraphDuration = arsenalItem.Value.telegraphDuration;
                    Debug.Log($"[{_stateMachineNew.gameObject.name}] Telegraph duration set to: {telegraphDuration}");


                    // Raise event for additional telegraph effects
                    // Trigger the telegraph event for VFX Manager to handle
                    EventManager.TriggerEvent(new AttackTelegraphEventData
                    {
                        AttackerTransform = transform,
                        WeaponType = arsenalItem.Value.name,
                        Duration = telegraphDuration,
                        TargetTransform = _stateMachineNew.Player,
                        EffectIntensity = 1.0f
                    });
                    // You can also directly call VFXManager if you prefer that approach
                    // VFXManager.Instance.SpawnTelegraphEffect(arsenalItem.Value.name, transform.position, transform.rotation, telegraphDuration);
                }
                else
                {
                    telegraphDuration = 1.0f; // Default fallback
                    Debug.Log(
                        $"[{_stateMachineNew.gameObject.name}] Using default telegraph duration: {telegraphDuration}");
                    EventManager.TriggerEvent(new AttackTelegraphEventData
                    {
                        AttackerTransform = transform,
                        WeaponType = arsenalItem.Value.name,
                        Duration = telegraphDuration,
                        TargetTransform = _stateMachineNew.Player,
                        EffectIntensity = 1.0f
                    });
                }
            }

            charAnim.SetAiming(true); // Example of a "tell"
            
            // Start the telegraph coroutine
            if (_telegraphCoroutine != null)
            {
                StopCoroutine(_telegraphCoroutine);
            }
            _telegraphCoroutine = StartCoroutine(TelegraphCoroutine());
        }
        
        // Coroutine to handle telegraph timing
        private IEnumerator TelegraphCoroutine()
        {
            float elapsedTime = 0f;
            
            Debug.Log($"[{_stateMachineNew.gameObject.name}] Starting telegraph coroutine. Duration: {telegraphDuration}s");
            
            while (elapsedTime < telegraphDuration)
            {
                elapsedTime += Time.deltaTime;
                timer = elapsedTime; // Update the timer variable for consistency
                
                // Log progress periodically
                if (Mathf.Floor(elapsedTime * 2) > Mathf.Floor((elapsedTime - Time.deltaTime) * 2))
                {
                    Debug.Log($"[{_stateMachineNew.gameObject.name}] Telegraph progress: {elapsedTime:F2}/{telegraphDuration:F2}");
                }
                
                yield return null;
            }
            
            Debug.Log($"[{_stateMachineNew.gameObject.name}] Telegraph complete after {elapsedTime:F2} seconds");
            
            // Only transition if we're still the current attacker
            if (NPCManager.Instance.GetAttackingNPC() == _stateMachineNew)
            {
                TransitionToStrikeState();
            }
            else
            {
                Debug.LogWarning($"[{_stateMachineNew.gameObject.name}] Lost attack slot during telegraph coroutine!");
                _stateMachineNew.SwitchState(_stateMachineNew.FindState<W_CirclingState>());
            }
        }

        public void OnStateUpdate(float deltaTime)
        {
            _stateMachineNew.RotateToFacePlayer(); // Keep facing
            
            // We don't need to update the timer or check for transition here anymore
            // The coroutine handles that independently
            
            // Only check for player distance and other conditions that might interrupt the telegraph
            if (_stateMachineNew.Player != null && 
                Vector3.Distance(transform.position, _stateMachineNew.Player.position) > _stateMachineNew.GetMaxEngagementDistance())
            {
                Debug.Log($"[{_stateMachineNew.gameObject.name}] Player moved too far during telegraph. Aborting to Chase.");
        
                // End telegraph effects
                var npcController = _stateMachineNew.NpcController;
                if (npcController != null)
                {
                    npcController.EndTelegraphAction();
                }
        
                // Trigger event for aborting telegraph
                EventManager.TriggerEvent(new AttackTelegraphAbortEventData
                {
                    AttackerTransform = transform,
                    Reason = "TargetOutOfRange"
                });
                
                // Stop the telegraph coroutine
                if (_telegraphCoroutine != null)
                {
                    StopCoroutine(_telegraphCoroutine);
                    _telegraphCoroutine = null;
                }
        
                // Switch to chase state
                _stateMachineNew.SwitchState(_stateMachineNew.FindState<ChaseState>());
            }
        }

        public void OnStateExit()
        {
            // Stop the telegraph coroutine if it's running
            if (_telegraphCoroutine != null)
            {
                StopCoroutine(_telegraphCoroutine);
                _telegraphCoroutine = null;
                Debug.Log($"[{_stateMachineNew.gameObject.name}] Stopped telegraph coroutine on state exit.");
            }
            
            // charAnim.StopTelegraphAnimation(); // Or reset bool
            charAnim.SetAiming(false); // Clean up tell
            Debug.Log($"[{_stateMachineNew.gameObject.name}] Exiting PrepareAttackState.");
        }

        private void TransitionToStrikeState()
        {
            // Check if still allowed to attack by NPCManager
            if (NPCManager.Instance.GetAttackingNPC() == _stateMachineNew)
            {
                // End telegraph animation/effects
                var npcController = _stateMachineNew.NpcController;
                if (npcController != null) npcController.EndTelegraphAction();

                // Trigger event for telegraph completion
                EventManager.TriggerEvent(new AttackTelegraphCompleteEventData
                {
                    AttackerTransform = transform,
                    WeaponType = npcController?.GetCurrentArsenalItem()?.name ?? "Unknown"
                });

                // Switch to strike state
                Debug.Log(
                    $"[{_stateMachineNew.gameObject.name}] Transitioning from PrepareAttackState to StrikeState.");
                _stateMachineNew.SwitchState(_stateMachineNew.FindState<W_StrikeState>());
            }
            else
            {
                // Lost attack slot during telegraph
                Debug.Log(
                    $"[{_stateMachineNew.gameObject.name}] Lost attack slot during PrepareAttack. Returning to Circle.");
                _stateMachineNew.SwitchState(_stateMachineNew.FindState<W_CirclingState>());
            }
        }

        public void InitReferences(StateMachineNew stateMachine)
        {
            _stateMachineNew = stateMachine;
            agent = _stateMachineNew.Agent;
            charAnim = _stateMachineNew.CharAnim;
        }
    }
}