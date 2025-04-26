using UnityEditor;
using UnityEngine;
using System.IO;

public class ProjectStructureSetup
{
    private static readonly string[] folders = new string[]
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

    [MenuItem("Tools/Setup Project Structure")]
    public static void CreateFolders()
    {
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                string folderName = Path.GetFileName(folder);
                string readmeFileName = $"{folderName}ReadMe.md";
                string readmeFilePath = Path.Combine(folder, readmeFileName);
                
                if (!File.Exists(readmeFilePath))
                {
                    File.WriteAllText(readmeFilePath, $"# {folderName} Folder\n\nThis folder contains the {folderName.ToLower()} of the project.");
                    Debug.Log($"Created README: {readmeFilePath}");
                }
                else
                {
                    Debug.Log($"README already exists: {readmeFilePath}");
                }
                Debug.Log($"Created folder: {folder}");
            }
            else
            {
                Debug.Log($"Folder already exists: {folder}");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("✅ Project structure setup complete!");
    }
}
