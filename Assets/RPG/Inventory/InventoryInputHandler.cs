using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPG.Inventory
{
    public class InventoryInputHandler : MonoBehaviour
    {
        [Header("Input Action References")] [SerializeField]
        private PlayerInput playerInput; // Assign the PlayerInput component

        [SerializeField] private InputActionReference navigateActionRef;
        [SerializeField] private InputActionReference submitActionRef;
        [SerializeField] private InputActionReference cancelActionRef;

        [Header("Navigation Settings")] [SerializeField]
        private float navigationDelay = 0.2f; // Prevent selecting multiple slots per single press

        private InputAction navigateAction;
        private InputAction submitAction;
        private InputAction cancelAction;

        private List<InventorySlotUI> slotUIElements; // Received from InventoryManager
        private int currentSelectionIndex = 0;
        private int numColumns = 5; // Default, will be updated
        private int totalSlots = 0; // Default, will be updated
        private bool canNavigate = true; // Cooldown flag
        private Coroutine navigationCooldownCoroutine;

        void Awake()
        {
            
            playerInput = GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                playerInput = FindObjectOfType<PlayerInput>(); // Try to find it globally
                if (playerInput == null) Debug.LogError("PlayerInput component not found!");
            }

            // It's generally safer to find actions via the PlayerInput component
            // using the references, especially if the Action Map might change.
            if (navigateActionRef != null) navigateAction = playerInput.actions[navigateActionRef.action.name];
            Debug.Log($"Navigate Action is null: {navigateAction == null}"); // ADD THIS

            if (submitActionRef != null) submitAction = playerInput.actions[submitActionRef.action.name];
            Debug.Log($"Submit Action is null: {submitAction == null}"); // ADD THIS

            if (cancelActionRef != null) cancelAction = playerInput.actions[cancelActionRef.action.name];
            Debug.Log($"Cancel Action is null: {cancelAction == null}"); // ADD THIS
            //...

            if (navigateAction == null) Debug.LogError("Navigate Action not found!");
            if (submitAction == null) Debug.LogError("Submit Action not found!");
            if (cancelAction == null) Debug.LogError("Cancel Action not found!");

        }

        // Called by InventoryManager after UI slots are created/updated
        public void SetSlotUIElements(List<InventorySlotUI> uiSlots)
        {
            slotUIElements = uiSlots; 
            totalSlots = slotUIElements?.Count ?? 0;
            // Get grid layout info from manager (or could be passed directly)
            InventoryManager manager = GetComponent<InventoryManager>(); // Assuming they are on the same object
            if (manager != null)
            {
                numColumns = manager.GetInventoryColumns();
            }
            else
            {
                Debug.LogError("InventoryInputHandler cannot find InventoryManager to get column count!");
                numColumns = 5; // Fallback
            }

            // Reset selection when slots change (e.g., inventory opened)
            currentSelectionIndex = 0;
            UpdateSelectionHighlight();
        }

        public void EnableNavigation()
        {
            if (navigateAction == null || submitAction == null || cancelAction == null) return;

            navigateAction.performed += OnNavigatePerformed;
            submitAction.performed += OnSubmitPerformed;
            cancelAction.performed += OnCancelPerformed;

            // Reset selection to the first slot when enabling
            currentSelectionIndex = 0;
            canNavigate = true; // Ensure navigation is possible immediately
            if (navigationCooldownCoroutine != null) StopCoroutine(navigationCooldownCoroutine);
            UpdateSelectionHighlight();
            Debug.Log("Inventory Navigation Enabled.");
        }

        public void DisableNavigation()
        {
            if (navigateAction == null || submitAction == null || cancelAction == null) return;

            navigateAction.performed -= OnNavigatePerformed;
            submitAction.performed -= OnSubmitPerformed;
            cancelAction.performed -= OnCancelPerformed;

            // Clear highlight when disabling
            if (slotUIElements != null && currentSelectionIndex >= 0 && currentSelectionIndex < totalSlots)
            {
                slotUIElements[currentSelectionIndex]?.SetHighlight(false);
            }

            Debug.Log("Inventory Navigation Disabled.");
        }

        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            if (!canNavigate || slotUIElements == null || totalSlots == 0 || numColumns <= 0) return;

            Vector2 input = context.ReadValue<Vector2>();
            int previousSelectionIndex = currentSelectionIndex;

            int currentRow = currentSelectionIndex / numColumns;
            int currentCol = currentSelectionIndex % numColumns;

            // Determine major direction (prioritize vertical or horizontal based on magnitude)
            if (Mathf.Abs(input.y) > Mathf.Abs(input.x))
            {
                // Vertical Movement
                if (input.y > 0.5f) // Up
                {
                    if (currentRow > 0)
                        currentSelectionIndex -= numColumns;
                    // Optional: Wrap around top to bottom
                    // else currentSelectionIndex = ((totalSlots - 1) / numColumns) * numColumns + currentCol;
                    // Ensure wrapped index is not out of bounds if last row isn't full
                    // currentSelectionIndex = Mathf.Min(currentSelectionIndex, totalSlots - 1);
                }
                else if (input.y < -0.5f) // Down
                {
                    if (currentRow < (totalSlots - 1) / numColumns)
                        currentSelectionIndex += numColumns;
                    // Optional: Wrap around bottom to top
                    // else currentSelectionIndex = currentCol;

                    // Ensure index doesn't go beyond the actual number of slots
                    currentSelectionIndex = Mathf.Min(currentSelectionIndex, totalSlots - 1);
                }
            }
            else if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                // Horizontal Movement
                if (input.x > 0.5f) // Right
                {
                    if (currentCol < numColumns - 1 &&
                        currentSelectionIndex + 1 < totalSlots) // Ensure not exceeding total slots
                        currentSelectionIndex++;
                    // Optional: Wrap around right to left
                    // else currentSelectionIndex = currentRow * numColumns;
                }
                else if (input.x < -0.5f) // Left
                {
                    if (currentCol > 0)
                        currentSelectionIndex--;
                    // Optional: Wrap around left to right
                    // else currentSelectionIndex = Mathf.Min(currentRow * numColumns + numColumns - 1, totalSlots - 1);
                }
            }


            // Clamp index just in case calculations go wrong
            currentSelectionIndex = Mathf.Clamp(currentSelectionIndex, 0, totalSlots - 1);

            if (currentSelectionIndex != previousSelectionIndex)
            {
                UpdateSelectionHighlight(previousSelectionIndex);
                StartNavigationCooldown();
            }
        }

        private void OnSubmitPerformed(InputAction.CallbackContext context)
        {
            if (slotUIElements != null && currentSelectionIndex >= 0 && currentSelectionIndex < totalSlots)
            {
                slotUIElements[currentSelectionIndex]?.OnSubmit();
            }
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            // Tell the InventoryManager (or a UIManager) to close the inventory
            InventoryManager manager = GetComponent<InventoryManager>(); // Assuming same object
            if (manager != null)
            {
                manager.ToggleInventory(); // Toggle will handle closing and map switching via Debug logs for now
            }
            else
            {
                Debug.LogWarning("Cancel pressed, but no InventoryManager found to close inventory.");
            }
        }

        public void UpdateSelectionHighlight(int previousIndex = -1)
        {
            if (slotUIElements == null) return;

            // Deselect previous (if valid and different from current)
            if (previousIndex >= 0 && previousIndex < totalSlots && previousIndex != currentSelectionIndex)
            {
                slotUIElements[previousIndex]?.SetHighlight(false);
            }
            else if (previousIndex == -1) // If no previous index provided, deselect all others
            {
                for (int i = 0; i < totalSlots; ++i)
                {
                    if (i != currentSelectionIndex) slotUIElements[i]?.SetHighlight(false);
                }
            }


            // Select current (if valid)
            if (currentSelectionIndex >= 0 && currentSelectionIndex < totalSlots)
            {
                slotUIElements[currentSelectionIndex]?.SetHighlight(true);
            }
        }


        private void StartNavigationCooldown()
        {
            canNavigate = false;
            if (navigationCooldownCoroutine != null) StopCoroutine(navigationCooldownCoroutine);
            navigationCooldownCoroutine = StartCoroutine(NavigationCooldownRoutine());
        }

        private System.Collections.IEnumerator NavigationCooldownRoutine()
        {
            yield return new WaitForSecondsRealtime(navigationDelay); // Use Realtime if inventory pauses game time
            canNavigate = true;
        }

        void OnDisable()
        {
            // Ensure actions are unsubscribed when the component is disabled or destroyed
            DisableNavigation();
            if (navigationCooldownCoroutine != null) StopCoroutine(navigationCooldownCoroutine);
        }
    }
}
