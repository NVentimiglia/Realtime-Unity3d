using System;
using System.Collections.Generic;
using System.Reflection;

namespace Realtime.Lobby
{
    /// <summary>
    /// Lobby Service message bus
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class LobbyMessenger<T> where T : class
    {
        public delegate void LobbyMessageDelegate(string channel, T message);

        /// <summary>
        /// Event
        /// </summary>
        public static event LobbyMessageDelegate OnMessage = delegate { };

        /// <summary>
        /// sends a message to subscriptions
        /// </summary>
        /// <param name="message"></param>
        public static void Publish(string channel, T message)
        {

            OnMessage(channel, message);
        }

        /// <summary>
        /// Adds a route
        /// </summary>
        public static void Subscribe(LobbyMessageDelegate handler)
        {
            OnMessage += handler;
        }

        /// <summary>
        /// removes a route
        /// </summary>
        public static void Unsubscribe(LobbyMessageDelegate handler)
        {
            OnMessage -= handler;
        }
    }

    /// <summary>
    /// Lobby Service message bus
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class LobbyMessenger
    {
        static Dictionary<Type, MethodInfo> _cache = new Dictionary<Type, MethodInfo>();

        public static void Publish(string channel, object message, Type type)
        {
            if (!_cache.ContainsKey(type))
            {
                _cache.Add(type, typeof(LobbyMessenger<>).MakeGenericType(type).GetMethod("Publish"));
            }

            _cache[type].Invoke(null, new[] { channel, message });

        }
    }
}
