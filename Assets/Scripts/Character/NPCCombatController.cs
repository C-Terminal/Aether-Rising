using System.Collections;
using Characters.ExoGray.Scripts;
using UnityEngine;

namespace Character
{
    public class NpcCombatController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Reference to the movement controller to get range status.")]
    [SerializeField] private AICharacterMovement movementController; // Assign in Inspector
    [Tooltip("Reference to the character animator to trigger attack.")]
    [SerializeField] private Character.CharacterAnimator characterAnimator; // Assign in Inspector

    [Header("Combat Settings")]
    [Tooltip("Time in seconds between consecutive attacks.")]
    [SerializeField] private float attackInterval = 1.5f;
    [Tooltip("Approximate duration of the attack animation. Used for timing.")]
    [SerializeField] private float attackAnimationDuration = 1.0f; // Adjust this!

    // --- State ---
    private bool _isTargetInAttackRange = false;
    private float _lastAttackTime = -Mathf.Infinity;
    private bool _isCurrentlyAttacking = false;
    private Coroutine _attackCoroutine = null;

    void Awake()
    {
        // Validation
        if (movementController == null)
        {
            Debug.LogError("NPCCombatController: MovementController reference not set.", this);
            enabled = false; return;
        }
        if (characterAnimator == null)
        {
            Debug.LogWarning("NPCCombatController: CharacterAnimator reference not set.", this);
             // Optionally try to find it on the same GameObject
            characterAnimator = GetComponent<Character.CharacterAnimator>();
            if (characterAnimator == null) {
                Debug.LogError("NPCCombatController: CharacterAnimator component not found.", this);
                enabled = false; return;
            }
        }
    }

    void OnEnable()
    {
        // Subscribe to the event when this component is enabled
        if (movementController != null)
        {
            movementController.OnTargetInRangeStatusChanged += HandleTargetInRangeChanged;
            // Optional: Immediately check current state on enable
            // _isTargetInAttackRange = CheckIfTargetInitiallyInRange(); // Requires access to movementController's range/target/distance
        }
    }

    void OnDisable()
    {
        // Unsubscribe from the event when this component is disabled
        // Crucial to prevent errors if movementController is destroyed first
        if (movementController != null)
        {
            movementController.OnTargetInRangeStatusChanged -= HandleTargetInRangeChanged;
        }

        // Stop any ongoing attack coroutine if disabled
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
             // Ensure animation state is reset if disabled mid-attack
            if (characterAnimator != null && characterAnimator.gameObject.activeInHierarchy)
            {
                 characterAnimator.SetAttack(false); // Ensure attack anim stops
            }
            _isCurrentlyAttacking = false;
             _attackCoroutine = null;
        }
         _isTargetInAttackRange = false; // Reset state on disable
    }

    /// <summary>
    /// Callback method subscribed to the movement controller's event.
    /// Updates the internal state based on whether the target is in range.
    /// </summary>
    private void HandleTargetInRangeChanged(bool isInRange)
    {
        _isTargetInAttackRange = isInRange;

        // Optional: If target moves out of range mid-attack, cancel the attack?
        // if (!isInRange && _isCurrentlyAttacking && _attackCoroutine != null)
        // {
        //     StopCoroutine(_attackCoroutine);
        //     characterAnimator.SetAttack(false);
        //     _isCurrentlyAttacking = false;
        //     _attackCoroutine = null;
        // }
    }

    void Update()
    {
        // Check conditions for attacking
        if (_isTargetInAttackRange && !_isCurrentlyAttacking && Time.time >= _lastAttackTime + attackInterval)
        {
            _attackCoroutine = StartCoroutine(AttackSequence());
        }
    }

    /// <summary>
    /// Coroutine to handle the timing of the attack animation.
    /// </summary>
    private IEnumerator AttackSequence()
    {
        _isCurrentlyAttacking = true;
        _lastAttackTime = Time.time;

        // Tell the animator to start attacking
        characterAnimator.SetAttack(true);

        // Wait for the duration of the attack animation
        // TODO: Improve this by querying animation length or using Animation Events
        yield return new WaitForSeconds(attackAnimationDuration);

        // Tell the animator to stop attacking
        // Important: Only set to false if we are still supposed to be the one controlling the attack state.
        // Could be complex if other things trigger attacks. For simple NPC, this is usually fine.
        characterAnimator.SetAttack(false);

        _isCurrentlyAttacking = false;
        _attackCoroutine = null;
    }
    }
}