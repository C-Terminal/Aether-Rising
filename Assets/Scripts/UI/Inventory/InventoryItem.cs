using UnityEngine;

namespace RPG.Inventory
{
// Create -> RPG -> Inventory Item in the Project window asset menu
    [CreateAssetMenu(fileName = "New Inventory Item", menuName = "RPG/Inventory Item")]
    public class InventoryItem : ScriptableObject
    {
        public string itemName = "New Item";
        [TextArea(3, 5)]
        public string description = "Item Description";
        public Sprite icon = null;
        public bool isStackable = true;
        public int maxStackSize = 99;
        // Add other relevant item properties: type (consumable, weapon, armor), stats, effects, etc.

        // Example method for using the item (can be expanded)
        public virtual void Use()
        {
            Debug.Log($"Using {itemName}");
            // Add specific use logic here (e.g., heal player, equip item)
        }
    }
}