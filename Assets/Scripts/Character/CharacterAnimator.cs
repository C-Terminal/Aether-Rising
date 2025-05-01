using UnityEngine;

namespace Character
{
    /// <summary>
    /// Manages character animations by interfacing with the Animator component.
    /// Provides methods to set animation parameters based on character actions.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimator : MonoBehaviour
    {
        private Animator _animator;

        // --- Animator Parameter IDs (Cached for Performance) ---
        // It's good practice to add all relevant parameter IDs here
        private readonly int _animIDAttack = Animator.StringToHash("Attack");
        private readonly int _animIDSpeed = Animator.StringToHash("Speed"); // Example: For movement
        private readonly int _animIDJump = Animator.StringToHash("Jump");   // Example: For jumping
        private readonly int _animIDGrounded = Animator.StringToHash("Grounded"); // Example: For grounded state

        private void Awake()
        {
            // Get the Animator component attached to this GameObject
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                Debug.LogError("CharacterAnimator requires an Animator component.", this);
            }
        }

        /// <summary>
        /// Sets the boolean parameter controlling the attack animation state.
        /// </summary>
        /// <param name="isAttacking">True to trigger the attack animation, false to stop.</param>
        public void SetAttack(bool isAttacking)
        {
            if (_animator != null)
            {
                _animator.SetBool(_animIDAttack, isAttacking);
            }
        }

        /// <summary>
        /// Sets the float parameter representing the character's movement speed.
        /// </summary>
        /// <param name="speed">The current speed value (e.g., 0 for idle, >0 for moving).</param>
        public void SetMovementSpeed(float speed)
        {
            if (_animator != null)
            {
                 // Typically use magnitude for blending walk/run
                _animator.SetFloat(_animIDSpeed, speed);
            }
        }

         /// <summary>
        /// Sets the boolean parameter indicating if the character is grounded.
        /// </summary>
        /// <param name="isGrounded">True if the character is on the ground, false otherwise.</param>
        public void SetGrounded(bool isGrounded)
        {
             if (_animator != null)
            {
                _animator.SetBool(_animIDGrounded, isGrounded);
            }
        }

        /// <summary>
        /// Triggers the jump animation parameter.
        /// </summary>
        public void TriggerJump()
        {
             if (_animator != null)
            {
                _animator.SetTrigger(_animIDJump);
            }
        }

        /// <summary>
        /// Gets the underlying Animator component. Useful for advanced scenarios
        /// like accessing state information directly (use with caution to maintain encapsulation).
        /// </summary>
        public Animator GetAnimator()
        {
            return _animator;
        }

        // --- Optional: Add methods for other parameters as needed ---
        // public void SetSomeOtherParameter(float value) { ... }
        // public void TriggerAnotherAction() { ... }
    }
}