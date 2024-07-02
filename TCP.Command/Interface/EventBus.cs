using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace TCP.Command.Interface
{

    public static class EventTypes
    {
        public const string SrateChanged = "SrateChanged";
    }


    public class EventBus
    {
        private static readonly Lazy<EventBus> instance = new Lazy<EventBus>(() => new EventBus());

        public static EventBus Instance => instance.Value;

        private readonly Dictionary<string, List<Delegate>> subscribers = new Dictionary<string, List<Delegate>>();

        private EventBus() { }

        public void Subscribe<T>(string eventType, Action<T> handler)
        {
            if (!subscribers.ContainsKey(eventType))
            {
                subscribers[eventType] = new List<Delegate>();
            }
            subscribers[eventType].Add(handler);
        }

        public void Unsubscribe<T>(string eventType, Action<T> handler)
        {
            if (subscribers.ContainsKey(eventType))
            {
                subscribers[eventType].Remove(handler);
            }
        }

        public void Publish<T>(string eventType, T data)
        {
            if (subscribers.ContainsKey(eventType))
            {
                foreach (var handler in subscribers[eventType])
                {
                    (handler as Action<T>)?.Invoke(data);
                }
            }
        }
    }

}
