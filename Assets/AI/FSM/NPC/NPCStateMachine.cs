using System.Collections.Generic;
using UnityEngine;

namespace AI.FSM.NPC
{
    public class NPCStateMachine : StateMachine
    {
        [Tooltip("NPC's distance from Player to begin Chase.")]
        [SerializeField] private float visibleChaseDistance = 15f;
        [Tooltip("NPC's visibility angle for initiating chase")]
        [SerializeField] private float visibleChaseAngle = 70f;
        [Tooltip("NPC's attack distance to Player")]
        [SerializeField] private float attackDistance = 3f;
        
        public List<IState> states = new List<IState>(); // Holds all attached state components
        private IState startingState;
        private Transform player; // Reference to the Player transform
        private float rotSpeed = 1.5f;
        // private bool isPlayerDead; // Example of shared data
        
        public Transform Player => player;
    }
}