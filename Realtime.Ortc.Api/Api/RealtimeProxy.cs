using System;
using System.Collections.Generic;
using UnityEngine;

namespace Realtime.Ortc.Api
{
    /// <summary>
    ///     Utility script for interfacing with unity
    /// </summary>
    [AddComponentMenu("Realtime/MessageProxy")]
    public class RealtimeProxy : MonoBehaviour
    {
        private static readonly object _syncLock = new object();
        private static readonly List<Action> _oneTime = new List<Action>();
        private static int counter;

        /// <summary>
        /// Enables passing callbacks to the main thread.
        /// </summary>
        /// <remarks>
        /// Turn this off if you want to mange threads yourself
        /// </remarks>
        public static bool EnableMainThreadCallbacks = true;
        
        private static RealtimeProxy _instance;
        public static RealtimeProxy Instance
        {
            get
            {
                if (_instance)
                    return _instance;

                var go = new GameObject("_RealtimeProxy");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<RealtimeProxy>();
                return _instance;
            }
        }

        static RealtimeProxy()
        {
            ConfirmInit();
        }

        /// <summary>
        /// Makes sure we have a proxy instance ready
        /// </summary>
        /// <returns></returns>
        public static RealtimeProxy ConfirmInit()
        {
            return Instance;
        }

        /// <summary>
        ///     Run an action on the main thread
        /// </summary>
        /// <param name="action"></param>
        public static void RunOnMainThread(Action action)
        {
            if (!EnableMainThreadCallbacks)
            {
                action();
                return;
            }

            lock (_syncLock)
            {
                _oneTime.Add(action);
            }
        }
        
        
        protected void Update()
        {
            Action[] actions;
            lock (_syncLock)
            {
                actions = _oneTime.ToArray();
                _oneTime.Clear();
            }

            for (var i = 0; i < actions.Length; i++)
            {
                try
                {
                    actions[i]();
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        }
    }
}