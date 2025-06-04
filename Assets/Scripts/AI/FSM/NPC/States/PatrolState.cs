using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AI.FSM.NPC;
using AI.FSM.NPC.States;
using AI.FSM.Utility;

using Characters.NPC;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace AI.FSM.NPC.States
{
    public class PatrolState : MonoBehaviour, IState
    {
        [Header("Waypoints (Optional)")]
        [Tooltip("Optional: Overrides zone patrol points")]
        [SerializeField] private Transform[] customWaypoints;

        [Header("Settings")]
        [SerializeField] private float patrolSpeed = 2.5f;
        [SerializeField] private float waypointTolerance = 1f;
        [SerializeField] private float waitAtWaypoint = 1.5f;
        [Range(0f, 1f)] public float idleChanceAfterCircuit = 0.3f;

        private StateMachineNew _fsm;
        private NavMeshAgent _agent;
        private NPCMemoryComponent _memory;
        private Transform[] _waypoints;
        private int _currentWaypointIndex = 0;

        private bool _isActive;
        private bool _circuitCompleted;
        private CancellationTokenSource _patrolCts;

        public event Action OnNpcPatrol;

        private void Awake()
        {
            _fsm = GetComponent<StateMachineNew>();
            _agent = GetComponent<NavMeshAgent>();
            _memory = GetComponent<NPCMemoryComponent>();
        }

        public void OnStateEnter()
        {
            _isActive = true;
            _patrolCts = new CancellationTokenSource();

            _agent.speed = patrolSpeed;
            _agent.isStopped = false;
            _circuitCompleted = false;

            // Select patrol route
            _waypoints = (customWaypoints != null && customWaypoints.Length > 0)
                ? customWaypoints
                : _memory?.currentZone?.patrolPoints?.Where(p => p != null).ToArray();

            if (_waypoints == null || _waypoints.Length == 0)
            {
                Debug.LogWarning($"[{name}] PatrolState: No patrol route found. Switching to Idle.");
                SwitchToIdle();
                return;
            }

            Debug.Log($"[{_fsm.name}] PatrolState: Starting patrol on {_waypoints.Length} waypoints.");
            OnNpcPatrol?.Invoke();

            PatrolLoop(_patrolCts.Token).Forget();
        }

        public void OnStateUpdate(float deltaTime)
        {
            // No logic needed here — handled by UniTask
        }

        public void OnStateExit()
        {
            _isActive = false;
            _patrolCts?.Cancel();
            _patrolCts?.Dispose();
            _patrolCts = null;

            Debug.Log($"[{_fsm.name}] PatrolState: Exit.");
        }

        private async UniTaskVoid PatrolLoop(CancellationToken ct)
        {
            while (_isActive && !ct.IsCancellationRequested)
            {
                if (_fsm.IsPlayerVisible())
                {
                    Debug.Log($"[{_fsm.name}] PatrolState: Player spotted! Transitioning to Chase.");
                    _fsm.SwitchState(_fsm.FindState<ChaseState>());
                    return;
                }

                var targetPos = _waypoints[_currentWaypointIndex].position;
                _agent.SetDestination(targetPos);

                // Wait until we reach destination
                await UniTask.WaitUntil(
                    () => !_agent.pathPending && _agent.remainingDistance <= waypointTolerance,
                    cancellationToken: ct);

                Debug.Log($"[{_fsm.name}] PatrolState: Reached waypoint {_currentWaypointIndex}");

                // Wait at the point
                await UniTask.Delay(TimeSpan.FromSeconds(waitAtWaypoint), cancellationToken: ct);

                // Go to next waypoint
                _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypoints.Length;

                if (_currentWaypointIndex == 0 && !_circuitCompleted)
                {
                    _circuitCompleted = true;

                    // Decide whether to idle now
                    if (UnityEngine.Random.value < idleChanceAfterCircuit)
                    {
                        Debug.Log($"[{_fsm.name}] PatrolState: Going idle after circuit.");
                        SwitchToIdle();
                        return;
                    }
                }
            }
        }

        private void SwitchToIdle()
        {
            var idle = _fsm.FindState<IdleState>();
            if (idle != null) _fsm.SwitchState(idle);
        }

        private void OnDrawGizmosSelected()
        {
            var displayPoints = customWaypoints?.Where(p => p != null).ToArray();
            if (displayPoints == null || displayPoints.Length <= 1) return;

            Gizmos.color = Color.cyan;

            for (int i = 0; i < displayPoints.Length; i++)
            {
                var current = displayPoints[i];
                var next = displayPoints[(i + 1) % displayPoints.Length];
                Gizmos.DrawLine(current.position, next.position);
                Gizmos.DrawSphere(current.position, 0.25f);
            }
        }
    }
}
