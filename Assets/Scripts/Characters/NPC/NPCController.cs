using System.Collections;
using Animation.AnimControllers;
using Characters.ExoGray.Scripts;
using UnityEngine;
using UnityEngine.Serialization;

// For List if used, though original used array
// Explicitly state for Random.Range
// For IEnumerator (AttackSequence)

// Assuming these namespaces/classes exist from your provided NpcCombatController
// For CharacterAnimator

// For AICharacterMovement
namespace Characters.NPC
{
    [RequireComponent(typeof(Animator))] // For RuntimeAnimatorController switching
// CharacterAnimator might also be required if it's a separate component.
// If CharacterAnimator *is* the Animator, then one RequireComponent is enough.
// For now, we assume CharacterAnimator might be distinct or a wrapper.
public class NPCController : MonoBehaviour
{
    // --- Arsenal and Weapon Setup (from Recipe 3-3's NPCController) ---
    [System.Serializable]
    public struct ArsenalItem // Renamed from Arsenal to avoid conflict if used elsewhere
    {
        public string name;
        public GameObject rightGunPrefab; // Prefab for right hand
        public GameObject leftGunPrefab;  // Prefab for left hand
        public RuntimeAnimatorController animatorController; // Specific animator for this weapon set
    }

    [Header("Weapon Arsenal")]
    [Tooltip("Transform for attaching right-hand weapons.")]
    [SerializeField] private Transform rightGunBone;
    [Tooltip("Transform for attaching left-hand weapons.")]
    [SerializeField] private Transform leftGunBone;
    [Tooltip("List of weapon setups available to this NPC.")]
    [SerializeField] private ArsenalItem[] arsenal = new ArsenalItem[0];

    private Animator _unityAnimator; // Standard Unity Animator for controller switching
    private GameObject _currentRightGunInstance;
    private GameObject _currentLeftGunInstance;

    // --- Combat Logic (from NpcCombatController) ---
    [FormerlySerializedAs("movementController")]
    [Header("Dependencies (Combat)")]
    [Tooltip("Reference to the movement controller to get range status.")]
    [SerializeField] private AIMovementSensor movementSensorController;
    [Tooltip("Reference to the character animator to trigger attack. This might be the same as the Unity Animator or a wrapper.")]
    [SerializeField] private CharacterAnimator characterAnimator;

    [Header("Combat Settings")]
    [Tooltip("Time in seconds between consecutive attacks.")]
    [SerializeField] private float attackInterval = 1.5f;
    [Tooltip("Approximate duration of the attack animation. Used for timing. Consider using Animation Events for precision.")]
    [SerializeField] private float attackAnimationDuration = 1.0f;

    private bool _isTargetInAttackRange = false;
    private float _lastAttackTime = -Mathf.Infinity; // So the first attack can happen immediately
    private bool _isCurrentlyAttacking = false;
    private Coroutine _attackCoroutine = null;
    private bool _combatLogicEnabled = false; // To be controlled by FSM (e.g., AttackState)

    void Awake()
    {
        _unityAnimator = GetComponent<Animator>();
        if (_unityAnimator == null)
        {
            Debug.LogError("NPCController: Unity Animator component not found! Required for weapon setup.", this);
            enabled = false; return;
        }

        // Arsenal validation
        if (rightGunBone == null) Debug.LogWarning("NPCController: RightGunBone not set. Right-handed weapons won't work.", this);
        if (leftGunBone == null) Debug.LogWarning("NPCController: LeftGunBone not set. Left-handed weapons won't work.", this);

        // Combat dependencies validation (from NpcCombatController)
        if (movementSensorController == null)
        {
            Debug.LogError("NPCController: MovementController reference not set for combat logic.", this);
            // Not disabling the whole component, as weapon setup might still be useful.
        }
        if (characterAnimator == null)
        {
            Debug.LogWarning("NPCController: CharacterAnimator reference not set for combat logic. Attempting to get it.", this);
            characterAnimator = GetComponent<CharacterAnimator>(); // Try to find it
            if (characterAnimator == null)
            {
                Debug.LogError("NPCController: CharacterAnimator component not found. Combat logic will fail.", this);
            }
        }

        // Initial weapon setup (from Recipe 3-3)
        if (arsenal.Length > 0)
        {
            // Select a random weapon setup, ensuring it's not "Empty" if "Empty" is index 0 and has no prefabs.
            // The original recipe started Random.Range from 1, assuming arsenal[0] was "Empty".
            int startIndex = (arsenal.Length > 1 && arsenal[0].name.ToLower() == "empty") ? 1 : 0;
            if (arsenal.Length > startIndex) {
                int randomIndex = Random.Range(startIndex, arsenal.Length);
                EquipWeaponSet(arsenal[randomIndex].name);
            } else if (arsenal.Length > 0) {
                 EquipWeaponSet(arsenal[0].name); // Fallback to first if only one, or only "Empty" exists
            }
        }
    }

    void OnEnable()
    {
        // Subscribe for combat logic
        if (movementSensorController != null)
        {
            movementSensorController.OnTargetInRangeStatusChanged += HandleTargetInRangeChanged;
            // Consider initial check:
            // _isTargetInAttackRange = movementController.IsTargetCurrentlyInRange(); // Hypothetical method
        }
    }

