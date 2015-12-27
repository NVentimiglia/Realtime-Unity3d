// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------

using System;

namespace Realtime.Ortc
{

    #region Delegates

    /// <summary>
    ///     Occurs when the client connects to the gateway.
    /// </summary>
    public delegate void OnConnectedDelegate();

    /// <summary>
    ///     Occurs when the client disconnects from the gateway.
    /// </summary>
    public delegate void OnDisconnectedDelegate();

    /// <summary>
    ///     Occurs when the client subscribed to a channel.
    /// </summary>
    public delegate void OnSubscribedDelegate(string channel);

    /// <summary>
    ///     Occurs when the client unsubscribed from a channel.
    /// </summary>
    public delegate void OnUnsubscribedDelegate(string channel);

    /// <summary>
    ///     Occurs when there is an exception.
    /// </summary>
    public delegate void OnExceptionDelegate(Exception ex);

    /// <summary>
    ///     Occurs when the client attempts to reconnect to the gateway.
    /// </summary>
    public delegate void OnReconnectingDelegate();

    /// <summary>
    ///     Occurs when the client reconnected to the gateway.
    /// </summary>
    public delegate void OnReconnectedDelegate();

    /// <summary>
    ///     Occurs when the client receives a message in the specified channel.
    /// </summary>
    public delegate void OnMessageDelegate(string channel, string message);

    #endregion

    /// <summary>
    ///     Represents a <see cref="IOrtcClient" /> that connects to a specified gateway.
    /// </summary>
    public interface IOrtcClient : IDisposable
    {
        #region Properties

        /// <summary>
        ///     Gets or sets the client object identifier.
        /// </summary>
        /// <value>Object identifier.</value>
        string Id { get; }

        /// <summary>
        ///     Gets or sets the session id.
        /// </summary>
        /// <value>
        ///     The session id.
        /// </value>
        string SessionId { get; }

        /// <summary>
        ///     Gets a value indicating whether this client object is connected.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this client is connected; otherwise, <c>false</c>.
        /// </value>
        bool IsConnecting { get; }

        /// <summary>
        ///     Gets a value indicating whether this client object is connecting
        /// </summary>
        /// <value>
        ///     <c>true</c> if this client is connecting; otherwise, <c>false</c>.
        /// </value>
        bool IsConnected { get; }

        /// <summary>
        ///     Gets a value indicating whether this instance is clustered.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is cluster; otherwise, <c>false</c>.
        /// </value>
        bool IsCluster { get; }

        /// <summary>
        ///     Gets or sets a value indicating how many times can the client fail the heartbeat.
        /// </summary>
        /// <value>
        ///     Failure limit.
        /// </value>
        int HeartbeatFails { get; }

        /// <summary>
        ///     Gets or sets the gateway URL.
        /// </summary>
        /// <value>Gateway URL where the socket is going to connect.</value>
        string Url { get; set; }

        /// <summary>
        ///     Gets or sets the cluster gateway URL.
        /// </summary>
        string ClusterUrl { get; set; }

        /// <summary>
        ///     Gets or sets the connection timeout. Default value is 5000 miliseconds.
        /// </summary>
        int ConnectionTimeout { get; set; }

        /// <summary>
        ///     Gets or sets the client connection metadata.
        /// </summary>
        string ConnectionMetadata { get; set; }

        /// <summary>
        ///     Gets or sets the client announcement subchannel.
        /// </summary>
        string AnnouncementSubChannel { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether this client has a heartbeat activated.
        /// </summary>
        /// <value>
        ///     <c>true</c> if the heartbeat is active; otherwise, <c>false</c>.
        /// </value>
        bool HeartbeatActive { get; set; }

        /// <summary>
        ///     Gets or sets the heartbeat interval.
        /// </summary>
        /// <value>
        ///     Interval in seconds between heartbeats.
        /// </value>
        int HeartbeatTime { get; set; }

        /// <summary>
        ///     Enables / Disables automatic reconnect on disconnect.
        /// </summary>
        /// <value>
        ///     true if enabled
        /// </value>
        bool EnableReconnect { get; set; }

        #endregion

        #region Events (7)

        /// <summary>
        ///     Occurs when a connection attempt was successful.
        /// </summary>
        event OnConnectedDelegate OnConnected;


        /// <summary>
        ///     Occurs when the client connection terminated.
        /// </summary>
        event OnDisconnectedDelegate OnDisconnected;


        /// <summary>
        ///     Occurs when the client subscribed to a channel.
        /// </summary>
        event OnSubscribedDelegate OnSubscribed;

        /// <summary>
        ///     Occurs when the client unsubscribed from a channel.
        /// </summary>
        event OnUnsubscribedDelegate OnUnsubscribed;

        /// <summary>
        ///     Occurs when there is an error.
        /// </summary>
        event OnExceptionDelegate OnException;

        /// <summary>
        ///     Occurs when a client attempts to reconnect.
        /// </summary>
        event OnReconnectingDelegate OnReconnecting;

        /// <summary>
        ///     Occurs when a client reconnected.
        /// </summary>
        event OnReconnectedDelegate OnReconnected;

        #endregion

        #region Methods

        /// <summary>
        ///     Connects to the gateway with the application key and authentication token. The gateway must be set before using
        ///     this method.
        /// </summary>
        /// <param name="applicationKey">Your application key to use ORTC.</param>
        /// <param name="authenticationToken">Authentication token that identifies your permissions.</param>
        /// <example>
        ///     <code>
        /// ortcClient.Connect("myApplicationKey", "myAuthenticationToken");
        ///   </code>
        /// </example>
        void Connect(string applicationKey, string authenticationToken);

        /// <summary>
        ///     Disconnects from the gateway.
        /// </summary>
        /// <example>
        ///     <code>
        /// ortcClient.Disconnect();
        ///   </code>
        /// </example>
        void Disconnect();

        /// <summary>
        ///     Subscribes to a channel.
        /// </summary>
        /// <param name="channel">Channel name.</param>
        /// <param name="subscribeOnReconnected">Subscribe to the specified channel on reconnect.</param>
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
        void Subscribe(string channel, bool subscribeOnReconnected, OnMessageDelegate onMessage);

        /// <summary>
        ///     Unsubscribes from a channel.
        /// </summary>
        /// <param name="channel">Channel name.</param>
        /// <example>
        ///     <code>
        /// ortcClient.Unsubscribe("channelName");
        ///   </code>
        /// </example>
        void Unsubscribe(string channel);

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
        void Send(string channel, string message);

        /// <summary>
        ///     Not implemented
        /// </summary>
        /// <param name="applicationKey"></param>
        /// <param name="privateKey"></param>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        void SendProxy(string applicationKey, string privateKey, string channel, string message);

        /// <summary>
        ///     Indicates whether is subscribed to a channel.
        /// </summary>
        /// <param name="channel">The channel name.</param>
        /// <returns>
        ///     <c>true</c> if subscribed to the channel; otherwise, <c>false</c>.
        /// </returns>
        bool IsSubscribed(string channel);

        #endregion
    }
}