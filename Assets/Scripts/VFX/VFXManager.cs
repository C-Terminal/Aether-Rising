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
        [SerializeField] private GameObject defaultTrailEffect;
        [SerializeField] private ParticleSystem defaultStrikeEffect;

        

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
            EventManager.AddListener<AttackStrikeEventData>(OnAttackStrike);
        }

        private void OnDisable()
        {
            // Unsubscribe to prevent memory leaks
            EventManager.RemoveListener<AttackTelegraphEventData>(OnAttackTelegraph);
            EventManager.RemoveListener<AttackStrikeEventData>(OnAttackStrike);
        }

        private void OnAttackStrike(AttackStrikeEventData data)
        {
            if (data.AttackerTransform == null)
                return;

            // Find the appropriate VFX for this weapon type
            ParticleSystem effectToSpawn = defaultStrikeEffect;
            GameObject trailEffect = null;
    
            if (!string.IsNullOrEmpty(data.WeaponType) && 
                _vfxLookup.TryGetValue(data.WeaponType, out var mapping))
            {
                if (mapping.impactEffect != null)
                    effectToSpawn = mapping.impactEffect;
            
                trailEffect = mapping.trailEffect;
            }

            // Spawn strike effect
            if (effectToSpawn != null)
            {
                // Position the effect between attacker and target or at target position
                Vector3 effectPosition = data.TargetTransform != null 
                    ? data.TargetTransform.position 
                    : data.AttackerTransform.position + data.AttackerTransform.forward * 1.5f;
            
                var effect = Instantiate(effectToSpawn, effectPosition, data.AttackerTransform.rotation);
        
                // Scale effect based on strike power
                var main = effect.main;
                main.startSizeMultiplier *= data.StrikePower;
        
                // Auto-destroy after completion
                Destroy(effect.gameObject, main.duration + main.startLifetime.constantMax);
            }
    
            // Activate weapon trail if available
            if (trailEffect != null)
            {
                var trail = Instantiate(trailEffect, data.AttackerTransform);
                // Auto-destroy after strike animation
                Destroy(trail, data.StrikeAnimDurationEstimate);
            }
    
            // Play strike sound
            // if (!string.IsNullOrEmpty(data.WeaponType) && _audioLookup.TryGetValue(data.WeaponType, out var audioClip))
            // {
            //     AudioSource.PlayClipAtPoint(audioClip, data.AttackerTransform.position);
            // }
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
                InstantiateParticleSystemWithDuration(
                    effectToSpawn, 
                    data.AttackerTransform.position, 
                    data.AttackerTransform.rotation, 
                    data.Duration, 
                    data.EffectIntensity, 
                    data.TargetTransform
                );
            }
        }

        /// <summary>
        /// Instantiates a particle system with a specific duration and other parameters.
        /// </summary>
        private GameObject InstantiateParticleSystemWithDuration(
            ParticleSystem prefab, 
            Vector3 position, 
            Quaternion rotation, 
            float duration, 
            float intensityMultiplier = 1.0f,
            Transform targetToLookAt = null)
        {
            // First, instantiate the particle system
            var instance = Instantiate(prefab, position, rotation);
            
            // Immediately stop it before it starts playing
            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            
            // Now it's safe to modify the properties
            var main = instance.main;
            
            // Create a new MainModule with the desired settings
            ParticleSystem.MainModule newMain = main;
            newMain.duration = duration;
            newMain.startSizeMultiplier = main.startSizeMultiplier * intensityMultiplier;
            
            // Look at target if provided
            if (targetToLookAt != null)
            {
                instance.transform.LookAt(targetToLookAt);
            }
            
            // Now play the system with our modified settings
            instance.Play();
            
            // Automatically destroy the effect after it completes
            float lifetime = duration + main.startLifetime.constantMax;
            Destroy(instance.gameObject, lifetime);
            
            return instance.gameObject;
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