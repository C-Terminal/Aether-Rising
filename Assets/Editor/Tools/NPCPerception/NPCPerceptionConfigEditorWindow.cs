using System.Linq;
using AI.NPC.Sensing;
using UnityEditor;
using UnityEngine;

namespace Editor.Tools.NPCPerception
{
    public class NPCPerceptionConfigEditorWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private NPCPerceptionConfig[] _configs;

        [MenuItem("Tools/AI/NPC Perception Config Viewer")]
        public static void Open()
        {
            var window = GetWindow<NPCPerceptionConfigEditorWindow>("NPC Perception Configs");
            window.Show();
        }

        private void OnEnable()
        {
            RefreshConfigs();
        }

        private void OnGUI()
        {
            if (_configs == null || _configs.Length == 0)
            {
                EditorGUILayout.HelpBox("No NPCPerceptionConfig assets found.", MessageType.Info);
                if (GUILayout.Button("Refresh")) RefreshConfigs();
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var config in _configs)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(config.name, EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                EditorGUI.BeginChangeCheck();

                config.fieldOfView = EditorGUILayout.Slider("Field of View", config.fieldOfView, 30, 360);
                config.actionRange = EditorGUILayout.FloatField("Attack Distance", config.actionRange);
                config.rotationSpeed = EditorGUILayout.FloatField("Rotation Speed", config.rotationSpeed);
                config.visibilityCheckInterval = EditorGUILayout.FloatField("Vision Interval", config.visibilityCheckInterval);

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(config);
                }

                EditorGUILayout.Space(3);
                if (GUILayout.Button("Ping in Project"))
                {
                    EditorGUIUtility.PingObject(config);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Refresh Configs"))
                RefreshConfigs();
        }

        private void RefreshConfigs()
        {
            _configs = AssetDatabase.FindAssets("t:NPCPerceptionConfig")
                .Select(guid => AssetDatabase.LoadAssetAtPath<NPCPerceptionConfig>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
        }
    }
}