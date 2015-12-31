// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------

#if UNITY_EDITOR || (!UNITY_WSA && !UNITY_WEBGL)
using System;
using WebSocketSharp;

namespace Realtime.Ortc.Internal
{
    public class DotNetConnection : IWebSocketConnection
    {
        #region Attributes (1)

        private WebSocketSharp.WebSocket _websocket;
        public bool IsOpen { get; set; }

        #endregion

        public void Dispose()
        {
            if (_websocket != null)
            {
                if (_websocket.IsAlive)
                    _websocket.Close();
                _websocket.OnOpen -= _websocket_OnOpen;
                _websocket.OnError -= _websocket_OnError;
                _websocket.OnClose -= _websocket_OnClose;
                _websocket.OnMessage -= _websocket_OnMessage;
            }
            IsOpen = false;
            _websocket = null;
        }

        #region Methods - Public (3)

        public void Open(string url)
        {
            if (url.StartsWith("https"))
            {
                UnityEngine.Debug.LogWarning("DotNetConnection does not support ssl. This is a limitation of mono.");
                url = url.Replace("https", "http");
            }

            var connectionUrl = HttpUtility.GenerateConnectionEndpoint(url, false);

            if (_websocket != null)
                Dispose();
            try
            {

                _websocket = new WebSocketSharp.WebSocket(connectionUrl);

                _websocket.OnOpen += _websocket_OnOpen;
                _websocket.OnError += _websocket_OnError;
                _websocket.OnClose += _websocket_OnClose;
                _websocket.OnMessage += _websocket_OnMessage;

                _websocket.Connect();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                OnError(ex.Message);
            }
        }

        public void Close()
        {
            IsOpen = false;
            if (_websocket != null)
            {
                _websocket.Close();
            }
        }

        public void Send(string message)
        {
            if (_websocket != null)
            {
                // Wrap in quotes, escape inner quotes
                _websocket.Send(string.Format("\"{0}\"", message));
            }
        }

        #endregion

        #region Methods - Private (1)

        #endregion

        #region Events (4)

        public event OnOpenedDelegate OnOpened = delegate { };
        public event OnClosedDelegate OnClosed = delegate { };
        public event OnErrorDelegate OnError = delegate { };
        public event OnMessageDelegate OnMessage = delegate { };

        #endregion

        #region Events Handles (4)


        private void _websocket_OnMessage(object sender, MessageEventArgs e)
        {
            OnMessage(e.Data);
        }

        private void _websocket_OnClose(object sender, CloseEventArgs e)
        {
            IsOpen = false;
            OnClosed();
        }

        private void _websocket_OnError(object sender, ErrorEventArgs e)
        {
            OnError(e.Message);
        }

        private void _websocket_OnOpen(object sender, EventArgs e)
        {
            IsOpen = true;
            OnOpened();
        }

        #endregion
    }
}

#endif