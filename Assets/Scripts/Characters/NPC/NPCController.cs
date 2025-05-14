using Animation.AnimControllers;
using Combat.Weapons.Melee;
using UnityEngine;

namespace Characters.NPC
{
    [RequireComponent(typeof(Animator))] // Standard Unity Animator for RuntimeAnimatorController
    // CharacterAnimator is also expected for detailed animation control.
    public class NPCController : MonoBehaviour
    {
        [System.Serializable]
        public struct ArsenalItem
        {
            public string name;
            public GameObject weaponPrefab; // Simplified: assumes one primary weapon object per set
            public RuntimeAnimatorController animatorController; // Specific animator for this weapon set
            // Weapon-specific timings (can be used by FSM states or this controller)
            public float telegraphDuration;
            public float strikeDuration; // Or use animation events to determine end
            public float recoveryDuration;
            // public MeleeWeaponDamage meleeDamageDealer; // If you want to link it directly here
        }

        [Header("Weapon Arsenal")]
        [Tooltip("Transform on the rig where the weapon will be parented (e.g., hand bone).")]
        [SerializeField] private Transform weaponAttachmentBone;
        [Tooltip("List of weapon setups available to this NPC.")]
        [SerializeField] private ArsenalItem[] arsenal = new ArsenalItem[0];
        [Tooltip("Name of the default weapon set to equip on Awake.")]
        [SerializeField] private string defaultWeaponSetName;

        private Animator _unityAnimator; // Standard Unity Animator
        private GameObject _currentWeaponInstance;
        private MeleeWeaponDamage _currentMeleeDamageDealer; // Cached from the instantiated weapon

        [Header("Animation & Combat Interface")]
        [Tooltip("Reference to the CharacterAnimator component for detailed animation control.")]
        [SerializeField] private CharacterAnimator characterAnimator;
        // No longer needs direct reference to AIMovementSensor for its own attack loop

        // --- State for FSM-driven actions ---
        private Coroutine _activeActionCoroutine; // For timed actions like strike if not purely anim event driven

        void Awake()
        {
            _unityAnimator = GetComponent<Animator>();
            if (_unityAnimator == null)
            {
                Debug.LogError($"[{gameObject.name}] NPCController: Unity Animator component not found!", this);
                enabled = false; return;
            }

            if (characterAnimator == null)
            {
                characterAnimator = GetComponent<CharacterAnimator>();
                if (characterAnimator == null)
                {
                    Debug.LogWarning($"[{gameObject.name}] NPCController: CharacterAnimator component not found. Combat animations might not work as expected.", this);
                }
            }

            if (weaponAttachmentBone == null) Debug.LogWarning($"[{gameObject.name}] NPCController: WeaponAttachmentBone not set. Weapons cannot be equipped.", this);

            InitializeDefaultWeapon();
        }

        private void InitializeDefaultWeapon()
        {
            if (!string.IsNullOrEmpty(defaultWeaponSetName))
            {
                EquipWeaponSet(defaultWeaponSetName);
            }
            else if (arsenal.Length > 0)
            {
                EquipWeaponSet(arsenal[0].name); // Fallback to the first item
            }
        }

