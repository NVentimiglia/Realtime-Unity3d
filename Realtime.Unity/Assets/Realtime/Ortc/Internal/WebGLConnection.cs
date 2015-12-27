// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------

#if UNITY_WEBGL
using System;
using System.Collections;
using Realtime.Ortc.Api;

namespace Realtime.Ortc.Internal
{
    public class WebGLConnection : IWebSocketConnection
    {
        #region Events (4)

        public event OnOpenedDelegate OnOpened = delegate { };
        public event OnClosedDelegate OnClosed = delegate { };
        public event OnErrorDelegate OnError = delegate { };
        public event OnMessageDelegate OnMessage = delegate { };

        #endregion

        #region Attributes (1)

        public bool IsOpen { get; set; }
        private WebGLBridge _websocket;
        private bool connected;
        #endregion

        public void Dispose()
        {
            if (_websocket != null)
                Close();
        }

        #region Methods - Public (3)

        public void Open(string url)
        {
            var connectionUrl = HttpUtility.GenerateConnectionEndpoint(url, true);

            if (_websocket != null)
                Dispose();

            UnityEngine.Debug.Log("WebGLConnection.Open...");

            _websocket = new WebGLBridge(new Uri(connectionUrl));

            RealtimeProxy.Instance.StartCoroutine(SocketAsync());
        }

        public IEnumerator SocketAsync()
        {
            UnityEngine.Debug.Log("WebGLConnection.SocketAsync...");

            yield return 1;

            yield return RealtimeProxy.Instance.StartCoroutine(_websocket.Connect());

            if (!string.IsNullOrEmpty(_websocket.error))
            {
                UnityEngine.Debug.Log("WebGLConnection.Connecting Failed !");
                OnError(_websocket.error);
                OnClosed();
                yield break;
            }

            UnityEngine.Debug.Log("WebGLConnection.Connecting Success !");
            connected = true;
            IsOpen = true;
            OnOpened();

            while (connected && _websocket != null)
            {
                var msg = _websocket.RecvString();
                if (!string.IsNullOrEmpty(msg))
                {
                    OnMessage(msg);
                }
                yield return 1;
            }
        }

        public void Close()
        {
            UnityEngine.Debug.Log("Close");
            connected = false;
            IsOpen = false;
            RealtimeProxy.Instance.StopCoroutine(SocketAsync());
            
            if (_websocket != null)
            {
                _websocket.Close();
                OnClosed();
                _websocket = null;
            }
        }

        public void Send(string message)
        {
            if (_websocket != null)
            {
                // Wrap in quotes, escape inner quotes
                _websocket.SendString(string.Format("\"{0}\"", message.Replace("\"", "\\\"")));
            }
        }

        #endregion
    }
}

#endif