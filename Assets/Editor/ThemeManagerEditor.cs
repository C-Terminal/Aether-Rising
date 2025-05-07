#if UNITY_EDITOR

using UI.Themes;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(ThemeManager))]
    public class ThemeManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Apply Theme"))
            {
                ((ThemeManager)target).ApplyTheme(((ThemeManager)target).currentTheme);
            }
        }
    }
}
#endif