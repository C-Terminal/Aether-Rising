using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.Inventory
{
    public class InventorySlotUI : MonoBehaviour
    {
        [Header("UI References")] [SerializeField]
        private Image iconImage;

        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject highlightIndicator; // A simple Image/Panel for selection

        private InventorySlot _currentSlotData;

        private void Awake()
        {
            // Ensure initial state is clean
            if (iconImage == null) Debug.LogError("Icon Image not assigned in InventorySlotUI on " + gameObject.name);
            if (quantityText == null)
                Debug.LogError("Quantity Text not assigned in InventorySlotUI on " + gameObject.name);
            if (highlightIndicator == null)
                Debug.LogError("Highlight Indicator not assigned in InventorySlotUI on " + gameObject.name);

            ClearDisplay();
            SetHighlight(false); // Start deselected
        }
        
        // Updates the visual representation based on slot data
        public void UpdateDisplay(InventorySlot slotData)
        {
            _currentSlotData = slotData;

            if (slotData != null && !slotData.IsEmpty())
            {
                if (iconImage != null)
                {
                    iconImage.sprite = slotData.item.icon;
                    iconImage.enabled = true;
                }
                if (quantityText != null)
                {
                    quantityText.text = slotData.quantity > 1 ? slotData.quantity.ToString() : "";
                    quantityText.enabled = slotData.quantity > 1; // Show only if stack > 1
                }
            }
            else
            {
                ClearDisplay();
            }
        }


        // Clears the visual representation for an empty slot

        public void ClearDisplay()
        {
            _currentSlotData = null;
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (quantityText != null)
            {
                quantityText.text = "";
                quantityText.enabled = false;
            }
        }

        public void SetHighlight(bool isHighlighted)
        {
            if (highlightIndicator != null)
            {
                highlightIndicator.SetActive(isHighlighted);
            }
        }
        // Called when the slot is "submitted" (e.g., Enter/Button South pressed)
        public void OnSubmit()
        {
            if (_currentSlotData != null && !_currentSlotData.IsEmpty())
            {
                Debug.Log($"Submitted action on slot containing: {_currentSlotData.item.itemName}");
                // Trigger item use or other action
                _currentSlotData.item.Use();
                // Potentially update inventory manager if item is consumed
                // InventoryManager.Instance.ConsumeItem(currentSlotData); // Example
            }
            else
            {
                Debug.Log("Submitted action on empty slot.");
            }
        }
    }
}