        public void EquipWeaponSet(string weaponSetName)
        {
            if (_unityAnimator == null || weaponAttachmentBone == null)
            {
                Debug.LogError($"[{gameObject.name}] NPCController: Animator or WeaponAttachmentBone missing, cannot equip weapon set '{weaponSetName}'.", this);
                return;
            }

            foreach (ArsenalItem item in arsenal)
            {
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
                        {
                            Debug.LogWarning($"[{gameObject.name}] NPCController: Equipped weapon '{item.name}' does not have a MeleeWeaponDamage component in its hierarchy.", this);
                        }
                    }

                    if (item.animatorController != null)
                    {
                        _unityAnimator.runtimeAnimatorController = item.animatorController;
                    }
                    else
                    {
                        Debug.LogWarning($"[{gameObject.name}] NPCController: No RuntimeAnimatorController specified for weapon set '{item.name}'. Using existing or default.", this);
                    }
                    Debug.Log($"[{gameObject.name}] NPCController: Equipped weapon set '{item.name}'. Damage dealer found: {_currentMeleeDamageDealer != null}");
                    if (_currentMeleeDamageDealer != null)
                    {
                        _currentMeleeDamageDealer.SetOwner(this.gameObject); // 'this.gameObject' is the NPC itself
                    }
                    return;
                }
            }
            Debug.LogWarning($"[{gameObject.name}] NPCController: Weapon set named '{weaponSetName}' not found.", this);
        }

        /// <summary>
        /// Gets the currently active MeleeWeaponDamage component.
        /// </summary>
        public MeleeWeaponDamage GetCurrentMeleeDamageDealer() => _currentMeleeDamageDealer;

        /// <summary>
        /// Gets the current ArsenalItem details if a weapon is equipped.
        /// </summary>
        public ArsenalItem? GetCurrentArsenalItem()
        {
            if (_currentWeaponInstance != null && _unityAnimator != null)
            {
                // Find which arsenal item corresponds to the current animator controller (or weapon name if stored)
                foreach (var item in arsenal)
                {
                    if (item.animatorController == _unityAnimator.runtimeAnimatorController || 
                        (_currentWeaponInstance.name.StartsWith(item.weaponPrefab.name))) // Check based on prefab name match
                    {
                        return item;
                    }
                }
            }
            return null;
        }


        // --- FSM-Driven Combat Actions ---

        public void StartTelegraphAction()
        {
            if (characterAnimator == null) return;
            Debug.Log($"[{gameObject.name}] NPCController: Action - StartTelegraph.");
            characterAnimator.SetAiming(true); // Assuming "Aiming" bool is used for telegraph
            // Or: characterAnimator.TriggerTelegraph(); if you have a specific trigger
        }

        public void EndTelegraphAction()
        {
            if (characterAnimator == null) return;
            Debug.Log($"[{gameObject.name}] NPCController: Action - EndTelegraph.");
            characterAnimator.SetAiming(false);
        }

        /// <summary>
        /// Initiates the strike animation. Hitbox enabling/disabling should be handled
        /// by Animation Events calling EnableHitbox/DisableHitbox on this NPCController instance.
        /// </summary>
        public void ExecuteStrikeAction()
        {
            if (characterAnimator == null) return;
            Debug.Log($"[{gameObject.name}] NPCController: Action - ExecuteStrike.");
            // Uses the "Attack" bool parameter, consistent with the CharacterAnimator's SetAttacking method
            characterAnimator.SetAttacking(true);
            // Alternatively, if your strike is a one-shot trigger:
            // characterAnimator.TriggerAttack();
        }

        /// <summary>
        /// Signals that the strike animation sequence (from FSM perspective) is complete.
        /// Resets any animation states related to the active strike.
        /// </summary>
        public void FinishStrikeAction()
        {
            if (characterAnimator == null) return;
            Debug.Log($"[{gameObject.name}] NPCController: Action - FinishStrike.");
            characterAnimator.SetAttacking(false); // Reset the "Attack" bool
            // Hitbox should have been disabled by an Animation Event already.
        }
        
        public void StartRecoveryAction()
        {
            if (characterAnimator == null) return;
            Debug.Log($"[{gameObject.name}] NPCController: Action - StartRecovery.");
            // May involve playing a specific recovery animation or just ensuring
            // the character is blending back to a ready/idle pose.
            // Often, just transitioning out of attack in the animator is enough.
            // CharacterAnimator.SetMovementSpeed(0); might be called by the FSM state.
        }

        // --- Animation Event Callbacks (called by WarriorAnimationEvents) ---
        public void EnableHitbox()
        {
            if (_currentMeleeDamageDealer != null)
            {
                _currentMeleeDamageDealer.EnableCollider();
                Debug.Log($"[{gameObject.name}] NPCController: Hitbox ENABLED via Animation Event.");
            }
            else Debug.LogWarning($"[{gameObject.name}] NPCController: EnableHitbox called, but no MeleeDamageDealer cached for current weapon.");
        }

        public void DisableHitbox()
        {
            if (_currentMeleeDamageDealer != null)
            {
                _currentMeleeDamageDealer.DisableCollider();
                Debug.Log($"[{gameObject.name}] NPCController: Hitbox DISABLED via Animation Event.");
            }
            else Debug.LogWarning($"[{gameObject.name}] NPCController: DisableHitbox called, but no MeleeDamageDealer cached for current weapon.");
        }

        // Optional: Stop any active timed actions if the component is disabled.
        void OnDestroy() // Or OnDisable if you re-enable
        {
            if (_activeActionCoroutine != null)
            {
                StopCoroutine(_activeActionCoroutine);
            }
        }
    }
}