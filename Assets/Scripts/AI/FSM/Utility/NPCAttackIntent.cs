using System;
using AI.FSM.NPC;
using AI.FSM.Warrior.States;
using UnityEngine;

namespace AI.FSM.Utility
{
    public class NPCAttackIntent
    {
        private readonly WarriorStateMachine _fsm;
        private readonly Transform _npcTransform;
        private readonly Transform _playerTransform;

        private readonly float _attackDistance;
        private readonly float _aggressionRadius;
        private readonly NPCMemoryComponent _memory;

        private float _nextRequestTime = 0f;
        private const float RequestCooldown = 1.5f;

        public NPCAttackIntent(WarriorStateMachine fsm, float aggressionRadius = 4f)
        {
            _fsm = fsm;
            _npcTransform = fsm.transform;
            _playerTransform = fsm.Player;
            _attackDistance = fsm.GetMaxEngagementDistance();
            _aggressionRadius = aggressionRadius;
            _memory = _fsm.GetComponent<NPCMemoryComponent>();
        }

        /// <summary>
        /// Determines whether this NPC should attack right now,
        /// either because it's their turn or due to close proximity override.
        /// </summary>
        public bool ShouldAttack()
        {
            if (_fsm == null || _fsm.IsDead || _playerTransform == null)
                return false;

            return HasAssignedAttackTurn() || CanForceAttackByProximity();
        }

        public bool HasAssignedAttackTurn()
        {
            return NPCManager.Instance?.GetPrimaryAttacker() == _fsm;
        }
        
        public bool CanForceAttackByProximity()
        {
            if (_playerTransform == null || !_fsm.IsPlayerAttackable()) return false;

            float distance = Vector3.Distance(_npcTransform.position, _playerTransform.position);
            if (distance > _aggressionRadius) return false;

            float cooldown = 1.5f - (_memory?.aggression ?? 0.5f); // More aggressive = lower cooldown

            if (_memory != null && _memory.TimeSinceLastAttackRequest < cooldown)
                return false;

            bool success = NPCManager.Instance.RequestAttackPermission(_fsm);
            _memory?.MarkAttackRequest(success);
            return success;
        }


        private bool IsPlayerVisibleOrRecentlyVisible()
        {
            // You can inject or cache this flag in FSM if needed
            return _fsm.IsPlayerVisible(); // Could be replaced with .WasRecentlyVisible if desired
        }

        /// <summary>
        /// Call when switching to PrepareAttack to centralize cleanup.
        /// </summary>
        public void CommitAttack()
        {
            Debug.Log($"[{_fsm.name}] Committing attack. Switching to PrepareAttackState.");
            var prep = _fsm.FindState<W_PrepareAttackState>();
            if (prep != null)
                _fsm.SwitchState(prep);
            else
                Debug.LogError($"[{_fsm.name}] W_PrepareAttackState not found.");
        }
    }
}
