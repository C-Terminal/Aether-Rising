﻿using System.Collections.Generic;
using GameInput;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPG.Inventory
{
    public class InventoryManager : MonoBehaviour
    {
        [Header("Inventory Settings")] [SerializeField]
        private int inventorySize = 20;

        [Header("UI References")] [SerializeField]
        private GameObject inventoryPanel;

        [SerializeField] private Transform inventoryGridContainer;
        [SerializeField] private GameObject inventorySlotPrefab;
        [SerializeField] private GameObject defaultInventoryButton; // Assign the first slot or a specific button

        // Reference to the input handler (can be on the same object or assigned)
        [SerializeField] private InventoryInputHandler inputHandler;
        private readonly List<InventorySlotUI> slotUIElements = new(); // UI elements for each slot
        [SerializeField] private List<InventorySlot> _inventorySlots;


        private ActionMapManager actionMapManager;

        //Singleton
        public static InventoryManager Instance { get; private set; }


        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            //init inventory
            _inventorySlots = new List<InventorySlot>(inventorySize);
            for (var i = 0; i < inventorySize; i++) _inventorySlots.Add(new InventorySlot());

            if (inventoryPanel == null) Debug.LogError("Inventory Panel not assigned!");
            if (inventoryGridContainer == null) Debug.LogError("Inventory Grid Container not assigned!");
            if (inventorySlotPrefab == null) Debug.LogError("Inventory Slot Prefab not assigned!");
            if (inventoryPanel == null) Debug.LogError("Inventory Panel not assigned!");
            // ... other null checks ...
            if (inputHandler == null) inputHandler = GetComponent<InventoryInputHandler>();


            InitializeUI();
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
        }

        private void Start()
        {
            actionMapManager = FindObjectOfType<ActionMapManager>();
            if (actionMapManager != null) actionMapManager.OnToggleUI += OnToggleUI;
        }

        private void OnDestroy()
        {
            if (actionMapManager != null) actionMapManager.OnToggleUI -= OnToggleUI;
        }

        private void OnToggleUI(InputAction.CallbackContext obj)
        {
            // Debug.Log("OnToggleUI called. Inventory panel is now: " + inventoryPanel.activeSelf);
            // ToggleInventory();
        }

        private void InitializeUI()
        {
            if (inventoryGridContainer == null || inventorySlotPrefab == null) return;

            // Clear any existing test slots in editor
            foreach (Transform child in inventoryGridContainer) Destroy(child.gameObject);
            slotUIElements.Clear();

            //instantiate UI slots based on inventory size
            for (var i = 0; i < inventorySize; i++)
            {
                var slotGO = Instantiate(inventorySlotPrefab, inventoryGridContainer);
                slotGO.name = $"InventorySlot_{i}";
                var slotUI = slotGO.GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotUIElements.Add(slotUI);
                    slotUI.UpdateDisplay(_inventorySlots[i]); // Update with initial data (likely empty)
                }
                else
                {
                    Debug.LogError("Inventory Slot Prefab is missing the InventorySlotUI script!");
                }
            }

            // Pass UI elements to input handler for navigation
            if (inputHandler != null) inputHandler.SetSlotUIElements(slotUIElements);
        }

        public void ToggleInventory()
        {
            if (inventoryPanel == null || GameInput.ActionMapManager.Instance == null) // Qualify with namespace if needed
            {
                Debug.LogError("InventoryPanel or ActionMapManager instance is missing!");
                return;
            }
            
            bool shouldBeActive = !inventoryPanel.activeSelf;
            inventoryPanel.SetActive(shouldBeActive);

            if (shouldBeActive)
            {
                GameInput.ActionMapManager.Instance.RequestInputFocus(GameInput.InputFocusState.UIMode, defaultInventoryButton);
                RefreshUI(); // Refresh when opening
                inputHandler?.EnableNavigation(); // Still needed for the inventory's internal navigation
            }
            else
            {
                // Request gameplay focus. ActionMapManager will handle setting the EventSystem's selected object to null.
                GameInput.ActionMapManager.Instance.RequestInputFocus(GameInput.InputFocusState.Gameplay);
                inputHandler?.DisableNavigation();
            }
        }
        
        public bool IsInventoryOpen()
        {
            return inventoryPanel != null && inventoryPanel.activeSelf;
        }
        
        public GameObject GetDefaultSelectedInventoryButton()
        {
            // Return the first slot UI element's GameObject, or a dedicated default button.
            if (defaultInventoryButton != null) return defaultInventoryButton;
            if (slotUIElements != null && slotUIElements.Count > 0 && slotUIElements[0] != null)
            {
                return slotUIElements[0].gameObject; // Assuming first slot is default
            }
            return null;
        }

        private void RefreshUI()
        {
            if (slotUIElements.Count != _inventorySlots.Count)
            {
                Debug.LogError("Mismatch between inventory data slots and UI slots!");
                InitializeUI(); // Attempt to fix by reinitializing
                // return; // Might be better to return after logging error depending on desired robustness
            }

            for (var i = 0; i < _inventorySlots.Count; i++)
                if (i < slotUIElements.Count) // Safety check
                    slotUIElements[i].UpdateDisplay(_inventorySlots[i]);

            // Optionally, tell input handler to update selection if needed after refresh
            inputHandler?.UpdateSelectionHighlight();
        }

        // --- Item Management Logic ---

        public bool AddItem(InventoryItem itemToAdd, int quantity = 1)
        {
            if (itemToAdd == null || quantity <= 0) return false;

            // 1. Try stacking onto existing slots
            if (itemToAdd.isStackable)
                for (var i = 0; i < _inventorySlots.Count; i++)
                    if (!_inventorySlots[i].IsEmpty() && _inventorySlots[i].item == itemToAdd)
                    {
                        var spaceAvailable = itemToAdd.maxStackSize - _inventorySlots[i].quantity;
                        if (spaceAvailable >= quantity)
                        {
                            _inventorySlots[i].quantity += quantity;
                            RefreshUI(); // Update the specific slot UI potentially
                            return true;
                        }

                        if (spaceAvailable > 0)
                        {
                            _inventorySlots[i].quantity += spaceAvailable;
                            quantity -= spaceAvailable; // Decrease quantity needed
                            // Continue searching for another stack or empty slot
                        }
                    }

            // 2. Try adding to a new empty slot
            for (var i = 0; i < _inventorySlots.Count; i++)
                if (_inventorySlots[i].IsEmpty())
                {
                    var canAdd = itemToAdd.isStackable ? Mathf.Min(quantity, itemToAdd.maxStackSize) : 1;
                    _inventorySlots[i].item = itemToAdd;
                    _inventorySlots[i].quantity = canAdd;
                    quantity -= canAdd;

                    if (quantity <= 0)
                    {
                        RefreshUI(); // Update the specific slot UI potentially
                        return true;
                    }
                    // Continue if more quantity needs placing (e.g., adding 200 potions)
                }

            // 3. Inventory is full or couldn't place remaining quantity
            Debug.LogWarning($"Inventory full or could not add remaining {quantity} of {itemToAdd.itemName}");
            RefreshUI(); // Refresh to show any partial additions
            return false;
        }

        public bool RemoveItem(InventoryItem itemToRemove, int quantity = 1)
        {
            if (itemToRemove == null || quantity <= 0) return false;

            var quantityFound = 0;
            var slotsToRemoveFrom = new List<int>();

            // Find total quantity and locations
            for (var i = 0; i < _inventorySlots.Count; i++)
                if (!_inventorySlots[i].IsEmpty() && _inventorySlots[i].item == itemToRemove)
                {
                    quantityFound += _inventorySlots[i].quantity;
                    slotsToRemoveFrom.Add(i);
                }

            if (quantityFound < quantity)
            {
                Debug.LogWarning(
                    $"Not enough {itemToRemove.itemName} in inventory to remove {quantity}. Found: {quantityFound}");
                return false;
            }

            // Remove quantity starting from the end (avoids issues if stacking changes)
            var quantityToRemove = quantity;
            for (var i = slotsToRemoveFrom.Count - 1; i >= 0; i--)
            {
                var slotIndex = slotsToRemoveFrom[i];
                if (_inventorySlots[slotIndex].quantity >= quantityToRemove)
                {
                    _inventorySlots[slotIndex].quantity -= quantityToRemove;
                    if (_inventorySlots[slotIndex].quantity <= 0) _inventorySlots[slotIndex].Clear();
                    quantityToRemove = 0;
                    break; // Done
                }

                quantityToRemove -= _inventorySlots[slotIndex].quantity;
                _inventorySlots[slotIndex].Clear();
            }

            RefreshUI();
            return true;
        }

        // Example: Consuming an item (called potentially after Use() or OnSubmit)
        public void ConsumeItem(InventorySlot slot)
        {
            if (slot != null && !slot.IsEmpty()) RemoveItem(slot.item);
        }

        // --- Getters for Input Handler ---
        public int GetInventoryColumns()
        {
            // Assumes a GridLayoutGroup is used on the container
            var gridLayout = inventoryGridContainer.GetComponent<GridLayoutGroup>();
            if (gridLayout != null)
            {
                // Calculate based on fixed column count if set, otherwise based on constraints
                if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                    return gridLayout.constraintCount;

                if (gridLayout.constraint == GridLayoutGroup.Constraint.Flexible &&
                    inventoryGridContainer is RectTransform rectTransform)
                    // Estimate based on container width and cell size (less reliable)
                    if (gridLayout.cellSize.x + gridLayout.spacing.x > 0)
                    {
                        var cols = Mathf.FloorToInt(
                            (rectTransform.rect.width - gridLayout.padding.left - gridLayout.padding.right +
                             gridLayout.spacing.x) / (gridLayout.cellSize.x + gridLayout.spacing.x));
                        return Mathf.Max(1, cols); // Ensure at least 1 column
                    }
            }

            // Fallback or default if grid layout info isn't available/reliable
            Debug.LogWarning(
                "Could not reliably determine grid columns. Falling back to default (e.g., 5). Adjust as needed.");
            return 5; // Adjust this fallback based on your typical layout
        }

        public int GetTotalSlots()
        {
            return _inventorySlots.Count;
        }

        public InventorySlotUI GetSlotUI(int index)
        {
            if (index >= 0 && index < slotUIElements.Count) return slotUIElements[index];
            return null;
        }
    }
}