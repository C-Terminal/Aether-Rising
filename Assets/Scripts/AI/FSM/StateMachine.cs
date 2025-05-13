using UnityEngine;

namespace AI.FSM
{
    public abstract class StateMachine : MonoBehaviour
    {
        private IState currentState;

        public IState PreviousState { get; private set; }

        // Unity's Update method, called every frame
        private void Update()
        {
            // Call OnStateUpdate on the current state, passing the time since the last frame
            currentState?.OnStateUpdate(Time.deltaTime);
        }

        // Method to switch to a new state
        public void SwitchState(IState newState)
        {
            currentState?.OnStateExit(); // Call OnStateExit on the current state
            PreviousState = currentState; // Update the previous state
            currentState = newState; // Set the new current state
            currentState?.OnStateEnter(); // Call OnStateEnter on the new state
        }
    }
}