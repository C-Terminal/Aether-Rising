using System;
using Animation.AnimControllers;
using Combat.Weapons.Melee;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using AI.NPC.Sensing; // Add for perception integration

namespace Characters.NPC
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class NPCController : MonoBehaviour, INPCController
    {
        [Header("Weapon Arsenal")]
        [Tooltip("Transform on the rig where the weapon will be parented (e.g., hand bone).")]
        [SerializeField]
        private Transform weaponAttachmentBone;

        [Tooltip("List of weapon setups available to this NPC.")] 
        [SerializeField]
        private ArsenalItem[] arsenal = new ArsenalItem[0];

        [Tooltip("Name of the default weapon set to equip on Awake.")] 
        [SerializeField]
        private string defaultWeaponSetName;

        [Header("Animation & Combat Interface")]
        [Tooltip("Reference to the CharacterAnimator component for detailed animation control.")]
        [SerializeField]
        private CharacterAnimator characterAnimator;

        [Header("Perception Integration")]
        [Tooltip("Optional: Reference to perception coordinator for combat targeting")]
        [SerializeField]
        private NPCPerceptionCoordinator perceptionCoordinator;

        // --- State for FSM-driven actions ---
        private Coroutine _activeActionCoroutine;

        // --- Cached Components ---
        private NavMeshAgent _agent;
        private Animator _unityAnimator;
        
        // --- Combat State ---
        private MeleeWeaponDamage _currentMeleeDamageDealer;
        private GameObject _currentWeaponInstance;
        private ArsenalItem? _currentArsenalItem; // Cache current item for performance

        // --- Properties ---
        public NavMeshAgent Agent => _agent;
        public CharacterAnimator CharAnim => characterAnimator;
        public NPCPerceptionCoordinator PerceptionCoordinator => perceptionCoordinator;

        private void Awake()
        {
            InitializeComponents();
            InitializeDefaultWeapon();
        }

        private void InitializeComponents()
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
                    Debug.LogWarning($"[{gameObject.name}] NPCController: CharacterAnimator component not found. Combat animations might not work as expected.", this);
            }

            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
            {
                Debug.LogError($"[{gameObject.name}] NPCController: NavMeshAgent component not found!", this);
                enabled = false;
                return;
            }

            // Auto-find perception coordinator if not assigned
            if (perceptionCoordinator == null)
            {
                perceptionCoordinator = GetComponentInChildren<NPCPerceptionCoordinator>();
                if (perceptionCoordinator == null)
                    Debug.LogWarning($"[{gameObject.name}] NPCController: NPCPerceptionCoordinator not found. Some combat features may not work optimally.");
            }

            if (weaponAttachmentBone == null)
                Debug.LogWarning($"[{gameObject.name}] NPCController: WeaponAttachmentBone not set. Weapons cannot be equipped.", this);
        }

        private void Update()
        {
            UpdateLocomotionAnimation();
        }

        private void UpdateLocomotionAnimation()
        {
            if (characterAnimator == null || _agent == null) return;

            // Get world-space velocity
            Vector3 worldVelocity = _agent.velocity;

            // Convert to local space (relative to NPC's facing direction)
            Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);

            // Set blend tree parameters
            characterAnimator.SetLocomotionDirection(
                forwardAmount: localVelocity.z,
                sidewaysAmount: localVelocity.x
            );
        }

        private void OnDestroy()
        {
            if (_activeActionCoroutine != null) 
                StopCoroutine(_activeActionCoroutine);
        }

        #region Weapon Management

        private void InitializeDefaultWeapon()
        {
            if (!string.IsNullOrEmpty(defaultWeaponSetName))
                EquipWeaponSet(defaultWeaponSetName);
            else if (arsenal.Length > 0) 
                EquipWeaponSet(arsenal[0].name);
        }

        public void EquipWeaponSet(string weaponSetName)
        {
            if (_unityAnimator == null || weaponAttachmentBone == null)
            {
                Debug.LogError($"[{gameObject.name}] NPCController: Animator or WeaponAttachmentBone missing, cannot equip weapon set '{weaponSetName}'.", this);
                return;
            }

            var arsenalItem = FindArsenalItem(weaponSetName);
            if (!arsenalItem.HasValue)
            {
                Debug.LogWarning($"[{gameObject.name}] NPCController: Weapon set named '{weaponSetName}' not found.", this);
                return;
            }

            UnequipCurrentWeapon();
            EquipWeapon(arsenalItem.Value);
            _currentArsenalItem = arsenalItem.Value;

            Debug.Log($"[{gameObject.name}] NPCController: Equipped weapon set '{weaponSetName}'. Damage dealer found: {_currentMeleeDamageDealer != null}");
        }

        private ArsenalItem? FindArsenalItem(string weaponSetName)
        {
            foreach (var item in arsenal)
            {
                if (item.name == weaponSetName)
                    return item;
            }
            return null;
        }

        private void UnequipCurrentWeapon()
        {
            if (_currentWeaponInstance != null)
            {
                Destroy(_currentWeaponInstance);
                _currentWeaponInstance = null;
            }
            _currentMeleeDamageDealer = null;
            _currentArsenalItem = null;
        }

        public void EquipWeapon(ArsenalItem item)
        {
            // Instantiate weapon
            if (item.weaponPrefab != null)
            {
                _currentWeaponInstance = Instantiate(item.weaponPrefab, weaponAttachmentBone);
                _currentWeaponInstance.transform.localPosition = Vector3.zero;
                _currentWeaponInstance.transform.localRotation = Quaternion.identity;

                _currentMeleeDamageDealer = _currentWeaponInstance.GetComponentInChildren<MeleeWeaponDamage>();
                if (_currentMeleeDamageDealer == null)
                {
                    Debug.LogWarning($"[{gameObject.name}] NPCController: Equipped weapon '{item.name}' does not have a MeleeWeaponDamage component in its hierarchy.", this);
                }
                else
                {
                    _currentMeleeDamageDealer.SetOwner(gameObject);
                }
            }

            // Set animator controller
            if (item.animatorController != null)
            {
                _unityAnimator.runtimeAnimatorController = item.animatorController;
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] NPCController: No RuntimeAnimatorController specified for weapon set '{item.name}'. Using existing or default.", this);
            }
        }

        public MeleeWeaponDamage GetCurrentMeleeDamageDealer() => _currentMeleeDamageDealer;

        public ArsenalItem? GetCurrentArsenalItem() => _currentArsenalItem;

        #endregion

        #region Combat Actions

        public void StartTelegraphAction()
        {
            if (characterAnimator == null) return;
            
            Debug.Log($"[{gameObject.name}] NPCController: Action - StartTelegraph.");
            characterAnimator.SetTelegraphing(true);
            
            // Optional: Notify perception system about combat state change
            NotifyPerceptionOfCombatState(CombatState.Telegraphing);
        }

        public void EndTelegraphAction()
        {
            if (characterAnimator == null) return;
            
            Debug.Log($"[{gameObject.name}] NPCController: Action - EndTelegraph.");
            characterAnimator.SetTelegraphing(false);
        }

        public void ExecuteStrikeAction()
        {
            if (characterAnimator == null) return;
            
            Debug.Log($"[{gameObject.name}] NPCController: Action - ExecuteStrike.");
            characterAnimator.SetAttacking(true);
            
            NotifyPerceptionOfCombatState(CombatState.Striking);
        }

        public void FinishStrikeAction()
        {
            if (characterAnimator == null) return;
            
            Debug.Log($"[{gameObject.name}] NPCController: Action - FinishStrike.");
            characterAnimator.SetAttacking(false);
            characterAnimator.SetRecovery(true);
            
            NotifyPerceptionOfCombatState(CombatState.Recovery);
        }

        public void ExecuteRecoveryAction()
        {
            if (characterAnimator == null) return;
            
            Debug.Log($"[{gameObject.name}] NPCController: Action - StartRecovery.");
            characterAnimator.SetAttacking(false);
            characterAnimator.SetRecovery(true);
        }

        public void FinishRecoveryAction()
        {
            if (characterAnimator == null) return;
            
            Debug.Log($"[{gameObject.name}] NPCController: Action - Finish Recovery.");
            characterAnimator.SetRecovery(false);
            characterAnimator.SetAttacking(false);
            
            NotifyPerceptionOfCombatState(CombatState.Ready);
        }
        
        public void ExecuteRangedAttack()
        {
            if (characterAnimator == null) return;
            
            Debug.Log($"[{gameObject.name}] NPCController: Action - ExecuteRangedAttack.");
            characterAnimator.SetAttacking(true);
            
            NotifyPerceptionOfCombatState(CombatState.Striking);
        }

        #endregion

        #region Animation Event Callbacks

        public void EnableHitbox()
        {
            if (_currentMeleeDamageDealer != null)
            {
                _currentMeleeDamageDealer.EnableCollider();
                Debug.Log($"[{gameObject.name}] NPCController: Hitbox ENABLED via Animation Event.");
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] NPCController: EnableHitbox called, but no MeleeDamageDealer cached for current weapon.");
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
                Debug.LogWarning($"[{gameObject.name}] NPCController: DisableHitbox called, but no MeleeDamageDealer cached for current weapon.");
            }
        }

        #endregion

        #region Perception Integration

        /// <summary>
        /// Gets the current target from the perception system
        /// </summary>
        public Transform GetCurrentTarget()
        {
            return perceptionCoordinator?.GetCurrentTarget();
        }

        /// <summary>
        /// Checks if we can currently see our target
        /// </summary>
        public bool CanSeeTarget()
        {
            return perceptionCoordinator?.IsPlayerCurrentlyVisible() ?? false;
        }

        /// <summary>
        /// Gets the distance to current target, or float.MaxValue if no target
        /// </summary>
        public float GetDistanceToTarget()
        {
            var target = GetCurrentTarget();
            if (target == null) return float.MaxValue;
            
            return Vector3.Distance(transform.position, target.position);
        }

        /// <summary>
        /// Notifies the perception system about combat state changes
        /// This could be used for advanced AI behaviors or debugging
        /// </summary>
        private void NotifyPerceptionOfCombatState(CombatState state)
        {
            // Optional: You could extend this to notify other systems
            // For now, it's just a placeholder for future functionality
            Debug.Log($"[{gameObject.name}] Combat state changed to: {state}");
        }

        #endregion

        #region Structs and Enums

        public enum CombatState
        {
            Ready,
            Telegraphing,
            Striking,
            Recovery
        }

        [Serializable]
        public struct ArsenalItem
        {
            public string name;
            public GameObject weaponPrefab;
            public RuntimeAnimatorController animatorController;

            [Header("Attack Parameters")] 
            public float telegraphDuration;
            public float strikeDuration;
            [FormerlySerializedAs("recoverDuration")]
            public float recoveryDuration;
            public float damageMultiplier;

            [Header("VFX/SFX References")] 
            public string telegraphEffectName;
            public string strikeEffectName;
        }

        #endregion
    }
}