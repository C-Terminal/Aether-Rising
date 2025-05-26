using System.Collections.Generic;
using Core.Events;
using Core.Events.Combat;
using UnityEngine;

namespace VFX
{
    /// <summary>
    /// Manages visual effects throughout the game, responding to game events.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        // Add singleton pattern
        private static VFXManager _instance;
        public static VFXManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<VFXManager>();
                    if (_instance == null)
                    {
                        Debug.LogWarning("No VFXManager found in scene. Some visual effects may not play.");
                    }
                }
                return _instance;
            }
        }
        
        [System.Serializable]
        public class WeaponVFXMapping
        {
            public string weaponType;
            public ParticleSystem telegraphEffect;
            public ParticleSystem impactEffect;
            public GameObject trailEffect;
        }

        [Header("Weapon VFX")]
        [SerializeField] private List<WeaponVFXMapping> weaponVFXMappings = new List<WeaponVFXMapping>();
        
        [Header("Common VFX")]
        [SerializeField] private ParticleSystem defaultTelegraphEffect;
        [SerializeField] private ParticleSystem defaultImpactEffect;

        // Cache for quick lookup
        private Dictionary<string, WeaponVFXMapping> _vfxLookup = new Dictionary<string, WeaponVFXMapping>();

        private void Awake()
        {
            // Build lookup dictionary for faster access
            foreach (var mapping in weaponVFXMappings)
            {
                if (!string.IsNullOrEmpty(mapping.weaponType))
                {
                    _vfxLookup[mapping.weaponType] = mapping;
                }
            }
        }

        private void OnEnable()
        {
            // Subscribe to relevant events
            EventManager.AddListener<AttackTelegraphEventData>(OnAttackTelegraph);
            // You can add more event subscriptions here as needed
        }

        private void OnDisable()
        {
            // Unsubscribe to prevent memory leaks
            EventManager.RemoveListener<AttackTelegraphEventData>(OnAttackTelegraph);
            // Unsubscribe from other events
        }

        private void OnAttackTelegraph(AttackTelegraphEventData data)
        {
            if (data.AttackerTransform == null)
                return;

            // Find the appropriate VFX for this weapon type
            ParticleSystem effectToSpawn = defaultTelegraphEffect;
            
            if (!string.IsNullOrEmpty(data.WeaponType) && 
                _vfxLookup.TryGetValue(data.WeaponType, out var mapping) && 
                mapping.telegraphEffect != null)
            {
                effectToSpawn = mapping.telegraphEffect;
            }

            if (effectToSpawn != null)
            {
                // Spawn the effect at the attacker's position
                var effect = Instantiate(effectToSpawn, data.AttackerTransform.position, data.AttackerTransform.rotation);
                
                // Scale duration based on telegraph time
                var main = effect.main;
                main.duration = data.Duration;
                
                // Scale size/intensity based on effect intensity
                main.startSizeMultiplier *= data.EffectIntensity;
                
                // Optionally point toward target if available
                if (data.TargetTransform != null)
                {
                    effect.transform.LookAt(data.TargetTransform);
                }
                
                // Automatically destroy the effect after it completes
                Destroy(effect.gameObject, data.Duration + main.startLifetime.constantMax);
            }
        }

        // Public methods for direct VFX spawning (for cases where you don't want to use events)
        public void SpawnTelegraphEffect(string weaponType, Vector3 position, Quaternion rotation, float duration = 1.0f)
        {
            ParticleSystem effectToSpawn = defaultTelegraphEffect;
            
            if (!string.IsNullOrEmpty(weaponType) && 
                _vfxLookup.TryGetValue(weaponType, out var mapping) && 
                mapping.telegraphEffect != null)
            {
                effectToSpawn = mapping.telegraphEffect;
            }

            if (effectToSpawn != null)
            {
                var effect = Instantiate(effectToSpawn, position, rotation);
                var main = effect.main;
                main.duration = duration;
                Destroy(effect.gameObject, duration + main.startLifetime.constantMax);
            }
        }

        // Add more methods for other types of effects as needed
    }
}