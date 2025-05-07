using UI.Elements;
using UnityEngine;

namespace UI
{
    public class InventoryDemo : MonoBehaviour
    {
        public Transform content;
        public GameObject inventorySlotPrefab;
        public Sprite sampleIcon;

        private void Start()
        {
            for (int i = 0; i < 10; i++)
            {
                var slotGO = Instantiate(inventorySlotPrefab, content);
                var slot = slotGO.GetComponent<InventorySlot>();
                slot.SetSlot(sampleIcon, $"Item {i + 1}");
            }
        }
    }
}