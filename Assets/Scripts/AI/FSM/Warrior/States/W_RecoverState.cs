using AI.FSM.NPC;
using AI.FSM.NPC.States;
using UnityEngine;

// ... other usings ...
namespace AI.FSM.Warrior.States
{
    public class W_RecoverState : MonoBehaviour, IState
    {
        private StateMachineNew _stateMachineNew;
        private float recoveryDuration = 0.5f; // Example
        private float timer;

        void Awake() { _stateMachineNew = GetComponent<StateMachineNew>(); }

        public void OnStateEnter()
        {
            Debug.Log($"[{_stateMachineNew.gameObject.name}] Entering RecoverState.");
            timer = 0f;
            // Play recovery/idle animation - CharacterAnimator should already be transitioning
            // or a specific recovery animation could be triggered.
            // stateMachine.CharAnim.SetMovementSpeed(0); // Ensure idle if recovery is just waiting
        }

        public void OnStateUpdate(float deltaTime)
        {
            timer += deltaTime;
            if (timer >= recoveryDuration)
            {
                _stateMachineNew.SwitchState(_stateMachineNew.FindState<W_CirclingState>());
                //add this later  ?? stateMachine.FindState<IdleState>()
            }
        }

        public void OnStateExit() { Debug.Log($"[{_stateMachineNew.gameObject.name}] Exiting RecoverState."); }
    }
}