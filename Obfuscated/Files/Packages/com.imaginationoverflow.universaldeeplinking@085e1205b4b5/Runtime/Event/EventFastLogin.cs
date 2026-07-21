using DragonPlus.Core;

namespace DragonPlus.DeepLinking.Event
{
    public class EventFastLogin : IEvent
    {
        public string RedirectUrl { get; private set; }

        public EventFastLogin(string redirectUrl)
        {
            RedirectUrl = redirectUrl;
        }
    }
}