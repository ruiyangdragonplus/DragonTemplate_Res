// #if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Beebyte.Obfuscator;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExportDllFixGuid
{
    public class GuidInfo
    {
        public string fullName;
        public long   fileID;
        public string guid;
    }

    public static class ExportDllFixGuidUtils
    {
        [MenuItem("ExportDllFixGuid/Settings", false, -99)]
        private static void Settings()
        {
            string settingPath = "Assets/Editor/ExportDllFixGuidConfig.asset";
            var    config      = AssetDatabase.LoadAssetAtPath<ExportDllFixGuidConfig>(settingPath);
            if (config == null)
            {
                string parentDirectory = Path.GetDirectoryName(settingPath);
                if (!Directory.Exists(parentDirectory) && parentDirectory != null)
                    Directory.CreateDirectory(parentDirectory);
                config = ScriptableObject.CreateInstance<ExportDllFixGuidConfig>();
                AssetDatabase.CreateAsset(config, settingPath);
            }

            Selection.objects = new Object[] {config};
            AssetDatabase.Refresh();
        }

        [MenuItem("ExportDllFixGuid/FindAssemblyDefinition(Selection)", false, -98)]
        private static void FindAssemblyDefinition()
        {
            string settingPath = "Assets/Editor/ExportDllFixGuidConfig.asset";
            var    config      = AssetDatabase.LoadAssetAtPath<ExportDllFixGuidConfig>(settingPath);
            if (config == null)
            {
                string parentDirectory = Path.GetDirectoryName(settingPath);
                if (!Directory.Exists(parentDirectory) && parentDirectory != null)
                    Directory.CreateDirectory(parentDirectory);
                config = ScriptableObject.CreateInstance<ExportDllFixGuidConfig>();
                AssetDatabase.CreateAsset(config, settingPath); 
            }

            var asmdefAssets = Selection.GetFiltered(typeof(AssemblyDefinitionAsset), SelectionMode.DeepAssets);
            if (asmdefAssets.Length == 0)
                return;
            foreach (var asmdefAsset in asmdefAssets)
            {
                var asmdef = asmdefAsset as AssemblyDefinitionAsset;
                if (config.assemblyDefinitionAssets.Contains(asmdef))
                {
                    Debug.LogError("已存在" + asmdef.name);
                    continue;
                }

                config.assemblyDefinitionAssets.Add(asmdef);
                Debug.Log("已添加" + asmdef.name);
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Selection.objects = new Object[] {config};
            AssetDatabase.Refresh();
        }
        
        [MenuItem("ExportDllFixGuid/FindDll(Selection)", false, -98)]
        private static void FindDlls()
        {
            string settingPath = "Assets/Editor/ExportDllFixGuidConfig.asset";
            var    config      = AssetDatabase.LoadAssetAtPath<ExportDllFixGuidConfig>(settingPath);
            if (config == null)
            {
                string parentDirectory = Path.GetDirectoryName(settingPath);
                if (!Directory.Exists(parentDirectory) && parentDirectory != null)
                    Directory.CreateDirectory(parentDirectory);
                config = ScriptableObject.CreateInstance<ExportDllFixGuidConfig>();
                AssetDatabase.CreateAsset(config, settingPath);
            }
            
            var asmdefAssets = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.DeepAssets);
            if (asmdefAssets.Length == 0)
                return;
            foreach (var asmdefAsset in asmdefAssets)
            {
                string assetPath = AssetDatabase.GetAssetPath(asmdefAsset);
                if (!assetPath.EndsWith(".dll"))
                    continue;
                if(!assetPath.Contains("Lib/Runtime"))
                    continue;
                string platform = Directory.GetParent(assetPath).Name;
                if (platform == "Android")
                {
                    if(!config.assemblysAndroid.Contains(asmdefAsset))
                        config.assemblysAndroid.Add(asmdefAsset);
                }
                else if (platform == "iOS")
                {
                    if(!config.assemblysIOS.Contains(asmdefAsset))
                        config.assemblysIOS.Add(asmdefAsset);
                }
                else if(platform == "StandaloneOSX")
                {
                    if(!config.assemblysStandalone.Contains(asmdefAsset))
                        config.assemblysStandalone.Add(asmdefAsset);
                }
            }
            
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Selection.objects = new Object[] {config};
            AssetDatabase.Refresh();
        }


        [MenuItem("ExportDllFixGuid/ExportDll", false, -1)]
        public static void ExportDll()
        {
            GenAssembly();
            GenSrcGuidMap();
        }

        [MenuItem("ExportDllFixGuid/GenAssembly", false, 1)]
        public static void GenAssembly()
        {
            ExportDllFixGuidConfig config = GetConfig();
            if (string.IsNullOrEmpty(config.dllOutDir))
                return;
            string platformName   = GetPlatformName();
            string dllPlatformDir = Path.Combine(config.dllOutDir, platformName);
            if (Directory.Exists(dllPlatformDir))
                Directory.Delete(dllPlatformDir, true);

            List<string> assemblies = new List<string>();
            foreach (var ada in config.assemblyDefinitionAssets)
            {
                if(ada == null)
                    continue;
                Match match = Regex.Match(ada.text, ".*\"name\"\\s*:\\s*\"(.*)\"");
                if (match.Success)
                {
                    assemblies.Add(match.Groups[1].ToString());
                }
            }

            List<string> obfuscateDlls = new List<string>();
            foreach (string assembly in assemblies)
            {
                string target = $"{dllPlatformDir}/{assembly}";
                CopyFile($"Library/ScriptAssemblies/{assembly}.dll",
                         $"{target}.dll");
                CopyFile($"Library/ScriptAssemblies/{assembly}.pdb",
                         $"{target}.pdb");
                obfuscateDlls.Add($"{target}.dll");
            }
            
            List<UnityEngine.Object> assemblyDlls = new List<UnityEngine.Object>();
            if(platformName == "Android")
                assemblyDlls = config.assemblysAndroid;
            else if(platformName == "iOS")
                assemblyDlls = config.assemblysIOS;
            else if(platformName == "WindowsMacLinux")
                assemblyDlls = config.assemblysStandalone;
            foreach (Object assembly in assemblyDlls)
            {
                if(assembly == null)
                    continue;
                string target = $"{dllPlatformDir}/{assembly.name}";
                string assetPath = AssetDatabase.GetAssetPath(assembly);
                CopyFile(assetPath, $"{target}.dll");
                obfuscateDlls.Add($"{target}.dll");
            }

            Obfuscate(obfuscateDlls.ToArray());

            // foreach (Object file in config.fileAssets)
            // {
            //     string path = AssetDatabase.GetAssetPath(file);
            //     CopyFile(path, $"{config.dllOutDir}/{path}");
            //     CopyFile($"{path}.meta", $"{config.dllOutDir}/{path}.meta");
            // }

            //拷贝引用的dll
            // List<string> libs = new List<string>();
            // foreach (var ada in config.assemblyDefinitionAssets)
            // {
            //     var assetPath = AssetDatabase.GetAssetPath(ada);
            //     string parentDirectory = System.IO.Path.GetDirectoryName(assetPath);
            //     var dlls = Directory.GetFiles(parentDirectory, "*.dll", SearchOption.AllDirectories);
            //     libs.AddRange(dlls);
            // }
            //
            // foreach (string lib in libs)
            // {
            //     string fileName = Path.GetFileNameWithoutExtension(lib);
            //     string parentDir = Path.GetDirectoryName(lib);
            //     string dllPath = $"{parentDir}/{fileName}.dll";
            //     string metaPath = $"{dllPath}.meta";
            //     if (File.Exists(dllPath))
            //         CopyFile(dllPath, $"{config.dllOutDir}/{platformName}/{fileName}.dll");
            //     if (File.Exists(metaPath))
            //         CopyFile(metaPath, $"{config.dllOutDir}/{platformName}/{fileName}.meta");
            // }
        }

        [MenuItem("ExportDllFixGuid/GenDllGuidMap", false, 2)]
        public static void GenDllGuidMap()
        {
            ExportDllFixGuidConfig config = GetConfig();
            if (string.IsNullOrEmpty(config.dllDir))
                return;

            Dictionary<string, GuidInfo> srcInfos = new Dictionary<string, GuidInfo>();
            var                          assets   = Directory.GetFiles(config.dllDir, "*.dll");
            foreach (var findAsset in assets)
            {
                var path         = $"{findAsset.Replace(Application.dataPath, "")}";
                var assetObjects = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assetObjects.Length == 0)
                    continue;
                foreach (var assetObject in assetObjects)
                {
                    if (assetObject is MonoScript monoScript)
                    {
                        var type = monoScript.GetClass();
                        if (monoScript != null && type != null && (type.IsSubclassOf(typeof(MonoBehaviour)) ||
                                                                   type.IsSubclassOf(typeof(ScriptableObject))))
                        {
                            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(monoScript, out string guid,
                                                                               out long fileId))
                            {
                                if (!srcInfos.ContainsKey(type.FullName))
                                    srcInfos.Add(type.FullName, new GuidInfo()
                                    {
                                        fullName = type.FullName,
                                        fileID   = fileId,
                                        guid     = guid
                                    });
                            }
                            else
                            {
                                Debug.LogError($"{monoScript.name}无法获取GUID和FileID");
                            }
                        }
                    }
                }
            }

            var    filePath = $"{Application.dataPath}/Scripts/DllGuidMap.json";
            string json     = JsonConvert.SerializeObject(srcInfos, Formatting.Indented);
            File.WriteAllText(filePath, json);
            AssetDatabase.Refresh();
        }

        [MenuItem("ExportDllFixGuid/GenSrcGuidMap", false, 3)]
        public static void GenSrcGuidMap()
        {
            ExportDllFixGuidConfig config = GetConfig();
            if (string.IsNullOrEmpty(config.dllOutDir))
                return;

            List<string> srcs = new List<string>();
            foreach (var ada in config.assemblyDefinitionAssets)
            {
                if(ada == null)
                    continue;
                var    assetPath       = AssetDatabase.GetAssetPath(ada);
                string parentDirectory = Path.GetDirectoryName(assetPath);
                var    srcpaths        = Directory.GetFiles(parentDirectory, "*.cs", SearchOption.AllDirectories);
                srcs.AddRange(srcpaths);
            }

            Dictionary<string, GuidInfo> srcInfos = new Dictionary<string, GuidInfo>();
            foreach (var src in srcs)
            {
                var path      = src.Replace(Application.dataPath, "Assets");
                var scriptObj = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (scriptObj == null)
                    continue;
                var type = scriptObj.GetClass();
                if (type == null)
                    continue;

                if (type.IsSubclassOf(typeof(MonoBehaviour)) || type.IsSubclassOf(typeof(ScriptableObject)))
                {
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(scriptObj, out string guid, out long fileId))
                    {
                        if (!srcInfos.ContainsKey(type.FullName))
                            srcInfos.Add(type.FullName, new GuidInfo()
                            {
                                fullName = type.FullName,
                                fileID   = fileId,
                                guid     = guid
                            });
                    }
                    else
                    {
                        Debug.LogError($"{scriptObj.name}无法获取GUID和FileID");
                    }
                }
            }

            string platformName = GetPlatformName();
            var    filePath     = $"{config.dllOutDir}/{platformName}/SrcGuidMap.json";
            var    dir          = Path.GetDirectoryName(filePath);
            if (Directory.Exists(dir) == false) Directory.CreateDirectory(dir);

            string json = JsonConvert.SerializeObject(srcInfos, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        [MenuItem("ExportDllFixGuid/Src2DllFixGuid", false, 4)]
        public static void Src2DllFixGuid()
        {
            ExportDllFixGuidConfig config = GetConfig();
            if (string.IsNullOrEmpty(config.dllDir))
                return;
            var    dllMapPath = $"{config.dllDir}/DllGuidMap.json";
            var    srcMapPath = $"{config.dllDir}/SrcGuidMap.json";
            string jsonDllMap = File.ReadAllText(dllMapPath);
            string jsonSrcMap = File.ReadAllText(srcMapPath);

            Dictionary<string, GuidInfo> dllGuidMaps =
                JsonConvert.DeserializeObject<Dictionary<string, GuidInfo>>(jsonDllMap);
            Dictionary<string, GuidInfo> srcGuidMaps =
                JsonConvert.DeserializeObject<Dictionary<string, GuidInfo>>(jsonSrcMap);

            string[] findAssetPaths =
                Directory.GetFiles(Application.dataPath, @"*.*", SearchOption.AllDirectories)
                    .Where(s => s.EndsWith(".prefab") || s.EndsWith(".asset") || s.EndsWith(".unity")).ToArray();
            foreach (var path in findAssetPaths)
            {
                string content = File.ReadAllText(path);
                foreach (var guidInfo in srcGuidMaps)
                {
                    if (!dllGuidMaps.ContainsKey(guidInfo.Key))
                        continue;
                    string   target      = $"fileID: {guidInfo.Value.fileID}, guid: {guidInfo.Value.guid}";
                    GuidInfo dllGuidInfo = dllGuidMaps[guidInfo.Key];
                    string   replace     = $"fileID: {dllGuidInfo.fileID}, guid: {dllGuidInfo.guid}";
                    if (content.Contains(target))
                    {
                        content = content.Replace(target, replace);
                        File.WriteAllText(path, content);
                        Debug.Log($"FixGuid {path} MonoScript:{guidInfo.Key} {target} -> {replace}");
                    }
                }
            }

            Debug.Log("FixGuid Done");
            AssetDatabase.Refresh();

            CheckAnyMissingScript();
        }

        [MenuItem("ExportDllFixGuid/Dll2SrcFixGuid", false, 5)]
        public static void Dll2SrcFixGuid()
        {
            ExportDllFixGuidConfig config = GetConfig();
            if (string.IsNullOrEmpty(config.dllDir))
                return;

            var    dllMapPath = $"{config.dllDir}/DllGuidMap.json";
            var    srcMapPath = $"{config.dllDir}/SrcGuidMap.json";
            string jsonDllMap = File.ReadAllText(dllMapPath);
            string jsonSrcMap = File.ReadAllText(srcMapPath);

            Dictionary<string, GuidInfo> dllGuidMaps =
                JsonConvert.DeserializeObject<Dictionary<string, GuidInfo>>(jsonDllMap);
            Dictionary<string, GuidInfo> srcGuidMaps =
                JsonConvert.DeserializeObject<Dictionary<string, GuidInfo>>(jsonSrcMap);

            string[] findAssetPaths =
                Directory.GetFiles(Application.dataPath, @"*.*", SearchOption.AllDirectories)
                    .Where(s => s.EndsWith(".prefab") || s.EndsWith(".asset") || s.EndsWith(".unity")).ToArray();
            foreach (var path in findAssetPaths)
            {
                string content = File.ReadAllText(path);
                foreach (var guidInfo in dllGuidMaps)
                {
                    if (!srcGuidMaps.ContainsKey(guidInfo.Key))
                        continue;
                    string   target     = $"fileID: {guidInfo.Value.fileID}, guid: {guidInfo.Value.guid}";
                    GuidInfo srcGuidMap = srcGuidMaps[guidInfo.Key];
                    string   replace    = $"fileID: {srcGuidMap.fileID}, guid: {srcGuidMap.guid}";
                    if (content.Contains(target))
                    {
                        content = content.Replace(target, replace);
                        File.WriteAllText(path, content);
                        Debug.Log($"FixGuid {path} MonoScript:{guidInfo.Key} {target} -> {replace}");
                    }
                }
            }

            Debug.Log("FixGuid Done");
            AssetDatabase.Refresh();

            CheckAnyMissingScript();
        }

        [MenuItem("ExportDllFixGuid/检查脚本是否有丢失", false, 6)]
        public static void CheckAnyMissingScript()
        {
            if (HasAnyMissingScript(!Application.isBatchMode))
            {
                if (Application.isBatchMode) throw new Exception("资源有脚本丢失！联系程序解决！");
                else EditorUtility.DisplayDialog("提示", "资源有脚本丢失！联系程序解决！", "现在就去！");
            }
            else
            {
                Debug.Log("检查完毕！资源一切正常。");
            }
        }

        private static Regex regex = new Regex(@"m_Script: {fileID: (\d*), guid: ([a-z0-9]*), type: 3}");

        private static Dictionary<string, HashSet<long>> scriptsCache = new Dictionary<string, HashSet<long>>();

        public static bool HasAnyMissingScript(bool displayProgress)
        {
            bool     anyMissing = false;
            string[] findAssets = AssetDatabase.FindAssets($"t:prefab t:scriptableobject t:scene", new string[] {"Assets/Res"});
            for (var i = 0; i < findAssets.Length; i++)
            {
                string findAsset = findAssets[i];
                string path      = AssetDatabase.GUIDToAssetPath(findAsset);
                if (displayProgress) EditorUtility.DisplayProgressBar($"脚本丢失检查中({i + 1}/{findAssets.Length})", path, (i + 1.0f) / findAssets.Length);

                if (HasAnyMissingScript(path))
                {
                    anyMissing = true;
                    Debug.LogError($"Missing script {path}", AssetDatabase.LoadAssetAtPath<Object>(path));
                }
            }

            if (displayProgress) EditorUtility.ClearProgressBar();
            return anyMissing;
        }

        public static bool HasAnyMissingScript(string assetPath)
        {
            string content = File.ReadAllText(assetPath);
            foreach (Match match in regex.Matches(content))
            {
                long          scriptFileID = long.TryParse(match.Groups[1].Value, out var result) ? result : 0;
                string        scriptGuid   = match.Groups[2].Value;
                HashSet<long> scripts;
                if (scriptsCache.TryGetValue(scriptGuid, out scripts) == false)
                {
                    string   scriptPath   = AssetDatabase.GUIDToAssetPath(scriptGuid);
                    Object[] scriptAssets = AssetDatabase.LoadAllAssetsAtPath(scriptPath);
                    scripts = new HashSet<long>();
                    foreach (Object scriptAsset in scriptAssets)
                    {
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(scriptAsset, out string scriptAssetGUID, out long scriptAssetFileID))
                        {
                            scripts.Add(scriptAssetFileID);
                        }
                    }

                    scriptsCache.Add(scriptGuid, scripts);
                }

                if (scripts.Contains(scriptFileID) == false)
                {
                    return true;
                }
            }

            return false;
        }

        static void Obfuscate(string[] dlls)
        {
            var options     = OptionsManager.LoadOptions();
            var lastEnabled = options.enabled;
            try
            {
                options.enabled = false;
                foreach (var dll in dlls)
                {
                    try
                    {
                        Obfuscator.Obfuscate(new[] { dll }, null, options, EditorUserBuildSettings.activeBuildTarget);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogErrorFormat("{0} Obfuscate Error, {1}", dll, ex.Message);
                    }
                }
            }
            finally
            {
                options.enabled = lastEnabled;
                EditorUtility.ClearProgressBar();
            }
        }

        static void CopyFile(string fromFilePath, string toFilePath)
        {
            CreateDirectory(toFilePath);
            File.Copy(fromFilePath, toFilePath, true);
        }

        static void CreateDirectory(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                string dirName = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dirName))
                {
                    Directory.CreateDirectory(dirName);
                }
            }
        }

        public static string GetPlatformName()
        {
#if UNITY_ANDROID
        string platformName = "Android";
#elif UNITY_IOS
        string platformName = "iOS";
#elif UNITY_STANDALONE_OSX
            string platformName = "WindowsMacLinux";
#elif UNITY_STANDALONE_WIN
            string platformName = "WindowsMacLinux";
#endif
            return platformName;
        }

        public static ExportDllFixGuidConfig GetConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ExportDllFixGuidConfig>("Assets/Editor/ExportDllFixGuidConfig.asset");
            return config;
        }
    }
}
// #endif