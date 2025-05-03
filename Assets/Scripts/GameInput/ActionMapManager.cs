using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GameInput
{
    public class ActionMapManager : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actionAsset; // Assign StarterAssets in Inspector
        [SerializeField] private GameObject gameUICanvas; // Game UI canvas
        [SerializeField] private GameObject rebindControlsCanvas; // Rebinding UI canvas
        [SerializeField] private GameObject gameUIDefaultButton; // Default UI button for focus
        private PlayerInput playerInput;
        private InputActionMap playerMap;
        private InputActionMap uiMap;
        private InputActionMap universalMap;
        private InputAction toggleUIAction;
        private InputAction toggleRebindControlsAction;
        private bool isGameUICanvasActive = true;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();

            // Cache action maps (assumes Player, UI, Universal order in StarterAssets)
            playerMap = actionAsset.FindActionMap("Player", true);
            uiMap = actionAsset.FindActionMap("UI", true);
            universalMap = actionAsset.FindActionMap("Universal", true);
            // Cache actions
            toggleUIAction = universalMap.FindAction("ToggleUI", true);
            toggleRebindControlsAction = universalMap.FindAction("ToggleRebindControls", true);
        }

        private void OnEnable()
        {
            toggleUIAction.performed += ToggleUIPerformed;
            toggleRebindControlsAction.performed += ToggleRebindControlsPerformed;
        }

        private void OnDisable()
        {
            toggleUIAction.performed -= ToggleUIPerformed;
            toggleRebindControlsAction.performed -= ToggleRebindControlsPerformed;
        }

        private void ToggleUIPerformed(InputAction.CallbackContext context)
        {
            isGameUICanvasActive = !isGameUICanvasActive;
            gameUICanvas.SetActive(isGameUICanvasActive);
            if (isGameUICanvasActive)
            {
                playerMap.Disable();
                uiMap.Enable();
                playerInput.SwitchCurrentActionMap("UI");
                EventSystem.current.SetSelectedGameObject(gameUIDefaultButton);
            }
            else
            {
                playerMap.Enable();
                uiMap.Disable();
                playerInput.SwitchCurrentActionMap("Player");
            }

            universalMap.Enable(); // Always keep Universal map active
            LogActionMapStates();
        }

        private void ToggleRebindControlsPerformed(InputAction.CallbackContext context)
        {
            rebindControlsCanvas.SetActive(true);
            playerInput.enabled = false; // Disable gameplay input during rebinding
            universalMap.Enable();
        }

        public void CloseRebindControlsCanvas()
        {
            rebindControlsCanvas.SetActive(false);
            playerInput.enabled = true; // Re-enable gameplay input
            if (isGameUICanvasActive)
            {
                uiMap.Enable();
                playerMap.Disable();
                EventSystem.current.SetSelectedGameObject(gameUIDefaultButton);
            }
            else
            {
                playerMap.Enable();
                uiMap.Disable();
            }

            universalMap.Enable();
            LogActionMapStates();
        }

        private void LogActionMapStates()
        {
            Debug.Log($"PlayerMap: {playerMap.enabled}, UIMap: {uiMap.enabled}, " +
                      $"UniversalMap: {universalMap.enabled}, CurrentMap: {playerInput.currentActionMap?.name}");
        }
    }
}