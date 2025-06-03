using System;
using System.Threading;
using AI.FSM.NPC;
using AI.FSM.NPC.States;
using AI.FSM.Utility;
using Characters.NPC;
using Core.Events;
using Core.Events.Combat;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace AI.FSM.Warrior.States
{
    public class RangedStrikeState : MonoBehaviour, IState
    {
        private WarriorStateMachine _fsm;
        private NavMeshAgent _agent;
        private NPCController _controller;
        private NPCMemoryComponent _memory;
        private CancellationTokenSource _cts;
        private bool _hasFired;
        private float _estimatedDuration = 1.5f;

        public void InitReferences(StateMachineNew machine)
        {
            _fsm = machine as WarriorStateMachine;
            _agent = _fsm.Agent;
            _controller = _fsm.NpcController;
            _memory = _fsm.GetComponent<NPCMemoryComponent>();
        }

        public void OnStateEnter()
        {
            if (_fsm == null || _agent == null || _controller == null) return;

            Debug.Log($"[{_fsm.name}] Entered RangedStrikeState.");
            _agent.isStopped = true;
            _hasFired = false;

            _fsm.RotateToFacePlayer();
            _controller.ExecuteRangedAttack(); // This should play animation + spawn projectile

            TriggerRangedEvent();

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            FireAndWait(_cts.Token).Forget();
        }

        private void TriggerRangedEvent()
        {
            EventManager.TriggerEvent(new AttackStrikeEventData
            {
                AttackerTransform = transform,
                WeaponType = "Bow",
                TargetTransform = _fsm.Player,
                StrikePower = 1.0f
            });
        }

        private async UniTaskVoid FireAndWait(CancellationToken token)
        {
            float timer = 0f;
            while (timer < _estimatedDuration)
            {
                token.ThrowIfCancellationRequested();
                timer += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            FinishRangedStrike();
        }

        private void FinishRangedStrike()
        {
            if (!_hasFired)
            {
                NPCManager.Instance?.ReleaseAttackPermission(_fsm);
                _hasFired = true;
                _memory?.MarkAttackRequest(true);
            }

            var recoverOrFallback = _fsm.FindFirstAvailableState(typeof(W_RecoverState), typeof(IdleState));
            _fsm.SwitchState(recoverOrFallback);
        }

        public void OnStateUpdate(float deltaTime) { }

        public void OnStateExit()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (!_hasFired)
            {
                NPCManager.Instance?.ReleaseAttackPermission(_fsm);
                _memory?.MarkAttackRequest(false);
            }
        }
    }
}
