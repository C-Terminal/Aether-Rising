using UnityEngine;

namespace Core.Events.Combat
{
    /// <summary>
    /// Event data for attack telegraph events.
    /// </summary>
    public class AttackTelegraphEventData
    {
        /// <summary>
        /// The transform of the entity performing the attack.
        /// </summary>
        public Transform AttackerTransform { get; set; }
        
        /// <summary>
        /// The type/name of the weapon being used.
        /// </summary>
        public string WeaponType { get; set; }
        
        /// <summary>
        /// Duration of the telegraph phase in seconds.
        /// </summary>
        public float Duration { get; set; }
        
        /// <summary>
        /// Optional target of the attack.
        /// </summary>
        public Transform TargetTransform { get; set; }
        
        /// <summary>
        /// Optional intensity factor for visual effects (0-1).
        /// </summary>
        public float EffectIntensity { get; set; } = 1.0f;
    }
}