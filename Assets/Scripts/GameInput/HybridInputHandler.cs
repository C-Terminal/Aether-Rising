using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameInput
{
    public class HybridInputHandler : MonoBehaviour
    {

        private PlayerInput _playerInput;

        private InputAction _attackAction;
        private InputAction _moveAction;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            if (_playerInput == null)
            {
                throw new InvalidOperationException("PlayerInput not found on the same game object");
            }

            // Find the actions in the PlayerInput asset
            _attackAction = _playerInput.actions["Attack"];
            _moveAction = _playerInput.actions["Move"];
        }

        private void OnEnable()
        {
            // Subscribe to the 'Attack' action's events directly
            if (_attackAction != null)
            {
                _attackAction.started += OnAttackStarted; // When the input starts (e.g., button press)
                _attackAction.canceled += OnAttackCanceled; // When the input is released or interrupted
            }
            // Note: 'Move' inputs might be handled by 'Send Messages' (e.g., OnMove method)
            // or 'Invoke Unity Events' configured on the PlayerInput component itself.
        }

        private void OnDisable()
        {
            // IMPORTANT: Unsubscribe to prevent memory leaks and errors
            if (_attackAction != null)
            {
                _attackAction.started -= OnAttackStarted;
                _attackAction.canceled -= OnAttackCanceled;
            }
        }

        // Handler for the 'Attack' action starting
        private void OnAttackStarted(InputAction.CallbackContext context)
        {
            Debug.Log("Attack Started!");
            // Trigger attack logic, animations, etc.
        }

        // Handler for the 'Attack' action ending
        private void OnAttackCanceled(InputAction.CallbackContext context)
        {
            Debug.Log("Attack Canceled/Released!");
            // Potentially stop charging, reset state, etc.
        }

        // Example method potentially called by PlayerInput's "Send Messages" behavior for the 'Move' action
        // Ensure the PlayerInput component's Behavior is set to "Send Messages" and it targets this GameObject.
        private void OnMove(InputValue value)
        {
            Vector2 moveInput = value.Get<Vector2>();
            Debug.Log($"Move Input: {moveInput}");
            // Apply movement based on moveInput vector
        }

        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}

