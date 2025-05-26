using UnityEngine;

namespace Core.Events.Combat
{
    public class AttackTelegraphAbortEventData
    {
        /// <summary>
        ///  The transform of the attacker.
        /// </summary>
        public Transform AttackerTransform;

        /// <summary>
        /// The reason for the abort.
        /// </summary>
        public string Reason;
    }
}