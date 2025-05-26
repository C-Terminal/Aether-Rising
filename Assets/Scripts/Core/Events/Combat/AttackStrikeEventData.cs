using UnityEngine;

namespace Core.Events.Combat
{
    /// <summary>
    /// Event data for attack strike events.
    /// </summary>
    public class AttackStrikeEventData
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
        /// Target of the attack.
        /// </summary>
        public Transform TargetTransform { get; set; }
        
        /// <summary>
        /// Power/intensity of the strike (0-1).
        /// </summary>
        public float StrikePower { get; set; } = 1.0f;
        
        /// <summary>
        /// Damage type of the strike.
        /// </summary>
        public string DamageType { get; set; }
        
        /// <summary>
        /// Estimate of the duration of the strike animation.
        /// </summary>
        public float StrikeAnimDurationEstimate = 1.0f;
        
        /// <summary>
        /// Audio clip associated with the strike.
        /// </summary>
        public AudioClip AudioClip { get; set; }
    }
}