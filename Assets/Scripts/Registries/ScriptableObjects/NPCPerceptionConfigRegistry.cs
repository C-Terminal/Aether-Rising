using AI.NPC.Sensing;
using UnityEngine;

namespace Registries.ScriptableObjects
{
    [CreateAssetMenu(fileName = "NPCPerceptionConfigRegistry", menuName = "AI/NPC Config Registry")]
    public class NPCPerceptionConfigRegistry : ScriptableObject
    {
        [System.Serializable]
        public struct RoleConfig
        {
            public NPCRoleType role;
            public NPCPerceptionConfig config;
        }

        public RoleConfig[] roleConfigs;

        public NPCPerceptionConfig GetConfig(NPCRoleType role)
        {
            foreach (var rc in roleConfigs)
            {
                if (rc.role == role)
                    return rc.config;
            }

            Debug.LogWarning($"[NPC Config Registry] No config found for role: {role}");
            return null;
        }
    }
    public enum NPCRoleType
    {
        Melee,
        Archer,
        Boss
    }
}