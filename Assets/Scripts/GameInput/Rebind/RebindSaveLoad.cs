using UnityEngine;
using UnityEngine.InputSystem;

namespace GameInput.Rebind
{
    public class RebindSaveLoad : MonoBehaviour
    {
        private const string RebindsKey = "inputRebinds"; // PlayerPrefs key

        [Tooltip("Reference to the InputActionAsset whose bindings will be saved and loaded.")] [SerializeField]
        private InputActionAsset actionAsset;

        private void Awake()
        {
            if (actionAsset == null)
            {
                // Attempt to find it on a PlayerInput component if not assigned
                var playerInput = FindObjectOfType<PlayerInput>();
                if (playerInput != null) actionAsset = playerInput.actions;

                if (actionAsset == null)
                {
                    Debug.LogError(
                        "RebindSaveLoad: InputActionAsset is not assigned and could not be found on a PlayerInput component. Disabling persistence.");
                    enabled = false; // Disable this component if asset is missing
                }
            }
        }

        private void OnEnable()
        {
            if (actionAsset == null) return; // Already handled in Awake, but good practice

            // Load saved bindings when the component is enabled
            var rebindData = PlayerPrefs.GetString(RebindsKey, string.Empty);
            if (!string.IsNullOrEmpty(rebindData))
            {
                actionAsset.LoadBindingOverridesFromJson(rebindData);
                Debug.Log("Input bindings loaded from PlayerPrefs.");
            }
            else
            {
                Debug.Log("No saved input bindings found, using defaults.");
            }
        }

        private void OnDisable()
        {
            if (actionAsset == null) return;

            // Save current bindings when the component is disabled or application quits
            var rebindData = actionAsset.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(RebindsKey, rebindData);
            PlayerPrefs.Save(); // Ensure PlayerPrefs are written to disk
            Debug.Log("Input bindings saved to PlayerPrefs.");
        }

        // Optional: Call this method if you want to reset bindings to default
        public void ResetBindingsToDefault()
        {
            if (actionAsset == null) return;

            actionAsset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(RebindsKey); // Remove the saved overrides
            Debug.Log("Input bindings have been reset to defaults.");
            // You might need to re-apply default bindings or reload the scene/UI
            // For immediate effect without re-loading, re-load from empty string:
            // actionAsset.LoadBindingOverridesFromJson(string.Empty);
        }
    }
}