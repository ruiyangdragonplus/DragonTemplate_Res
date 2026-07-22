using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_6000_3_OR_NEWER
using UnityEditor.Toolbars; // Unity 6000.3 官方主工具栏 API（旧反射注入的 m_Root 已成废树，见下）
#endif
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public static class UpdateScripts
{
    private const string Platform = "WindowsMacLinux";
    private const string CopyFromPath            = "../Obfuscated";
    private const string CopyDllFromPath         = CopyFromPath + "/Dll/" + Platform;
    private const string CopyDllToPath           = "Library/ScriptAssemblies";
    private const string LocalVersionPath        = "Library/version";
    private const string ProgressBarTitle        = "更新代码库...";
    private const double AutoCheckUpdateTimeSpan = 30.0;

    private static bool AutoUpdate
    {
        get => EditorPrefs.GetBool("AUTO_UPDATE_SCRIPTS", true);
        set => EditorPrefs.SetBool("AUTO_UPDATE_SCRIPTS", value);
    }

    private static readonly Regex s_PercentRegex = new Regex(@"(\d+)(.?)(\d*)%");

#if !UNITY_6000_3_OR_NEWER
    private static Type             s_toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
    private static ScriptableObject s_currentToolbar;
#endif

    private static DateTime s_NextAutoCheckUpdateTime;
    private static Process  s_UpdateProcess;
    private static float    s_UpdateProgress;
    private static string   s_UpdateLogger;
    private static bool     s_IsAnythingNeedToUpdate;
    private static bool     s_IsUpdating;

    [DidReloadScripts]
    private static void RegisterDownloadButton()
    {
        EditorApplication.update -= AutoCheckVersion;
        EditorApplication.update += AutoCheckVersion;

#if !UNITY_6000_3_OR_NEWER
        // 旧版：反射注入 Toolbar.m_Root（6000.3 起该树已分离不上屏，改走官方 API，见下方 MainToolbarElement）
        EditorApplication.update -= CreateToolbarButton;
        EditorApplication.update += CreateToolbarButton;
#endif

        s_NextAutoCheckUpdateTime = DateTime.Now;
    }

    #region Toolbar

#if UNITY_6000_3_OR_NEWER
    // ═══ Unity 6000.3+：官方 MainToolbar API（[MainToolbarElement] 注册，Overlay 化主工具栏）═══
    private const string kToolbarElementPath = "Dragon/更新代码";

    [MainToolbarElement(kToolbarElementPath, defaultDockPosition = MainToolbarDockPosition.Right)]
    public static MainToolbarElement CreateUpdateDropdown()
    {
        string text = s_IsAnythingNeedToUpdate ? "更新代码 ●" : "更新代码";
        string tooltip = s_IsAnythingNeedToUpdate ? "代码库有更新，点击操作" : "美术工程代码库更新";
        var content = new MainToolbarContent(text, null, tooltip);
        return new MainToolbarDropdown(content, ShowUpdateMenu);
    }

    private static void ShowUpdateMenu(Rect buttonRect)
    {
        var menu = new GenericMenu();
        bool busy = s_IsUpdating || EditorApplication.isPlayingOrWillChangePlaymode;
        if (busy)
            menu.AddDisabledItem(new GUIContent("立即更新"));
        else
            menu.AddItem(new GUIContent("立即更新"), false, ExecuteUpdate);
        menu.AddItem(new GUIContent("自动更新"), AutoUpdate, () => { AutoUpdate = !AutoUpdate; });
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("还原引擎"), false, () =>
        {
            if (EditorUtility.DisplayDialog("还原引擎", "还原后美术库将无法运行，确定要还原吗？", "确定", "好吧，那算了"))
                RevertFromEncryptVersionUnity();
        });
        menu.DropDown(buttonRect);
    }

    /// <summary>红点状态变化时刷新工具栏元素。</summary>
    private static void RefreshToolbarElement()
    {
        try { MainToolbar.Refresh(kToolbarElementPath); }
        catch { /* MainToolbar 可能尚未就绪 */ }
    }
