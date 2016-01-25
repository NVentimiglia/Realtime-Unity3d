// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Foundation.Debuging;
using Realtime.Ortc;
using Realtime.Ortc.Api;
using UnityEngine;

namespace Realtime.Demos
{
    /// <summary>
    /// Demo Client using the Ortc CLient
    /// </summary>
    [AddComponentMenu("Realtime/Demos/OrtcTest")]
    public class OrtcTest : MonoBehaviour
    {
        protected const string OrtcDisconnected = "ortcClientDisconnected";
        protected const string OrtcConnected = "ortcClientConnected";
        protected const string OrtcSubscribed = "ortcClientSubscribed";
        protected const string OrtcUnsubscribed = "ortcClientUnsubscribed";

        /// <summary>
        /// 
        /// </summary>
        public string URL = "http://ortc-developers.realtime.co/server/2.1";
    
        /// <summary>
        /// 
        /// </summary>
        public string URLSSL = "https://ortc-developers.realtime.co/server/ssl/2.1";

        /// <summary>
        /// Identifies the client.
        /// </summary>
        public string ClientMetaData = "UnityClient1";

        /// <summary>
        /// Identities your channel group
        /// </summary>
        public string ApplicationKey = "BsnG6J";

        /// <summary>
        /// Send / Subscribe channel
        /// </summary>
        public string Channel = "myChannel";

        /// <summary>
        /// Message Content
        /// </summary>
        public string Message = "This is my message";

        /// <summary>
        /// 15 second inactivity check
        /// </summary>
        public bool Heartbeat = false;

        /// <summary>
        /// For dedicated servers
        /// </summary>
        public bool ClientIsCluster = true;

        /// <summary>
        /// Automatic reconnection
        /// </summary>
        public bool EnableReconnect = true;

        #region auth settings

        // Note : this section should really be handled on a webserver you control. It is here only as education.

        /// <summary>
        /// Important
        /// Dont publish your app with this.
        /// This will allow users to authenticate themselves.
        /// Authentication should take place on your authentication server
        /// </summary>
        public string PrivateKey = "eH4nshYKQMYh";

        /// <summary>
        /// Approved channels
        /// </summary>
        public string[] AuthChannels = { "myChannel", "myChannel:sub" };

        /// <summary>
        /// Permission
        /// </summary>
        public ChannelPermissions[] AuthPermission = { ChannelPermissions.Presence, ChannelPermissions.Read, ChannelPermissions.Write };

        /// <summary>
        /// The token that the client uses to access the Pub/Sub network
        /// </summary>
        public string AuthToken = "UnityClient1";

        /// <summary> 
        /// Only one connection can use this token since it's private for each user
        /// </summary>
        public bool AuthTokenIsPrivate = false;

        /// <summary>
        /// Time to live. Expiration of the authentication token.
        /// </summary>
        public int AuthTTL = 1400;
        #endregion

        private IOrtcClient _ortc;

        void Awake()
        {
            UnityOrtcStartup.ConfigureOrtc();
        }

        protected void Start()
        {
            LoadFactory();
            LoadCommands();
        }

        void ReadText(string text)
        {
            if (text.StartsWith("."))
            {
                ClientMetaData = text.Replace(".", "");
                _ortc.ConnectionMetadata = ClientMetaData;
                Terminal.LogImportant("Name set to " + ClientMetaData);
            }
            else
            {
                if (!_ortc.IsConnected)
                {
                    Debug.LogError("Not Connected");
                }
                else
                {
                    Message = string.Format("{0} : {1}", ClientMetaData, text);
                    Send();
                }
            }
        }
        protected void OnApplicationPause(bool isPaused)
        {
            if (_ortc != null && _ortc.IsConnected)
                _ortc.Disconnect();
        }

