// RebindActionUI.cs
// Purpose: Represents a UI element for a single rebindable action.
//          This is a simplified version. Unity's Input System samples provide a more complete one.
// Usage: Attach to each UI row/element that allows rebinding for a specific action.

using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameInput.Rebind
{
    // Required if you use TextMeshPro for text elements

// Event to broadcast when the binding display needs an update
// Parameters: This RebindActionUI component, the new binding display string, the device layout name
    [System.Serializable]
    public class UpdateBindingUIEvent : UnityEvent<RebindActionUI, string, string> { }

    public class RebindActionUI : MonoBehaviour
    {
        [Tooltip("Reference to the action that will be rebound. Assign this in the Inspector.")]
        [SerializeField] private InputActionReference actionReference;

        [Tooltip("Text component to display the binding path or action name.")]
        [SerializeField] private TextMeshProUGUI bindingDisplayNameText; // Use Text if not using TextMeshPro

        [Tooltip("Image component to display a gamepad button icon.")]
        [SerializeField] private Image bindingIconImage;

        [Tooltip("Text component to display the action's friendly name.")]
        [SerializeField] private TextMeshProUGUI actionNameText; // Use Text if not using TextMeshPro

        [Tooltip("Button used to start the rebinding process for this action.")]
        [SerializeField] private Button rebindButton;

        [Tooltip("Text component on the rebind button (e.g., to show 'Press any key').")]
        [SerializeField] private TextMeshProUGUI rebindButtonText; // Use Text if not using TextMeshPro

        [Tooltip("GameObject to show while waiting for input during rebinding.")]
        [SerializeField] private GameObject waitingForInputOverlay;

        public UpdateBindingUIEvent updateBindingUIEvent = new UpdateBindingUIEvent();

        private InputActionRebindingExtensions.RebindingOperation rebindingOperation;

        // Public property to access the action reference
        public InputActionReference ActionReference => actionReference;
        public TextMeshProUGUI BindingDisplayNameText => bindingDisplayNameText;
        public Image BindingIconImage => bindingIconImage;


        private void Awake()
        {
            if (actionReference == null || actionReference.action == null)
            {
                Debug.LogError($"RebindActionUI on {gameObject.name} has no InputActionReference assigned or the action is null.", this);
                if(rebindButton != null) rebindButton.interactable = false;
                return;
            }

            if (actionNameText != null)
            {
                actionNameText.text = actionReference.action.name; // Display the action's name
            }

            if (waitingForInputOverlay != null)
            {
                waitingForInputOverlay.SetActive(false);
            }

            if (rebindButton != null)
            {
                rebindButton.onClick.AddListener(StartRebinding);
            }
        }

        private void OnEnable()
        {
            UpdateDisplay(); // Update display when UI element is enabled
        }

        private void OnDisable()
        {
            // Clean up the rebinding operation if it's in progress
            rebindingOperation?.Dispose();
            rebindingOperation = null;
        }

        public void UpdateDisplay()
        {
            if (actionReference == null || actionReference.action == null) return;

            int bindingIndex = actionReference.action.GetBindingIndexForControl(actionReference.action.controls[0]);
            string currentBindingDisplayString = string.Empty;
            string deviceLayoutName = string.Empty;

            if (bindingIndex >= 0)
            {
                currentBindingDisplayString = InputControlPath.ToHumanReadableString(
                    actionReference.action.bindings[bindingIndex].effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);

                // Get device layout name for icon lookup
                var control = actionReference.action.controls.Count > 0 ? actionReference.action.controls[0] : null;
                if (control != null)
                {
                    deviceLayoutName = control.device.layout;
                }
            }

            if (bindingDisplayNameText != null)
            {
                bindingDisplayNameText.text = currentBindingDisplayString;
            }

            // Invoke the event to allow GamepadIconManager to update icons
            updateBindingUIEvent.Invoke(this, actionReference.action.bindings[bindingIndex].effectivePath, deviceLayoutName);
        }


        public void StartRebinding()
        {
            if (actionReference == null || actionReference.action == null) return;

            if (rebindButtonText != null) rebindButtonText.text = "Listening..."; // Or use waitingForInputOverlay
            if (waitingForInputOverlay != null) waitingForInputOverlay.SetActive(true);
            if (rebindButton != null) rebindButton.interactable = false;


            actionReference.action.Disable(); // Disable action during rebind

            rebindingOperation = actionReference.action.PerformInteractiveRebinding()
                // Optional: Filter by specific device types or control paths
                // .WithControlsExcluding("Mouse") // Example: Exclude mouse
                .OnMatchWaitForAnother(0.1f) // Wait a short time for composite bindings
                .OnComplete(operation =>
                {
                    actionReference.action.Enable(); // Re-enable action
                    operation.Dispose();
                    if (rebindButton != null) rebindButton.interactable = true;
                    if (waitingForInputOverlay != null) waitingForInputOverlay.SetActive(false);
                    // Restore original button text if needed
                    // if (rebindButtonText != null) rebindButtonText.text = "Rebind";

                    UpdateDisplay(); // Update our own display

                    // Optional: Save bindings immediately after a successful rebind
                    RebindSaveLoad saveLoadComponent = FindObjectOfType<RebindSaveLoad>();
                    if (saveLoadComponent != null)
                    {
                        // This will trigger OnDisable/OnEnable if you save by disabling/enabling the component
                        // Or, add a public SaveBindingsNow() method to RebindSaveLoad
                        // For simplicity, assume OnDisable of RebindSaveLoad will handle saving eventually.
                    }

                    Debug.Log($"Rebound '{actionReference.action.name}' to '{operation.selectedControl.path}'");
                })
                .OnCancel(operation =>
                {
                    actionReference.action.Enable();
                    operation.Dispose();
                    if (rebindButton != null) rebindButton.interactable = true;
                    if (waitingForInputOverlay != null) waitingForInputOverlay.SetActive(false);
                    // Restore original button text
                    // if (rebindButtonText != null) rebindButtonText.text = "Rebind";
                    UpdateDisplay(); // Restore display to previous binding
                    Debug.Log($"Rebinding cancelled for '{actionReference.action.name}'");
                })
                .Start(); // Start the rebinding process
        }
    }
}