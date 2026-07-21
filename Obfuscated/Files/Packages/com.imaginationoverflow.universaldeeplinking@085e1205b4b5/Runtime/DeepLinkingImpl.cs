using DragonPlus.Core;
using DragonPlus.DeepLinking.Event;
using DragonU3DSDK.Network.API.Protocol;
using UnityEngine;

namespace DragonPlus.DeepLinking
{
    public class DeepLinkingImpl : IDeepLinking
    {
        public void Initialize()
        {
#if !UNITY_STANDALONE
            ImaginationOverflow.UniversalDeepLinking.DeepLinkManager.Instance.LinkActivated += OnLinkActivated;
#endif
        }

        //此后实现其他具体逻辑
        private void OnLinkActivated(ImaginationOverflow.UniversalDeepLinking.LinkActivation s)
        {
            SDKLog.Info("DeepLink New RawQueryString: " + s.RawQueryString);
            SDKLog.Info("DeepLink New QueryString: "    + s.QueryString);
            SDKLog.Info("DeepLink New Uri: "            + s.Uri);

            if (string.IsNullOrEmpty(s.RawQueryString)) return;

            var nvc = SDKUtil.Misc.ParseQueryString(s.RawQueryString);
            var evt = new EventDeepLinking(nvc, s.Uri);
            EventBus.Dispatch(evt);
            PlayerPrefs.SetString("DeepLinkRawQueryString", s.RawQueryString);

            var commonGameEvent = new BiEventCommon.Types.CommonGameEvent
            {
                CommonGameEventType = BiEventCommon.Types.CommonGameEventType.DeepLink,
            };
            commonGameEvent.Params.Add(s.Uri);
            commonGameEvent.Params.Add(s.RawQueryString);
            EventBus.DispatchInMainThread(new EventCommonBiTracking(commonGameEvent));
        }
    }
}