        void LoadCommands()
        {
            Terminal.Add(new TerminalInterpreter
            {
                Label = "Chat",
                Method = ReadText
            });
            Terminal.Add(new TerminalCommand
            {
                Label = "Connect",
                Method = () => StartCoroutine(Connect())
            });

            Terminal.Add(new TerminalCommand
            {
                Label = "ConnectSSL",
                Method = () => StartCoroutine(ConnectSSL())
            });

            Terminal.Add(new TerminalCommand
            {
                Label = "Disconnect",
                Method = Disconnect
            });

            //

            Terminal.Add(new TerminalCommand
            {
                Label = "Subscribe",
                Method = Subscribe
            });

            Terminal.Add(new TerminalCommand
            {
                Label = "Unsubscribe",
                Method = Unsubscribe
            });
            Terminal.Add(new TerminalCommand
            {
                Label = "Send",
                Method = Send
            });

            Terminal.Add(new TerminalCommand
            {
                Label = "Subscribe SubChannels",
                Method = SubscribeeSubs
            });

            Terminal.Add(new TerminalCommand
            {
                Label = "Unsubscribe SubChannels",
                Method = UnsubscribeSubs
            });


            Terminal.Add(new TerminalCommand
            {
                Label = "Auth",
                Method = () => StartCoroutine(Auth())
            });

            //

            Terminal.Add(new TerminalCommand
            {
                Label = "EnablePresence",
                Method = () => StartCoroutine(EnablePresence())
            });


            Terminal.Add(new TerminalCommand
            {
                Label = "DisablePresense",
                Method = () => StartCoroutine(DisablePresence())
            });

            Terminal.Add(new TerminalCommand
            {
                Label = "Presence",
                Method = () => StartCoroutine(RequestPresence())
            });

            //


        }

