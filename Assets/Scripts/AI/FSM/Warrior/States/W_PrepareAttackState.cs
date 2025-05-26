using AI.FSM.NPC;
using Animation.AnimControllers;
using UnityEngine;
using UnityEngine.AI;

namespace AI.FSM.Warrior.States
{
    public class W_PrepareAttackState : MonoBehaviour, IState
    {
        private StateMachineNew _stateMachineNew;
        private NavMeshAgent agent;
        private CharacterAnimator charAnim; // Assuming this is used
        private float telegraphDuration = 1.0f; // Example, configure this
        private float timer;
        private Vector3 attackPosition; // Position to move to before telegraphing

        void Awake()
        {
            _stateMachineNew = GetComponent<StateMachineNew>();
            agent = _stateMachineNew.Agent;
            charAnim = _stateMachineNew.CharAnim;
        }

        public void OnStateEnter()
        {
            Debug.Log($"[{_stateMachineNew.gameObject.name}] Entering PrepareAttackState.");
            timer = 0f;
            // Potentially move to an optimal attack spot if not already there
            // attackPosition = CalculateOptimalAttackPosition();
            // agent.SetDestination(attackPosition);
            // agent.isStopped = false;
            // For now, assume in position or handled by Chase/Circle

            agent.isStopped = true; // Stop to telegraph
            _stateMachineNew.RotateToFacePlayer();
            // charAnim.PlayTelegraphAnimation(); // Or set a bool/trigger
            charAnim.SetAiming(true); // Example of a "tell"
            // Or use NPCController to start a specific telegraph sequence:
            // stateMachine.NpcWeaponController?.StartTelegraph();
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
                    _stateMachineNew.SwitchState(_stateMachineNew.FindState<W_StrikeState>()); // Or your main attack state
                }
                else
                {
                    // Lost attack slot during telegraph (e.g., player moved far, another NPC took over)
                    Debug.Log($"[{_stateMachineNew.gameObject.name}] Lost attack slot during PrepareAttack. Returning to Circle.");
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
    }
}