using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Elements
{
    public class InventorySlot : MonoBehaviour
    
    {
        public Image icon;
        public TMP_Text itemName;

        public void SetSlot(Sprite itemIcon, string name)
        {
            icon.sprite = itemIcon;
            itemName.text = name;
        }
    }
}