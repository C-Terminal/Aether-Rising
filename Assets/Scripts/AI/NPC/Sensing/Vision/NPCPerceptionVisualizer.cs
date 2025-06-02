using UnityEngine;

namespace AI.NPC.Sensing.Vision
{
    [ExecuteAlways]
    public class NPCPerceptionVisualizer : MonoBehaviour
    {
        [SerializeField] private NPCPerceptionConfig config;
        [SerializeField] private Color viewConeColor = new Color(1f, 0.5f, 0f, 0.2f);
        [SerializeField] private float viewDistance = 10f;
        [SerializeField] private bool drawOnSelectedOnly = true;

        private void OnDrawGizmos()
        {
            if (!drawOnSelectedOnly) DrawFOV();
        }

        private void OnDrawGizmosSelected()
        {
            if (drawOnSelectedOnly) DrawFOV();
        }

        private void DrawFOV()
        {
            if (config == null) return;

            Gizmos.color = viewConeColor;

            Vector3 origin = transform.position + Vector3.up * 1.5f; // Eye-level
            Vector3 forward = transform.forward;

            float halfFOV = config.fieldOfView / 2f;

            Quaternion leftRayRotation = Quaternion.Euler(0, -halfFOV, 0);
            Quaternion rightRayRotation = Quaternion.Euler(0, halfFOV, 0);

            Vector3 leftRayDirection = leftRayRotation * forward;
            Vector3 rightRayDirection = rightRayRotation * forward;

            Gizmos.DrawRay(origin, leftRayDirection * viewDistance);
            Gizmos.DrawRay(origin, rightRayDirection * viewDistance);

            // Optional arc preview (wire arc is not built-in; use Handles for editor drawing if needed)
            Gizmos.DrawWireSphere(origin, config.actionRange);
        }
    }
}