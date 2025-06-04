using System.Collections.Generic;
using UnityEngine;

namespace Environment.Zones
{
    [CreateAssetMenu(fileName = "NewNavZone", menuName = "AI/NavZone", order = 100)]
    public class NavZone : ScriptableObject
    {
        [Tooltip("Name for debugging or categorization")]
        public string zoneName;

        [Tooltip("Patrol points assigned to this zone")]
        public List<Transform> patrolPoints = new();
    }
}