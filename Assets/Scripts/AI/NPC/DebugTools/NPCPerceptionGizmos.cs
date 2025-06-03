using UnityEngine;

namespace AI.NPC.DebugTools
{
    [ExecuteAlways]
    public class NPCPerceptionGizmos : MonoBehaviour
    {
        [SerializeField] private float hearingRadius = 8f;
        [SerializeField] private float threatZoneRadius = 12f;

        [SerializeField] private Color audioColor = new Color(0.3f, 0.7f, 1f, 0.2f);
        [SerializeField] private Color threatColor = new Color(1f, 0.2f, 0.2f, 0.2f);

        public Vector3? lastKnownPlayerPosition;

        private void OnDrawGizmosSelected()
        {
            Vector3 pos = transform.position + Vector3.up * 1f;

            Gizmos.color = audioColor;
            Gizmos.DrawWireSphere(pos, hearingRadius);

            Gizmos.color = threatColor;
            Gizmos.DrawWireSphere(pos, threatZoneRadius);

            if (lastKnownPlayerPosition.HasValue)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(lastKnownPlayerPosition.Value + Vector3.up * 1.5f, 0.25f);
                UnityEditor.Handles.Label(lastKnownPlayerPosition.Value + Vector3.up * 2f, "Last Known Position");
            }
        }

        public void MarkPlayerPosition(Vector3 pos)
        {
            lastKnownPlayerPosition = pos;
            // You can also start a timer to clear it after X seconds.
        }
    }
}