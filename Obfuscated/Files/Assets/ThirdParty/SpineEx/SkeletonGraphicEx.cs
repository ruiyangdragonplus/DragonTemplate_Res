using UnityEngine;
using Spine.Unity;
using UnityEditor;

public class SkeletonGraphicEx : SkeletonGraphic
{
    public string dataGuid;
    public string matGuid;
    public string projDataPath;
    public string projMatPath;
    public string dataPath;
    public string matPath;
    public bool immediate = true; 

    public void StartExecute()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            skeletonDataAsset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(projDataPath);
            if (!string.IsNullOrEmpty(projMatPath))
                material = AssetDatabase.LoadAssetAtPath<Material>(projMatPath);
        }
#endif
        
        base.Awake();        
    }

    protected override void Awake()
    {        
        if (string.IsNullOrEmpty(dataPath))
        {
            Debug.LogError("SkeletonGraphicEx, data path empty!");
            return;
        }

        if (!immediate)
            return;

        StartExecute();       
    }

    protected override void OnDestroy()
    {
        if (!Application.isPlaying)
            return;

        skeletonDataAsset = null;
        material = null;

        base.OnDestroy();        
    }
}
