using UnityEngine;

namespace Core.Events.Combat
{
    public class AttackRecoveryCompleteEventData
    {
        /// <summary>
        /// The transform of the entity that performed the strike.
        /// </summary>
        public Transform AttackerTransform { get; set; }
        
        /// <summary>
        /// The type/name of the weapon used.
        /// </summary>
        public string WeaponType { get; set; }
        
        /// <summary>
        /// Whether the strike successfully hit a target.
        /// </summary>
        public bool WasHit { get; set; }
        
        /// <summary>
        /// Position where the strike occurred.
        /// </summary>
        public Vector3 StrikePosition { get; set; }

        // public Transform AttackerTransform { get; set; }
        // public string WeaponType { get; set; }
        // public Vector3 StrikePosition { get; set; }
        // public Quaternion StrikeRotation { get; set; }
        // public float DamageAmount { get; set; }
        // public bool IsCriticalHit { get; set; }
        // public GameObject[] HitTargets { get; set; } = Array.Empty<GameObject>();
    }
}