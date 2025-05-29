using System;
using System.Collections;
using UnityEngine;

namespace AI.NPC.Sensing
{
    public class VisionSensor : MonoBehaviour
    {
        [SerializeField] private float checkInterval = 0.2f;
        private Coroutine _checkRoutine;

        public event Action<bool> OnVisibilityChanged;
        public Func<bool> VisibilityEvaluator;

        private bool _lastSeen;

        public void StartChecking() => _checkRoutine ??= StartCoroutine(CheckLoop());
        public void StopChecking()
        {
            if (_checkRoutine != null)
            {
                StopCoroutine(_checkRoutine);
                _checkRoutine = null;
            }
        }

        private IEnumerator CheckLoop()
        {
            while (true)
            {
                bool visible = VisibilityEvaluator?.Invoke() ?? false;

                if (visible != _lastSeen)
                {
                    _lastSeen = visible;
                    OnVisibilityChanged?.Invoke(visible);
                }

                yield return new WaitForSeconds(checkInterval);
            }
        }

        private void OnDisable() => StopChecking();
    }
}