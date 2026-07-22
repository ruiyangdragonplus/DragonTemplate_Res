using System;
using UnityEngine;

public class StompyRobotInit : MonoBehaviour
{
    private bool m_LastEnable;
    
    void Awake()
    {
        SRDebug.Init();
    }

    private void LateUpdate()
    {
        if (SRDebug.Instance.IsDebugPanelVisible != m_LastEnable)
        {
            m_LastEnable = SRDebug.Instance.IsDebugPanelVisible;
            if (SRDebug.Instance.IsDebugPanelVisible)
            {
                GameObject SRDebugger = GameObject.Find("SRDebugger");
                SRDebugger?.SetActive(false);
                SRDebugger?.SetActive(true);
            }
        }
    }
}