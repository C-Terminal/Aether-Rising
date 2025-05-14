using System.Collections.Generic;
using Combat.DamageSystem.Health;
using UnityEngine;

// Required for List

namespace Combat.Weapons.Melee
{
    /// <summary>
    /// Attached to a weapon's collider (set as a trigger) to detect hits and apply damage.
    /// Its collider should be enabled/disabled by Animation Events via NPCController.
    /// </summary>
    [RequireComponent(typeof(Collider))] // Ensures there's a collider to work with
    public class MeleeWeaponDamage : MonoBehaviour
    {
        [Header("Damage Settings")]
        [Tooltip("Amount of damage this weapon inflicts on a successful hit.")]
        [SerializeField] private int damageAmount = 10;
        [Tooltip("Tags of objects that can be damaged by this weapon.")]
        [SerializeField] private string[] damageableTags = { "Player", "NPC" }; // Add other tags if needed

        [Header("Hit Detection Control")]
        [Tooltip("Minimum time in seconds between consecutive hits on the SAME target during one swing. Helps prevent rapid multi-hits if collider stays active.")]
        [SerializeField] private float hitCooldownPerTarget = 0.5f;

        private Collider _weaponCollider;
        private List<GameObject> _hitObjectsThisSwing; // Tracks objects hit during the current active phase of the collider
        private Dictionary<GameObject, float> _lastHitTimePerTarget; // Tracks last hit time for cooldown

        // Reference to the owner of this weapon (the NPC or Player wielding it)
        // This helps prevent self-damage and identify the attacker.
        private GameObject _weaponOwner;

        void Awake()
        {
            _weaponCollider = GetComponent<Collider>();
            if (_weaponCollider == null)
            {
                Debug.LogError($"[{gameObject.name}] MeleeWeaponDamage: Collider component not found!", this);
                enabled = false;
                return;
            }

            // Ensure the collider is a trigger, as we'll use OnTriggerEnter
            if (!_weaponCollider.isTrigger)
            {
                Debug.LogWarning($"[{gameObject.name}] MeleeWeaponDamage: Collider is not set to 'Is Trigger'. Forcing it now. Please set this in the Inspector.", this);
                _weaponCollider.isTrigger = true;
            }

            _hitObjectsThisSwing = new List<GameObject>();
            _lastHitTimePerTarget = new Dictionary<GameObject, float>();

            // Initially, the weapon collider should be disabled.
            // It will be enabled by animation events during an attack swing.
            DisableCollider();
        }

        /// <summary>
        /// Sets the owner of this weapon. Important to avoid self-damage.
        /// This should be called by NPCController or player's weapon handler when the weapon is equipped/instantiated.
        /// </summary>
        public void SetOwner(GameObject owner)
        {
            _weaponOwner = owner;
            // Debug.Log($"[{gameObject.name}] MeleeWeaponDamage owner set to: {_weaponOwner?.name}");
        }

        /// <summary>
        /// Enables the weapon's collider to allow hit detection.
        /// Typically called by an Animation Event at the start of an attack's damage window.
        /// </summary>
        public void EnableCollider()
        {
            if (_weaponCollider != null)
            {
                _weaponCollider.enabled = true;
                _hitObjectsThisSwing.Clear(); // Clear list of hit objects for the new swing/active phase
                // Cooldown dictionary (_lastHitTimePerTarget) can persist or be cleared depending on desired cooldown behavior across swings.
                // For a strict once-per-swing-per-target, clearing _hitObjectsThisSwing is enough.
                // The dictionary handles rapid re-hits if the collider stays active for a bit.
                // Debug.Log($"[{gameObject.name}] MeleeWeaponDamage: Collider ENABLED.");
            }
        }

        /// <summary>
        /// Disables the weapon's collider to stop hit detection.
        /// Typically called by an Animation Event at the end of an attack's damage window.
        /// </summary>
        public void DisableCollider()
        {
            if (_weaponCollider != null)
            {
                _weaponCollider.enabled = false;
                // Debug.Log($"[{gameObject.name}] MeleeWeaponDamage: Collider DISABLED.");
            }
        }

        void OnTriggerEnter(Collider other)
        {
            // Do nothing if the collider is not enabled (e.g., outside of an attack swing)
            if (!_weaponCollider.enabled)
            {
                return;
            }

            // Prevent self-damage or hitting the owner's other colliders
            if (_weaponOwner != null && (other.gameObject == _weaponOwner || other.transform.IsChildOf(_weaponOwner.transform)))
            {
                // Debug.Log($"[{gameObject.name}] MeleeWeaponDamage: Ignored collision with self/owner ({other.gameObject.name}).");
                return;
            }

            // Check if the hit object has one of the damageable tags
            bool isDamageableTag = false;
            foreach (string tag in damageableTags)
            {
                if (other.CompareTag(tag))
                {
                    isDamageableTag = true;
                    break;
                }
            }

            if (!isDamageableTag)
            {
                // Debug.Log($"[{gameObject.name}] MeleeWeaponDamage: Collided with non-damageable object '{other.gameObject.name}' with tag '{other.tag}'.");
                return; // Not a damageable target
            }


            // Check if this object has already been hit during this current swing (active collider phase)
            if (_hitObjectsThisSwing.Contains(other.gameObject))
            {
                // Optional: Check cooldown for rapid re-hits on the same target if collider stays active
                if (_lastHitTimePerTarget.ContainsKey(other.gameObject) &&
                    Time.time < _lastHitTimePerTarget[other.gameObject] + hitCooldownPerTarget)
                {
                    // Debug.Log($"[{gameObject.name}] MeleeWeaponDamage: Hit {other.name} again too soon (cooldown).");
                    return; // Hit too soon after previous hit on this target
                }
                // If it's not on cooldown (or cooldown is very short), but already hit *this swing*, we might still ignore it.
                // The _hitObjectsThisSwing list handles the "once per swing active phase"
                // Debug.Log($"[{gameObject.name}] MeleeWeaponDamage: {other.name} already hit this swing. (This message implies cooldown logic might be bypassed if hitCooldownPerTarget is 0 or very small).");
                return;
            }


            // Attempt to get the Health component from the hit object
            // It might be on a parent object if colliders are on child hitboxes
            Health healthComponent = other.GetComponentInParent<Health>();
            if (healthComponent != null && !healthComponent.IsDead)
            {
                Debug.Log($"[{gameObject.name}] MeleeWeaponDamage: Successfully hit '{other.gameObject.name}' (owner: {_weaponOwner?.name}) dealing {damageAmount} damage.");
                healthComponent.TakeDamage(damageAmount, gameObject.tag);

                // Add to list of objects hit this swing to prevent multiple damage applications from one swing
                _hitObjectsThisSwing.Add(other.gameObject);
                _lastHitTimePerTarget[other.gameObject] = Time.time; // Update last hit time for this target

                // Optional: Instantiate hit particle effect at other.ClosestPoint(transform.position)
                // Optional: Play hit sound effect

                // Optional: If you want the weapon to stop on first hit or only hit one target per swing:
                // DisableCollider(); // This would make it a single-target-per-swing weapon
            }
            else
            {
                // Debug.Log($"[{gameObject.name}] MeleeWeaponDamage: Collided with '{other.gameObject.name}' but it has no Health component or is already dead.");
            }
        }
    }
}