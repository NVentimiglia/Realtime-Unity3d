// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------
#if UNITY_ANDROID
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Realtime.Ortc.Api;
using UnityEngine;

namespace Realtime.Ortc.Internal
{
    /// <summary>
    /// A Websocket connection via androidBridge application
    /// </summary>
    public class DroidConnection : IWebSocketConnection
    {
        #region client instance

        public bool IsOpen { get; set; }
        public int Id { get; private set; }

        public event OnClosedDelegate OnClosed = delegate { };
        public event OnErrorDelegate OnError = delegate { };
        public event OnMessageDelegate OnMessage = delegate { };
        public event OnOpenedDelegate OnOpened = delegate { };

        private readonly AndroidJavaClass _javaClass;
        private readonly AndroidJavaObject _javaObject;

        static readonly Dictionary<int, DroidConnection> Connections = new Dictionary<int, DroidConnection>();

        public DroidConnection()
        {
            _javaClass = new AndroidJavaClass("realtime.droidbridge.BridgeClient");
            _javaObject = _javaClass.CallStatic<AndroidJavaObject>("GetInstance");
            Id = _javaObject.Call<int>("GetId");
            Connections.Add(Id, this);
        }

        public void Close()
        {
            IsOpen = false;
            RealtimeProxy.RunOnMainThread(() =>
            {
                _javaObject.Call("Close");
            });
        }

        public void Open(string url)
        {
            var connectionUrl = HttpUtility.GenerateConnectionEndpoint(url, true);

            RealtimeProxy.RunOnMainThread(() =>
            {
                _javaObject.Call("Open", connectionUrl);
            });
        }

        public void Dispose()
        {
            Close();
        }

        public void Send(string message)
        {
            RealtimeProxy.RunOnMainThread(() =>
            {
                // Wrap in quotes, escape inner quotes
                _javaObject.Call("Send", string.Format("\"{0}\"", message.Replace("\"", "\\\"")));
            });
        }

        //Called by native

        protected void RaiseOpened()
        {
            IsOpen = true;
            OnOpened();
        }

        protected void RaiseClosed()
        {
            IsOpen = false;
            OnClosed();
        }

        protected void RaiseMessage(string message)
        {
            OnMessage(message);
        }

        protected void RaiseLog(string message)
        {
            Debug.Log(message);
        }

        protected void RaiseError(string message)
        {
            OnError(message);
        }

        #endregion

        #region native 
        
        protected delegate void NativeOpenedDelegate(int id);

        protected delegate void NativeClosedDelegate(int id);

        protected delegate void NativeMessageDelegate(int id, string m);

        protected delegate void NativeLogDelegate(int id, string m);

        protected delegate void NativeErrorDelegate(int id, string m);

        [DllImport("RealtimeDroid")]
        protected static extern void RegisterOpenedDelegate(NativeOpenedDelegate callback);

        [DllImport("RealtimeDroid")]
        protected static extern void RegisterClosedDelegate(NativeClosedDelegate callback);

        [DllImport("RealtimeDroid")]
        protected static extern void RegisterMessageDelegate(NativeMessageDelegate callback);

        [DllImport("RealtimeDroid")]
        protected static extern void RegisterLogDelegate(NativeLogDelegate callback);

        [DllImport("RealtimeDroid")]
        protected static extern void RegisterErrorDelegate(NativeErrorDelegate callback);

        static DroidConnection()
        {
            RegisterOpenedDelegate(OnNativeOpened);
            RegisterClosedDelegate(OnNativeClosed);
            RegisterMessageDelegate(OnNativeMessage);
            RegisterLogDelegate(OnNativeLog);
            RegisterErrorDelegate(OnNativeError);
        }

        [MonoPInvokeCallback(typeof(NativeOpenedDelegate))]
        protected static void OnNativeOpened(int id)
        {
            if (!Connections.ContainsKey(id))
            {
                Debug.LogError("Droid Client not found : " + id);
                return;
            }

            Connections[id].RaiseOpened();
        }

        [MonoPInvokeCallback(typeof(NativeClosedDelegate))]
        protected static void OnNativeClosed(int id)
        {
            if (!Connections.ContainsKey(id))
            {
                Debug.LogError("Droid Client not found : " + id);
                return;
            }

            Connections[id].RaiseClosed();
        }

        [MonoPInvokeCallback(typeof(NativeMessageDelegate))]
        protected static void OnNativeMessage(int id, string m)
        {
            if (!Connections.ContainsKey(id))
            {
                Debug.LogError("Droid Client not found : " + id);
                return;
            }

            Connections[id].RaiseMessage(m);
        }

        [MonoPInvokeCallback(typeof(NativeLogDelegate))]
        protected static void OnNativeLog(int id, string m)
        {
            if (!Connections.ContainsKey(id))
            {
                Debug.LogError("Droid Client not found : " + id);
                return;
            }

            Connections[id].RaiseLog(m);
        }

        [MonoPInvokeCallback(typeof(NativeErrorDelegate))]
        protected static void OnNativeError(int id, string m)
        {
            if (!Connections.ContainsKey(id))
            {
                Debug.LogError("Droid Client not found : " + id);
                return;
            }

            Connections[id].RaiseError(m);
        }


        #endregion
    }
}
#endif