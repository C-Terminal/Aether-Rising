using AI.FSM.NPC;
using AI.FSM.NPC.States;
using AI.FSM.Warrior.States;
using UnityEngine;

namespace AI.FSM.Utility
{
    public class NPCIntentController 
    {
        private readonly WarriorStateMachine _fsm;
        private readonly Transform _player;
        private readonly Transform _npc;

        private readonly float fallbackDistance = 12f;
        private readonly float coverSearchRadius = 10f;

        public NPCIntentController(WarriorStateMachine fsm)
        {
            _fsm = fsm;
            _npc = fsm.transform;
            _player = fsm.Player;
        }

        /// <summary>
        /// Should seek cover due to low health or outnumbered status.
        /// </summary>
        public bool ShouldSeekCover()
        {
            if (_fsm.WarriorHealth == null) return false;
            if (_fsm.WarriorHealth.CurrentHealth > 40f) return false;

            // Add more logic here (e.g. flanked, multiple enemies nearby)
            return true;
        }

        /// <summary>
        /// Transition to cover state if possible.
        /// </summary>
        public bool SeekCover()
        {
            IState cover = _fsm.FindState<CoverState>();
            if (cover != null)
            {
                _fsm.SwitchState(cover);
                Debug.Log($"[{_fsm.name}] Seeking cover!");
                return true;
            }

            Debug.LogWarning($"[{_fsm.name}] No CoverState found.");
            return false;
        }


        /// <summary>
        /// Retreat if player is too close and low on health.
        /// </summary>
        public bool ShouldFallback()
        {
            if (_player == null || _fsm.IsDead) return false;
        
            float distance = Vector3.Distance(_npc.position, _player.position);
            float morale = _fsm.GetComponent<NPCMemoryComponent>()?.morale ?? 0.7f;
        
            bool lowHealth = _fsm.WarriorHealth.CurrentHealth <= 30f;
            bool isAfraid = morale < 0.5f;
        
            return lowHealth && isAfraid && distance < 6f;
        }


        /// <summary>
        /// Transition to fallback/retreat state.
        /// </summary>
        public bool Fallback()
        {
            IState retreat = _fsm.FindState<W_RetreatState>();
            if (retreat != null)
            {
                _fsm.SwitchState(retreat);
                Debug.Log($"[{_fsm.name}] Falling back!");
                return true;
            }

            Debug.LogWarning($"[{_fsm.name}] Retreat state not found.");
            return false;
        }

        /// <summary>
        /// Flank if player is visible and stationary.
        /// </summary>
        public bool ShouldFlank()
        {
            // Later: Add flank based on player orientation, AI role
            return false;
        }

        public void TickPerFrame()
        {
            // Optional: add adaptive urgency, group calls, or dynamic priorities
        }

        public bool ShouldFallbackFromCircling()
        {
            return Random.value < 0.25f;
        }
    }
}
