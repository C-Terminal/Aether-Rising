using AI.FSM;
using AI.FSM.NPC;
using AI.FSM.NPC.States;
using AI.FSM.Warrior.States;
using AI.NPC.DebugTools;
using UnityEngine;

public class AIHearingSensor : MonoBehaviour
{
    [SerializeField] private float hearingRadius = 10f;
    [SerializeField] private LayerMask soundLayerMask;

    public void HearSound(Vector3 soundPosition, float volume)
    {
        float effectiveRadius = hearingRadius * volume;

        float distance = Vector3.Distance(transform.position, soundPosition);
        if (distance <= effectiveRadius)
        {
            Debug.Log($"{name}: Heard sound at {soundPosition} (vol: {volume})");

            // Optional: Visual debug
            var visualizer = GetComponent<NPCPerceptionGizmos>();
            if (visualizer != null)
                visualizer.MarkPlayerPosition(soundPosition);

            // Notify FSM if appropriate
            var fsm = GetComponent<WarriorStateMachine>();
            if (fsm != null)
            {
                // Transition to alert/search state, or face sound
                IState alert = fsm.FindState<W_AlertState>();
                if (alert != null)
                    fsm.SwitchState(alert);
            }
        }
    }
}