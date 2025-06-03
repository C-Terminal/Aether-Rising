using System;
using System.Threading;
using AI.FSM.NPC;
using AI.FSM.NPC.States;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace AI.FSM.Warrior.States
{
    public class W_AlertState : MonoBehaviour, IState, IInitializableState
    {
        [Header("Alert Search Settings")]
        [SerializeField] private float alertSpeed = 3.5f;
        [SerializeField] private float searchRadius = 12f;
        [SerializeField] private float waitTimeAtPoint = 2.5f;
        [SerializeField] private float minTimeBetweenMoves = 3f;
        [SerializeField] private float maxTimeBetweenMoves = 6f;
        [SerializeField] private int maxNavTries = 5;

        private StateMachineNew _stateMachine;
        private NavMeshAgent _agent;
        private Vector3 _investigationCenter;
        private CancellationTokenSource _cts;
        private bool _hasMemory;

        public void Initialize(StateMachineNew machine)
        {
            _stateMachine = machine;
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
                Debug.LogError($"{name}: AlertState requires a NavMeshAgent.", this);
        }

        public void OnStateEnter()
        {
            Debug.Log($"[AlertState] Entered alert state.");

            _hasMemory = NPCManager.Instance.IsPlayerGloballySpotted;
            _investigationCenter = NPCManager.Instance.LastKnownPlayerPosition ?? transform.position;

            _agent.speed = alertSpeed;
            _agent.isStopped = false;

            _cts = new CancellationTokenSource();
            RunAlertSearch(_cts.Token).Forget();
        }

        public void OnStateUpdate(float deltaTime)
        {
            // Check if player became visible
            if (_stateMachine.IsPlayerVisible())
            {
                Debug.Log("[AlertState] Player spotted during search.");
                var chase = _stateMachine.FindState<ChaseState>();
                if (chase != null)
                    _stateMachine.SwitchState(chase);
            }
        }

        public void OnStateExit()
        {
            _cts?.Cancel();
            _agent?.SetDestination(transform.position); // halt movement
        }

        private async UniTaskVoid RunAlertSearch(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Vector3 searchPoint = GetRandomSearchPoint();

                if (MoveTo(searchPoint))
                {
                    await UniTask.WaitUntil(() => _agent.remainingDistance <= _agent.stoppingDistance + 0.1f,
                        cancellationToken: token);

                    await LookAround(token);
                    await UniTask.Delay(TimeSpan.FromSeconds(Random.Range(minTimeBetweenMoves, maxTimeBetweenMoves)), cancellationToken: token);
                }
                else
                {
                    Debug.LogWarning("[AlertState] Failed to find valid point, retrying.");
                    await UniTask.Delay(1000, cancellationToken: token);
                }
            }
        }

        private async UniTask LookAround(CancellationToken token)
        {
            float totalTime = 0f;
            float duration = waitTimeAtPoint;
            float interval = 1.5f;

            while (totalTime < duration && !token.IsCancellationRequested)
            {
                float angle = Random.Range(-90f, 90f);
                Vector3 lookDir = Quaternion.Euler(0, angle, 0) * transform.forward;

                await RotateToDirection(lookDir, 0.4f, token);
                totalTime += interval;
            }
        }

        private async UniTask RotateToDirection(Vector3 dir, float duration, CancellationToken token)
        {
            if (dir == Vector3.zero) return;

            Quaternion target = Quaternion.LookRotation(dir.normalized);
            float elapsed = 0f;
            Quaternion initial = transform.rotation;

            while (elapsed < duration && !token.IsCancellationRequested)
            {
                transform.rotation = Quaternion.Slerp(initial, target, elapsed / duration);
                elapsed += Time.deltaTime;
                await UniTask.Yield(token);
            }

            transform.rotation = target;
        }

        private Vector3 GetRandomSearchPoint()
        {
            Vector3 basePoint = _hasMemory ? _investigationCenter : transform.position;
            Vector3 result = basePoint;

            for (int i = 0; i < maxNavTries; i++)
            {
                Vector3 randomOffset = Random.insideUnitSphere * searchRadius;
                randomOffset.y = 0;
                Vector3 candidate = basePoint + randomOffset;

                if (NavMesh.SamplePosition(candidate, out var hit, searchRadius, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            return result; // fallback
        }

        private bool MoveTo(Vector3 point)
        {
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.SetDestination(point);
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_hasMemory)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_investigationCenter, searchRadius);
            }
            else
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireSphere(transform.position, searchRadius);
            }
        }
#endif
    }
}
