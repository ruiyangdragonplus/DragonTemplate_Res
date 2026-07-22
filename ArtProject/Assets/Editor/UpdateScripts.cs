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

#if UNITY_6000_3_OR_NEWER
        // 官方 [MainToolbarElement] 注册之外的自愈检查（见 EnsureToolbarOverlay 注释）
        s_EnsureRetry = 0;
        EditorApplication.delayCall += EnsureToolbarOverlay;
#else
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
        menu.DropDown(buttonRect);
    }

    /// <summary>红点状态变化时刷新工具栏元素。</summary>
    private static void RefreshToolbarElement()
    {
        try { MainToolbar.Refresh(kToolbarElementPath); }
        catch { /* MainToolbar 可能尚未就绪 */ }
    }

    // ═══ 自愈注入 ═══
    // OverlayCanvas.Initialize(MainToolbar 模式) 只在主工具栏窗口建立时枚举一次 [MainToolbarElement] 定义；
    // 若那一刻本程序集尚未编译（工程首次打开时工具栏先于脚本编译建立），本元素的 Overlay 永远不会被创建，
    // 之后的域重载也不会补建。这里在每次域重载后检查 Overlay 是否存在，缺失则按官方 Initialize 的
    // 同款流程（new MainToolbarOverlay → AddOverlay → RestoreOverlay）反射补注入。
    private static int s_EnsureRetry;

    private static void EnsureToolbarOverlay()
    {
        try
        {
            if (EnsureToolbarOverlayOnce())
                return;
        }
        catch { return; /* 反射面变化（后续 Unity 版本），放弃自愈，官方路径仍可能生效 */ }

        if (++s_EnsureRetry <= 20)
            EditorApplication.delayCall += EnsureToolbarOverlay; // 工具栏窗口尚未就绪，稍后重试
    }

    /// <returns>true=已存在或注入成功；false=窗口未就绪需重试。</returns>
    private static bool EnsureToolbarOverlayOnce()
    {
        const BindingFlags kAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var editorAsm = typeof(Editor).Assembly;
        var mtType = editorAsm.GetType("UnityEditor.Toolbars.MainToolbar");
        if (mtType == null) return true; // 无此 API，视为无需处理

        var tryGet = mtType.GetMethod("TryGetOverlay", kAll);
        var getArgs = new object[] { kToolbarElementPath, null };
        var existsProp = mtType.GetProperty("windowExists", kAll);
        if (existsProp == null || !(bool)existsProp.GetValue(null)) return false; // 窗口未就绪
        if ((bool)tryGet.Invoke(null, getArgs))
        {
            // Overlay 已在（官方路径已建）。但 Unity 6 对已有布局下新出现的第三方元素默认 displayed=false，
            // 美术同学不会知道要去工具栏菜单手动开启——每台机器首次强制显示一次，之后尊重用户显隐选择。
            EnsureDisplayedFirstTime(getArgs[1]);
            return true;
        }

        var window = mtType.GetProperty("window", kAll).GetValue(null);
        var canvas = window.GetType().GetProperty("overlayCanvas", kAll).GetValue(window);
        var method = typeof(UpdateScripts).GetMethod(nameof(CreateUpdateDropdown), kAll);

        var ovType = editorAsm.GetType("UnityEditor.Overlays.MainToolbarOverlay");
        var overlay = Activator.CreateInstance(ovType, true);
        ovType.GetProperty("createElementMethod", kAll).SetValue(overlay, method);

        var oaType = editorAsm.GetType("UnityEditor.Overlays.OverlayAttribute");
        var oa = Activator.CreateInstance(oaType);
        Func<string, object> attr = p => oaType.GetProperty(p, kAll).GetValue(oa);
        ovType.GetMethod("Initialize", kAll, null,
                new[] { typeof(string), typeof(string), typeof(string), typeof(Vector2), typeof(Vector2), typeof(Vector2), typeof(int), typeof(string) }, null)
            .Invoke(overlay, new[] { kToolbarElementPath, kToolbarElementPath, "更新代码", attr("defaultSize"), attr("minSize"), attr("maxSize"), attr("priority"), attr("group") });

        var overlayBase = editorAsm.GetType("UnityEditor.Overlays.Overlay");
        canvas.GetType().GetMethod("AddOverlay", kAll, null, new[] { overlayBase, typeof(bool) }, null)
            .Invoke(canvas, new[] { overlay, (object)false });
        var saveData = canvas.GetType().GetMethod("FindSaveData", kAll).Invoke(canvas, new[] { overlay });
        canvas.GetType().GetMethod("RestoreOverlay", kAll).Invoke(canvas, new[] { overlay, saveData });

        EnsureDisplayedFirstTime(overlay);
        return true;
    }

    /// <summary>每台机器/每工程只强制显示一次；之后用户手动隐藏则尊重其选择。</summary>
    private static void EnsureDisplayedFirstTime(object overlay)
    {
        if (overlay == null) return;
        string key = "Dragon.UpdateScripts.ToolbarShown." + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false)) return;
        overlay.GetType().GetProperty("displayed")?.SetValue(overlay, true);
        EditorPrefs.SetBool(key, true);
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