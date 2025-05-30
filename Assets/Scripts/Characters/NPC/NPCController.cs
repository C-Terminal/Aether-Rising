using System;
using Animation.AnimControllers;
using Combat.Weapons.Melee;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Characters.NPC
{
    [RequireComponent(typeof(Animator))] // Standard Unity Animator for RuntimeAnimatorController
    // CharacterAnimator is also expected for detailed animation control.
    [RequireComponent(typeof(NavMeshAgent))]
    public class NPCController : MonoBehaviour
    {
        [Header("Weapon Arsenal")]
        [Tooltip("Transform on the rig where the weapon will be parented (e.g., hand bone).")]
        [SerializeField]
        private Transform weaponAttachmentBone;

        [Tooltip("List of weapon setups available to this NPC.")] [SerializeField]
        private ArsenalItem[] arsenal = new ArsenalItem[0];

        [Tooltip("Name of the default weapon set to equip on Awake.")] [SerializeField]
        private string defaultWeaponSetName;

        [Header("Animation & Combat Interface")]
        [Tooltip("Reference to the CharacterAnimator component for detailed animation control.")]
        [SerializeField]
        private CharacterAnimator characterAnimator;

        // --- State for FSM-driven actions ---
        private Coroutine _activeActionCoroutine; // For timed actions like strike if not purely anim event driven

        private NavMeshAgent _agent;
        private MeleeWeaponDamage _currentMeleeDamageDealer; // Cached from the instantiated weapon
        private GameObject _currentWeaponInstance;

        private Animator _unityAnimator; // Standard Unity Animator

        private void Awake()
        {
            _unityAnimator = GetComponent<Animator>();
            if (_unityAnimator == null)
            {
                Debug.LogError($"[{gameObject.name}] NPCController: Unity Animator component not found!", this);
                enabled = false;
                return;
            }

            if (characterAnimator == null)
            {
                characterAnimator = GetComponent<CharacterAnimator>();
                if (characterAnimator == null)
                    Debug.LogWarning(
                        $"[{gameObject.name}] NPCController: CharacterAnimator component not found. Combat animations might not work as expected.",
                        this);
            }


            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
                if (_agent == null)
                {
                    Debug.LogError($"[{gameObject.name}] NPCController: NavMeshAgent component not found!", this);
                    enabled = false;
                    return;
                }
            }

            if (weaponAttachmentBone == null)
                Debug.LogWarning(
                    $"[{gameObject.name}] NPCController: WeaponAttachmentBone not set. Weapons cannot be equipped.",
                    this);

            InitializeDefaultWeapon();
        }

        private void Update()
        {
            // Get world-space velocity
            Vector3 worldVelocity = _agent.velocity;

            // Convert to local space (relative to NPC's facing direction)
            Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);

            // Set blend tree parameters using your API
            characterAnimator.SetLocomotionDirection(
                forwardAmount: localVelocity.z,
                sidewaysAmount: localVelocity.x
            );
        }

        // Optional: Stop any active timed actions if the component is disabled.
        private void OnDestroy() // Or OnDisable if you re-enable
        {
            if (_activeActionCoroutine != null) StopCoroutine(_activeActionCoroutine);
        }

        private void InitializeDefaultWeapon()
        {
            if (!string.IsNullOrEmpty(defaultWeaponSetName))
                EquipWeaponSet(defaultWeaponSetName);
            else if (arsenal.Length > 0) EquipWeaponSet(arsenal[0].name); // Fallback to the first item
        }

        public void EquipWeaponSet(string weaponSetName)
        {
            if (_unityAnimator == null || weaponAttachmentBone == null)
            {
                Debug.LogError(
                    $"[{gameObject.name}] NPCController: Animator or WeaponAttachmentBone missing, cannot equip weapon set '{weaponSetName}'.",
                    this);
                return;
            }

            foreach (var item in arsenal)
                if (item.name == weaponSetName)
                {
                    if (_currentWeaponInstance != null) Destroy(_currentWeaponInstance);
                    _currentMeleeDamageDealer = null;

                    if (item.weaponPrefab != null)
                    {
                        _currentWeaponInstance = Instantiate(item.weaponPrefab, weaponAttachmentBone);
                        // Ensure local position/rotation are reset if prefab isn't authored for it
                        _currentWeaponInstance.transform.localPosition = Vector3.zero;
                        _currentWeaponInstance.transform.localRotation = Quaternion.identity;

                        _currentMeleeDamageDealer = _currentWeaponInstance.GetComponentInChildren<MeleeWeaponDamage>();
                        if (_currentMeleeDamageDealer == null)
                            Debug.LogWarning(
                                $"[{gameObject.name}] NPCController: Equipped weapon '{item.name}' does not have a MeleeWeaponDamage component in its hierarchy.",
                                this);
                    }

                    if (item.animatorController != null)
                        _unityAnimator.runtimeAnimatorController = item.animatorController;
                    else
                        Debug.LogWarning(
                            $"[{gameObject.name}] NPCController: No RuntimeAnimatorController specified for weapon set '{item.name}'. Using existing or default.",
                            this);
                    Debug.Log(
                        $"[{gameObject.name}] NPCController: Equipped weapon set '{item.name}'. Damage dealer found: {_currentMeleeDamageDealer != null}");
                    if (_currentMeleeDamageDealer != null)
                        _currentMeleeDamageDealer.SetOwner(gameObject); // 'this.gameObject' is the NPC itself
                    return;
                }

            Debug.LogWarning($"[{gameObject.name}] NPCController: Weapon set named '{weaponSetName}' not found.", this);
        }

        /// <summary>
        ///     Gets the currently active MeleeWeaponDamage component.
        /// </summary>
        public MeleeWeaponDamage GetCurrentMeleeDamageDealer()
        {
            return _currentMeleeDamageDealer;
        }

        /// <summary>
        ///     Gets the current ArsenalItem details if a weapon is equipped.
        /// </summary>
        public ArsenalItem? GetCurrentArsenalItem()
        {
            if (_currentWeaponInstance != null && _unityAnimator != null)
                // Find which arsenal item corresponds to the current animator controller (or weapon name if stored)
                foreach (var item in arsenal)
                    if (item.animatorController == _unityAnimator.runtimeAnimatorController ||
                        _currentWeaponInstance.name.StartsWith(item.weaponPrefab
                            .name)) // Check based on prefab name match
                        return item;

            return null;
        }


        // --- FSM-Driven Combat Actions ---

        public void  StartTelegraphAction()
        {
            if (characterAnimator == null) return;
            Debug.Log($"[{gameObject.name}] NPCController: Action - StartTelegraph.");
            characterAnimator.SetTelegraphing(true);
        }

        public void EndTelegraphAction()
        {
            if (characterAnimator == null) return;
            Debug.Log($"[{gameObject.name}] NPCController: Action - EndTelegraph.");
            characterAnimator.SetTelegraphing(false);
        }

        /// <summary>
        ///     Initiates the strike animation. Hitbox enabling/disabling should be handled
        ///     by Animation Events calling EnableHitbox/DisableHitbox on this NPCController instance.
        /// </summary>
        public void ExecuteStrikeAction()
        {
            if (characterAnimator == null) return;
            Debug.Log($"[{gameObject.name}] NPCController: Action - ExecuteStrike.");
            // Uses the "Attack" bool parameter, consistent with the CharacterAnimator's SetAttacking method
            // characterAnimator.SetTelegraphing(false); // Set the "Attack" bool parameter
            characterAnimator.SetAttacking(true);
            // Alternatively, if your strike is a one-shot trigger:
            // characterAnimator.TriggerAttack();
        }

        /// <summary>
        ///     Signals that the strike animation sequence (from FSM perspective) is complete.
        ///     Resets any animation states related to the active strike.
        /// </summary>
        public void FinishStrikeAction()
        {
            if (characterAnimator == null) return;
            Debug.Log($"[{gameObject.name}] NPCController: Action - FinishStrike.");
            characterAnimator.SetAttacking(false); // Reset the "Attack" bool
            characterAnimator.SetRecovery(true);
            // Hitbox should have been disabled by an Animation Event already.
        }

        public void ExecuteRecoveryAction()
        {
            if (characterAnimator == null) return;
            Debug.Log($"[{gameObject.name}] NPCController: Action - StartRecovery.");
            // May involve playing a specific recovery animation or just ensuring
            // the character is blending back to a ready/idle pose.
            // Often, just transitioning out of attack in the animator is enough.
            // CharacterAnimator.SetMovementSpeed(0); might be called by the FSM state.
            characterAnimator.SetAttacking(false);
            characterAnimator.SetRecovery(true);
        }

        // --- Animation Event Callbacks (called by WarriorAnimationEvents) ---
        public void EnableHitbox()
        {
            if (_currentMeleeDamageDealer != null)
            {
                _currentMeleeDamageDealer.EnableCollider();
                Debug.Log($"[{gameObject.name}] NPCController: Hitbox ENABLED via Animation Event.");
            }
            else
            {
                Debug.LogWarning(
                    $"[{gameObject.name}] NPCController: EnableHitbox called, but no MeleeDamageDealer cached for current weapon.");
            }
        }

        public void DisableHitbox()
        {
            if (_currentMeleeDamageDealer != null)
            {
                _currentMeleeDamageDealer.DisableCollider();
                Debug.Log($"[{gameObject.name}] NPCController: Hitbox DISABLED via Animation Event.");
            }
            else
            {
                Debug.LogWarning(
                    $"[{gameObject.name}] NPCController: DisableHitbox called, but no MeleeDamageDealer cached for current weapon.");
            }
        }

        public void FinishRecoveryAction()
        {
            if (characterAnimator == null) return;
            Debug.Log($"[{gameObject.name}] NPCController: Action - Finish Recovery.");
            characterAnimator.SetRecovery(false); // Reset the "Recovery" bool
            characterAnimator.SetAttacking(false);
            // Hitbox should have been disabled by an Animation Event already.
        }

        [Serializable]
        public struct ArsenalItem
        {
            public string name;
            public GameObject weaponPrefab; // Simplified: assumes one primary weapon object per set

            public RuntimeAnimatorController animatorController; // Specific animator for this weapon set

            // Weapon-specific timings (can be used by FSM states or this controller)
            [Header("Attack Parameters")] public float telegraphDuration;
            public float strikeDuration;

            [FormerlySerializedAs("recoverDuration")]
            public float recoveryDuration;

            public float damageMultiplier;

            [Header("VFX/SFX References")] public string telegraphEffectName;

            public string strikeEffectName;
            // public MeleeWeaponDamage meleeDamageDealer; // If you want to link it directly here
        }
    }
}