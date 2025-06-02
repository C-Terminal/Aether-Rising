using UnityEngine;

namespace Animation.AnimControllers
{
    /// <summary>
    /// Manages character animations by interfacing with the Animator component.
    /// Provides a comprehensive API to set animation parameters based on character actions and FSM states.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimator : MonoBehaviour, ICharacterAnimator
    {
        private Animator _animator;

        // --- Animator Parameter StringHashes (Cached for Performance) ---
        private readonly int _animIDSpeed = Animator.StringToHash("Speed");
        private readonly int _animIDAttack = Animator.StringToHash("Attack"); // Used as bool by NPCController
        private readonly int _animIDAttackTrigger = Animator.StringToHash("AttackTrigger"); // Optional: if Actions.cs uses a trigger for attack

        private readonly int _animIDAiming = Animator.StringToHash("Aiming");
        private readonly int _animIDSquat = Animator.StringToHash("Squat"); // Or "IsCrouching"

        private readonly int _animIDDamageTrigger = Animator.StringToHash("Damage"); // Trigger for hit reaction
        private readonly int _animIDDamageID = Animator.StringToHash("DamageID"); // Integer for type of hit animation

        private readonly int _animIDDeathTrigger = Animator.StringToHash("Death"); // Trigger for death animation

        // Existing parameters (good to keep for general character control)
        private readonly int _animIDJumpTrigger = Animator.StringToHash("Jump");
        private readonly int _animIDGrounded = Animator.StringToHash("Grounded");
        
        // New parameters for movement locomotion blend and backward movement
        private readonly int _animIDLocomotionBlend = Animator.StringToHash("LocomotionBlend");
        private readonly int _animIDBackwardMovement = Animator.StringToHash("BackwardMovement");
        
        // Add to CharacterAnimator.cs
        private readonly int _animIDSpeedForward = Animator.StringToHash("SpeedForward");
        private readonly int _animIDSpeedSideways = Animator.StringToHash("SpeedSideways");
        private readonly int _animIDTelegraph = Animator.StringToHash("Telegraph");
        private readonly int _animIDRecovery = Animator.StringToHash("Recovery");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                Debug.LogError("CharacterAnimator: Animator component not found on this GameObject. Script will not function.", this);
                enabled = false; // Disable if critical component is missing
            }
        }

        /// <summary>
        /// Sets the character's current movement speed for blend trees (idle, walk, run).
        /// </summary>
        /// <param name="speed">The normalized speed (e.g., 0 for idle, 0.5 for walk, 1.0 for run).</param>
        public void SetMovementSpeed(float speed)
        {
            if (_animator != null)
            {
                _animator.SetFloat(_animIDSpeed, speed);
            }
        }

        /// <summary>
        /// Sets the character's locomotion blend parameter to control animation speed/intensity.
        /// </summary>
        /// <param name="blendValue">The normalized blend value (0.0 to 1.0) controlling animation intensity.</param>
        public void SetLocomotionBlend(float blendValue)
        {
            if (_animator != null)
            {
                // Clamp value between 0 and 1 to ensure valid blend parameter
                blendValue = Mathf.Clamp01(blendValue);
                _animator.SetFloat(_animIDLocomotionBlend, blendValue);
            }
        }

        /// <summary>
        /// Sets whether the character is moving backward, which affects animation selection/blending.
        /// </summary>
        /// <param name="isMovingBackward">True if character is moving backward, false otherwise.</param>
        public void SetBackwardMovement(bool isMovingBackward)
        {
            if (_animator != null)
            {
                _animator.SetBool(_animIDBackwardMovement, isMovingBackward);
            }
        }

        /// <summary>
        /// Sets the boolean parameter controlling a continuous attack animation state.
        /// Typically used by NPCController for its attack loop.
        /// </summary>
        /// <param name="isAttacking">True to start/continue attacking, false to stop.</param>
        public void SetAttacking(bool isAttacking) // Renamed from SetAttack to avoid confusion with TriggerAttack
        {
            if (_animator != null)
            {
                _animator.SetBool(_animIDAttack, isAttacking);
            }
        }
        
        public void SetRecovery(bool isRecovering) // Renamed from SetAttack to avoid confusion with TriggerAttack
        {
            if (_animator != null)
            {
                _animator.SetBool(_animIDRecovery, isRecovering);
            }
        }

        /// <summary>
        /// Triggers a one-shot attack animation.
        /// Potentially used by Actions.cs if FSM dictates a specific single attack.
        /// </summary>
        public void TriggerAttack()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(_animIDAttackTrigger);
            }
        }

        /// <summary>
        /// Sets the aiming state of the character.
        /// </summary>
        /// <param name="isAiming">True if the character is aiming, false otherwise.</param>
        public void SetAiming(bool isAiming)
        {
            if (_animator != null)
            {
                _animator.SetBool(_animIDAiming, isAiming);
            }
        }

        /// <summary>
        /// Sets the squatting/crouching state of the character.
        /// </summary>
        /// <param name="isSquatting">True if the character is squatting, false otherwise.</param>
        public void SetSquatting(bool isSquatting)
        {
            if (_animator != null)
            {
                _animator.SetBool(_animIDSquat, isSquatting);
            }
        }

        /// <summary>
        /// Triggers a hit reaction animation and sets the type of damage animation.
        /// </summary>
        /// <param name="damageTypeID">An integer ID to select a specific damage animation variant.</param>
        public void TriggerHitReaction(int damageTypeID)
        {
            if (_animator != null)
            {
                _animator.SetInteger(_animIDDamageID, damageTypeID);
                _animator.SetTrigger(_animIDDamageTrigger);
            }
        }

        /// <summary>
        /// Triggers the death animation.
        /// </summary>
        public void TriggerDeath()
        {
            if (_animator != null)
            {
                // Ensure other states that might prevent death anim are reset
                SetMovementSpeed(0f);
                SetLocomotionBlend(0f);
                SetBackwardMovement(false);
                SetAiming(false);
                SetSquatting(false);
                SetAttacking(false);
                _animator.SetTrigger(_animIDDeathTrigger);
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
                _animator.SetTrigger(_animIDJumpTrigger);
            }
        }

        /// <summary>
        /// Gets the underlying Animator component. Useful for advanced scenarios
        /// like directly querying animation state names or lengths. Use with caution.
        /// </summary>
        public Animator GetRawAnimator() // Renamed for clarity
        {
            return _animator;
        }
        
        public void SetLocomotionDirection(float forwardAmount, float sidewaysAmount)
        {
            if (_animator != null)
            {
                _animator.SetFloat(_animIDSpeedForward, forwardAmount, 0.2f, Time.deltaTime);
                _animator.SetFloat(_animIDSpeedSideways, sidewaysAmount, 0.2f, Time.deltaTime);
            }
        }

        public void SetTelegraphing(bool isTelegraphing)
        {
            if (_animator != null)
            {
                _animator.SetBool(_animIDTelegraph, isTelegraphing);
            }
        }
        
        

        /// <summary>
        /// Checks if the Animator is currently in a specific animation state.
        /// Useful for preventing interruptions or redundant triggers.
        /// </summary>
        /// <param name="stateName">The name of the state to check (e.g., "Death", "Attack_Loop").</param>
        /// <param name="layerIndex">The animator layer index (default is 0 for base layer).</param>
        /// <returns>True if the Animator is currently in the specified state.</returns>
        public bool IsInAnimationState(string stateName, int layerIndex = 0)
        {
            if (_animator != null)
            {
                return _animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(stateName);
            }
            return false;
        }

        public void PlayAnimation(string animationName)
        {
            if (_animator != null)
            {
                _animator.Play(animationName);
            }
        }
    }
}