#else
    private static void RefreshToolbarElement() { }

    private static void CreateToolbarButton()
    {
        // Relying on the fact that toolbar is ScriptableObject and gets deleted when layout changes
        if (s_currentToolbar == null)
        {
            // Find toolbar
            var toolbars = Resources.FindObjectsOfTypeAll(s_toolbarType);
            s_currentToolbar = toolbars.Length > 0 ? (ScriptableObject) toolbars[0] : null;
            if (s_currentToolbar != null)
            {
                var root    = s_currentToolbar.GetType().GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var rawRoot = root.GetValue(s_currentToolbar);
                var mRoot   = rawRoot as VisualElement;
                RegisterCallback("ToolbarZoneRightAlign");
                EditorApplication.update -= CreateToolbarButton;

                void RegisterCallback(string root)
                {
                    var toolbarZone = mRoot.Q(root);

                    var parent = new VisualElement()
                    {
                        style =
                        {
                            flexGrow      = 1,
                            flexDirection = FlexDirection.Row,
                        }
                    };
                    var container = new IMGUIContainer();
                    container.onGUIHandler += DrawDownloadButton;
                    parent.Add(container);
                    toolbarZone.Add(parent);
                }
            }
        }
    }

    private static void DrawDownloadButton()
    {
        var guiEnable = GUI.enabled;
        GUI.enabled = s_IsUpdating == false && EditorApplication.isPlaying == false && EditorApplication.isPlayingOrWillChangePlaymode == false;

        if (GUILayout.Button("更新代码", GUILayout.Width(90f)))
        {
            if (Event.current.button == 1)
            {
                GenericMenu genericMenu = new GenericMenu();
                genericMenu.AddItem(new GUIContent("自动更新"), AutoUpdate, () => { AutoUpdate = !AutoUpdate; });
                genericMenu.AddItem(new GUIContent("还原引擎"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("还原引擎", "还原后美术库将无法运行，确定要还原吗？", "确定", "好吧，那算了"))
                    {
                        RevertFromEncryptVersionUnity();
                    }
                });

                genericMenu.ShowAsContext();
            }
            else
            {
                ExecuteUpdate();
            }
        }

        if (s_IsAnythingNeedToUpdate)
        {
            Rect redRect = GUILayoutUtility.GetLastRect();
            redRect = new Rect(redRect.x, redRect.y, 20f, 10f);
            Color guiColor = GUI.color;
            GUI.color = Color.red;
            GUI.Label(redRect, "●");
            GUI.color = guiColor;
        }

        GUI.enabled = guiEnable;
    }
