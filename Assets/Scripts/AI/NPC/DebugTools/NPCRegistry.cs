using System.Collections.Generic;
using AI.FSM.NPC;
using UnityEngine;

namespace AI.NPC.DebugTools
{
    public class NPCRegistry : MonoBehaviour
    {
        public static NPCRegistry Instance { get; private set; }

        private readonly List<WarriorStateMachine> _npcs = new();

        public IReadOnlyList<WarriorStateMachine> NPCs => _npcs;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Register(WarriorStateMachine fsm)
        {
            if (!_npcs.Contains(fsm))
                _npcs.Add(fsm);
        }

        public void Unregister(WarriorStateMachine fsm)
        {
            _npcs.Remove(fsm);
        }
    }
}