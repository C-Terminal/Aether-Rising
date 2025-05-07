using System;
using UnityEngine;

namespace UI.Themes
{
    public class ThemeManager : MonoBehaviour
    {
        public static ThemeManager Instance { get; private set; }

        public UITheme currentTheme;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            ApplyTheme(currentTheme);
        }

        public void ApplyTheme(UITheme uiTheme)
        {
            currentTheme = uiTheme;
            
            // UIThemedElement[] elements = FindObjectsByType<UIThemedElement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            UIThemedElement[] elements = FindObjectsOfType<UIThemedElement>();
            Debug.Log("Found " + elements.Length + " elements");
            foreach (UIThemedElement element in elements)
            {
                Debug.Log("Applying theme to: " + element.name + " - " + element.GetType().Name);
            }
            foreach (UIThemedElement element in elements)
            {
                element.ApplyTheme(uiTheme);
            }
        }
    }
}