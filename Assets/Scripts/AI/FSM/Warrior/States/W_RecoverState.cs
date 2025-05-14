using AI.FSM.NPC;
using AI.FSM.NPC.States;
using UnityEngine;

// ... other usings ...
namespace AI.FSM.Warrior.States
{
    public class W_RecoverState : MonoBehaviour, IState
    {
        private WarriorStateMachine stateMachine;
        private float recoveryDuration = 0.5f; // Example
        private float timer;

        void Awake() { stateMachine = GetComponent<WarriorStateMachine>(); }

        public void OnStateEnter()
        {
            Debug.Log($"[{stateMachine.gameObject.name}] Entering RecoverState.");
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
                stateMachine.SwitchState(stateMachine.FindState<W_CirclingState>());
                //add this later  ?? stateMachine.FindState<IdleState>()
            }
        }

        public void OnStateExit() { Debug.Log($"[{stateMachine.gameObject.name}] Exiting RecoverState."); }
    }
}