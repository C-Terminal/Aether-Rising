using System;
using UnityEngine;

namespace AI.FSM.Utility
{
    [DisallowMultipleComponent]
    public class NPCMemoryComponent : MonoBehaviour
    {
        [Header("Combat Personality")]
        [Range(0f, 1f)]
        public float aggression = 0.5f;

        [Range(0f, 1f)]
        public float morale = 0.7f;

        [Header("Runtime Memory")]
        public bool lastAttackRequestSuccess;
        public float lastAttackRequestTime;

        public Vector3? lastKnownPlayerPosition;
        public float lastPlayerSightingTime;

        public void MarkPlayerSeen(Vector3 position)
        {
            lastKnownPlayerPosition = position;
            lastPlayerSightingTime = Time.time;
        }

        public void ClearPlayerMemory()
        {
            lastKnownPlayerPosition = null;
        }

        public bool HasRecentPlayerMemory(float maxAge = 5f)
        {
            return lastKnownPlayerPosition.HasValue && (Time.time - lastPlayerSightingTime) <= maxAge;
        }

        public void MarkAttackRequest(bool success)
        {
            lastAttackRequestSuccess = success;
            lastAttackRequestTime = Time.time;
        }
        
        

        public float TimeSinceLastAttackRequest => Time.time - lastAttackRequestTime;
    }
}