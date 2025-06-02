using System;
using System.Collections;
using AI.NPC.Sensing.Vision;
using UnityEngine;

namespace AI.NPC.Sensing
{
    /// <summary>
    /// Vision sensor that periodically checks visibility using a configurable evaluator function.
    /// Supports manual checks, state tracking, and debugging capabilities.
    /// </summary>
    public class VisionSensor : MonoBehaviour, IVisionSensor
    {
        [Header("Configuration")]
        [SerializeField] private NPCPerceptionConfig config;
        [SerializeField] private bool enableDebugLogs = false;
        
        [Header("Debug Info (Runtime)")]
        [SerializeField, ReadOnly] private bool _isChecking;
        [SerializeField, ReadOnly] private bool _lastSeen;
        
        // Events
        public event Action<bool> OnVisibilityChanged;
        
        // Visibility evaluation delegate
        public Func<bool> VisibilityEvaluator { get; set; }

        // State tracking
        private Coroutine _checkRoutine;
        private bool _hasStarted;

        // Properties for external access
        public bool IsChecking => _checkRoutine != null;
        public bool LastVisibilityState => _lastSeen;
        public float CheckInterval 
        { 
            get => config.visibilityCheckInterval; 
            set => config.visibilityCheckInterval = Mathf.Max(0.01f, value); 
        }

        #region Public Interface

        /// <summary>
        /// Starts the periodic visibility checking.
        /// </summary>
        public void StartChecking()
        {
            if (_checkRoutine == null)
            {
                _checkRoutine = StartCoroutine(CheckLoop());
                _isChecking = true;
                _hasStarted = true;
                Log("Started visibility checking");
            }
            else
            {
                LogWarning("Attempted to start checking when already running");
            }
        }

        /// <summary>
        /// Stops the periodic visibility checking.
        /// </summary>
        public void StopChecking()
        {
            if (_checkRoutine != null)
            {
                StopCoroutine(_checkRoutine);
                _checkRoutine = null;
                _isChecking = false;
                Log("Stopped visibility checking");
            }
        }

        /// <summary>
        /// Forces an immediate visibility check outside of the normal interval.
        /// Useful for debugging or when immediate response is needed.
        /// </summary>
        public void ForceCheck()
        {
            if (VisibilityEvaluator == null)
            {
                LogWarning("Cannot force check - VisibilityEvaluator is null");
                return;
            }

            bool visible = VisibilityEvaluator.Invoke();
            Log($"Force check result: {visible} (was: {_lastSeen})");

            if (visible != _lastSeen)
            {
                _lastSeen = visible;
                OnVisibilityChanged?.Invoke(visible);
                Log($"Visibility changed to: {visible}");
            }
        }

        /// <summary>
        /// Resets the sensor state without triggering events.
        /// Useful when repositioning or reinitializing the sensor.
        /// </summary>
        public void ResetState()
        {
            StopChecking();
            _lastSeen = false;
            _hasStarted = false;
            Log("Sensor state reset");
        }

        /// <summary>
        /// Sets the visibility state manually without evaluation.
        /// Useful for external systems that want to override sensor behavior.
        /// </summary>
        public void SetVisibilityState(bool visible, bool triggerEvent = true)
        {
            if (_lastSeen != visible)
            {
                _lastSeen = visible;
                Log($"Visibility manually set to: {visible}");
                
                if (triggerEvent)
                {
                    OnVisibilityChanged?.Invoke(visible);
                }
            }
        }

        /// <summary>
        /// Gets the current visibility state without triggering a check.
        /// </summary>
        public bool GetCurrentVisibilityState()
        {
            return _lastSeen;
        }

        /// <summary>
        /// Checks if the sensor has been started at least once.
        /// </summary>
        public bool HasBeenStarted()
        {
            return _hasStarted;
        }

        #endregion

        #region Core Logic

        private IEnumerator CheckLoop()
        {
            Log("Visibility check loop started");
            
            while (true)
            {
                PerformVisibilityCheck();
                yield return new WaitForSeconds(config.visibilityCheckInterval);
            }
        }

        private void PerformVisibilityCheck()
        {
            if (VisibilityEvaluator == null)
            {
                // If no evaluator is set, assume not visible
                if (_lastSeen)
                {
                    _lastSeen = false;
                    OnVisibilityChanged?.Invoke(false);
                    LogWarning("VisibilityEvaluator is null - defaulting to not visible");
                }
                return;
            }

            bool visible = VisibilityEvaluator.Invoke();

            if (visible != _lastSeen)
            {
                _lastSeen = visible;
                OnVisibilityChanged?.Invoke(visible);
                Log($"Visibility changed: {(!visible ? "visible" : "hidden")} -> {(visible ? "visible" : "hidden")}");
            }
        }

        #endregion

        #region Unity Lifecycle

        private void OnDisable()
        {
            StopChecking();
        }

        private void OnDestroy()
        {
            StopChecking();
            VisibilityEvaluator = null;
        }

        #endregion

        #region Validation & Utilities

        private void OnValidate()
        {
            // Ensure check interval is reasonable
            if (config.visibilityCheckInterval <= 0)
            {
                config.visibilityCheckInterval = 0.1f;
                Debug.LogWarning($"[{gameObject.name}] VisionSensor checkInterval must be positive. Reset to 0.1s");
            }
        }

        private void Log(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[{gameObject.name} - VisionSensor] {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[{gameObject.name} - VisionSensor] {message}");
            }
        }

        #endregion

        #region Debug & Testing

        /// <summary>
        /// Context menu method for testing in the editor.
        /// </summary>
        [ContextMenu("Test Force Check")]
        private void TestForceCheck()
        {
            if (Application.isPlaying)
            {
                ForceCheck();
            }
            else
            {
                Debug.Log("Force check can only be used during play mode");
            }
        }

        /// <summary>
        /// Context menu method for testing state reset.
        /// </summary>
        [ContextMenu("Test Reset State")]
        private void TestResetState()
        {
            if (Application.isPlaying)
            {
                ResetState();
            }
            else
            {
                Debug.Log("Reset state can only be used during play mode");
            }
        }

        /// <summary>
        /// Context menu method for starting checks in editor.
        /// </summary>
        [ContextMenu("Start Checking")]
        private void TestStartChecking()
        {
            if (Application.isPlaying)
            {
                StartChecking();
            }
            else
            {
                Debug.Log("Start checking can only be used during play mode");
            }
        }

        /// <summary>
        /// Context menu method for stopping checks in editor.
        /// </summary>
        [ContextMenu("Stop Checking")]
        private void TestStopChecking()
        {
            if (Application.isPlaying)
            {
                StopChecking();
            }
            else
            {
                Debug.Log("Stop checking can only be used during play mode");
            }
        }

        #endregion

        public bool CanSeePlayer(Transform player)
        {
            return LastVisibilityState;
        }
    }

    #region Custom Attributes

    /// <summary>
    /// Attribute to make fields read-only in the inspector.
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute
    {
        public ReadOnlyAttribute() { }
    }

    #if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
    #endif

    #endregion
}