    void OnDisable()
    {
        // Unsubscribe for combat logic
        if (movementSensorController != null)
        {
            movementSensorController.OnTargetInRangeStatusChanged -= HandleTargetInRangeChanged;
        }

        // Clean up combat coroutine and state
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            if (characterAnimator != null && characterAnimator.gameObject.activeInHierarchy)
            {
                 characterAnimator.SetAttacking(false); // Ensure attack anim stops
            }
            _isCurrentlyAttacking = false;
            _attackCoroutine = null;
        }
        _isTargetInAttackRange = false;
    }

    /// <summary>
    /// Equips a weapon set by name from the arsenal.
    /// Destroys current weapons, instantiates new ones, and sets the animator controller.
    /// </summary>
    public void EquipWeaponSet(string weaponSetName)
    {
        if (_unityAnimator == null)
        {
            Debug.LogError("NPCController: Unity Animator is null, cannot equip weapon set.", this);
            return;
        }

        foreach (ArsenalItem item in arsenal)
        {
            if (item.name == weaponSetName)
            {
                // Clear existing weapons
                if (_currentRightGunInstance != null) Destroy(_currentRightGunInstance);
                if (_currentLeftGunInstance != null) Destroy(_currentLeftGunInstance);

                // Instantiate and attach right gun
                if (item.rightGunPrefab != null && rightGunBone != null)
                {
                    _currentRightGunInstance = Instantiate(item.rightGunPrefab, rightGunBone);
                    // Original recipe set specific local pos/rot:
                    // _currentRightGunInstance.transform.localPosition = Vector3.zero;
                    // _currentRightGunInstance.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    // Adjust as needed, or ensure prefabs are authored correctly for zeroing.
                    // For now, let's assume prefabs are authored for direct parenting.
                }

                // Instantiate and attach left gun
                if (item.leftGunPrefab != null && leftGunBone != null)
                {
                    _currentLeftGunInstance = Instantiate(item.leftGunPrefab, leftGunBone);
                    // _currentLeftGunInstance.transform.localPosition = Vector3.zero;
                    // _currentLeftGunInstance.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }

                // Set the animator controller
                if (item.animatorController != null)
                {
                    _unityAnimator.runtimeAnimatorController = item.animatorController;
                }
                else
                {
                    Debug.LogWarning($"NPCController: No RuntimeAnimatorController specified for weapon set '{weaponSetName}'.", this);
                }
                Debug.Log($"NPCController: Equipped weapon set '{weaponSetName}'.");
                return;
            }
        }
        Debug.LogWarning($"NPCController: Weapon set named '{weaponSetName}' not found in arsenal.", this);
    }

    // --- Combat Logic Methods ---

    /// <summary>
    /// Enables or disables the combat logic. To be called by the FSM (e.g., AttackState).
    /// </summary>
    public void SetCombatLogicActive(bool isActive)
    {
        _combatLogicEnabled = isActive;
        if (!isActive && _isCurrentlyAttacking && _attackCoroutine != null) // If disabling mid-attack
        {
            StopCoroutine(_attackCoroutine);
            if (characterAnimator != null && characterAnimator.gameObject.activeInHierarchy)
            {
                characterAnimator.SetAttacking(false);
            }
            _isCurrentlyAttacking = false;
            _attackCoroutine = null;
        }
         Debug.Log($"NPCController: Combat logic set to {isActive}");
    }


    private void HandleTargetInRangeChanged(bool isInRange)
    {
        _isTargetInAttackRange = isInRange;
         Debug.Log($"NPCController: Target in attack range status: {isInRange}");
        // Optional: If target moves out of range mid-attack, logic in SetCombatLogicActive or here.
    }

    void Update()
    {
        // Only run combat update if logic is enabled (by FSM state) and dependencies are met
        if (!_combatLogicEnabled || characterAnimator == null || movementSensorController == null)
        {
            return;
        }

        if (_isTargetInAttackRange && !_isCurrentlyAttacking && Time.time >= _lastAttackTime + attackInterval)
        {
            _attackCoroutine = StartCoroutine(AttackSequence());
        }
    }

    private IEnumerator AttackSequence()
    {
        _isCurrentlyAttacking = true;
        _lastAttackTime = Time.time;

        Debug.Log("NPCController: Starting Attack Sequence.");
        characterAnimator.SetAttacking(true); // Assumes CharacterAnimator handles this

        // Wait for the duration of the attack animation
        // Consider using Animation Events from the animation itself for more precise timing
        // of when an attack "lands" or when the animation sequence is truly over.
        yield return new WaitForSeconds(attackAnimationDuration);

        // Check if combat is still enabled before stopping attack animation
        // (e.g. FSM might have transitioned out of attack state due to other reasons)
        if(_combatLogicEnabled) {
            characterAnimator.SetAttacking(false);
        }
        Debug.Log("NPCController: Ending Attack Sequence.");

        _isCurrentlyAttacking = false;
        _attackCoroutine = null;
    }
}
}