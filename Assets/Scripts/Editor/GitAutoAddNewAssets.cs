using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 把 Unity 新导入资产和对应 meta 一起暂存，避免只提交资产或只提交 GUID 映射。
/// </summary>
public static class GitAutoAddNewAssets
{
    private const string MenuPath = "Tools/Git/Auto Add New Assets";
    private const string PrefKey = "SS3CDemo.GitAutoAddNewAssets.Enabled";

    // ============ 编辑器偏好与菜单 ============

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, false);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [InitializeOnLoadMethod]
    private static void InitMenuCheck()
    {
        Menu.SetChecked(MenuPath, Enabled);
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        Menu.SetChecked(MenuPath, Enabled);
        Debug.Log($"[GitAutoAdd] {(Enabled ? "Enabled" : "Disabled")}");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    // ============ 资产收集 ============

    /// <summary>
    /// 只处理 importedAssets，删除和移动仍交给开发者确认，避免工具隐式扩大 Git 变更范围。
    /// </summary>
    internal static void HandleImportedAssets(string[] importedAssets)
    {
        if (!Enabled || importedAssets == null || importedAssets.Length == 0)
            return;

        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!Directory.Exists(Path.Combine(repoRoot, ".git")))
            return;

        var toAdd = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string assetPath in importedAssets)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) continue;
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal)) continue;

            toAdd.Add(assetPath);

            if (!assetPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                string metaPath = assetPath + ".meta";
                if (File.Exists(Path.Combine(repoRoot, metaPath)))
                    toAdd.Add(metaPath);
            }
        }

        if (toAdd.Count == 0)
            return;

        RunGitAdd(repoRoot, toAdd);
    }

    // ============ Git 调用 ============

    /// <summary>
    /// 使用参数列表限定到已筛选路径，避免在编辑器回调里暂存整个工作区。
    /// </summary>
    private static void RunGitAdd(string repoRoot, IEnumerable<string> paths)
    {
        string args = "add --";
        foreach (string p in paths)
            args += $" \"{p.Replace("\\", "/")}\"";

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null) return;
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                string err = proc.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(err))
                    Debug.LogWarning($"[GitAutoAdd] git add failed: {err}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GitAutoAdd] failed to run git: {ex.Message}");
        }
    }
}

/// <summary>
/// 保持 AssetPostprocessor 只做桥接，让 Git 策略能够脱离 Unity 回调单独维护。
/// </summary>
public sealed class GitAutoAddAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        GitAutoAddNewAssets.HandleImportedAssets(importedAssets);
    }
}
