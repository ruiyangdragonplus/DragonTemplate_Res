using System.Collections.Specialized;
using DragonPlus.Core;

namespace DragonPlus.DeepLinking.Event
{
    public class EventDeepLinking : IEvent
    {
        public string              Route   { get; private set; }
        public string              Title   { get; private set; }
        public string              Content { get; private set; }
        public NameValueCollection RawData { get; private set; }
        public string              Uri     { get; private set; }

        public EventDeepLinking(NameValueCollection nvc, string uri)
        {
            Route   = "";
            Title   = "";
            Content = "";
            RawData = null;
            Uri     = uri;

            if (nvc == null) return;

            foreach (var key in nvc.AllKeys)
            {
                switch (key)
                {
                    case "route":
                        Route = nvc.Get(key);
                        continue;
                    case "title":
                        Title = nvc.Get(key);
                        continue;
                    case "content":
                        Content = nvc.Get(key);
                        continue;
                }
            }

            RawData = nvc;
        }
    }
}