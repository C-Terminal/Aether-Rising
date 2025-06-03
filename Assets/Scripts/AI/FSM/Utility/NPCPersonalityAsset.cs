using UnityEngine;

[CreateAssetMenu(fileName = "NewNPCPersonality", menuName = "AI/NPC Personality")]
public class NPCPersonalityAsset : ScriptableObject
{
    [Range(0f, 1f)] public float aggression = 0.5f;
    [Range(0f, 1f)] public float morale = 0.7f;

    [Tooltip("Name for designer reference")]
    public string description = "Default personality template.";
}