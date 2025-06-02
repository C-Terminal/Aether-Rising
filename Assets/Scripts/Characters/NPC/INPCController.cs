using UnityEngine;

namespace Characters.NPC
{
    public interface INPCController
    {
        void EquipWeapon(NPCController.ArsenalItem item);
        // void EngageTarget(Transform target);
    }
}