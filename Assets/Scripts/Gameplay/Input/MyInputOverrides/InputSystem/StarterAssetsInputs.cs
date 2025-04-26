using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.Input.MyInputOverrides.InputSystem
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
		public bool attack;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

		
		private PlayerInput playerInput;
    
		private void Awake()
		{
			// Get reference to the PlayerInput component
			playerInput = GetComponent<PlayerInput>();
        
			// Subscribe to the events
			if (playerInput != null)
			{
				// Subscribe to all actions
				playerInput.onActionTriggered += HandleAction;
			}
		}
    
		private void OnDestroy()
		{
			// Unsubscribe when the object is destroyed
			if (playerInput != null)
			{
				playerInput.onActionTriggered -= HandleAction;
			}
		}
    
		private void HandleAction(InputAction.CallbackContext context)
		{
			// Get the action name
			string actionName = context.action.name;
        
			// Handle different actions
			switch(actionName)
			{
				case "Move":
					move = context.ReadValue<Vector2>();
					break;
				case "Look":
					look = context.ReadValue<Vector2>();
					break;
				case "Jump":
					JumpInput(context.performed);
					break;
				case "Sprint":
					SprintInput(context.performed);
					break;
				case "Attack":
					AttackInput(context.performed);
					break;
			}
		}
#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}
		
		public void OnAttack(InputValue value)
		{
			Debug.Log("Attack button pressed: " + value.isPressed);
			AttackInput(value.isPressed);
		}
    

#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}
		
		public void AttackInput(bool newAttackState)
		{
			Debug.Log("Attack button pressed: " + newAttackState);
			attack = newAttackState;
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
	}
	
}