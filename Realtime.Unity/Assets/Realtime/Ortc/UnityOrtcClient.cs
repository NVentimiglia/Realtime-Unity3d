// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Realtime.Ortc.Api;
using Realtime.Ortc.Internal;
using UnityEngine;

#if UNITY_WSA && !UNITY_EDITOR
using System.Collections.Concurrent;
#endif

namespace Realtime.Ortc
{
    /// <summary>
    ///     IBT Real Time SJ type client.
    /// </summary>
    public class UnityOrtcClient : IOrtcClient
    {
        #region Constants (11)

        // REGEX patterns
        private const string OPERATION_PATTERN = @"^a\[""{""op"":""(?<op>[^\""]+)"",(?<args>.*)}""\]$";
        private const string CLOSE_PATTERN = @"^c\[?(?<code>[^""]+),?""?(?<message>.*)""?\]?$";
        private const string VALIDATED_PATTERN = @"^(""up"":){1}(?<up>.*)?,""set"":(?<set>.*)$";
        private const string CHANNEL_PATTERN = @"^""ch"":""(?<channel>.*)""$";

        private const string EXCEPTION_PATTERN = @"^""ex"":{(""op"":""(?<op>[^""]+)"",)?(""ch"":""(?<channel>.*)"",)?""ex"":""(?<error>.*)""}$";

        private const string RECEIVED_PATTERN = @"^a\[""{""ch"":""(?<channel>.*)"",""m"":""(?<message>[\s\S]*)""}""\]$";

        private const string MULTI_PART_MESSAGE_PATTERN = @"^(?<messageId>.[^_]*)_(?<messageCurrentPart>.[^-]*)-(?<messageTotalPart>.[^_]*)_(?<message>[\s\S]*)$";

        private const string PERMISSIONS_PATTERN = @"""(?<key>[^""]+)"":{1}""(?<value>[^,""]+)"",?";


        // ReSharper disable InconsistentNaming

        /// <summary>
        ///     Message maximum size in bytes
        /// </summary>
        /// <exclude />
        public const int MAX_MESSAGE_SIZE = 700;

        /// <summary>
        ///     Channel maximum size in bytes
        /// </summary>
        /// <exclude />
        public const int MAX_CHANNEL_SIZE = 100;

        /// <summary>
        ///     Connection Metadata maximum size in bytes
        /// </summary>
        /// <exclude />
        public const int MAX_CONNECTION_METADATA_SIZE = 256;


        protected const int HEARTBEAT_MAX_TIME = 60;
        protected const int HEARTBEAT_MIN_TIME = 10;
        protected const int HEARTBEAT_MAX_FAIL = 6;
        protected const int HEARTBEAT_MIN_FAIL = 1;

        #endregion
        
        #region Attributes (17)

        private string _url;
        private string _clusterUrl;
        private string _applicationKey;
        private string _authenticationToken;

        private bool _alreadyConnectedFirstTime;
        private bool _forcedClosed;
        private bool _waitingServerResponse;

        // private int _sessionExpirationTime; // minutes

        private List<KeyValuePair<string, string>> _permissions;

#if !UNITY_WSA || UNITY_EDITOR
        private readonly RealtimeDictionary<string, ChannelSubscription> _subscribedChannels;
        private readonly RealtimeDictionary<string, RealtimeDictionary<int, BufferedMessage>> _multiPartMessagesBuffer;
#else
        private readonly ConcurrentDictionary<string, ChannelSubscription> _subscribedChannels;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, BufferedMessage>> _multiPartMessagesBuffer;
#endif

        private IWebSocketConnection _webSocketConnection;

        private readonly TaskTimer _reconnectTimer;
        private readonly TaskTimer _heartbeatTimer;

        #endregion

        #region Properties (9)

        public string Id { get; set; }

        public string SessionId { get; set; }

        public string Url
        {
            get { return _url; }
            set
            {
                IsCluster = false;
                _url = string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
            }
        }

        public string ClusterUrl
        {
            get { return _clusterUrl; }
            set
            {
                IsCluster = true;
                _clusterUrl = string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
            }
        }

        public int HeartbeatTime
        {
            get { return _heartbeatTimer.Interval; }
            set
            {
                _heartbeatTimer.Interval = value > HEARTBEAT_MAX_TIME
                    ? HEARTBEAT_MAX_TIME
                    : (value < HEARTBEAT_MIN_TIME ? HEARTBEAT_MIN_TIME : value);
            }
        }

        public int HeartbeatFails { get; set; }

        public int ConnectionTimeout { get; set; }

        public string ConnectionMetadata { get; set; }

        public string AnnouncementSubChannel { get; set; }

        public bool HeartbeatActive
        {
            get { return _heartbeatTimer.IsRunning; }
            set
            {
                if (value)
                    _heartbeatTimer.Start();
                else
                    _heartbeatTimer.Stop();
            }
        }

        public bool EnableReconnect { get; set; }

        public bool IsConnected { get; set; }

        public bool IsConnecting { get; set; }

        public bool IsCluster { get; set; }

        #endregion

        #region Events (7)

        /// <summary>
        ///     Occurs when a connection attempt was successful.
        /// </summary>
        public event OnConnectedDelegate OnConnected = delegate { };

        /// <summary>
        ///     Occurs when the client connection terminated.
        /// </summary>
        public event OnDisconnectedDelegate OnDisconnected = delegate { };

        /// <summary>
        ///     Occurs when the client subscribed to a channel.
        /// </summary>
        public event OnSubscribedDelegate OnSubscribed = delegate { };

        /// <summary>
        ///     Occurs when the client unsubscribed from a channel.
        /// </summary>
        public event OnUnsubscribedDelegate OnUnsubscribed = delegate { };

        /// <summary>
        ///     Occurs when there is an error.
        /// </summary>
        public event OnExceptionDelegate OnException = delegate { };

        /// <summary>
        ///     Occurs when a client attempts to reconnect.
        /// </summary>
        public event OnReconnectingDelegate OnReconnecting = delegate { };

        /// <summary>
        ///     Occurs when a client reconnected.
        /// </summary>
        public event OnReconnectedDelegate OnReconnected = delegate { };

        #endregion

        #region Constructor (1)

        static UnityOrtcClient()
        {
            //WSA Fix
            UnityOrtcStartup.ConfigureOrtc();
            RealtimeProxy.ConfirmInit();
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="UnityOrtcClient" /> class.
        /// </summary>
        public UnityOrtcClient()
        {
            _heartbeatTimer = new TaskTimer { Interval = 2, AutoReset = true };
            _heartbeatTimer.Elapsed += _heartbeatTimer_Elapsed;
            _heartbeatTimer.Start();

            _reconnectTimer = new TaskTimer { Interval = 2, AutoReset = false };
            _reconnectTimer.Elapsed += _reconnectTimer_Elapsed;

            IsCluster = true;
            Url = "http://ortc-developers.realtime.co/server/2.1";

            EnableReconnect = true;

            _permissions = new List<KeyValuePair<string, string>>();

#if !UNITY_WSA || UNITY_EDITOR
            _subscribedChannels = new RealtimeDictionary<string, ChannelSubscription>();
            _multiPartMessagesBuffer = new RealtimeDictionary<string, RealtimeDictionary<int, BufferedMessage>>();
#else
            _subscribedChannels = new ConcurrentDictionary<string, ChannelSubscription>();
            _multiPartMessagesBuffer = new ConcurrentDictionary<string, ConcurrentDictionary<int, BufferedMessage>>();
#endif
        }

        void CreateConnection(bool isSSL)
        {
            DisposeConnection();
           
#if UNITY_EDITOR
                _webSocketConnection = new DotNetConnection();
#elif UNITY_ANDROID
                _webSocketConnection = new DroidConnection();
#elif UNITY_WEBGL
                _webSocketConnection = new WebGLConnection();
#elif UNITY_WSA
                _webSocketConnection = new WSAConnection();
#elif UNITY_IOS
                //IOS does not support HTTP
                _webSocketConnection = isSSL ? (IWebSocketConnection) new IOSConnection() : (IWebSocketConnection) new DotNetConnection();
#else
                _webSocketConnection = new DotNetConnection();
#endif

            _webSocketConnection.OnOpened += _webSocketConnection_OnOpened;
            _webSocketConnection.OnClosed += _webSocketConnection_OnClosed;
            _webSocketConnection.OnError += _webSocketConnection_OnError;
            _webSocketConnection.OnMessage += WebSocketConnectionOnMessage;
        }

        void DisposeConnection()
        {
            if (_webSocketConnection == null) return;

            _webSocketConnection.OnOpened -= _webSocketConnection_OnOpened;
            _webSocketConnection.OnClosed -= _webSocketConnection_OnClosed;
            _webSocketConnection.OnError -= _webSocketConnection_OnError;
            _webSocketConnection.OnMessage -= WebSocketConnectionOnMessage;
            _webSocketConnection.Dispose();
            _webSocketConnection = null;
        }

        #endregion

        #region Public Methods (6)


        public void Dispose()
        {
            if (_webSocketConnection != null)
                _webSocketConnection.Dispose();
        }

        /// <summary>
        ///     Connects to the gateway with the application key and authentication token. The gateway must be set before using
        ///     this method.
        /// </summary>
        /// <param name="appKey">Your application key to use ORTC.</param>
        /// <param name="authToken">Authentication token that identifies your permissions.</param>
        /// <example>
        ///     <code>
        /// ortcClient.Connect("myApplicationKey", "myAuthenticationToken");
        ///   </code>
        /// </example>
        public void Connect(string appKey, string authToken)
        {
            Debug.LogFormat("Ortc.Connect key:{0} token:{1}", appKey, authToken);

            #region Sanity Checks

            if (IsConnected)
            {
                DelegateExceptionCallback(new OrtcException(OrtcExceptionReason.InvalidArguments, "Already connected"));
            }
            else if (IsConnecting)
            {
                DelegateExceptionCallback(new OrtcException(OrtcExceptionReason.InvalidArguments,
                    "Already trying to connect"));
            }
            else if (string.IsNullOrEmpty(ClusterUrl) && string.IsNullOrEmpty(Url))
            {
                DelegateExceptionCallback(new OrtcException(OrtcExceptionReason.InvalidArguments,
                    "URL and Cluster URL are null or empty"));
            }
            else if (string.IsNullOrEmpty(appKey))
            {
                DelegateExceptionCallback(new OrtcException(OrtcExceptionReason.InvalidArguments,
                    "Application Key is null or empty"));
            }
            else if (string.IsNullOrEmpty(authToken))
            {
                DelegateExceptionCallback(new OrtcException(OrtcExceptionReason.InvalidArguments,
                    "Authentication ToKen is null or empty"));
            }
            else if (!IsCluster && !Url.OrtcIsValidUrl())
            {
                DelegateExceptionCallback(new OrtcException(OrtcExceptionReason.InvalidArguments, "Invalid URL"));
            }
            else if (IsCluster && !ClusterUrl.OrtcIsValidUrl())
            {
                DelegateExceptionCallback(new OrtcException(OrtcExceptionReason.InvalidArguments, "Invalid Cluster URL"));
            }
            else if (!appKey.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcException(OrtcExceptionReason.InvalidArguments,
                    "Application Key has invalid characters"));
            }
            else if (!authToken.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcException(OrtcExceptionReason.InvalidArguments,
                    "Authentication Token has invalid characters"));
            }
            else if (AnnouncementSubChannel != null && !AnnouncementSubChannel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcException(OrtcExceptionReason.InvalidArguments,
                    "Announcement Subchannel has invalid characters"));
            }
            else if (!string.IsNullOrEmpty(ConnectionMetadata) &&
                     ConnectionMetadata.Length > MAX_CONNECTION_METADATA_SIZE)
            {
                DelegateExceptionCallback(new OrtcException(OrtcExceptionReason.InvalidArguments,
                    string.Format("Connection metadata size exceeds the limit of {0} characters",
                        MAX_CONNECTION_METADATA_SIZE)));
            }

            else

            #endregion

            {
                _forcedClosed = false;
                _authenticationToken = authToken;
                _applicationKey = appKey;

                DoConnect();
            }
        }

        /// <summary>
        ///     Sends a message to a channel.
        /// </summary>
        /// <param name="channel">Channel name.</param>
        /// <param name="message">Message to be sent.</param>
        /// <example>
        ///     <code>
        /// ortcClient.Send("channelName", "messageToSend");
        ///   </code>
        /// </example>
        public void Send(string channel, string message)
        {
            #region Sanity Checks

            if (!IsConnected)
            {
                DelegateExceptionCallback(new OrtcException("Not connected"));
            }
            else if (string.IsNullOrEmpty(channel))
            {
                DelegateExceptionCallback(new OrtcException("Channel is null or empty"));
            }
            else if (!channel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcException("Channel has invalid characters"));
            }
            else if (string.IsNullOrEmpty(message))
            {
                DelegateExceptionCallback(new OrtcException("Message is null or empty"));
            }
            else

            #endregion

            {
                var channelBytes = Encoding.UTF8.GetBytes(channel);

                if (channelBytes.Length > MAX_CHANNEL_SIZE)
                {
                    DelegateExceptionCallback(
                        new OrtcException(string.Format("Channel size exceeds the limit of {0} characters",
                            MAX_CHANNEL_SIZE)));
                }
                else
                {
                    var domainChannelCharacterIndex = channel.IndexOf(':');
                    var channelToValidate = channel;

                    if (domainChannelCharacterIndex > 0)
                    {
                        channelToValidate = channel.Substring(0, domainChannelCharacterIndex + 1) + "*";
                    }


                    var hash = GetChannelHash(channel, channelToValidate);

                    if (_permissions != null && _permissions.Count > 0 && string.IsNullOrEmpty(hash))
                    {
                        DelegateExceptionCallback(
                            new OrtcException(string.Format("No permission found to send to the channel '{0}'", channel)));
                    }
                    else
                    {

                        if (channel != string.Empty && message != string.Empty)
                        {
                            try
                            {
                                message = message.Replace(Environment.NewLine, "\n");
                                var messageBytes = Encoding.UTF8.GetBytes(message);
                                var messageParts = new List<string>();
                                var pos = 0;
                                int remaining;
                                var messageId = Strings.GenerateId(8);

                                // Multi part
                                while ((remaining = messageBytes.Length - pos) > 0)
                                {
                                    byte[] messagePart;

                                    if (remaining >= MAX_MESSAGE_SIZE - channelBytes.Length)
                                    {
                                        messagePart = new byte[MAX_MESSAGE_SIZE - channelBytes.Length];
                                    }
                                    else
                                    {
                                        messagePart = new byte[remaining];
                                    }

                                    Array.Copy(messageBytes, pos, messagePart, 0, messagePart.Length);

#if UNITY_WSA
                                    var b = (byte[])messagePart;
                                    messageParts.Add(Encoding.UTF8.GetString(b, 0, b.Length));
#else
                                    messageParts.Add(Encoding.UTF8.GetString(messagePart));
#endif

                                    pos += messagePart.Length;
                                }

                                for (var i = 0; i < messageParts.Count; i++)
                                {
                                    var s = string.Format("send;{0};{1};{2};{3};{4}", _applicationKey,_authenticationToken, channel, hash,string.Format("{0}_{1}-{2}_{3}", messageId, i + 1, messageParts.Count,messageParts[i]));

                                    DoSend(s);
                                }
                            }
                            catch (Exception ex)
                            {
                                string exName = null;

                                if (ex.InnerException != null)
                                {
                                    exName = ex.InnerException.GetType().Name;
                                }

                                switch (exName)
                                {
                                    case "OrtcNotConnectedException":
                                        // Server went down
                                        if (IsConnected)
                                        {
                                            DoDisconnect();
                                        }
                                        break;
                                    default:
                                        DelegateExceptionCallback(
                                            new OrtcException(string.Format("Unable to send: {0}", ex)));
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public void SendProxy(string applicationKey, string privateKey, string channel, string message)
        {
            #region Sanity Checks

            if (!IsConnected)
            {
                DelegateExceptionCallback(new OrtcException("Not connected"));
            }
            else if (string.IsNullOrEmpty(applicationKey))
            {
                DelegateExceptionCallback(new OrtcException("Application Key is null or empty"));
            }
            else if (string.IsNullOrEmpty(privateKey))
            {
                DelegateExceptionCallback(new OrtcException("Private Key is null or empty"));
            }
            else if (string.IsNullOrEmpty(channel))
            {
                DelegateExceptionCallback(new OrtcException("Channel is null or empty"));
            }
            else if (!channel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcException("Channel has invalid characters"));
            }
            else if (string.IsNullOrEmpty(message))
            {
                DelegateExceptionCallback(new OrtcException("Message is null or empty"));
            }
            else

            #endregion

            {
                var channelBytes = Encoding.UTF8.GetBytes(channel);

                if (channelBytes.Length > MAX_CHANNEL_SIZE)
                {
                    DelegateExceptionCallback(
                        new OrtcException(string.Format("Channel size exceeds the limit of {0} characters",
                            MAX_CHANNEL_SIZE)));
                }
                else
                {
                    message = message.Replace(Environment.NewLine, "\n");

                    if (channel != string.Empty && message != string.Empty)
                    {
                        try
                        {
                            var messageBytes = Encoding.UTF8.GetBytes(message);
                            var messageParts = new List<string>();
                            var pos = 0;
                            int remaining;
                            var messageId = Strings.GenerateId(8);

                            // Multi part
                            while ((remaining = messageBytes.Length - pos) > 0)
                            {
                                byte[] messagePart;

                                if (remaining >= MAX_MESSAGE_SIZE - channelBytes.Length)
                                {
                                    messagePart = new byte[MAX_MESSAGE_SIZE - channelBytes.Length];
                                }
                                else
                                {
                                    messagePart = new byte[remaining];
                                }

                                Array.Copy(messageBytes, pos, messagePart, 0, messagePart.Length);

#if UNITY_WSA
                                var b = (byte[])messagePart;
                                messageParts.Add(Encoding.UTF8.GetString(b, 0, b.Length));
#else
                                messageParts.Add(Encoding.UTF8.GetString(messagePart));
#endif

                                pos += messagePart.Length;
                            }

                            for (var i = 0; i < messageParts.Count; i++)
                            {
                                var s = string.Format("sendproxy;{0};{1};{2};{3}", applicationKey, privateKey, channel,
                                    string.Format("{0}_{1}-{2}_{3}", messageId, i + 1, messageParts.Count,
                                        messageParts[i]));

                                DoSend(s);
                            }
                        }
                        catch (Exception ex)
                        {
                            string exName = null;

                            if (ex.InnerException != null)
                            {
                                exName = ex.InnerException.GetType().Name;
                            }

                            switch (exName)
                            {
                                case "OrtcNotConnectedException":
                                    // Server went down
                                    if (IsConnected)
                                    {
                                        DoDisconnect();
                                    }
                                    break;
                                default:
                                    DelegateExceptionCallback(new OrtcException(string.Format("Unable to send: {0}", ex)));
                                    break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Subscribes to a channel.
        /// </summary>
        /// <param name="channel">Channel name.</param>
        /// <param name="onMessage"><see cref="OnMessageDelegate" /> callback.</param>
        /// <example>
        ///     <code>
        /// ortcClient.Subscribe("channelName", true, OnMessageCallback);
        /// private void OnMessageCallback(object sender, string channel, string message)
        /// {
        /// // Do something
        /// }
        ///   </code>
        /// </example>
        public void Subscribe(string channel, bool subscribeOnReconnected, OnMessageDelegate onMessage)
        {
            #region Sanity Checks

            var sanityChecked = true;

            if (!IsConnected)
            {
                DelegateExceptionCallback(new OrtcException("Not connected"));
                sanityChecked = false;
            }
            else if (string.IsNullOrEmpty(channel))
            {
                DelegateExceptionCallback(new OrtcException("Channel is null or empty"));
                sanityChecked = false;
            }
            else if (!channel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcException("Channel has invalid characters"));
                sanityChecked = false;
            }
            else if (_subscribedChannels.ContainsKey(channel))
            {
                ChannelSubscription channelSubscription = null;
                _subscribedChannels.TryGetValue(channel, out channelSubscription);

                if (channelSubscription != null)
                {
                    if (channelSubscription.IsSubscribing)
                    {
                        DelegateExceptionCallback(
                            new OrtcException(string.Format("Already subscribing to the channel {0}", channel)));
                        sanityChecked = false;
                    }
                    else if (channelSubscription.IsSubscribed)
                    {
                        DelegateExceptionCallback(
                            new OrtcException(string.Format("Already subscribed to the channel {0}", channel)));
                        sanityChecked = false;
                    }
                }
            }
            else
            {
                var channelBytes = Encoding.UTF8.GetBytes(channel);

                if (channelBytes.Length > MAX_CHANNEL_SIZE)
                {
                    if (_subscribedChannels.ContainsKey(channel))
                    {
                        ChannelSubscription channelSubscription = null;
                        _subscribedChannels.TryGetValue(channel, out channelSubscription);

                        if (channelSubscription != null)
                        {
                            channelSubscription.IsSubscribing = false;
                        }
                    }

                    DelegateExceptionCallback(
                        new OrtcException(string.Format("Channel size exceeds the limit of {0} characters",
                            MAX_CHANNEL_SIZE)));
                    sanityChecked = false;
                }
            }

            #endregion

            if (sanityChecked)
            {
                var domainChannelCharacterIndex = channel.IndexOf(':');
                var channelToValidate = channel;

                if (domainChannelCharacterIndex > 0)
                {
                    channelToValidate = channel.Substring(0, domainChannelCharacterIndex + 1) + "*";
                }

                var hash = GetChannelHash(channel, channelToValidate);

                if (_permissions != null && _permissions.Count > 0 && string.IsNullOrEmpty(hash))
                {
                    DelegateExceptionCallback(
                        new OrtcException(string.Format("No permission found to subscribe to the channel '{0}'", channel)));
                }
                else
                {
                    if (!_subscribedChannels.ContainsKey(channel))
                    {
                        _subscribedChannels.TryAdd(channel,
                            new ChannelSubscription
                            {
                                IsSubscribing = true,
                                IsSubscribed = false,
                                SubscribeOnReconnected = subscribeOnReconnected,
                                OnMessage = onMessage
                            });
                    }

                    try
                    {
                        if (_subscribedChannels.ContainsKey(channel))
                        {
                            ChannelSubscription channelSubscription = null;
                            _subscribedChannels.TryGetValue(channel, out channelSubscription);

                            channelSubscription.IsSubscribing = true;
                            channelSubscription.IsSubscribed = false;
                            channelSubscription.SubscribeOnReconnected = subscribeOnReconnected;
                            channelSubscription.OnMessage = onMessage;
                        }

                        var s = string.Format("subscribe;{0};{1};{2};{3}", _applicationKey, _authenticationToken,
                            channel, hash);
                        DoSend(s);
                    }
                    catch (Exception ex)
                    {
                        string exName = null;

                        if (ex.InnerException != null)
                        {
                            exName = ex.InnerException.GetType().Name;
                        }

                        switch (exName)
                        {
                            case "OrtcNotConnectedException":
                                // Server went down
                                if (IsConnected)
                                {
                                    DoDisconnect();
                                }
                                break;
                            default:
                                DelegateExceptionCallback(
                                    new OrtcException(string.Format("Unable to subscribe: {0}", ex)));
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Unsubscribes from a channel.
        /// </summary>
        /// <param name="channel">Channel name.</param>
        /// <example>
        ///     <code>
        /// ortcClient.Unsubscribe("channelName");
        ///   </code>
        /// </example>
        public void Unsubscribe(string channel)
        {
            #region Sanity Checks

            var sanityChecked = true;

            if (!IsConnected)
            {
                DelegateExceptionCallback(new OrtcException("Not connected"));
                sanityChecked = false;
            }
            else if (string.IsNullOrEmpty(channel))
            {
                DelegateExceptionCallback(new OrtcException("Channel is null or empty"));
                sanityChecked = false;
            }
            else if (!channel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcException("Channel has invalid characters"));
                sanityChecked = false;
            }
            else if (!_subscribedChannels.ContainsKey(channel))
            {
                DelegateExceptionCallback(new OrtcException(string.Format("Not subscribed to the channel {0}", channel)));
                sanityChecked = false;
            }
            else if (_subscribedChannels.ContainsKey(channel))
            {
                ChannelSubscription channelSubscription = null;
                _subscribedChannels.TryGetValue(channel, out channelSubscription);

                if (channelSubscription != null && !channelSubscription.IsSubscribed)
                {
                    DelegateExceptionCallback(
                        new OrtcException(string.Format("Not subscribed to the channel {0}", channel)));
                    sanityChecked = false;
                }
            }
            else
            {
                var channelBytes = Encoding.UTF8.GetBytes(channel);

                if (channelBytes.Length > MAX_CHANNEL_SIZE)
                {
                    DelegateExceptionCallback(
                        new OrtcException(string.Format("Channel size exceeds the limit of {0} characters",
                            MAX_CHANNEL_SIZE)));
                    sanityChecked = false;
                }
            }

            #endregion

            if (sanityChecked)
            {
                try
                {
                    var s = string.Format("unsubscribe;{0};{1}", _applicationKey, channel);
                    DoSend(s);
                }
                catch (Exception ex)
                {
                    string exName = null;

                    if (ex.InnerException != null)
                    {
                        exName = ex.InnerException.GetType().Name;
                    }

                    switch (exName)
                    {
                        case "OrtcNotConnectedException":
                            // Server went down
                            if (IsConnected)
                            {
                                DoDisconnect();
                            }
                            break;
                        default:
                            DelegateExceptionCallback(new OrtcException(string.Format("Unable to unsubscribe: {0}", ex)));
                            break;
                    }
                }
            }
        }

        /// <summary>
        ///     Disconnects from the gateway.
        /// </summary>
        /// <example>
        ///     <code>
        /// ortcClient.Disconnect();
        ///   </code>
        /// </example>
        public void Disconnect()
        {
            // Clear subscribed channels
            _subscribedChannels.Clear();

            #region Sanity Checks

            if (!IsConnecting && !IsConnected)
            {
                DelegateExceptionCallback(new OrtcException("Not connected"));
            }
            else

            #endregion

            {
                DoDisconnect();
            }
        }

        /// <summary>
        ///     Indicates whether is subscribed to a channel.
        /// </summary>
        /// <param name="channel">The channel name.</param>
        /// <returns>
        ///     <c>true</c> if subscribed to the channel; otherwise, <c>false</c>.
        /// </returns>
        public bool IsSubscribed(string channel)
        {
            var result = false;

            #region Sanity Checks

            if (!IsConnected)
            {
                DelegateExceptionCallback(new OrtcException("Not connected"));
            }
            else if (string.IsNullOrEmpty(channel))
            {
                DelegateExceptionCallback(new OrtcException("Channel is null or empty"));
            }
            else if (!channel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcException("Channel has invalid characters"));
            }
            else

            #endregion

            {
                result = false;

                if (_subscribedChannels.ContainsKey(channel))
                {
                    ChannelSubscription channelSubscription = null;
                    _subscribedChannels.TryGetValue(channel, out channelSubscription);

                    if (channelSubscription != null && channelSubscription.IsSubscribed)
                    {
                        result = true;
                    }
                }
            }

            return result;
        }

        protected List<string> SplitMessage(byte[] channelBytes, string message)
        {

            message = message.Replace(Environment.NewLine, "\n");

            var messageBytes = Encoding.UTF8.GetBytes(message);
            var messageParts = new List<string>();
            var pos = 0;
            int remaining;

            // Multi part
            while ((remaining = messageBytes.Length - pos) > 0)
            {
                var messagePart = remaining >= MAX_MESSAGE_SIZE - channelBytes.Length
                    ? new byte[MAX_MESSAGE_SIZE - channelBytes.Length]
                    : new byte[remaining];

                Array.Copy(messageBytes, pos, messagePart, 0, messagePart.Length);

#if UNITY_WSA
                messageParts.Add(Encoding.UTF8.GetString(messagePart, 0, messagePart.Length));
#else
                messageParts.Add(Encoding.UTF8.GetString(messagePart));
#endif

                pos += messagePart.Length;
            }

            return messageParts;
        }

        #endregion

        #region Private Methods (13)

        private string GetChannelHash(string channel, string channelToValidate)
        {
            foreach (var keyValuePair in _permissions)
            {
                if (keyValuePair.Key == channel)
                    return keyValuePair.Value;

                if (keyValuePair.Key == channelToValidate)
                    return keyValuePair.Value;
            }

            return null;
        }

        /// <summary>
        ///     Processes the operation validated.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        private void ProcessOperationValidated(string arguments)
        {
            if (!string.IsNullOrEmpty(arguments))
            {
                var isValid = false;

                // Try to match with authentication
                var validatedAuthMatch = Regex.Match(arguments, VALIDATED_PATTERN);

                if (validatedAuthMatch.Success)
                {
                    isValid = true;

                    var userPermissions = string.Empty;

                    if (validatedAuthMatch.Groups["up"].Length > 0)
                    {
                        userPermissions = validatedAuthMatch.Groups["up"].Value;
                    }

                    if (validatedAuthMatch.Groups["set"].Length > 0)
                    {
                        //  _sessionExpirationTime = int.Parse(validatedAuthMatch.Groups["set"].Value);
                    }

                    if (!string.IsNullOrEmpty(userPermissions) && userPermissions != "null")
                    {
                        var matchCollection = Regex.Matches(userPermissions, PERMISSIONS_PATTERN);

                        var permissions = new List<KeyValuePair<string, string>>();

                        foreach (Match match in matchCollection)
                        {
                            var channel = match.Groups["key"].Value;
                            var hash = match.Groups["value"].Value;

                            permissions.Add(new KeyValuePair<string, string>(channel, hash));
                        }

                        _permissions = new List<KeyValuePair<string, string>>(permissions);
                    }
                }

                if (isValid)
                {
                    IsConnecting = false;

                    if (_alreadyConnectedFirstTime)
                    {
                        var channelsToRemove = new List<string>();

                        // Subscribe to the previously subscribed channels
                        foreach (var item in _subscribedChannels)
                        {
                            var channel = item.Key;
                            var channelSubscription = item.Value;

                            // Subscribe again
                            if (channelSubscription.SubscribeOnReconnected &&
                                (channelSubscription.IsSubscribing || channelSubscription.IsSubscribed))
                            {
                                channelSubscription.IsSubscribing = true;
                                channelSubscription.IsSubscribed = false;

                                var domainChannelCharacterIndex = channel.IndexOf(':');
                                var channelToValidate = channel;

                                if (domainChannelCharacterIndex > 0)
                                {
                                    channelToValidate = channel.Substring(0, domainChannelCharacterIndex + 1) + "*";
                                }

                                var hash = GetChannelHash(channel, channelToValidate);

                                var s = string.Format("subscribe;{0};{1};{2};{3}", _applicationKey, _authenticationToken,
                                    channel, hash);

                                DoSend(s);
                            }
                            else
                            {
                                channelsToRemove.Add(channel);
                            }
                        }

                        for (var i = 0; i < channelsToRemove.Count; i++)
                        {
                            ChannelSubscription removeResult = null;
                            _subscribedChannels.TryRemove(channelsToRemove[i], out removeResult);
                        }

                        // Clean messages buffer (can have lost message parts in memory)
                        _multiPartMessagesBuffer.Clear();

                        DelegateReconnectedCallback();
                    }
                    else
                    {
                        _alreadyConnectedFirstTime = true;

                        // Clear subscribed channels
                        _subscribedChannels.Clear();

                        DelegateConnectedCallback();
                    }

                    if (arguments.IndexOf("busy") < 0)
                    {
                        StopReconnect();
                    }
                }
            }
        }

        /// <summary>
        ///     Processes the operation subscribed.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        private void ProcessOperationSubscribed(string arguments)
        {
            if (!string.IsNullOrEmpty(arguments))
            {
                var subscribedMatch = Regex.Match(arguments, CHANNEL_PATTERN);

                if (subscribedMatch.Success)
                {
                    var channelSubscribed = string.Empty;

                    if (subscribedMatch.Groups["channel"].Length > 0)
                    {
                        channelSubscribed = subscribedMatch.Groups["channel"].Value;
                    }

                    if (!string.IsNullOrEmpty(channelSubscribed))
                    {
                        ChannelSubscription channelSubscription = null;
                        _subscribedChannels.TryGetValue(channelSubscribed, out channelSubscription);

                        if (channelSubscription != null)
                        {
                            channelSubscription.IsSubscribing = false;
                            channelSubscription.IsSubscribed = true;
                        }

                        DelegateSubscribedCallback(channelSubscribed);
                    }
                }
            }
        }

        /// <summary>
        ///     Processes the operation unsubscribed.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        private void ProcessOperationUnsubscribed(string arguments)
        {
            // UnityEngine.Debug.Log("ProcessOperationUnsubscribed");
            if (!string.IsNullOrEmpty(arguments))
            {
                var unsubscribedMatch = Regex.Match(arguments, CHANNEL_PATTERN);

                if (unsubscribedMatch.Success)
                {
                    var channelUnsubscribed = string.Empty;

                    if (unsubscribedMatch.Groups["channel"].Length > 0)
                    {
                        channelUnsubscribed = unsubscribedMatch.Groups["channel"].Value;
                    }

                    if (!string.IsNullOrEmpty(channelUnsubscribed))
                    {
                        ChannelSubscription channelSubscription = null;
                        _subscribedChannels.TryGetValue(channelUnsubscribed, out channelSubscription);

                        if (channelSubscription != null)
                        {
                            channelSubscription.IsSubscribed = false;
                        }

                        DelegateUnsubscribedCallback(channelUnsubscribed);
                    }
                }
            }
        }

        /// <summary>
        ///     Processes the operation error.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        private void ProcessOperationError(string arguments)
        {
            if (!string.IsNullOrEmpty(arguments))
            {
                var errorMatch = Regex.Match(arguments, EXCEPTION_PATTERN);

                if (errorMatch.Success)
                {
                    var op = string.Empty;
                    var error = string.Empty;
                    var channel = string.Empty;

                    if (errorMatch.Groups["op"].Length > 0)
                    {
                        op = errorMatch.Groups["op"].Value;
                    }

                    if (errorMatch.Groups["error"].Length > 0)
                    {
                        error = errorMatch.Groups["error"].Value;
                    }

                    if (errorMatch.Groups["channel"].Length > 0)
                    {
                        channel = errorMatch.Groups["channel"].Value;
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        DelegateExceptionCallback(new OrtcException(error));
                    }

                    if (!string.IsNullOrEmpty(op))
                    {
                        switch (op)
                        {
                            case "validate":
                                if (!string.IsNullOrEmpty(error) &&
                                    (error.Contains("Unable to connect") || error.Contains("Server is too busy")))
                                {
                                    DelegateExceptionCallback(new Exception(error));
                                    DoReconnectOrDisconnect();
                                }
                                else
                                {
                                    DoDisconnect();
                                }
                                break;
                            case "subscribe":
                                if (!string.IsNullOrEmpty(channel))
                                {
                                    ChannelSubscription channelSubscription = null;
                                    _subscribedChannels.TryGetValue(channel, out channelSubscription);

                                    if (channelSubscription != null)
                                    {
                                        channelSubscription.IsSubscribing = false;
                                    }
                                }
                                break;
                            case "subscribe_maxsize":
                            case "unsubscribe_maxsize":
                            case "send_maxsize":
                                if (!string.IsNullOrEmpty(channel))
                                {
                                    ChannelSubscription channelSubscription = null;
                                    _subscribedChannels.TryGetValue(channel, out channelSubscription);

                                    if (channelSubscription != null)
                                    {
                                        channelSubscription.IsSubscribing = false;
                                    }
                                }

                                DoDisconnect();
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }


        private void ProcessOperationReceived(string message)
        {
            var receivedMatch = Regex.Match(message, RECEIVED_PATTERN);

            // Received
            if (receivedMatch.Success)
            {
                var channelReceived = string.Empty;
                var messageReceived = string.Empty;

                if (receivedMatch.Groups["channel"].Length > 0)
                {
                    channelReceived = receivedMatch.Groups["channel"].Value;
                }

                if (receivedMatch.Groups["message"].Length > 0)
                {
                    messageReceived = receivedMatch.Groups["message"].Value;
                }

                if (!string.IsNullOrEmpty(channelReceived) && !string.IsNullOrEmpty(messageReceived) &&
                    _subscribedChannels.ContainsKey(channelReceived))
                {
                    messageReceived =
                        messageReceived.Replace(@"\\n", Environment.NewLine)
                            .Replace("\\\\\"", @"""")
                            .Replace("\\\\\\\\", @"\");

                    // Multi part
                    var multiPartMatch = Regex.Match(messageReceived, MULTI_PART_MESSAGE_PATTERN);

                    var messageId = string.Empty;
                    var messageCurrentPart = 1;
                    var messageTotalPart = 1;
                    var lastPart = false;
#if !UNITY_WSA || UNITY_EDITOR
                    RealtimeDictionary<int, BufferedMessage> messageParts = null;
#else
                    ConcurrentDictionary<int, BufferedMessage> messageParts = null;
#endif

                    if (multiPartMatch.Success)
                    {
                        if (multiPartMatch.Groups["messageId"].Length > 0)
                        {
                            messageId = multiPartMatch.Groups["messageId"].Value;
                        }

                        if (multiPartMatch.Groups["messageCurrentPart"].Length > 0)
                        {
                            messageCurrentPart = int.Parse(multiPartMatch.Groups["messageCurrentPart"].Value);
                        }

                        if (multiPartMatch.Groups["messageTotalPart"].Length > 0)
                        {
                            messageTotalPart = int.Parse(multiPartMatch.Groups["messageTotalPart"].Value);
                        }

                        if (multiPartMatch.Groups["message"].Length > 0)
                        {
                            messageReceived = multiPartMatch.Groups["message"].Value;
                        }
                    }

                    lock (_multiPartMessagesBuffer)
                    {
                        // Is a message part
                        if (!string.IsNullOrEmpty(messageId))
                        {
                            if (!_multiPartMessagesBuffer.ContainsKey(messageId))
                            {
                                _multiPartMessagesBuffer.TryAdd(messageId,

#if !UNITY_WSA || UNITY_EDITOR
                                new RealtimeDictionary<int, BufferedMessage>());
#else
                                new ConcurrentDictionary<int, BufferedMessage>());
#endif
                            }


                            _multiPartMessagesBuffer.TryGetValue(messageId, out messageParts);

                            if (messageParts != null)
                            {
                                lock (messageParts)
                                {
                                    messageParts.TryAdd(messageCurrentPart,
                                        new BufferedMessage(messageCurrentPart, messageReceived));

                                    // Last message part
                                    if (messageParts.Count == messageTotalPart)
                                    {
                                        //messageParts.Sort();

                                        lastPart = true;
                                    }
                                }
                            }
                        }
                        // Message does not have multipart, like the messages received at announcement channels
                        else
                        {
                            lastPart = true;
                        }

                        if (lastPart)
                        {
                            if (_subscribedChannels.ContainsKey(channelReceived))
                            {
                                ChannelSubscription channelSubscription = null;
                                _subscribedChannels.TryGetValue(channelReceived, out channelSubscription);

                                if (channelSubscription != null)
                                {
                                    var ev = channelSubscription.OnMessage;

                                    if (ev != null)
                                    {
                                        if (!string.IsNullOrEmpty(messageId) &&
                                            _multiPartMessagesBuffer.ContainsKey(messageId))
                                        {
                                            messageReceived = string.Empty;
                                            //lock (messageParts)
                                            //{
                                            var bufferedMultiPartMessages = new List<BufferedMessage>();

                                            foreach (var part in messageParts.Keys)
                                            {
                                                bufferedMultiPartMessages.Add(messageParts[part]);
                                            }

                                            bufferedMultiPartMessages.Sort();

                                            foreach (var part in bufferedMultiPartMessages)
                                            {
                                                if (part != null)
                                                {
                                                    messageReceived = string.Format("{0}{1}", messageReceived,
                                                        part.Message);
                                                }
                                            }
                                            //}

                                            // Remove from messages buffer

#if !UNITY_WSA || UNITY_EDITOR
                                            RealtimeDictionary<int, BufferedMessage> removeResult = null;
#else
                                            ConcurrentDictionary<int, BufferedMessage> removeResult = null;
#endif

                                            _multiPartMessagesBuffer.TryRemove(messageId, out removeResult);
                                        }

                                        if (!string.IsNullOrEmpty(messageReceived))
                                        {
                                            RealtimeProxy.RunOnMainThread(
                                                () => { ev(channelReceived, messageReceived); });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Unknown
                DelegateExceptionCallback(new OrtcException(string.Format("Unknown message received: {0}", message)));
            }
        }
        
        /// <summary>
        ///     Do the Connect Task
        /// </summary>
        private void DoConnect()
        {
            IsConnecting = true;

            if (IsCluster)
            {
                ClusterClient.GetClusterServer(ClusterUrl, _applicationKey, (exception, s) =>
                {
                    if (exception != null || string.IsNullOrEmpty(s))
                    {
                        DoReconnectOrDisconnect();
                        DelegateExceptionCallback(new OrtcException("Connection Failed. Unable to get URL from cluster"));
                    }
                    else
                    {
                        Url = s;
                        IsCluster = true;
                        DoConnectInternal();
                    }
                });
            }
            else
            {
                DoConnectInternal();
            }
        }

        private void DoConnectInternal()
        {
            if (!string.IsNullOrEmpty(Url))
            {
                try
                {
                    CreateConnection(Url.StartsWith("https"));

                    IsConnecting = true;

                    _reconnectTimer.Start();

                    // use socket
                    _webSocketConnection.Open(Url);

                    // Just in case the server does not respond
                    _waitingServerResponse = true;
                }
                catch (Exception ex)
                {
                    DoReconnectOrDisconnect();
                    DelegateExceptionCallback(new OrtcException("Connection Failed. " + ex.Message));
                }
            }
        }

        /// <summary>
        ///     Disconnect the TCP client.
        /// </summary>
        private void DoDisconnect()
        {
            _forcedClosed = true;
            StopReconnect();

            if (IsConnecting || IsConnected || (_webSocketConnection != null && _webSocketConnection.IsOpen))
            {
                try
                {
                    IsConnecting = false;
                    IsConnected = false;
                    _webSocketConnection.Close();
                }
                catch (Exception ex)
                {
                    DelegateExceptionCallback(new OrtcException(string.Format("Error disconnecting: {0}", ex)));
                    DelegateDisconnectedCallback();
                    DisposeConnection();
                }
            }
            else
            {
                DelegateDisconnectedCallback();
                DisposeConnection();
            }
        }

        /// <summary>
        ///     Sends a message through the TCP client.
        /// </summary>
        /// <param name="message"></param>
        private void DoSend(string message)
        {
            try
            {
                _webSocketConnection.Send(message);
            }
            catch (Exception ex)
            {
                DelegateExceptionCallback(new OrtcException(string.Format("Unable to send: {0}", ex)));
            }
        }

        private void DoReconnectOrDisconnect()
        {
            if (EnableReconnect)
                DoReconnect();
            else
                DoDisconnect();
        }


        private void DoReconnect()
        {
            IsConnecting = true;

            _reconnectTimer.Start();
        }

        private void StopReconnect()
        {
            IsConnecting = false;
            _reconnectTimer.Stop();
        }

        #endregion

        #region Events handlers (6)

        private void _reconnectTimer_Elapsed()
        {
            if (!IsConnected)
            {
                if (_waitingServerResponse)
                {
                    _waitingServerResponse = false;
                    DelegateExceptionCallback(new OrtcException("Unable to connect"));
                }

                DelegateReconnectingCallback();
                DoConnect();
            }
        }

        private void _heartbeatTimer_Elapsed()
        {
            if (IsConnected)
            {
                DoSend("b");
            }
        }

        /// <summary>
        /// </summary>
        private void _webSocketConnection_OnOpened()
        {
            Debug.Log("Ortc.Opened");
        }

        /// <summary>
        /// </summary>
        private void _webSocketConnection_OnClosed()
        {
            IsConnected = false;

            if (!_forcedClosed && EnableReconnect)
                DoReconnect();
            else
                DoDisconnect();
        }

        /// <summary>
        /// </summary>
        /// <param name="error"></param>
        private void _webSocketConnection_OnError(string error)
        {
            DelegateExceptionCallback(new OrtcException(error));
        }

        /// <summary>
        /// </summary>
        /// <param name="message"></param>
        private void WebSocketConnectionOnMessage(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                // Open
                if (message == "o")
                {
                    try
                    {
                        SessionId = Strings.GenerateId(16);

                        string s;
                        if (HeartbeatActive)
                        {
                            s = string.Format("validate;{0};{1};{2};{3};{4};{5};{6}", _applicationKey,
                                _authenticationToken, AnnouncementSubChannel, SessionId,
                                ConnectionMetadata, HeartbeatTime, HeartbeatFails);
                        }
                        else
                        {
                            s = string.Format("validate;{0};{1};{2};{3};{4}", _applicationKey, _authenticationToken,
                                AnnouncementSubChannel, SessionId, ConnectionMetadata);
                        }
                        DoSend(s);
                    }
                    catch (Exception ex)
                    {
                        DelegateExceptionCallback(new OrtcException(string.Format("Exception sending validate: {0}", ex)));
                    }
                }
                // Heartbeat
                else if (message == "h")
                {
                    // Do nothing
                }
                else
                {
                    message = message.Replace("\\\"", @"""");

                    // UnityEngine.Debug.Log(message);

                    // Operation
                    var operationMatch = Regex.Match(message, OPERATION_PATTERN);

                    //   Debug.Log(operationMatch.Success);

                    if (operationMatch.Success)
                    {
                        var operation = operationMatch.Groups["op"].Value;
                        var arguments = operationMatch.Groups["args"].Value;

                        // Debug.Log(operation);
                        // Debug.Log(arguments);

                        switch (operation)
                        {
                            case "ortc-validated":
                                ProcessOperationValidated(arguments);
                                break;
                            case "ortc-subscribed":
                                ProcessOperationSubscribed(arguments);
                                break;
                            case "ortc-unsubscribed":
                                ProcessOperationUnsubscribed(arguments);
                                break;
                            case "ortc-error":
                                ProcessOperationError(arguments);
                                break;
                            default:
                                // Unknown operation
                                DelegateExceptionCallback(
                                    new OrtcException(string.Format(
                                        "Unknown operation \"{0}\" for the message \"{1}\"", operation, message)));
                                DoDisconnect();
                                break;
                        }
                    }
                    else
                    {
                        // Close
                        var closeOperationMatch = Regex.Match(message, CLOSE_PATTERN);

                        if (!closeOperationMatch.Success)
                        {
                            ProcessOperationReceived(message);
                        }
                    }
                }
            }
        }

        #endregion

        #region Events calls (7)

        private void DelegateConnectedCallback()
        {
            IsConnected = true;
            IsConnecting = false;
            _reconnectTimer.Stop();
            //_heartbeatTimer.Start();

            RealtimeProxy.RunOnMainThread(() =>
            {
                Debug.Log("Ortc.Connected");
                var ev = OnConnected;

                if (ev != null)
                {
                    ev();
                }
            });
        }

        private void DelegateDisconnectedCallback()
        {
            IsConnected = false;
            IsConnecting = false;
            _alreadyConnectedFirstTime = false;
            _reconnectTimer.Stop();
            //_heartbeatTimer.Stop();

            // Clear user permissions
            _permissions.Clear();

            DisposeConnection();

            RealtimeProxy.RunOnMainThread(() =>
            {
                Debug.Log("Ortc.Disconnected");
                var ev = OnDisconnected;
                if (ev != null)
                {
                    ev();
                }
            });
        }

        private void DelegateSubscribedCallback(string channel)
        {
            RealtimeProxy.RunOnMainThread(() =>
            {
                Debug.Log("Ortc.Subscribed " + channel);
                var ev = OnSubscribed;

                if (ev != null)
                {
                    ev(channel);
                }
            });
        }

        private void DelegateUnsubscribedCallback(string channel)
        {
            RealtimeProxy.RunOnMainThread(() =>
            {
                Debug.Log("Ortc.Unsubscribed " + channel);
                var ev = OnUnsubscribed;

                if (ev != null)
                {
                    ev(channel);
                }
            });
        }

        private void DelegateExceptionCallback(Exception ex)
        {
            RealtimeProxy.RunOnMainThread(() =>
            {
                var ev = OnException;
                if (ev != null)
                {
                    ev(ex);
                }
            });
        }

        private void DelegateReconnectingCallback()
        {
            RealtimeProxy.RunOnMainThread(() =>
            {
                Debug.Log("Ortc.Reconnecting");
                var ev = OnReconnecting;

                if (ev != null)
                {
                    ev();
                }
            });
        }

        private void DelegateReconnectedCallback()
        {
            IsConnected = true;
            IsConnecting = false;
            _reconnectTimer.Stop();

            RealtimeProxy.RunOnMainThread(() =>
            {
                Debug.Log("Ortc.Reconnected");
                var ev = OnReconnected;
                if (ev != null)
                {
                    ev();
                }
            });
        }

        #endregion
    }
}