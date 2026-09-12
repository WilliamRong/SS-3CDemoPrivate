using System.IO;
using System.Security;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps Unity-generated C# projects valid when the project path contains XML characters.
/// </summary>
public sealed class ProjectFileXmlSanitizer : AssetPostprocessor
{
    private static string OnGeneratedCSProject(string path, string content)
    {
        DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
        if (projectDirectory == null)
        {
            return content;
        }

        string projectRoot = projectDirectory.FullName;
        string escapedProjectRoot = SecurityElement.Escape(projectRoot);
        if (projectRoot == escapedProjectRoot)
        {
            return content;
        }

        content = content.Replace(projectRoot, escapedProjectRoot);

        string forwardSlashRoot = projectRoot.Replace('\\', '/');
        string escapedForwardSlashRoot = SecurityElement.Escape(forwardSlashRoot);
        return content.Replace(forwardSlashRoot, escapedForwardSlashRoot);
    }
}
