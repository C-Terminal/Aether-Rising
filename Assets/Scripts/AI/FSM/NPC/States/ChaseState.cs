using System;
using Characters.ExoGray.Scripts;
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
        private AIMovementSensor movementSensor;
        private IState previousStateBeforeChase; // Store the state before entering chase
        private Transform playerTarget;
        private FSM.StateMachineNew _stateMachineNew;

        private void Awake()
        {
            _stateMachineNew = GetComponent<FSM.StateMachineNew>(); // Assuming NPCStateMachine is on the same GameObject
            if (_stateMachineNew == null) Debug.LogError($"[ChaseState - {gameObject.name}] : NPCStateMachine not found.");
            agent = GetComponent<NavMeshAgent>();
            if (agent == null) Debug.LogError($"[ChaseState - {gameObject.name}] : No NavMesh Agent found.");
            movementSensor = GetComponent<AIMovementSensor>();
        }

        public void OnStateEnter()
        {
            movementSensor.SetTarget(_stateMachineNew.Player);
            if (agent != null && agent.enabled)
            {
                agent.speed = npcSpeed;
                agent.isStopped = false;
            }

            OnNpcChase?.Invoke(); // Trigger chase animation/sound
            Debug.Log("ChaseState: Enter");

            // Store the state from which we entered Chase, unless it was Chase itself or Attack
            if (_stateMachineNew.PreviousState != null &&
                _stateMachineNew.PreviousState.GetType() != typeof(ChaseState) &&
                _stateMachineNew.PreviousState.GetType() != typeof(AttackState))
                previousStateBeforeChase = _stateMachineNew.PreviousState;
        }

        public void OnStateUpdate(float deltaTime)
        {
            if (!_stateMachineNew.IsPlayerVisible())
            {
                // Player lost, revert to previous relevant state (e.g., Patrol or Wander)
                if (previousStateBeforeChase != null)
                {
                    _stateMachineNew.SwitchState(previousStateBeforeChase);
                }
                else // Fallback to Idle or a default state if previous is not set
                {
                    var idle = _stateMachineNew.states.Find(s => s.GetType() == typeof(IdleState));
                    if (idle != null) _stateMachineNew.SwitchState(idle);
                }

                return;
            }

            if (agent != null && agent.enabled && _stateMachineNew.Player != null)
            {
                //TODO: add boolean to prevent repeat calls
                playerTarget = _stateMachineNew.Player;
                agent.SetDestination(playerTarget.position); // Or NPCStateMachine.MoveToPlayer());
                if (Vector3.Distance(transform.position, playerTarget.position) <= agent.stoppingDistance)
                    movementSensor.FaceCurrentTarget(); // Or NPCStateMachine.RotateToFacePlayer()
            }

            if (_stateMachineNew.IsPlayerAttackable())
            {
                Debug.Log("ChaseState: Player in Attackable Range");
                var attack = _stateMachineNew.states.Find(s => s.GetType() == typeof(AttackState));
                if (attack != null) _stateMachineNew.SwitchState(attack);
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