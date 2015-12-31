// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------
#if UNITY_IOS || UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Realtime.Ortc.Api;
using UnityEngine;

namespace Realtime.Ortc.Internal
{
    /// <summary>
    /// A Websocket connection via ios bridge application
    /// </summary>
    public class IOSConnection : IWebSocketConnection
    {
        #region client instance

        public bool IsOpen { get; set; }
        public int Id { get; private set; }

        public event OnClosedDelegate OnClosed = delegate { };
        public event OnErrorDelegate OnError = delegate { };
        public event OnMessageDelegate OnMessage = delegate { };
        public event OnOpenedDelegate OnOpened = delegate { };

        static readonly Dictionary<int, IOSConnection> Connections = new Dictionary<int, IOSConnection>();
        
        public void Close()
        {
            IsOpen = false;

            Close(Id);
        }

        public void Open(string url)
        {
            var connectionUrl = HttpUtility.GenerateConnectionEndpoint(url, true);

            Id = Create(connectionUrl);

            Connections.Add(Id, this);
        }

        public void Dispose()
        {
            Close();
        }

        public void Send(string message)
        {
            // Wrap in quotes, escape inner quotes
            Send(Id, string.Format("\"{0}\"", message));
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

        [DllImport("__Internal")]
        protected static extern void RegisterOpenedDelegate(NativeOpenedDelegate callback);

        [DllImport("__Internal")]
        protected static extern void RegisterClosedDelegate(NativeClosedDelegate callback);

        [DllImport("__Internal")]
        protected static extern void RegisterMessageDelegate(NativeMessageDelegate callback);

        [DllImport("__Internal")]
        protected static extern void RegisterLogDelegate(NativeLogDelegate callback);

        [DllImport("__Internal")]
        protected static extern void RegisterErrorDelegate(NativeErrorDelegate callback);

        // Commands

        [DllImport("__Internal")]
        protected static extern void Init();
        
        [DllImport("__Internal")]
        protected static extern void Destroy(int id);

        [DllImport("__Internal")]
        protected static extern int Create(string url);

        [DllImport("__Internal")]
        protected static extern void Send(int clientId, string msg);

        [DllImport("__Internal")]
        protected static extern void Close(int clientId);


        static IOSConnection()
        {
            Init();
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