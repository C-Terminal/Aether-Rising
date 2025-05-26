using AI.FSM.NPC;
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

            if (timer >= telegraphDuration)
            {
                // Check if still allowed to attack by NPCManager (important!)
                if (NPCManager.Instance.GetAttackingNPC() == _stateMachineNew)
                {
                    _stateMachineNew.SwitchState(_stateMachineNew
                        .FindState<W_StrikeState>()); // Or your main attack state
                }
                else
                {
                    // Lost attack slot during telegraph (e.g., player moved far, another NPC took over)
                    Debug.Log(
                        $"[{_stateMachineNew.gameObject.name}] Lost attack slot during PrepareAttack. Returning to Circle.");
                    _stateMachineNew.SwitchState(_stateMachineNew.FindState<W_CirclingState>());
                }
            }

            // Add logic: if player moves too far during telegraph, maybe abort to Chase/Circle
            // Add logic: if damaged during telegraph, maybe abort to HitState
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