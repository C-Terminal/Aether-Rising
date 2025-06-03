using System;
using System.Threading;
using AI.FSM.NPC;
using AI.FSM.NPC.States;
using AI.FSM.Utility;
using Animation.AnimControllers;
using Characters.NPC;
using Combat.Weapons.Melee;
using Core.Events;
using Core.Events.Combat;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace AI.FSM.Warrior.States
{
    public class W_StrikeState : MonoBehaviour, IState
    {
        private WarriorStateMachine _fsm;
        private NavMeshAgent _agent;
        private CharacterAnimator _charAnim;
        private NPCController _npcController;
        private MeleeWeaponDamage _weapon;

        private float _estimatedStrikeDuration = 1.2f;
        private CancellationTokenSource _cts;
        private bool _hasClearedSlot;
        private bool _strikeCompleted;

        private NPCMemoryComponent _memory;

        public void InitReferences(StateMachineNew machine)
        {
            _fsm = machine as WarriorStateMachine;
            if (_fsm == null)
            {
                Debug.LogError($"[{gameObject.name}] W_StrikeState: Invalid FSM passed.");
                enabled = false;
                return;
            }

            _agent = _fsm.Agent;
            _charAnim = _fsm.CharAnim;
            _npcController = _fsm.NpcController;
            _memory = _fsm.GetComponent<NPCMemoryComponent>();

            if (_npcController != null)
            {
                _weapon = _npcController.GetCurrentMeleeDamageDealer();

                var item = _npcController.GetCurrentArsenalItem();
                if (item.HasValue && item.Value.strikeDuration > 0)
                    _estimatedStrikeDuration = item.Value.strikeDuration;
            }

            _weapon ??= GetComponentInChildren<MeleeWeaponDamage>();
        }

        public void OnStateEnter()
        {
            if (_fsm == null || _npcController == null || _agent == null || _charAnim == null)
            {
                Debug.LogError($"[{gameObject.name}] W_StrikeState: Missing dependencies.");
                _fsm?.SwitchState(_fsm.FindState<IdleState>());
                return;
            }

            Debug.Log($"[{_fsm.name}] Entered StrikeState");

            _hasClearedSlot = false;
            _strikeCompleted = false;

            _agent.isStopped = true;
            _fsm.RotateToFacePlayer();

            EventManager.AddListener<AttackStrikeCompleteEventData>(OnStrikeComplete);

            _npcController.ExecuteStrikeAction();

            TriggerStrikeEvent();

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            RunStrikeTimer(_cts.Token).Forget();
        }

        private void TriggerStrikeEvent()
        {
            var item = _npcController.GetCurrentArsenalItem();
            if (!item.HasValue) return;

            _estimatedStrikeDuration = item.Value.strikeDuration > 0 ? item.Value.strikeDuration : _estimatedStrikeDuration;

            EventManager.TriggerEvent(new AttackStrikeEventData
            {
                AttackerTransform = transform,
                WeaponType = item.Value.name,
                TargetTransform = _fsm.Player,
                StrikePower = 1.0f
            });
        }

        private async UniTaskVoid RunStrikeTimer(CancellationToken token)
        {
            try
            {
                float elapsed = 0f;
                while (elapsed < _estimatedStrikeDuration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                Debug.Log($"[{_fsm.name}] Strike timer elapsed.");
                FinishStrike();
            }
            catch (OperationCanceledException)
            {
                if (_strikeCompleted)
                    Debug.Log($"[{_fsm.name}] Strike cancelled by animation event.");
                else
                    Debug.Log($"[{_fsm.name}] Strike cancelled prematurely.");
            }
        }

        private void OnStrikeComplete(AttackStrikeCompleteEventData evt)
        {
            if (evt.AttackerTransform != transform) return;

            Debug.Log($"[{_fsm.name}] Strike animation completed via event.");

            _strikeCompleted = true;
            _memory?.MarkAttackRequest(true);

            _cts?.Cancel();

            FinishStrike();
        }

        private void FinishStrike()
        {
            _npcController?.FinishStrikeAction();

            if (!_hasClearedSlot)
            {
                NPCManager.Instance?.ReleaseAttackPermission(_fsm);
                _hasClearedSlot = true;
            }

            EventManager.RemoveListener<AttackStrikeCompleteEventData>(OnStrikeComplete);

            var recover = _fsm.FindState<W_RecoverState>();
            _fsm.SwitchState(recover);
        }

        public void OnStateUpdate(float deltaTime)
        {
            // Optional: Allow minimal rotation during windup/strike
        }

        public void OnStateExit()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            EventManager.RemoveListener<AttackStrikeCompleteEventData>(OnStrikeComplete);

            if (!_strikeCompleted)
            {
                _npcController?.FinishStrikeAction();
                _memory?.MarkAttackRequest(false);
            }

            if (!_hasClearedSlot)
            {
                NPCManager.Instance?.ReleaseAttackPermission(_fsm);
                _hasClearedSlot = true;
                Debug.LogWarning($"[{_fsm.name}] Force-cleared attacker slot in OnStateExit.");
            }
        }
    }
}
