using System;
using System.Collections;
using DragonPlus.Account;
using DragonPlus.Core;
using DragonPlus.DeepLinking.Event;
using DragonPlus.Native.Bridge;
using DragonPlus.Save;
using UnityEngine;
using UnityEngine.Networking;

namespace DragonPlus.DeepLinking
{
    public class FastLoginImpl : EventListenerHolder, IFastLogin
    {
        #region Properties

        private const float DelaySecondsWhenFailed = 3.0f;

        private bool   _profileLoaded;
        private string _fastLoginRedirectUrl = string.Empty;
        private bool   _fastLoginRequested;

        #endregion

        #region IFastLogin Apis

        public void Initialize()
        {
            _profileLoaded        = false;
            _fastLoginRequested   = false;
            _fastLoginRedirectUrl = string.Empty;

            SubscribeEvents();

            SDKLog.Info("[FastLogin] Initialized.");
        }

        public void JumpBack(string url, string avatarId, string playerName)
        {
            JumpBackInternal(url, avatarId, playerName, 10);
        }

        #endregion

        #region EventListenerHolder Apis

        public override void SubscribeEvents()
        {
            base.SubscribeEvents();

            SubscribeEvent<EventDeepLinking>(OnDeepLinking);
            SubscribeEvent<EventProfileCreateSuccess>(OnProfileCreateSuccess);
            SubscribeEvent<EventProfileGetSuccess>(OnProfileGetSuccess);
            SubscribeEvent<EventProfileResolved>(OnProfileResolved);
            SubscribeEvent<EventProfileReplaced>(OnProfileReplaced);
        }

        #endregion

        #region Private Apis

        private void OnDeepLinking(EventDeepLinking evt)
        {
            try
            {
                if (string.IsNullOrEmpty(evt.Uri)) return;

                var uri          = new Uri(evt.Uri);
                var source       = evt.RawData.Get("source");
                var redirectPath = evt.RawData.Get("cb");

                SDKLog.Info($"[FastLogin] OnNotify: {uri}, {source}, {redirectPath}");

                if (string.IsNullOrEmpty(source) ||
                    string.IsNullOrEmpty(redirectPath))
                {
                    return;
                }

                // request for fast login
                if (!IsFastLoginUri(uri) ||
                    source != "1")
                {
                    return;
                }

                var redirectUrl = GetGameBaseUrl() + redirectPath;

                _fastLoginRedirectUrl = redirectUrl;
                _fastLoginRequested   = true;

                SDKLog.Info($"[FastLogin] Requested: {_fastLoginRedirectUrl}");

                if (_profileLoaded)
                {
                    TriggerFastLogin();
                }
                else
                {
                    WaitProfileLoaded();
                }
            }
            catch (Exception exception)
            {
                SDKLog.Error(
                    $"[FastLogin] Parse notification failed:  {exception.Message}\n{exception.StackTrace}");
            }
        }

        private void OnProfileCreateSuccess(EventProfileCreateSuccess evt)
        {
            _profileLoaded = true;

            TriggerFastLogin();
        }

        private void OnProfileGetSuccess(EventProfileGetSuccess evt)
        {
            _profileLoaded = true;

            TriggerFastLogin();
        }

        private void OnProfileReplaced(EventProfileReplaced evt)
        {
            _profileLoaded = true;

            TriggerFastLogin();
        }

        private void OnProfileResolved(EventProfileResolved evt)
        {
            _profileLoaded = true;

            TriggerFastLogin();
        }


        private void WaitProfileLoaded()
        {
            if (_profileLoaded)
            {
                TriggerFastLogin();
                return;
            }

            try
            {
                if (!SDK<IStorage>.CanGet())
                {
                    DelayCall(WaitProfileLoaded, 1.0f);
                    return;
                }

                if (!SDK<IAccount>.CanGet())
                {
                    DelayCall(WaitProfileLoaded, 1.0f);
                    return;
                }

                if (!SDK<IAccount>.Instance.HasLogin)
                {
                    DelayCall(WaitProfileLoaded, 1.0f);
                    return;
                }

                TriggerFastLogin();
            }
            catch (Exception exception)
            {
                SDKLog.Error(
                    $"[FastLogin] Waiting profile failed: {exception.Message} {exception.StackTrace}");
            }
        }

