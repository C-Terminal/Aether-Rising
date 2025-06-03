using System.Collections.Generic;
using AI.NPC.Sensing;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AI.NPC.Debugging
{
    [ExecuteAlways]
    public class NPCVisionDebugger : MonoBehaviour
    {
        [SerializeField] private Color seesPlayerColor = Color.green;
        [SerializeField] private Color inZoneColor = Color.yellow;
        [SerializeField] private Color idleColor = Color.gray;
        [SerializeField] private bool showLabels = true;

        private readonly List<NPCPerceptionCoordinator> coordinators = new();

        private void OnEnable()
        {
            RefreshCoordinatorList();
        }

        private void Update()
        {
#if UNITY_EDITOR
            // Refresh every 60 frames in editor
            if (!Application.isPlaying && Time.frameCount % 60 == 0)
                RefreshCoordinatorList();
#endif
        }

        private void RefreshCoordinatorList()
        {
            coordinators.Clear();
            coordinators.AddRange(FindObjectsOfType<NPCPerceptionCoordinator>());
        }

        private void OnDrawGizmos()
        {
            foreach (var coordinator in coordinators)
            {
                if (coordinator == null) continue;

                var npcPos = coordinator.transform.position;
                var player = coordinator.GetCurrentTarget();
                var visible = coordinator.IsPlayerCurrentlyVisible();
                var inZone = coordinator.IsPlayerInDetectionZone();

                if (player == null) continue;

                Vector3 eyeLevelNPC = npcPos + Vector3.up * 1.5f;
                Vector3 eyeLevelPlayer = player.position + Vector3.up * 1.5f;

                Gizmos.color = visible ? seesPlayerColor :
                               inZone ? inZoneColor : idleColor;

                Gizmos.DrawLine(eyeLevelNPC, eyeLevelPlayer);
                Gizmos.DrawSphere(eyeLevelPlayer, visible ? 0.3f : 0.2f);

#if UNITY_EDITOR
                if (showLabels)
                {
                    Handles.color = Gizmos.color;
                    Handles.Label(eyeLevelNPC + Vector3.up,
                        $"[{coordinator.name}] {(visible ? "SEES" : inZone ? "IN ZONE" : "IDLE")}");
                }
#endif
            }
        }
    }
}
