using AI.FSM.NPC;
using UnityEditor;
using UnityEngine;

namespace AI.NPC.DebugTools
{
    public class AIDebugConsole : MonoBehaviour
    {
        [SerializeField] private Color labelColor = Color.white;
        [SerializeField] private float verticalOffset = 2.5f;
        [SerializeField] private bool showDebug = true;

        private WarriorStateMachine _fsm;

        private void Awake()
        {
            _fsm = GetComponent<WarriorStateMachine>();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !showDebug || _fsm == null) return;

            Vector3 labelPosition = transform.position + Vector3.up * verticalOffset;

            string label = $"<b>{_fsm.name}</b>\n";
            label += $"State: <color=cyan>{_fsm.CurrentState?.GetType().Name}</color>\n";

            if (_fsm.Player != null)
            {
                bool seesPlayer = _fsm.IsPlayerVisible();
                bool canAttack = _fsm.IsPlayerAttackable();

                label += $"Target: {_fsm.Player.name}\n";
                label += seesPlayer
                    ? "<color=green>[SEES]</color>\n"
                    : "<color=grey>[NO SIGHT]</color>\n";

                label += canAttack
                    ? "<color=red>[CAN ATTACK]</color>\n"
                    : "<color=yellow>[TOO FAR]</color>\n";
            }
            else
            {
                label += "<color=grey>No Target</color>\n";
            }

            Handles.BeginGUI();
            var view = SceneView.currentDrawingSceneView.camera.WorldToScreenPoint(labelPosition);

            if (view.z > 0)
            {
                var rect = new Rect(view.x - 60, Screen.height - view.y - 30, 160, 70);
                GUI.color = labelColor;
                GUI.Label(rect, label, new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
            }

            Handles.EndGUI();
        }
#endif
    }
}