        private void TriggerFastLogin()
        {
            SDKLog.Info(
                $"[FastLogin] Try trigger fast login: {_fastLoginRequested}, {_fastLoginRedirectUrl}.");

            if (!_fastLoginRequested)
            {
                return;
            }

            try
            {
                var evt = new EventFastLogin(_fastLoginRedirectUrl);
                EventBus.Dispatch(evt);
                _fastLoginRequested = false;
            }
            catch (Exception exception)
            {
                SDKLog.Error(
                    $"[FastLogin] Trigger fast login failed:  {exception.Message}\n{exception.StackTrace}");
            }
        }

        private static bool IsFastLoginUri(Uri uri)
        {
            if (uri.Host == "app" && uri.AbsolutePath == "/fastlogin")
            {
                return true;
            }

            if (uri.AbsolutePath == "/app/fastlogin")
            {
                return true;
            }

            return false;
        }

        private static string GetGameBaseUrl()
        {
            var apiUrl     = ConfigurationController.Instance.APIServerURL;
            var isAlphaUrl = apiUrl.Contains("-api-alpha");
            if (isAlphaUrl)
            {
                return apiUrl
                    .Replace("-api-alpha", "|alpha")
                    .Replace("-api",       "")
                    .Replace("-",          "")
                    .Replace("|alpha",     "-alpha");
            }

            return apiUrl
                .Replace("-api-alpha", "")
                .Replace("-api",       "")
                .Replace("-",          "");
        }

        private static void JumpBackInternal(string url, string avatarId, string playerName, int retryCount)
        {
            if (retryCount <= 0)
            {
                SDKLog.Warning($"[FastLogin] JumpBack to {url} failed.");
                return;
            }

            SDKLog.Info($"[FastLogin] JumpBack to {url}, {avatarId}, {playerName}");

            if (SDK<IAccount>.Instance.LoginStatus == LoginStatus.LOGIN_LOCKING)
            {
                DelayCall(
                    () => { JumpBackInternal(url, avatarId, playerName, retryCount - 1); },
                    DelaySecondsWhenFailed);
            }
            else
            {
                SDK<IAccount>.Instance.Login(OnLoginCallback);
            }

            return;

            void OnLoginCallback(bool success, string message)
            {
                if (success)
                {
                    var encodedPlayerName = UnityWebRequest.EscapeURL(playerName);
                    var encodedAvatarId   = UnityWebRequest.EscapeURL(avatarId);
                    var finalUrl =
                        $"{url}?token={SDK<IAccount>.Instance.Token}&avt={encodedAvatarId}&name={encodedPlayerName}";
                    SDKLog.Info($"[FastLogin] Try open url: {finalUrl}");

                    if (Application.platform == RuntimePlatform.IPhonePlayer)
                    {
                        SDK<INative>.Instance.OpenAppStoreRate(finalUrl);
                    }
                    else
                    {
                        Application.OpenURL(finalUrl);
                    }
                }
                else
                {
                    SDKLog.Error($"[FastLogin] Failed to fetch token.");

                    DelayCall(
                        () => { JumpBackInternal(url, avatarId, playerName, retryCount - 1); },
                        DelaySecondsWhenFailed);
                }
            }
        }


        private static void DelayCall(Action action, float delayInSeconds)
        {
            SDKUtil.Unity.StartCoroutine(DelayCallInternal(action, delayInSeconds));
        }

        private static IEnumerator DelayCallInternal(Action action, float delayInSeconds)
        {
            yield return new WaitForSeconds(delayInSeconds);

            action?.Invoke();
        }

        #endregion
    }
}