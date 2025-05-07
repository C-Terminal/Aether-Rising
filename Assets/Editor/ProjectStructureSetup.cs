using System.IO;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class ProjectStructureSetup
    {
        private static readonly string[] rootFolders = new string[]
        {
            "Assets/Animations",
            "Assets/Art",
            "Assets/Audio",
            "Assets/Editor",
            "Assets/Materials",
            "Assets/Prefabs",
            "Assets/Resources",
            "Assets/Scenes",
            "Assets/Scripts",
            "Assets/ThirdPartyLibs"
        };

        private static readonly string[] scriptSubFolders = new string[]
        {
            "Core/Utilities",
            "Core/Events",
            "Core/Input",
            "Core/StateMachine",
            "Core/Interfaces",

            "Game/SceneManagement",
            "Game",

            "Characters/Player",
            "Characters/NPC",
            "Characters/Common",

            "AI/FSM",
            "AI/BehaviorTree",
            "AI/Perception",

            "Combat/Weapons",
            "Combat/DamageSystem",
            "Combat/Abilities",
            "Combat/Effects",

            "Animation/AnimControllers",
            "Animation/IK",
            "Animation/BlendSystems",

            "UI/Menus",
            "UI/HUD",
            "UI/Dialogue",
            "UI/Notifications",

            "Audio/AudioEvents",

            "Environment/Triggers",
            "Environment/Doors",
            "Environment/Props",

            "Economy/Inventory",
            "Economy/Items",
            "Economy/Crafting",
            "Economy/ShopSystem",

            "Quests/Objectives",
            "Quests/Rewards",

            "DebugTools/Cheats",
            "DebugTools/ConsoleCommands"
        };

        [MenuItem("Tools/Setup Full Project Structure")]
        public static void CreateFullStructure()
        {
            CreateBaseFolders();
            CreateScriptSubFolders();
            AssetDatabase.Refresh();
            Debug.Log("✅ Full project structure setup complete!");
        }

        private static void CreateBaseFolders()
        {
            foreach (var folder in rootFolders)
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    Debug.Log($"📁 Created folder: {folder}");
                }
                else
                {
                    Debug.Log($"📁 Folder already exists: {folder}");
                }

                CreateReadme(folder);
            }
        }

        private static void CreateScriptSubFolders()
        {
            string scriptsRoot = "Assets/Scripts/";

            foreach (var subFolder in scriptSubFolders)
            {
                string fullPath = Path.Combine(scriptsRoot, subFolder);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    Debug.Log($"📁 Created script subfolder: {fullPath}");
                }

                CreateReadme(fullPath);
            }
        }

        private static void CreateReadme(string folderPath)
        {
            string folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
            string readmeFileName = $"README.md";
            string readmeFilePath = Path.Combine(folderPath, readmeFileName);

            if (!File.Exists(readmeFilePath))
            {
                string content = $"# {folderName} Folder\n\nThis folder contains the {folderName.ToLower()} of the project.";
                File.WriteAllText(readmeFilePath, content);
                Debug.Log($"📝 Created README: {readmeFilePath}");
            }
        }
    }
}
