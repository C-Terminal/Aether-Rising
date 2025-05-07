  using System;
using TMPro;
using UI.Themes;
using UnityEngine;

namespace UI.Elements
{
    [RequireComponent(typeof(TMP_Text))]
    public class ThemedText : UIThemedElement
    {
        public bool isHeader = true;

        private TMP_Text _textComponent;

        private void Awake()
        {
            var components = GetComponents<Component>();
            foreach (var component in components)
                Debug.Log($"Component: {component.GetType().Name}, Name: {component.name}");
            _textComponent = GetComponent<TMP_Text>();
            if (_textComponent == null)
                Debug.LogError($"[ThemedText] TMP_Text component missing on {gameObject.name}");
        }

        public override void ApplyTheme(UITheme theme)
        {
            try
            {
                if (theme == null)
                    throw new ArgumentNullException(nameof(theme), "Theme cannot be null");

                _textComponent.font = isHeader ? theme.headerFont : theme.bodyFont;
                _textComponent.color = isHeader ? theme.accentColor : theme.secondaryColor;
            }
            catch (ArgumentNullException ex)
            {
                Debug.LogError($"[ThemedText] ArgumentNullException: {ex.ParamName} - {ex.Message}");
            }
            catch (NullReferenceException ex)
            {
                Debug.LogError("[ThemedText] NullReferenceException: Expected type UITheme, but received null.");
            }
            
        }
    }
}