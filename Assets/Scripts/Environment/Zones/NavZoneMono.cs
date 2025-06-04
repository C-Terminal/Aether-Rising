using System.Collections.Generic;
using UnityEngine;

namespace Environment.Zones
{
    [DisallowMultipleComponent]
    public class NavZoneMono : MonoBehaviour
    {
        [Tooltip("Patrol points (empty GameObjects) within this zone")]
        public List<Transform> patrolPoints = new();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < patrolPoints.Count; i++)
            {
                if (patrolPoints[i] == null) continue;

                Gizmos.DrawSphere(patrolPoints[i].position, 0.25f);

                var next = patrolPoints[(i + 1) % patrolPoints.Count];
                if (next != null)
                    Gizmos.DrawLine(patrolPoints[i].position, next.position);
            }
        }
    }
}