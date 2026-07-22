#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditorInternal;
using UnityEngine;

namespace ExportDllFixGuid
{
    [CreateAssetMenu(fileName = "ExportDllFixGuidConfig", menuName = "ExportDllFixGuidConfig")]
    public class ExportDllFixGuidConfig : ScriptableObject
    {
        [Header("输出目录")]
        public string dllOutDir = "ExportDll";
        [Header("dll在美术项目中的目录")]
        public string dllDir = "Assets/Scripts";
        // [Header("是否混淆[需要Obfuscate插件]")]
        // public bool isObfuscate = false;
        [Header("需要导出Dll的ADA文件")]
        public List<AssemblyDefinitionAsset> assemblyDefinitionAssets = new List<AssemblyDefinitionAsset>();
        [Header("需要混淆的已编译Dll(Android)")]
        public List<UnityEngine.Object> assemblysAndroid = new List<UnityEngine.Object>();
        [Header("需要混淆的已编译Dll(iOS)")]
        public List<UnityEngine.Object> assemblysIOS = new List<UnityEngine.Object>();
        [Header("需要混淆的已编译Dll(Standalone)")]
        public List<UnityEngine.Object> assemblysStandalone = new List<UnityEngine.Object>();

        // [Header("需要导出的资源")]
        // public List<Object> fileAssets = new List<Object>();
    }
}
#endif
