using System;
using UnityEngine;
using UnityEngine.AI;

namespace AI.FSM.NPC.States
{
// ChaseState.cs (Excerpt from Listing 3-10 - demonstrating a concrete state)
    public class ChaseState : MonoBehaviour, IState
    {
        [Tooltip("Speed at which NavMeshAgent moves while Chasing")] [SerializeField]
        private float npcSpeed = 4f;

        private NavMeshAgent agent;
        private IState previousStateBeforeChase; // Store the state before entering chase

        private NPCStateMachine stateMachine;

        private void Awake()
        {
            stateMachine = GetComponent<NPCStateMachine>(); // Assuming NPCStateMachine is on the same GameObject
            if (stateMachine == null) Debug.LogError($"[ChaseState - {gameObject.name}] : NPCStateMachine not found.");
            agent = GetComponent<NavMeshAgent>();
            if (agent == null) Debug.LogError($"[ChaseState - {gameObject.name}] : No NavMesh Agent found.");
        }

        public void OnStateEnter()
        {
            OnNpcChase?.Invoke();
            
            if (agent != null && agent.enabled)
            {
                agent.speed = npcSpeed;
                agent.isStopped = false;
            }

            OnNpcChase?.Invoke(); // Trigger chase animation/sound
            Debug.Log("ChaseState: Enter");

            // Store the state from which we entered Chase, unless it was Chase itself or Attack
            if (stateMachine.PreviousState != null &&
                stateMachine.PreviousState.GetType() != typeof(ChaseState) &&
                stateMachine.PreviousState.GetType() != typeof(AttackState))
                previousStateBeforeChase = stateMachine.PreviousState;
        }

        public void OnStateUpdate(float deltaTime)
        {
            if (!stateMachine.IsPlayerVisible())
            {
                // Player lost, revert to previous relevant state (e.g., Patrol or Wander)
                if (previousStateBeforeChase != null)
                {
                    stateMachine.SwitchState(previousStateBeforeChase);
                }
                else // Fallback to Idle or a default state if previous is not set
                {
                    var idle = stateMachine.states.Find(s => s.GetType() == typeof(IdleState));
                    if (idle != null) stateMachine.SwitchState(idle);
                }

                return;
            }

            if (agent != null && agent.enabled && stateMachine.Player != null)
                agent.SetDestination(stateMachine.Player.position);

            if (stateMachine.IsPlayerAttackable())
            {
                Debug.Log("ChaseState: Player in Attackable Range");
                var attack = stateMachine.states.Find(s => s.GetType() == typeof(AttackState));
                if (attack != null) stateMachine.SwitchState(attack);
            }
        }

        public void OnStateExit()
        {
            // Optional: stop agent if needed, or handled by next state's OnStateEnter
            // agent.isStopped = true; 
            Debug.Log("ChaseState: Exit");
        }

        public event Action OnNpcChase;
    }
}