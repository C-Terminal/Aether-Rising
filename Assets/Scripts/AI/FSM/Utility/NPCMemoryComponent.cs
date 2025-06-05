using System;
using Environment.Zones;
using UnityEngine;
#if UNITY_EDITOR
#endif

namespace AI.FSM.Utility
{
    [DisallowMultipleComponent]
    public class NPCMemoryComponent : MonoBehaviour
    {
        [Header("Personality Template")]
        [SerializeField] private NPCPersonalityAsset personalityPreset;
        
        public float aggression = 0.5f;

        [Range(0f, 1f)] public float morale = 0.7f;
        [Tooltip("Current NavZone the NPC is assigned to (e.g. patrol area)")]
        public NavZoneMono currentZone;

        [Header("Runtime Memory")] public bool lastAttackRequestSuccess;

        public float lastAttackRequestTime;
        public float lastPlayerSightingTime;
        public float timeUntilNextCirclingAllowed = 0f;

        [Header("Debug Visualization")] [SerializeField]
        private bool showDebugGizmos = true;

        [SerializeField] private Color aggressionColor = Color.red;
        [SerializeField] private Color moraleColor = Color.blue;
        [SerializeField] private float sphereSize = 0.25f;

        public Vector3? lastKnownPlayerPosition;


        
        public float TimeSinceLastAttackRequest => Time.time - lastAttackRequestTime;

        private void Awake()
        {
            if (personalityPreset != null)
            {
                aggression = personalityPreset.aggression;
                morale = personalityPreset.morale;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            // Aggression intensity as red sphere
            Gizmos.color = aggressionColor;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, aggression * 2f);

            // Morale intensity as blue sphere
            Gizmos.color = moraleColor;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f + Vector3.right * 0.5f, morale * 2f);

            // Last seen player position
            if (lastKnownPlayerPosition.HasValue)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(lastKnownPlayerPosition.Value + Vector3.up * 1f, sphereSize);
                Gizmos.DrawLine(transform.position + Vector3.up, lastKnownPlayerPosition.Value + Vector3.up);
            }
        }

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
            return lastKnownPlayerPosition.HasValue && Time.time - lastPlayerSightingTime <= maxAge;
        }

        public void MarkAttackRequest(bool success)
        {
            lastAttackRequestSuccess = success;
            lastAttackRequestTime = Time.time;
        }
    }
}