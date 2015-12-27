// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------

#if UNITY_WSA && !UNITY_EDITOR
using Windows.Web;
using UnityEngine;
using Windows.Foundation;
using Windows.Storage.Streams;
using System.Threading;
using System;
using System.Collections.Generic;
using Windows.Networking.Sockets;
using Windows.Security.Credentials;
using Realtime.Ortc.Api;

namespace Realtime.Ortc.Internal
{
    public class WSAConnection : IWebSocketConnection
    {

        #region Events (4)

        public event OnOpenedDelegate OnOpened = delegate { };
        public event OnClosedDelegate OnClosed = delegate { };
        public event OnErrorDelegate OnError = delegate { };
        public event OnMessageDelegate OnMessage = delegate { };

        #endregion

        #region Events Handles (4)

        void MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (var reader = args.GetDataReader())
                {
                    reader.UnicodeEncoding = UnicodeEncoding.Utf8;
                    var read = reader.ReadString(reader.UnconsumedBufferLength);
                    OnMessage(read);
                }
            }
            catch
            {

            }

        }

        void Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            // You can add code to log or display the code and reason
            // for the closure (stored in args.Code and args.Reason)

            // This is invoked on another thread so use Interlocked 
            // to avoid races with the Start/Close/Reset methods.
            var webSocket = Interlocked.Exchange(ref streamWebSocket, null);
            if (webSocket != null)
            {
                webSocket.Dispose();
            }

            IsOpen = false;
            OnClosed();

            streamWebSocket = null;
        }

        void RaiseError(string m)
        {
            OnError(m);
        }

        #endregion

        #region Attributes (1)

        private MessageWebSocket pending;
        private MessageWebSocket streamWebSocket;
        private DataWriter messageWriter;

        public bool IsOpen { get; set; }

        #endregion

        #region Methods - Public (3)

        public async void Open(string url)
        {
            if (pending != null)
            {
                pending.Dispose();
            }

            //http://stackoverflow.com/questions/26675209/winrt-windows-store-apps-enforcing-to-use-tls12-instead-of-sslv3
            //System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            if (url.StartsWith("https"))
            {
                Debug.LogWarning("WSAConnection does not support ssl.");
                url = url.Replace("https", "http");
            }



            var connectionUrl = HttpUtility.GenerateConnectionEndpoint(url, false);

            try
            {
                pending = new MessageWebSocket();
                pending.Control.MessageType = SocketMessageType.Utf8;
                pending.Closed += Closed;
                pending.MessageReceived += MessageReceived;

                try
                {
                    var uri = new Uri(connectionUrl);
                    await pending.ConnectAsync(uri);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);

                    WebErrorStatus status = WebSocketError.GetStatus(ex.GetBaseException().HResult);
                    switch (status)
                    {
                        case WebErrorStatus.CannotConnect:
                            OnError("Can't connect" + ex.Message);
                            return;
                        case WebErrorStatus.NotFound:
                            OnError("Not  found" + ex.Message);
                            return;
                        case WebErrorStatus.RequestTimeout:
                            OnError("Request timeout" + ex.Message);
                            return;
                        default:
                            OnError("unknown " + ex.Message);
                            return;
                    }
                }

                streamWebSocket = pending;
                messageWriter = new DataWriter(pending.OutputStream);
                IsOpen = true;
                OnOpened();
            }
            catch
            {
                throw new OrtcException(OrtcExceptionReason.InvalidArguments, String.Format("Invalid URL: {0}", url));
            }
        }

        public void Close()
        {
            if (pending != null)
            {
                pending.Dispose();
                pending = null;
            }

            IsOpen = false;
            if (streamWebSocket != null)
            {
                streamWebSocket.Close(1000, "Normal closure");
                streamWebSocket.Dispose();
                streamWebSocket = null;
            }
        }

        public async void Send(string message)
        {
            if (streamWebSocket != null)
            {
                if (messageWriter != null)
                {
                    try
                    {
                        message = "\"" + message + "\"";
                        messageWriter.WriteString(message);
                        await ((IAsyncOperation<uint>)messageWriter.StoreAsync());
                    }
                    catch
                    {
                        RaiseError("Send failed");
                        Close();
                    }
                }
            }
        }

        #endregion

        public void Dispose()
        {
            Close();
        }
    }
}
#endif