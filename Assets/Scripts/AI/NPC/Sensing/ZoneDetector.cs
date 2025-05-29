using System;
using UnityEngine;

namespace AI.NPC.Sensing
{
    [RequireComponent(typeof(Collider))]
    public class ZoneDetector : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";

        public event Action<Transform> OnPlayerEnter;
        public event Action OnPlayerExit;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(playerTag))
                OnPlayerEnter?.Invoke(other.transform);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(playerTag))
                OnPlayerExit?.Invoke();
        }
    }
}