using AI.FSM.NPC;
using TMPro;
using UnityEngine;

namespace AI.FSM.Utility
{
    public class NPCDebuggerHUD : MonoBehaviour
    {
        public TextMeshProUGUI stateText;
        public TextMeshProUGUI memoryText;
        public Transform followTarget;

        private WarriorStateMachine _fsm;
        private NPCMemoryComponent _memory;

        void Start()
        {
            _fsm = followTarget?.GetComponent<WarriorStateMachine>();
            _memory = _fsm?.GetComponent<NPCMemoryComponent>();
        }

        void Update()
        {
            if (_fsm == null || _memory == null) return;

            transform.position = Camera.main.WorldToScreenPoint(followTarget.position + Vector3.up * 2.5f);

            stateText.text = $"<b>State:</b> {_fsm.CurrentState?.GetType().Name}";
            memoryText.text = $"<b>Aggression:</b> {_memory.aggression:F2}\n" +
                              $"<b>Morale:</b> {_memory.morale:F2}\n" +
                              $"<b>Last Seen:</b> " +
                              $"{(_memory.HasRecentPlayerMemory() ? "Yes" : "No")}";
        }
    }
}