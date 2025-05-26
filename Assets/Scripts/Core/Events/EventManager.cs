using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Events
{
    /// <summary>
    /// A powerful, type-safe event management system that allows for decoupled communication
    /// between different parts of your game.
    /// </summary>
    public static class EventManager
    {
        // Dictionary to store event handlers by event type
        private static readonly Dictionary<Type, List<Delegate>> _eventHandlers = new();
        
        // Logging control
        private static bool _loggingEnabled = false;
        
        /// <summary>
        /// Enable or disable event logging for debugging purposes.
        /// </summary>
        public static void SetLoggingEnabled(bool enabled)
        {
            _loggingEnabled = enabled;
        }
        
        /// <summary>
        /// Subscribe to an event of type T.
        /// </summary>
        /// <typeparam name="T">The event data type</typeparam>
        /// <param name="handler">The event handler function</param>
        public static void AddListener<T>(Action<T> handler)
        {
            var eventType = typeof(T);
            
            if (!_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Delegate>();
                _eventHandlers[eventType] = handlers;
            }
            
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
                
                if (_loggingEnabled)
                    Debug.Log($"[EventManager] Added listener for event type {eventType.Name}. Total listeners: {handlers.Count}");
            }
            else if (_loggingEnabled)
            {
                Debug.LogWarning($"[EventManager] Attempted to add duplicate listener for event type {eventType.Name}");
            }
        }
        
        /// <summary>
        /// Unsubscribe from an event of type T.
        /// </summary>
        /// <typeparam name="T">The event data type</typeparam>
        /// <param name="handler">The event handler function to remove</param>
        public static void RemoveListener<T>(Action<T> handler)
        {
            var eventType = typeof(T);
            
            if (_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                
                if (handlers.Count == 0)
                    _eventHandlers.Remove(eventType);
                    
                if (_loggingEnabled)
                    Debug.Log($"[EventManager] Removed listener for event type {eventType.Name}. Remaining listeners: {handlers.Count}");
            }
            else if (_loggingEnabled)
            {
                Debug.LogWarning($"[EventManager] Attempted to remove listener for unregistered event type {eventType.Name}");
            }
        }
        
        /// <summary>
        /// Trigger an event of type T with the provided event data.
        /// </summary>
        /// <typeparam name="T">The event data type</typeparam>
        /// <param name="eventData">The event data to pass to handlers</param>
        public static void TriggerEvent<T>(T eventData)
        {
            var eventType = typeof(T);
            
            if (_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                // Create a copy of the handlers list to allow for handlers to unsubscribe during event processing
                var handlersCopy = new List<Delegate>(handlers);
                
                if (_loggingEnabled)
                    Debug.Log($"[EventManager] Triggering event {eventType.Name} with {handlersCopy.Count} listeners");
                
                foreach (var handler in handlersCopy)
                {
                    try
                    {
                        ((Action<T>)handler).Invoke(eventData);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[EventManager] Error invoking handler for event {eventType.Name}: {e.Message}\n{e.StackTrace}");
                    }
                }
            }
            else if (_loggingEnabled)
            {
                Debug.Log($"[EventManager] Triggered event {eventType.Name} but no listeners were registered");
            }
        }
        
        /// <summary>
        /// Check if there are any listeners for a specific event type.
        /// </summary>
        /// <typeparam name="T">The event data type</typeparam>
        /// <returns>True if there are listeners, false otherwise</returns>
        public static bool HasListeners<T>()
        {
            return _eventHandlers.TryGetValue(typeof(T), out var handlers) && handlers.Count > 0;
        }
        
        /// <summary>
        /// Clear all event handlers. Useful when changing scenes or during application shutdown.
        /// </summary>
        public static void ClearAllListeners()
        {
            _eventHandlers.Clear();
            
            if (_loggingEnabled)
                Debug.Log("[EventManager] All event listeners cleared");
        }
    }
}