        private void LoadFactory()
        {
            try
            {
                // Construct object
                _ortc = OrtcFactory.Create();

                if (_ortc != null)
                {
                    //_ortc.ConnectionTimeout = 10000;

                    // Handlers
                    _ortc.OnConnected += ortc_OnConnected;
                    _ortc.OnDisconnected += ortc_OnDisconnected;
                    _ortc.OnReconnecting += ortc_OnReconnecting;
                    _ortc.OnReconnected += ortc_OnReconnected;
                    _ortc.OnSubscribed += ortc_OnSubscribed;
                    _ortc.OnUnsubscribed += ortc_OnUnsubscribed;
                    _ortc.OnException += ortc_OnException;
                    Terminal.LogInput("Ortc Ready");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Debug.LogException(ex.InnerException);
            }

            if (_ortc == null)
            {

                Terminal.LogError("ORTC object is null");
            }
        }

        #region methods
        private void Log(string text)
        {
            var dt = DateTime.Now;
            const string datePatt = @"HH:mm:ss";

            Terminal.Log(String.Format("{0}: {1}", dt.ToString(datePatt), text));
        }

        IEnumerator Auth()
        {
            Log("Posting permissions...");
            yield return 1;

            var perms = new Dictionary<string, List<ChannelPermissions>>();

            foreach (var authChannel in AuthChannels)
            {
                perms.Add(authChannel, AuthPermission.ToList());
            }

            AuthenticationClient.PostAuthentication(URLSSL,
                ClientIsCluster, AuthToken, AuthTokenIsPrivate, ApplicationKey,
                AuthTTL, PrivateKey, perms, (exception, s) =>
                {
                    if (exception)
                        Terminal.LogError("Unable to post permissions");
                    else
                        Log("Permissions posted");
                });

        }

        IEnumerator Connect()
        {
            yield return 1;
            
            // Update URL with user entered text
            if (ClientIsCluster)
            {
                _ortc.ClusterUrl = URL;
            }
            else
            {
                _ortc.Url = URL;
            }


            _ortc.ConnectionMetadata = ClientMetaData;
            _ortc.HeartbeatActive = Heartbeat;
            _ortc.EnableReconnect = EnableReconnect;

            Log(String.Format("Connecting to: {0}...", URL));
            _ortc.Connect(ApplicationKey, AuthToken);
        }


        IEnumerator ConnectSSL()
        {
            yield return 1;

            // Update URL with user entered text
            if (ClientIsCluster)
            {
                _ortc.ClusterUrl = URLSSL;
            }
            else
            {
                _ortc.Url = URLSSL;
            }


            _ortc.ConnectionMetadata = ClientMetaData;
            _ortc.HeartbeatActive = Heartbeat;
            _ortc.EnableReconnect = EnableReconnect;

            Log(String.Format("Connecting to: {0}...", URLSSL));
            _ortc.Connect(ApplicationKey, AuthToken);
        }


        void Disconnect()
        {
            Log("Disconnecting...");
            _ortc.Disconnect();
        }

        void Subscribe()
        {
            Log(String.Format("Subscribing to: {0}...", Channel));

            _ortc.Subscribe(Channel, true, OnMessageCallback);
        }

        void Unsubscribe()
        {
            Log(String.Format("Unsubscribing from: {0}...", Channel));

            _ortc.Unsubscribe(Channel);
        }

        void SubscribeeSubs()
        {
            Log(String.Format("Subscribing to: {0}...", "Subchannels"));

            _ortc.Subscribe(OrtcConnected, true, OnMessageCallback);
            _ortc.Subscribe(OrtcDisconnected, true, OnMessageCallback);
            _ortc.Subscribe(OrtcSubscribed, true, OnMessageCallback);
            _ortc.Subscribe(OrtcUnsubscribed, true, OnMessageCallback);
        }

        void UnsubscribeSubs()
        {
            Log(String.Format("Unsubscribing from: {0}...", "Subchannels"));

            _ortc.Unsubscribe(OrtcConnected);
            _ortc.Unsubscribe(OrtcDisconnected);
            _ortc.Unsubscribe(OrtcSubscribed);
            _ortc.Unsubscribe(OrtcUnsubscribed);
        }

        IEnumerator RequestPresence()
        {
            yield return 1;

            PresenceClient.GetPresence(URLSSL, ClientIsCluster, ApplicationKey, AuthToken, Channel,
                (exception, presence) =>
                {
                    if (exception)
                    {
                        Terminal.LogError(String.Format("Error: {0}", exception.Message));
                    }
                    else
                    {
                        var result  = presence;

                        Log(String.Format("Subscriptions {0}", result.Subscriptions));

                        if (result.Metadata != null)
                        {
                            foreach (var metadata in result.Metadata)
                            {
                                Log(metadata.Key + " - " + metadata.Value);
                            }
                        }
                    }
                });
        }

        IEnumerator EnablePresence()
        {
            yield return 1;
            PresenceClient.EnablePresence(URLSSL, ClientIsCluster, ApplicationKey, PrivateKey, Channel, true,
                (exception, s) =>
                {

                    if (exception)
                        Terminal.LogError(String.Format("Error: {0}", exception.Message));
                    else
                        Log(s);
                });

        }

        IEnumerator DisablePresence()
        {
            yield return 1;
            PresenceClient.DisablePresence(URLSSL, ClientIsCluster, ApplicationKey, PrivateKey, Channel,
                (exception, s) =>
                {
                    if (exception)
                        Terminal.LogError(String.Format("Error: {0}", exception.Message));
                    else
                        Log(s);
                } );
        }


        void Send()
        {  // Parallel Task: Send
            Log("Send:" + Message + " to " + Channel);

            _ortc.Send(Channel, Message);
        }
        #endregion

        #region Events

        private void OnMessageCallback(string channel, string message)
        {
            switch (channel)
            {
                case OrtcConnected:
                    Log(String.Format("A client connected: {0}", message));
                    break;
                case OrtcDisconnected:
                    Log(String.Format("A client disconnected: {0}", message));
                    break;
                case OrtcSubscribed:
                    Log(String.Format("A client subscribed: {0}", message));
                    break;
                case OrtcUnsubscribed:
                    Log(String.Format("A client unsubscribed: {0}", message));
                    break;
                default:
                    Log(String.Format("[{0}] {1}", channel, message));
                    break;
            }
        }

        private void ortc_OnConnected()
        {
            Log(String.Format("Connected to: {0}", _ortc.Url));
            Log(String.Format("Connection metadata: {0}", _ortc.ConnectionMetadata));
            Log(String.Format("Session ID: {0}", _ortc.SessionId));
            Log(String.Format("Heartbeat: {0}", _ortc.HeartbeatActive ? "active" : "inactive"));
            if (_ortc.HeartbeatActive)
            {
                Log(String.Format("Heartbeat time: {0} Heartbeat fails: {1}", _ortc.HeartbeatTime, _ortc.HeartbeatFails));
            }
        }

        private void ortc_OnDisconnected()
        {
            Log("Disconnected");
        }

        private void ortc_OnReconnecting()
        {
            // Update URL with user entered text
            if (ClientIsCluster)
            {
                Log(String.Format("Reconnecting to: {0}", _ortc.ClusterUrl));
            }
            else
            {
                Log(String.Format("Reconnecting to: {0}", _ortc.Url));
            }
        }

        private void ortc_OnReconnected()
        {
            Log(String.Format("Reconnected to: {0}", _ortc.Url));
        }

        private void ortc_OnSubscribed(string channel)
        {
            Log(String.Format("Subscribed to: {0}", channel));
        }

        private void ortc_OnUnsubscribed(string channel)
        {
            Log(String.Format("Unsubscribed from: {0}", channel));
        }

        private void ortc_OnException(Exception ex)
        {
            Log(String.Format("Error: {0}", ex.Message));
        }

        #endregion

    }


}
