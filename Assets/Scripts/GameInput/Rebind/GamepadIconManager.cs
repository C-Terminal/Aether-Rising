// GamepadIconManager.cs
// Purpose: Manages dynamic gamepad icon updates for UI elements (RebindActionUI).
// Usage: Attach to a UI Canvas or a manager GameObject. Assign icon sets and RebindActionUI elements.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
// For Image component

// For List if dynamically finding RebindActionUI components
// using TMPro; // If RebindActionUI uses TextMeshPro for its text

namespace GameInput.Rebind
{
    public class GamepadIconManager : MonoBehaviour
    {
        [System.Serializable]
        public struct GamepadIconSet
        {
            public string deviceLayoutNameContains; // e.g., "Xbox", "DualShock", "Switch Pro"
            public Sprite buttonSouth;
            public Sprite buttonNorth;
            public Sprite buttonEast;
            public Sprite buttonWest;
            public Sprite leftStick;
            public Sprite rightStick;
            public Sprite dpad;
            public Sprite leftShoulder;
            public Sprite rightShoulder;
            public Sprite leftTrigger;
            public Sprite rightTrigger;
            public Sprite startButton; // Or "Options", "Menu"
            public Sprite selectButton; // Or "View", "Share"
            // Add more mappings as needed (e.g., stick presses, specific vendor buttons)
        }

        [Tooltip("Define icon sets for different gamepad types.")]
        [SerializeField] private List<GamepadIconSet> iconSets = new List<GamepadIconSet>();

        [Tooltip("Assign all RebindActionUI components that this manager should control. Can also be found dynamically.")]
        [SerializeField] private RebindActionUI[] rebindActionUIs;

        // Fallback icons if a specific device or button is not mapped
        [Header("Fallback Settings")]
        [SerializeField] private Sprite defaultFallbackIcon; // A generic button icon
        [SerializeField] private bool showTextIfNoIcon = true;


        void Awake()
        {
            // If rebindActionUIs are not assigned in inspector, try to find them.
            // This is useful if they are part of a dynamically generated list.
            if (rebindActionUIs == null || rebindActionUIs.Length == 0)
            {
                rebindActionUIs = FindObjectsOfType<RebindActionUI>(true); // true to include inactive
                Debug.LogWarning("GamepadIconManager: RebindActionUI components were not assigned in inspector. Found " + rebindActionUIs.Length + " dynamically. This might impact performance if done frequently.");
            }
        }
        private void OnEnable()
        {
            if (rebindActionUIs == null) return;

            // Subscribe to UI update events for all rebind actions
            foreach (var rebindAction in rebindActionUIs)
            {
                if (rebindAction != null)
                {
                    rebindAction.updateBindingUIEvent.AddListener(OnUpdateBindingDisplay);
                    // Initial update in case the UI is already active
                    rebindAction.UpdateDisplay();
                }
            }
        }

        private void OnDisable()
        {
            if (rebindActionUIs == null) return;

            // Unsubscribe to prevent memory leaks
            foreach (var rebindAction in rebindActionUIs)
            {
                if (rebindAction != null)
                {
                    rebindAction.updateBindingUIEvent.RemoveListener(OnUpdateBindingDisplay);
                }
            }
        }

