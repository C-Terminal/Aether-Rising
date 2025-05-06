using System;
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
        private bool isGameUICanvasActive = true;
        [SerializeField] PlayerInput playerInput;
        private InputActionMap playerMap;
        private InputAction toggleRebindControlsAction;
        private InputAction toggleUIAction;
        private InputActionMap uiMap;
        private InputActionMap universalMap;


        private void Awake()
        {
            if (playerInput == null)
                playerInput = GetComponent<PlayerInput>();

            Debug.Assert(gameUICanvas != null, "gameUICanvas is not assigned!");
            Debug.Assert(rebindControlsCanvas != null, "rebindControlsCanvas is not assigned!");
            Debug.Assert(gameUIDefaultButton != null, "gameUIDefaultButton is not assigned!");
            Debug.Assert(actionAsset != null, "InputActionAsset is not assigned!");
            

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

        // At the top of ActionMapManager.cs
        public event Action<InputAction.CallbackContext> OnToggleUI;

    // In the ToggleUIPerformed method:


        private void ToggleUIPerformed(InputAction.CallbackContext context)
        {
            
            Debug.Log($"gameUICanvas is {(gameUICanvas == null ? "NULL" : "SET")}");
            Debug.Log($"gameUIDefaultButton is {(gameUIDefaultButton == null ? "NULL" : "SET")}");
            Debug.Log($"EventSystem.current is {(EventSystem.current == null ? "NULL" : "SET")}");
            
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
            OnToggleUI?.Invoke(context);
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