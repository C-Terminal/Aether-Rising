using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Input
{
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputHandler: MonoBehaviour, IInputSource
    {
        public event Action OnAttackStarted;
        public event Action OnAttackCompleted;

        private PlayerInput _playerInput;
        private InputAction _attackAction;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            _attackAction = _playerInput.actions["Attack"];
        }

        private void OnEnable()
        {
            _attackAction.started += HandleAttackStarted;
            _attackAction.canceled += HandleAttackCanceled;
        }

        private void OnDisable()
        {
            _attackAction.started -= HandleAttackStarted;
            _attackAction.canceled -= HandleAttackCanceled;
        }

        private void HandleAttackStarted(InputAction.CallbackContext context)
        {
            OnAttackStarted?.Invoke();
        }

        private void HandleAttackCanceled(InputAction.CallbackContext context)
        {
            OnAttackCompleted?.Invoke();
        }

        public bool IsAttacking { get; }
    }
}