        /// <summary>
        /// Called when a RebindActionUI's binding display needs to be updated.
        /// </summary>
        /// <param name="rebindUI">The RebindActionUI component that triggered the event.</param>
        /// <param name="bindingEffectivePath">The full effective path of the binding (e.g., "<Gamepad>/buttonSouth").</param>
        /// <param name="deviceLayoutName">The layout name of the device for the current binding (e.g., "XboxOneGamepadWindows").</param>
        private void OnUpdateBindingDisplay(RebindActionUI rebindUI, string bindingEffectivePath, string deviceLayoutName)
        {
            if (rebindUI == null || string.IsNullOrEmpty(bindingEffectivePath) || string.IsNullOrEmpty(deviceLayoutName))
            {
                // If critical info is missing, try to show text and hide icon
                if (rebindUI != null) SetTextActive(rebindUI, true);
                return;
            }

            // We only care about gamepad devices for icons
            if (!deviceLayoutName.ToLower().Contains("gamepad") && !deviceLayoutName.ToLower().Contains("joystick") &&
                !deviceLayoutName.ToLower().Contains("dualshock") && !deviceLayoutName.ToLower().Contains("xbox") &&
                !deviceLayoutName.ToLower().Contains("switch")) // Add other gamepad identifiers
            {
                SetTextActive(rebindUI, true); // Show text for non-gamepad bindings
                return;
            }

            // Extract the control path part (e.g., "buttonSouth" from "<Gamepad>/buttonSouth")
            string controlPath = GetControlPathName(bindingEffectivePath);
            Sprite icon = GetSpriteForDevice(controlPath, deviceLayoutName);

            Image bindingIconImage = rebindUI.BindingIconImage; // Use the public property

            if (icon != null && bindingIconImage != null)
            {
                bindingIconImage.sprite = icon;
                bindingIconImage.gameObject.SetActive(true);
                SetTextActive(rebindUI, false); // Hide text if icon is shown
            }
            else
            {
                // Fallback: Show text if no icon is found or if icon image is missing
                SetTextActive(rebindUI, true);
                if (bindingIconImage != null)
                {
                    bindingIconImage.gameObject.SetActive(false); // Hide icon image
                }
            }
        }

        private void SetTextActive(RebindActionUI rebindUI, bool isActive)
        {
            if (rebindUI.BindingDisplayNameText != null)
            {
                // Only activate text if the fallback option is enabled or if we are activating it
                rebindUI.BindingDisplayNameText.gameObject.SetActive(isActive && showTextIfNoIcon || isActive);
            }
            // If we are activating text, ensure icon is off (unless icon is specifically set later)
            if (isActive && rebindUI.BindingIconImage != null) {
                rebindUI.BindingIconImage.gameObject.SetActive(false);
            }
        }


        /// <summary>
        /// Extracts the control name (e.g., "buttonSouth") from a full binding path.
        /// </summary>
        private string GetControlPathName(string effectivePath)
        {
            if (string.IsNullOrEmpty(effectivePath)) return string.Empty;
            // Example: <Gamepad>/buttonSouth -> buttonSouth
            // Example: <Keyboard>/a -> a
            int lastSlash = effectivePath.LastIndexOf('/');
            if (lastSlash != -1 && lastSlash < effectivePath.Length - 1)
            {
                return effectivePath.Substring(lastSlash + 1);
            }
            return effectivePath; // Fallback if path format is unexpected
        }

        /// <summary>
        /// Gets the appropriate sprite for a given control path and device layout.
        /// </summary>
        private Sprite GetSpriteForDevice(string controlPath, string deviceLayoutName)
        {
            if (string.IsNullOrEmpty(controlPath) || string.IsNullOrEmpty(deviceLayoutName)) return defaultFallbackIcon;

            foreach (var iconSet in iconSets)
            {
                if (deviceLayoutName.ToLower().Contains(iconSet.deviceLayoutNameContains.ToLower()))
                {
                    // Map control paths to sprites within the matched set
                    switch (controlPath.ToLower())
                    {
                        case "buttonsouth": return iconSet.buttonSouth;
                        case "buttonnorth": return iconSet.buttonNorth;
                        case "buttoneast": return iconSet.buttonEast;
                        case "buttonwest": return iconSet.buttonWest;
                        case "leftstick": return iconSet.leftStick;
                        case "rightstick": return iconSet.rightStick;
                        case "dpad": return iconSet.dpad; // This might need more specific up/down/left/right icons
                        case "leftshoulder": case "lb": return iconSet.leftShoulder; // Common alternative names
                        case "rightshoulder": case "rb": return iconSet.rightShoulder;
                        case "lefttrigger": case "lt": return iconSet.leftTrigger;
                        case "righttrigger": case "rt": return iconSet.rightTrigger;
                        case "start": case "options": case "menu": return iconSet.startButton;
                        case "select": case "view": case "back": case "share": return iconSet.selectButton; // Common alternative names
                        // Add more specific cases here
                        default: break; // Continue to next icon set or return default
                    }
                }
            }
            return defaultFallbackIcon; // Fallback if no specific icon is found
        }
    }
}