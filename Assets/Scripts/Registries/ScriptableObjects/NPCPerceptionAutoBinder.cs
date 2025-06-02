using AI.FSM.NPC;
using AI.NPC.Movement;
using AI.NPC.Sensing;
using Registries.ScriptableObjects;
using UnityEngine;

public class NPCPerceptionAutoBinder : MonoBehaviour
{
    [Tooltip("What role does this NPC fulfill?")]
    [SerializeField] private NPCRoleType roleType;

    [Tooltip("Global config registry")]
    [SerializeField] private NPCPerceptionConfigRegistry configRegistry;

    private void Awake()
    {
        if (configRegistry == null)
        {
            Debug.LogError($"{name}: No config registry assigned for NPC auto-binder.");
            return;
        }

        var config = configRegistry.GetConfig(roleType);
        if (config == null) return;

        AssignConfigToComponents(config);
    }

    private void AssignConfigToComponents(NPCPerceptionConfig config)
    {
        // Assign to VisionSensor
        var vision = GetComponent<VisionSensor>();
        if (vision != null) vision.SetConfig(config);

        // Assign to WarriorStateMachine (or your FSM base)
        var fsm = GetComponent<WarriorStateMachine>();
        if (fsm != null)
        {
            var field = typeof(WarriorStateMachine).GetField("perceptionConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(fsm, config);
        }

        // Assign to movement (optional)
        var move = GetComponent<AIMovementController>();
        if (move != null)
        {
            var field = typeof(AIMovementController).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(move, config);
        }
    }
}