#endif // !UNITY_6000_3_OR_NEWER

    /// <summary>
    /// 执行更新代码操作（封装后的公共方法）
    /// </summary>
    public static void ExecuteUpdate()
    {
        EditorApplication.update += NextUpdate;

        void NextUpdate()
        {
            EditorApplication.update -= NextUpdate;
            Update();
        }
    }

    [MenuItem("Tools/更新代码")]
    private static void MenuItemUpdate()
    {
        ExecuteUpdate();
    }

    #endregion

    #region Update

    private static void AutoCheckVersion()
    {
        if (AutoUpdate == false                  ||
            s_IsAnythingNeedToUpdate             ||
            s_UpdateProcess != null              ||
            EditorApplication.isCompiling        ||
            EditorApplication.isFocused == false ||
            DateTime.Now                < s_NextAutoCheckUpdateTime)
        {
            return;
        }

        Debug.Log("开始自动检查代码更新...");
        s_NextAutoCheckUpdateTime =  DateTime.Now.AddSeconds(AutoCheckUpdateTimeSpan);
        EditorApplication.update  += CheckVersionBackground;
    }

    private static void CheckVersionBackground()
    {
        if (s_UpdateProcess == null)
        {
            s_NextAutoCheckUpdateTime = DateTime.Now.AddSeconds(AutoCheckUpdateTimeSpan);
            s_UpdateProcess           = StartUpdateProgress((log) => s_UpdateLogger += $"{log}\r\n");
        }

        if (s_UpdateProcess.HasExited == false)
        {
            return;
        }

        s_UpdateProcess.Dispose();
        EditorApplication.update -= CheckVersionBackground;
        s_IsAnythingNeedToUpdate =  DoCompareVersion();
        RefreshToolbarElement();
        Debug.Log($"代码自动检查更新完毕！\r\n{s_UpdateLogger}");
    }

    private static void CheckVersion()
    {
        using (s_UpdateProcess = StartUpdateProgress((log) => s_UpdateLogger = log))
        {
            s_NextAutoCheckUpdateTime = DateTime.Now.AddSeconds(AutoCheckUpdateTimeSpan);
            while (s_UpdateProcess.HasExited == false)
            {
                if (EditorUtility.DisplayCancelableProgressBar(ProgressBarTitle, $"检查更新...{s_UpdateLogger}", s_UpdateProgress))
                {
                    return;
                }
            }
        }

        if (Directory.Exists(CopyDllFromPath) == false)
        {
            EditorUtility.DisplayDialog(ProgressBarTitle, "代码库为空！无法更新代码！联系程序解决", "ok");
            return;
        }

        s_IsAnythingNeedToUpdate = DoCompareVersion();
        RefreshToolbarElement();
    }

    private static Process StartUpdateProgress(Action<string> onLog)
    {
        s_UpdateProcess?.Kill();
        s_UpdateProgress = 0f;

        string           fileName  = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{Environment.CurrentDirectory}\\UpdateScripts.bat" : "sh";
        string           arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? string.Empty : $"{Environment.CurrentDirectory}/UpdateScripts.sh";
        ProcessStartInfo psi       = new ProcessStartInfo(fileName, arguments);

        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError  = true;
        psi.UseShellExecute        = false;
        psi.CreateNoWindow         = true;

        Process p = Process.Start(psi)!;

        p.Disposed           += OnDispose;
        p.OutputDataReceived += (sender, args) => { UpdateProgress(args.Data); };
        p.ErrorDataReceived  += (sender, args) => { UpdateProgress(args.Data); };

        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        return p;

        void UpdateProgress(string log)
        {
            if (string.IsNullOrEmpty(log)) return;

            onLog?.Invoke(log);

            Match match = s_PercentRegex.Match(log);
            if (match.Success == false)
            {
                return;
            }

            s_UpdateProgress = int.Parse(match.Value.Replace("%", string.Empty)) / 100f;
        }

        void OnDispose(object sender, EventArgs e)
        {
            s_UpdateProcess          =  null;
            EditorApplication.update -= CheckVersionBackground;
            EditorUtility.ClearProgressBar();
        }
    }

    private static bool DoCompareVersion()
    {
        if (File.Exists(LocalVersionPath) == false)
        {
            return true;
        }

        string localVersion   = File.ReadAllText(LocalVersionPath);
        string currentVersion = File.ReadAllText($"{CopyFromPath}/version");
        return localVersion != currentVersion;
    }

    public static void Update()
    {
        EditorUtility.DisplayProgressBar(ProgressBarTitle, "检查代码版本", 0f);

        CheckVersion();

        EditorUtility.ClearProgressBar();
        if (s_IsAnythingNeedToUpdate == false)
        {
            if (EditorUtility.DisplayDialog(ProgressBarTitle, "当前已是最新，无需更新！", "不，强制更新！", "好的，那算了") == false)
            {
                return;
            }
        }

        // 如果需要更新unity，则直接跳过
        // if (UpdateToEncryptVersionUnity())
        // {
        //     return;
        // }

        s_IsUpdating = true;

        try
        {
            CopyDlls();
            MirrorCopyDirectory($"{CopyFromPath}/Files/", "./");
            // 模板布局：Framework/Game 双代码区（对应旧模式 Assets/Scripts）
            foreach (var codeDir in new[] { "Assets/Framework", "Assets/Game" })
                if (AssetDatabase.IsValidFolder(codeDir))
                    EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<Object>(codeDir));

            File.WriteAllText(LocalVersionPath, File.ReadAllText($"{CopyFromPath}/version"));

            EditorUtility.DisplayDialog(ProgressBarTitle, "更新完毕！", "ok");
            s_IsAnythingNeedToUpdate = false;
            RefreshToolbarElement();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog(ProgressBarTitle, "更新失败！详情查看Log", "ok");

            RevertUpdateAsMuchAsPossible();
        }
        finally
        {
            s_IsUpdating = false;

            EditorUtility.ClearProgressBar();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static void CopyDlls()
    {
        string[] dlls = Directory.GetFiles(CopyDllFromPath, "*", SearchOption.AllDirectories);
        for (var i = 0; i < dlls.Length; i++)
        {
            EditorUtility.DisplayProgressBar(ProgressBarTitle, $"{dlls[i]}...", i * 1f / dlls.Length);

            string fromPath = dlls[i];
            string toPath   = $"{CopyDllToPath}/{Path.GetRelativePath(CopyDllFromPath, dlls[i])}";

            if (File.Exists(toPath)) File.Delete(toPath);
            File.Copy(fromPath, toPath);
        }
    }

    static void MirrorCopyDirectory(string sourceDir, string destDir)
    {
        // Ensure the source directory exists
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDir}");
        }

        // Create the destination directory if it doesn't exist
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        // Copy all files from source to destination
        string[] allFiles      = Directory.GetFiles(sourceDir);
        string[] filteredFiles = Array.FindAll(allFiles, file => !file.EndsWith(".DS_Store", StringComparison.OrdinalIgnoreCase));
        foreach (string sourceFilePath in filteredFiles)
        {
            string fileName     = Path.GetFileName(sourceFilePath);
            string destFilePath = Path.Combine(destDir, fileName);
            File.Copy(sourceFilePath, destFilePath, true); // true to overwrite if exists
        }

        // Copy subdirectories and their contents from source to destination
        foreach (string sourceSubDirPath in Directory.GetDirectories(sourceDir))
        {
            string subDirName     = Path.GetFileName(sourceSubDirPath);
            string destSubDirPath = Path.Combine(destDir, subDirName);
            MirrorCopyDirectory(sourceSubDirPath, destSubDirPath);
        }

        // Only proceed to delete if the source directory contains files
        if (DirectoryContainsFiles(sourceDir))
        {
            // Delete files in destination that don't exist in source
            allFiles      = Directory.GetFiles(destDir);
            filteredFiles = Array.FindAll(allFiles, file => !file.EndsWith(".DS_Store", StringComparison.OrdinalIgnoreCase));
            foreach (string destFilePath in filteredFiles)
            {
                string fileName       = Path.GetFileName(destFilePath);
                string sourceFilePath = Path.Combine(sourceDir, fileName);
                if (!File.Exists(sourceFilePath))
                {
                    File.Delete(destFilePath);
                }
            }

            // Delete directories in destination that don't exist in source
            foreach (string destSubDirPath in Directory.GetDirectories(destDir))
            {
                string subDirName       = Path.GetFileName(destSubDirPath);
                string sourceSubDirPath = Path.Combine(sourceDir, subDirName);
                if (!Directory.Exists(sourceSubDirPath))
                {
                    Directory.Delete(destSubDirPath, true); // true to delete subdirectories and files
                }
            }
        }
    }

    // Helper method to check if a directory contains any files
    static bool DirectoryContainsFiles(string dir)
    {
        // Check if the directory contains any files
        string[] allFiles = Directory.GetFiles(dir);
        string[] filteredFiles = Array.FindAll(allFiles,
                                               file => !file.EndsWith(".DS_Store", StringComparison.OrdinalIgnoreCase));
        if (filteredFiles.Length > 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 尽可能的还原本次更新
    /// </summary>
    private static void RevertUpdateAsMuchAsPossible()
    {
        // 删除复制过来的所有dll
        string[] dlls = Directory.GetFiles(CopyDllFromPath, "*", SearchOption.AllDirectories);
        for (var i = 0; i < dlls.Length; i++)
        {
            string toPath = $"{CopyDllToPath}/{Path.GetRelativePath(CopyDllFromPath, dlls[i])}";

            if (File.Exists(toPath)) File.Delete(toPath);
        }

        // 删除所有脚本（模板双代码区）
        if (Directory.Exists("Assets/Framework")) Directory.Delete("Assets/Framework", true);
        if (Directory.Exists("Assets/Game")) Directory.Delete("Assets/Game", true);
    }

    #endregion

    #region Encrypt

    private static string GetEncryptToolPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "../Encrypt/Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            switch (RuntimeInformation.OSArchitecture)
            {
                case Architecture.Arm:
                    break;
                case Architecture.Arm64:
                    return "../Encrypt/MacOS_arm64";
                case Architecture.X64:
                case Architecture.X86:
                    return "../Encrypt/MacOS_x86_64";
            }
        }

        EditorUtility.DisplayDialog(ProgressBarTitle, "不支持当前平台，联系程序解决。", "ok");
        throw new NotSupportedException();
    }

    private static string GetUnityPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.GetDirectoryName(EditorApplication.applicationPath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return EditorApplication.applicationPath;
        }

        EditorUtility.DisplayDialog(ProgressBarTitle, "不支持当前平台，联系程序解决。", "ok");
        throw new NotSupportedException();
    }

    private static bool UpdateToEncryptVersionUnity()
    {
        string toolPath  = GetEncryptToolPath();
        string unityPath = GetUnityPath();

        string backupPath = $"{toolPath}/Backup";
        if (Directory.Exists(backupPath) == false)
        {
            BackupFiles($"{toolPath}/Unity", unityPath, $"{toolPath}/Backup");
        }

        if (SyncFiles($"{toolPath}/Unity", unityPath) == false)
        {
            return false;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            EditorUtility.DisplayDialog("更新引擎", $"Windows玩家请自己手动拷贝{Path.GetFullPath(toolPath)}/Unity/Data文件夹到{GetUnityPath()}里替换掉Data文件夹", "ok");
        }
        else
        {
            EditorUtility.DisplayDialog("更新引擎", "引擎还原成功，请重新打开unity", "ok");
        }

        EditorApplication.Exit(0);
        return true;
    }

    private static void RevertFromEncryptVersionUnity()
    {
        string toolPath  = GetEncryptToolPath();
        string unityPath = GetUnityPath();

        string backupPath = $"{toolPath}/Backup";
        if (Directory.Exists(backupPath) == false)
        {
            EditorUtility.DisplayDialog("还想还原引擎？", "还原失败了，备份不见了！！！\r\n要还原就卸载重装unity吧！", "哈哈哈，好玩吧？！");
            return;
        }

        if (SyncFiles(backupPath, unityPath) == false)
        {
            EditorUtility.DisplayDialog("还原引擎", "当前已是正常版本，无需还原。", "ok");
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            EditorUtility.DisplayDialog("还原引擎", $"Windows玩家请自己手动拷贝{Path.GetFullPath(toolPath)}/Backup/Data文件夹到{GetUnityPath()}里替换掉Data文件夹", "ok");
        }
        else
        {
            EditorUtility.DisplayDialog("还原引擎", "引擎还原成功，请重新打开unity", "ok");
        }

        EditorApplication.Exit(0);
    }

    private static void BackupFiles(string from, string willReplacePath, string backupToPath)
    {
        string[] files = Directory.GetFiles(from, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            string relativePath = Path.GetRelativePath(from, file);
            string directory    = $"{backupToPath}/{Path.GetDirectoryName(relativePath)}";
            if (Directory.Exists(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy($"{willReplacePath}/{relativePath}", $"{backupToPath}/{relativePath}");
        }
    }

    private static bool SyncFiles(string from, string to)
    {
        bool     anyChange = false;
        string[] files     = Directory.GetFiles(from, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            string relativePath = Path.GetRelativePath(from, file);
            string toPath       = $"{to}/{relativePath}";
            if (File.Exists(toPath) && IsSameFile(file, toPath))
            {
                continue;
            }

            anyChange = true;
            break;
        }

        if (!anyChange)
        {
            return false;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
        {
            foreach (var file in files)
            {
                string relativePath = Path.GetRelativePath(from, file);
                string toPath       = $"{to}/{relativePath}";
                if (File.Exists(toPath))
                {
                    File.Delete(toPath);
                }

                File.Copy(file, toPath);
            }

            return true;
        }

        return true;

        static bool IsSameFile(string a, string b)
        {
            var aData = File.ReadAllBytes(a);
            var bData = File.ReadAllBytes(b);

            if (aData.Length != bData.Length) return false;
            for (var i = 0; i < aData.Length; i++)
            {
                if (aData[i] != bData[i]) return false;
            }

            return true;
        }
    }

    #endregion

    #region

    [InitializeOnLoadMethod]
    private static void FixDll()
    {
        try
        {
            string[] dlls = Directory.GetFiles(CopyDllFromPath, "*", SearchOption.AllDirectories);
            for (var i = 0; i < dlls.Length; i++)
            {
                string fromPath = dlls[i];
                string toPath   = $"{CopyDllToPath}/{Path.GetRelativePath(CopyDllFromPath, dlls[i])}";

                if (File.Exists(toPath)) File.Delete(toPath);
                File.Copy(fromPath, toPath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    #endregion
}