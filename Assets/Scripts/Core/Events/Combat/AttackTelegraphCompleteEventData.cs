using UnityEngine;

namespace Core.Events.Combat
{
    /// <summary>
    /// Event data for when an attack telegraph phase completes successfully.
    /// </summary>
    public class AttackTelegraphCompleteEventData
    {
        /// <summary>
        /// The transform of the entity that completed the telegraph.
        /// </summary>
        public Transform AttackerTransform { get; set; }
        
        /// <summary>
        /// The type/name of the weapon being used.
        /// </summary>
        public string WeaponType { get; set; }
    }
}