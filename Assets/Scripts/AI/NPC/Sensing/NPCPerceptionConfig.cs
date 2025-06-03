using UnityEngine;

namespace AI.NPC.Sensing
{
    [CreateAssetMenu(fileName = "NewNPCPerceptionConfig", menuName = "AI/NPC Perception Config")]
    public class NPCPerceptionConfig : ScriptableObject
    {
        [Header("Vision Settings")]
        [Tooltip("Field of view in degrees (e.g., 180)")]
        public float fieldOfView = 180f;

        [Tooltip("How frequently to check for visibility (in seconds)")]
        public float visibilityCheckInterval = 0.2f;

        [Header("Attack Logic")]
        [Tooltip("Distance at which NPC considers player attackable")]
        public float actionRange = 3f;

        [Header("Rotation")]
        [Tooltip("Rotation speed when turning to face the player")]
        public float rotationSpeed = 2f;
    
        public FOVMode fovMode = FOVMode.Calm;

        public float GetFOVAngle()
        {
            return (float)fovMode;
        }
        
    }
    
    
    
    public enum FOVMode
    {
        Calm = 120,
        Alert = 180,
        Aggressive = 270,
        Panicked = 360
    }
}