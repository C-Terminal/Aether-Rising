using AI.FSM.NPC;
using AI.FSM.NPC.States;
using Animation.AnimControllers;
using Core.Events;
using Core.Events.Combat;
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
                if (arsenalItem.HasValue)
                {
                    telegraphDuration = arsenalItem.Value.telegraphDuration;

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
            }

            charAnim.SetAiming(true); // Example of a "tell"
            // Or use NPCController to start a specific telegraph sequence:
            // _stateMachineNew.NpcWeaponController?.StartTelegraph();
        }

        public void OnStateUpdate(float deltaTime)
        {
            _stateMachineNew.RotateToFacePlayer(); // Keep facing
            timer += deltaTime;
            
            // Debug logging to track timer progress
            if (timer % 0.5f < 0.01f) // Log roughly every 0.5 seconds to avoid spam
            {
                Debug.Log($"[{_stateMachineNew.gameObject.name}] PrepareAttackState timer: {timer:F2}/{telegraphDuration:F2}");
            }

            if (timer >= telegraphDuration)
            {
                Debug.Log($"[{_stateMachineNew.gameObject.name}] PrepareAttackState timer reached threshold: {timer:F2}/{telegraphDuration:F2}");
                TransitionToStrikeState();
            }

            // Check if player moved too far during telegraph
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
        
                // Switch to chase state
                _stateMachineNew.SwitchState(_stateMachineNew.FindState<ChaseState>());
            }
            
            // Add logic: if player moves too far during telegraph, maybe abort to Chase/Circle
            // Add logic: if damaged during telegraph, maybe abort to HitState
        }

        private void TransitionToStrikeState()
        {
            // Check if still allowed to attack by NPCManager
            if (NPCManager.Instance.GetAttackingNPC() == _stateMachineNew)
            {
                // End telegraph animation/effects
                var npcController = _stateMachineNew.NpcController;
                if (npcController != null)
                {
                    npcController.EndTelegraphAction();
                }
        
                // Trigger event for telegraph completion
                EventManager.TriggerEvent(new AttackTelegraphCompleteEventData
                {
                    AttackerTransform = transform,
                    WeaponType = npcController?.GetCurrentArsenalItem()?.name ?? "Unknown"
                });
        
                // Switch to strike state
                Debug.Log($"[{_stateMachineNew.gameObject.name}] Transitioning from PrepareAttackState to StrikeState.");
                _stateMachineNew.SwitchState(_stateMachineNew.FindState<W_StrikeState>());
            }
            else
            {
                // Lost attack slot during telegraph
                Debug.Log($"[{_stateMachineNew.gameObject.name}] Lost attack slot during PrepareAttack. Returning to Circle.");
                _stateMachineNew.SwitchState(_stateMachineNew.FindState<W_CirclingState>());
            }
        }

        public void OnStateExit()
        {
            // charAnim.StopTelegraphAnimation(); // Or reset bool
            charAnim.SetAiming(false); // Clean up tell
            Debug.Log($"[{_stateMachineNew.gameObject.name}] Exiting PrepareAttackState.");
        }


        public void InitReferences(StateMachineNew stateMachine)
        {
            _stateMachineNew = stateMachine;
            agent = _stateMachineNew.Agent;
            charAnim = _stateMachineNew.CharAnim;
        }
    }
}