using System;
using RPG.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GameInput
{
    // Define the possible input focus states for the game
    public enum InputFocusState
    {
        Gameplay, // Player control is active
        UIMode, // General UI navigation is active (menus, inventory, etc.)

        Rebinding // Controls rebinding UI is active
        // Add more states as needed (e.g., Dialogue, Cutscene)
    }

    public class ActionMapManager : MonoBehaviour
    {
        [Header("Action Asset")] [SerializeField]
        private InputActionAsset actionAsset;

        [SerializeField] private PlayerInput playerInput; // Assign this in Inspector

        [Header("UI Canvases & Defaults")] [SerializeField]
        private GameObject gameUICanvas; // General game UI (if any, distinct from inventory)

        [SerializeField] private GameObject rebindControlsCanvas;
        [SerializeField] private GameObject gameUIDefaultButton; // Default for general game UI

        // Current State
        private InputFocusState currentFocusState;
        private bool isGeneralUICanvasActive; // If you have a separate general UI

        // Action Maps - Cached in Awake
        private InputActionMap playerMap;

        // Actions - Cached in Awake
        private InputAction toggleGeneralUIAction; // Renamed for clarity if it's for a general UI
        private InputAction toggleRebindControlsAction;
        private InputActionMap uiMap;
        private InputActionMap universalMap;

        // Singleton for easy access (optional, but can be convenient)
        public static ActionMapManager Instance { get; private set; }


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;


            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>(); // Try to get it if not assigned
                if (playerInput == null)
                {
                    Debug.LogError("PlayerInput component not assigned or found on ActionMapManager's GameObject!");
                    enabled = false; // Disable script if critical component is missing
                    return;
                }
            }

            if (actionAsset == null)
            {
                actionAsset = playerInput.actions; // Get it from PlayerInput if not directly assigned
                if (actionAsset == null)
                {
                    Debug.LogError("ActionAsset not assigned and could not be retrieved from PlayerInput!");
                    enabled = false;
                    return;
                }
            }

            // Cache action maps (assumes Player, UI, Universal order in StarterAssets)
            playerMap = actionAsset.FindActionMap("Player", true);
            uiMap = actionAsset.FindActionMap("UI", true);
            universalMap = actionAsset.FindActionMap("Universal", true);

            if (playerMap == null || uiMap == null || universalMap == null)
            {
                Debug.LogError(
                    "One or more critical action maps (Player, UI, Universal) not found in the ActionAsset!");
                enabled = false;
                return;
            }

            // Cache actions from Universal Map
            // Ensure these action names exist in your "Universal" Action Map
            toggleGeneralUIAction =
                universalMap.FindAction("ToggleGeneralUI", true); // Example: if you have a general UI toggle
            toggleRebindControlsAction = universalMap.FindAction("ToggleRebindControls", true);

            // Initial state: Gameplay
            // Ensure Universal map is enabled from the start.
            universalMap.Enable();
            RequestInputFocus(InputFocusState.Gameplay); // Start in Gameplay mode
        }

        private void OnEnable()
        {
            // Subscribe to actions
            if (toggleGeneralUIAction != null) toggleGeneralUIAction.performed += HandleToggleGeneralUIPerformed;
            if (toggleRebindControlsAction != null)
                toggleRebindControlsAction.performed += HandleToggleRebindControlsPerformed;
        }

        private void OnDisable()
        {
            // Unsubscribe to prevent errors and memory leaks
            if (toggleGeneralUIAction != null) toggleGeneralUIAction.performed -= HandleToggleGeneralUIPerformed;
            if (toggleRebindControlsAction != null)
                toggleRebindControlsAction.performed -= HandleToggleRebindControlsPerformed;
        }

        // At the top of ActionMapManager.cs
        public event Action<InputAction.CallbackContext> OnToggleUI;

        /// <summary>
        ///     Central method to request a change in input focus.
        ///     All systems should call this to change input modes.
        /// </summary>
        /// <param name="requestedState">The desired input focus state.</param>
        /// <param name="uiDefaultSelection">Optional: The UI element to select when entering a UI state.</param>
        public void RequestInputFocus(InputFocusState requestedState, GameObject uiDefaultSelection = null)
        {
            if (playerInput == null)
            {
                Debug.LogError("PlayerInput is null. Cannot change input focus.");
                return;
            }

            currentFocusState = requestedState;
            LogActionMapStates("Before Change");

            // Disable all primary maps first, then enable the correct one
            playerMap.Disable();
            uiMap.Disable();
            // Note: universalMap remains enabled

            // Disable PlayerInput component temporarily if needed for rebinding
            // This is a more direct way to stop player character input during full-screen rebinding.
            playerInput.enabled = requestedState != InputFocusState.Rebinding;


            switch (requestedState)
            {
                case InputFocusState.Gameplay:
                    playerMap.Enable();
                    playerInput.SwitchCurrentActionMap("Player");
                    // Ensure no UI element is selected when returning to gameplay
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                    Debug.Log("Switched to Gameplay focus.");
                    break;

                case InputFocusState.UIMode:
                    uiMap.Enable();
                    playerInput.SwitchCurrentActionMap("UI");
                    if (uiDefaultSelection != null && EventSystem.current != null)
                        EventSystem.current.SetSelectedGameObject(uiDefaultSelection);
                    else if (gameUIDefaultButton != null && EventSystem.current != null) // Fallback to general default
                        EventSystem.current.SetSelectedGameObject(gameUIDefaultButton);
                    Debug.Log("Switched to UI Mode focus.");
                    break;

                case InputFocusState.Rebinding:
                    // Rebinding might use the UI map or its own map.
                    // For this example, we assume it might use the UI map, or input is handled by Unity UI directly.
                    // The key is that playerInput component is disabled above for this state.
                    // If rebinding UI uses InputSystem actions, ensure its map is enabled here.
                    // For simplicity, if rebinding canvas handles its own input (e.g. standard Unity UI buttons),
                    // disabling playerInput and enabling UI map for potential navigation is okay.
                    uiMap.Enable(); // Or a specific "Rebinding" map if you have one.
                    playerInput.SwitchCurrentActionMap("UI"); // Or "Rebinding"
                    // Set default for rebinding canvas if applicable
                    // if (rebindControlsCanvasDefaultButton != null && EventSystem.current != null)
                    // {
                    //    EventSystem.current.SetSelectedGameObject(rebindControlsCanvasDefaultButton);
                    // }
                    Debug.Log("Switched to Rebinding focus.");
                    break;
            }

            LogActionMapStates("After Change");
        }


        private void HandleToggleGeneralUIPerformed(InputAction.CallbackContext context)
        {

            // This is for a general UI, NOT the inventory specifically
            isGeneralUICanvasActive = !isGeneralUICanvasActive;
            if (gameUICanvas != null) gameUICanvas.SetActive(isGeneralUICanvasActive);

            if (isGeneralUICanvasActive)
            {
                RequestInputFocus(InputFocusState.UIMode, gameUIDefaultButton);
            }
            else
            {
                // If no other UI is requesting focus, return to gameplay
                // This needs to be smarter if multiple UIs can overlap or request focus
                RequestInputFocus(InputFocusState.Gameplay);
            }

            LogActionMapStates("Toggling General UI");
            OnToggleUI?.Invoke(context);
        }

        private void HandleToggleRebindControlsPerformed(InputAction.CallbackContext context)
        {
            if (rebindControlsCanvas != null)
            {
                bool isActive = !rebindControlsCanvas.activeSelf;
                rebindControlsCanvas.SetActive(isActive);
                if (isActive)
                {
                    RequestInputFocus(InputFocusState.Rebinding /*, optional default button for rebind canvas */);
                }
                else
                {
                    // When closing rebind, decide what state to return to.
                    // If a general UI was open before, return to that. Otherwise, gameplay.
                    // This requires more sophisticated state tracking if UIs can stack.
                    // For now, assume returning to gameplay or the general UI if it was active.
                    if (InventoryManager.Instance != null && InventoryManager.Instance.IsInventoryOpen())
                    {
                        RequestInputFocus(InputFocusState.UIMode,
                            InventoryManager.Instance.GetDefaultSelectedInventoryButton());
                    }
                    else if (isGeneralUICanvasActive && gameUICanvas != null && gameUICanvas.activeSelf)
                    {
                        RequestInputFocus(InputFocusState.UIMode, gameUIDefaultButton);
                    }
                    else
                    {
                        RequestInputFocus(InputFocusState.Gameplay);
                    }
                }
            }
        }


        // Public method to be called by a UI button on the rebind canvas
        public void CloseRebindControlsAndRestoreFocus()
        {
            if (rebindControlsCanvas != null) rebindControlsCanvas.SetActive(false);
            // Logic to restore previous focus (simplified here)
            // A more robust system might use a stack of previous states.
            if (InventoryManager.Instance != null && InventoryManager.Instance.IsInventoryOpen())
            {
                RequestInputFocus(InputFocusState.UIMode,
                    InventoryManager.Instance.GetDefaultSelectedInventoryButton());
            }
            else if (isGeneralUICanvasActive && gameUICanvas != null && gameUICanvas.activeSelf)
            {
                RequestInputFocus(InputFocusState.UIMode, gameUIDefaultButton);
            }
            else
            {
                RequestInputFocus(InputFocusState.Gameplay);
            }
        }


        private void LogActionMapStates(string contextMessage)
        {
            if (playerInput == null) return;
            Debug.Log($"--- {contextMessage} --- \n" +
                      $"PlayerMap Active: {playerMap.enabled}, UIMap Active: {uiMap.enabled}, " +
                      $"UniversalMap Active: {universalMap.enabled}\n" +
                      $"PlayerInput Component Enabled: {playerInput.enabled}\n" +
                      $"PlayerInput Current Action Map: {playerInput.currentActionMap?.name}\n" +
                      $"Current Focus State: {currentFocusState}");
        }
    }
}

