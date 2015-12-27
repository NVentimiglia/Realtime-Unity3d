using System;
using System.Collections.Generic;
using System.Linq;
using Realtime.Ortc;
using Realtime.Ortc.Api;
using UnityEngine;

namespace Realtime.Lobby
{

    /// <summary>
    /// Wraps the ortc client to provide common game-play functionality such as found in uNet
    /// </summary>
    public class LobbyService
    {
        #region subs
        protected const string LOBBY = "lobby";
        protected const string OrtcDisconnected = "ortcClientDisconnected";
        protected const string OrtcConnected = "ortcClientConnected";
        protected const string OrtcSubscribed = "ortcClientSubscribed";
        protected const string OrtcUnsubscribed = "ortcClientUnsubscribed";
        protected const string Seperator = "-ortc-";

        /// <summary>
        /// Internal announcment scheme
        /// </summary>
        protected class OrtcAnnouncement
        {
            /// <summary>
            /// User Metadata
            /// </summary>
            public string cm;
        }

        public enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected,
            Reconnecting,
            Disconnecting
        }
        #endregion

        #region events
        /// <summary>
        /// Connection state. eg : connected, reconnecting
        /// </summary>
        public event Action<ConnectionState> OnState = delegate { };

        /// <summary>
        /// Raised when a new room is available. This is in response to the FindRoom request
        /// </summary>
        public event Action<RoomFindResponse> OnRoomFound = delegate { };

        /// <summary>
        /// when a peer joins to the lobby
        /// </summary>
        public event Action<UserDetails> OnLobbyUserAdd = delegate { };

        /// <summary>
        /// when a peer leaves the lobby
        /// </summary>
        public event Action<UserDetails> OnLobbyUserRemove = delegate { };

        /// <summary>
        /// when a peer is added to the room
        /// </summary>
        public event Action<UserDetails> OnRoomUserAdd = delegate { };

        /// <summary>
        /// when a peer is removed from the room
        /// </summary>
        public event Action<UserDetails> OnRoomUserRemove = delegate { };

        /// <summary>
        /// Raised when self joins the lobby
        /// </summary>
        public event Action OnLobbyEnter = delegate { };

        /// <summary>
        /// Raised when self leaves the lobby
        /// </summary>
        public event Action OnLobbyExit = delegate { };

        /// <summary>
        /// Raised when self leaves a room
        /// </summary>
        public event Action OnRoomExit = delegate { };

        /// <summary>
        /// Raised when self joins a room
        /// </summary>
        public event Action<RoomDetails> OnRoomEnter = delegate { };

        /// <summary>
        /// Raised when the room is updated. eg, authority changes
        /// </summary>
        public event Action<RoomDetails> OnRoomUpdate = delegate { };

        #endregion

        #region properties
        /// <summary>
        /// Current connection state
        /// </summary>
        public ConnectionState State { get; private set; }

        /// <summary>
        /// Self
        /// </summary>
        public UserDetails User { get; private set; }

        /// <summary>
        /// In the lobby channel ?
        /// </summary>
        public bool InLobby { get; set; }

        /// <summary>
        /// All lobby users including self
        /// </summary>
        public List<UserDetails> LobbyUsers { get; set; }

        /// <summary>
        /// In a room
        /// </summary>
        public bool InRoom { get; private set; }

        /// <summary>
        /// Current room
        /// </summary>
        public RoomDetails Room { get; private set; }

        /// <summary>
        /// Users in the room
        /// </summary>
        public List<UserDetails> RoomUsers { get; set; }

        /// <summary>
        /// Current Authority
        /// </summary>
        public UserDetails RoomAuthority { get; protected set; }

        #endregion

        #region methods

        #region init

        /// <summary>
        /// Static instance
        /// </summary>
        public static LobbyService Instance { get; protected set; }

        /// <summary>
        /// Initializer
        /// </summary>
        /// <param name="client"></param>
        /// <param name="appKey"></param>
        /// <param name="privateKey"></param>
        /// <param name="url"></param>
        /// <param name="isCluster"></param>
        /// <returns></returns>
        public static LobbyService Init(IOrtcClient client, string appKey, string privateKey, string url, bool isCluster)
        {
            Instance = new LobbyService(client, appKey, privateKey, url, isCluster);
            return Instance;
        }

        /// <summary>
        /// Connect
        /// </summary>
        public void Connect(string authToken, UserDetails self, Action<ConnectionState> callback)
        {
            if (State == ConnectionState.Connected)
            {
                Debug.LogError("Already Connected !");
                return;
            }

            AuthKey = authToken;
            User = self;
            connectCallback = callback;

            _client.ConnectionMetadata = User.UserId;

            State = ConnectionState.Connecting;
            OnState(State);

            EnablePresence(LOBBY);

            _client.Connect(AppKey, AuthKey);
        }

        public void Disconnect()
        {
            if (State == ConnectionState.Disconnected)
            {
                Debug.LogError("Already Disconnected !");
                return;
            }

            if (State == ConnectionState.Connected)
            {
                var dto = LobbyMessage.GetDefault<UserLeaveMessage>();
                if (InLobby)
                {
                    dto.UserId = User.UserId;
                    dto.RoomId = LOBBY;
                    SendRPC(LOBBY, dto);
                }
                if (InRoom)
                {
                    dto.UserId = User.UserId;
                    dto.RoomId = Room.RoomId;
                    SendRPC(Room.RoomId, dto);
                }
            }

            State = ConnectionState.Disconnecting;
            OnState(State);
            User = null;
            InRoom = false;
            InLobby = false;
            Room = null;
            _client.Disconnect();
        }

        #endregion

        #region Lobby

        /// <summary>
        /// Sends a request for rooms
        /// </summary>
        public void FindRooms(Action<RoomFindResponse> callback)
        {
            if (State != ConnectionState.Connected)
            {
                Debug.LogError("Invalid connection state");
                return;
            }

            if (!InLobby)
            {
                Debug.LogError("Not in the lobby !");
                return;
            }

            findCallback = callback;

            var m = LobbyMessage.GetDefault<RoomFindRequest>();
            m.UserId = User.UserId;

            SendRPC(LOBBY, m);
        }

        /// <summary>
        /// Subscribe
        /// </summary>
        /// <param name="callback"></param>
        public void JoinLobby(Action<bool> callback)
        {
            if (State != ConnectionState.Connected)
            {
                Debug.LogError("Invalid connection state");
                return;
            }
            lobbyJoinCallback = callback;

            EnablePresence(LOBBY);

            _client.Subscribe(LOBBY, true, OnOrtcMessage);

        }

        /// <summary>
        ///  Unsubscribes from the lobby
        /// </summary>
        public void LeaveLobby()
        {
            if (State != ConnectionState.Connected)
            {
                Debug.LogError("Invalid connection state");
                return;
            }

            if (!InLobby)
            {
                Debug.LogError("Not in the lobby !");
                return;
            }


            var m = LobbyMessage.GetDefault<UserLeaveMessage>();
            m.RoomId = LOBBY;
            m.UserId = User.UserId;

            SendRPC(LOBBY, m);

            _client.Unsubscribe(LOBBY);

            LobbyUsers.Clear();
        }
        #endregion

        #region Room

        /// <summary>
        /// Subscribes to the lobby
        /// </summary>
        /// <param name="room"></param>
        /// <param name="callback"></param>
        public void JoinRoom(RoomDetails room, Action<bool> callback)
        {
            if (State != ConnectionState.Connected)
            {
                Debug.LogError("Invalid connection state");
                return;
            }

            if (InRoom)
            {
                Debug.LogError("Already in a room !");
                return;
            }

            if (_pendingRoom != null)
            {
                Debug.LogError("Already joining a room !");
                return;
            }


            roomJoinCallback = callback;

            _pendingRoom = room;
            _client.Subscribe(room.RoomId, true, OnOrtcMessage);
        }

        /// <summary>
        /// Creates a new room
        /// </summary>
        /// <param name="name">friendly name</param>
        /// <param name="callback"></param>
        public void CreateRoom(string name, Action<bool> callback)
        {
            if (State != ConnectionState.Connected)
            {
                Debug.LogError("Invalid connection state");
                return;
            }

            if (InRoom)
            {
                Debug.LogError("Already in a room !");
                return;
            }
            _pendingRoom = new RoomDetails
            {
                RoomId = Guid.NewGuid().ToString(),
                RoomName = name,
            };

            EnablePresence(_pendingRoom.RoomId);

            roomJoinCallback = callback;

            _client.Subscribe(_pendingRoom.RoomId, true, OnOrtcMessage);
        }

        /// <summary>
        /// Updates the room
        /// </summary>
        /// <param name="name">friendly name</param>
        /// <param name="properties"></param>
        public void UpdateRoom(string name, Dictionary<string, string> properties)
        {
            if (State != ConnectionState.Connected)
            {
                Debug.LogError("Invalid connection state");
                return;
            }

            if (!InRoom)
            {
                Debug.LogError("Not in a room !");
                return;
            }

            Room.RoomName = name;

            var details = LobbyMessage.GetDefault<RoomFindResponse>();
            details.Room = Room;
            details.Users = RoomUsers.ToArray();

        }

        /// <summary>
        /// leave room
        /// </summary>
        public void LeaveRoom()
        {
            if (!InRoom)
            {
                Debug.LogError("Not in a room !"); return;
            }

            var m = LobbyMessage.GetDefault<UserLeaveMessage>();
            m.RoomId = Room.RoomId;
            m.UserId = User.UserId;

            SendRPC(Room.RoomId, m);

            _client.Unsubscribe(Room.RoomId);

            _pendingRoom = null;

            InRoom = false;
            RoomUsers.Clear();
            RoomAuthority = null;
        }

        /// <summary>
        /// Is self authority
        /// </summary>
        /// <returns></returns>
        public bool IsAuthority()
        {
            return IsAuthority(User);
        }

        /// <summary>
        /// Is user the authority
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool IsAuthority(UserDetails user)
        {
            return user.IsAuthority;
        }
        #endregion

        #region sending

        /// <summary>
        /// Sends a [LobbyMessage] object
        /// </summary>
        /// <param name="message"></param>
        public void SendRoomRPC(LobbyMessage message)
        {
            if (!InRoom)
            {
                Debug.LogError("Not in a room !"); return;
            }

            SendRPC(Room.RoomId, message);

        }

        /// <summary>
        /// send a chat message
        /// </summary>
        /// <param name="message"></param>
        public void SendRoomChat(string message)
        {
            if (!InRoom)
            {
                Debug.LogError("Not in a room !"); return;
            }

            var m = LobbyMessage.GetDefault<ChatMessage>();
            m.UserId = User.UserId;
            m.Content = message;

            SendRPC(Room.RoomId, m);

        }

        /// <summary>
        /// Sends a [LobbyMessage] object
        /// </summary>
        public void SendUserRPC(string userId, LobbyMessage message)
        {
            if (State != ConnectionState.Connected)
            {
                Debug.LogError("Invalid connection state");
                return;
            }


            SendRPC(LOBBY, message);
        }

        /// <summary>
        /// send a chat message
        /// </summary>
        public void SendUserChat(string userId, string message)
        {
            if (State != ConnectionState.Connected)
            {
                Debug.LogError("Invalid connection state");
                return;
            }

            var m = LobbyMessage.GetDefault<ChatMessage>();
            m.UserId = User.UserId;
            m.Content = message;

            SendRPC(userId, m);

        }

        /// <summary>
        /// Sends a [LobbyMessage] object
        /// </summary>
        public void SendLobbyRPC(LobbyMessage message)
        {
            if (!InLobby)
            {
                Debug.LogError("Not in a lobby !"); return;
            }

            SendRPC(LOBBY, message);

        }

        /// <summary>
        /// send a chat message
        /// </summary>
        public void SendLobbyChat(string message)
        {
            if (!InLobby)
            {
                Debug.LogError("Not in a lobby !"); return;
            }

            var m = LobbyMessage.GetDefault<ChatMessage>();
            m.UserId = User.UserId;
            m.Content = message;

            SendRPC(LOBBY, m);

        }

        #endregion

        #region receiving

        /// <summary>
        /// Add a listener
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="callback"></param>
        public void Subscribe<T>(LobbyMessenger<T>.LobbyMessageDelegate callback) where T : LobbyMessage
        {
            LobbyMessenger<T>.Subscribe(callback);
        }

        /// <summary>
        /// Remove a listener
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="callback"></param>
        public void Unsubscribe<T>(LobbyMessenger<T>.LobbyMessageDelegate callback) where T : LobbyMessage
        {
            LobbyMessenger<T>.Unsubscribe(callback);
        }

        #endregion

        #endregion

        #region internal


        IOrtcClient _client;

        public string AuthKey { get; set; }
        public string AppKey { get; set; }
        public string PrivateKey { get; set; }
        public string Url { get; set; }
        public bool IsCluster { get; set; }

        Action<bool> roomJoinCallback;
        Action<bool> lobbyJoinCallback;
        Action<RoomFindResponse> findCallback;
        Action<ConnectionState> connectCallback;
        RoomDetails _pendingRoom;

        LobbyService(IOrtcClient client, string appKey, string privateKey, string url, bool isCluster)
        {
            AppKey = appKey;
            PrivateKey = privateKey;
            Url = url;
            IsCluster = isCluster;

            _client = client;

            if (isCluster)
            {
                _client.ClusterUrl = url;
            }
            else
            {
                _client.Url = url;
            }

            _client.OnException += _client_OnException;
            _client.OnConnected += _client_OnConnected;
            _client.OnDisconnected += _client_OnDisconnected;
            _client.OnReconnected += _client_OnReconnected;
            _client.OnReconnecting += _client_OnReconnecting;
            _client.OnSubscribed += _client_OnSubscribed;
            _client.OnUnsubscribed += _client_OnUnsubscribed;

            MapRoutes();

            LobbyUsers = new List<UserDetails>();
            RoomUsers = new List<UserDetails>();
        }

        void SetAuthority()
        {
            if (InRoom)
            {
                var auth  = RoomUsers.Where(o => o.JoinedRoom > 0).OrderBy(o => o.JoinedRoom).FirstOrDefault();

                if (auth != null)
                {
                    if (!auth.Equals(RoomAuthority))
                    {
                        RoomAuthority = auth;
                        OnRoomUpdate(Room);
                    }
                }
            }
            else
            {
                RoomAuthority = null;
            }
        }

        void EnablePresence(string channel)
        {
            PresenceClient.EnablePresence(Url, IsCluster, AppKey, PrivateKey, channel, true,
                (ex, r) =>
                {
                    if (ex == null)
                    {
                        Debug.Log("Presence Enabled : " + channel);
                    }
                    else
                    {
                        Debug.LogError("Presence Error : " + ex.Message);
                    }
                });
        }

        void SendRPC(string channel, LobbyMessage message)
        {
            var json = JsonUtility.ToJson(message);
            var key = LobbyMessage.GetTypeKey(message.GetType());

            Debug.Log("LobbyService:SendRPC " + message.GetType().Name);

            _client.Send(channel, string.Format("{0}{1}{2}", key, Seperator, json));
        }


        void _client_OnUnsubscribed(string channel)
        {
            if (channel == LOBBY)
            {
                User.JoinedLobby = -1;
                LobbyUsers.Clear();
                InLobby = false;
                OnLobbyExit();
                Debug.Log("LobbyService ExitLobby");
            }
            else if (Room != null && Room.RoomId == channel)
            {
                User.JoinedRoom = -1;
                Room = null;
                InRoom = false;
                RoomUsers.Clear();
                SetAuthority();
                OnRoomExit();
                Debug.Log("LobbyService ExitRoom");
            }
        }

        void _client_OnSubscribed(string channel)
        {
            if (channel == LOBBY)
            {
                User.JoinedLobby = DateTime.UtcNow.Ticks;
                LobbyUsers.RemoveAll(o => o.UserId == User.UserId);
                LobbyUsers.Add(User);
                InLobby = true;
                OnLobbyEnter();

                SendRPC(LOBBY, User);

                if (lobbyJoinCallback != null)
                    lobbyJoinCallback(true);

                lobbyJoinCallback = null;

                Debug.Log("LobbyService EnterLobby");
            }
            else if (_pendingRoom != null && _pendingRoom.RoomId == channel)
            {
                User.JoinedRoom = DateTime.UtcNow.Ticks;
                RoomUsers.RemoveAll(o => o.UserId == User.UserId);
                RoomUsers.Add(User);
                SetAuthority();
                InRoom = true;
                Room = _pendingRoom;
                OnRoomEnter(Room);

                SendRPC(Room.RoomId, User);

                if (roomJoinCallback != null)
                    roomJoinCallback(true);
                roomJoinCallback = null;

                //Update Room State
                var dto = LobbyMessage.GetDefault<RoomFindResponse>();
                dto.Room = Room;
                dto.Users = RoomUsers.ToArray();
                //TODO HOST
                SendRPC(LOBBY, dto);

                _pendingRoom = null;
                Debug.Log("LobbyService EnterRoom");
            }
            else if (channel == User.UserId)
            {
                State = ConnectionState.Connected;
                OnState(State);

                if (connectCallback != null)
                    connectCallback(ConnectionState.Connected);

                connectCallback = null;
            }

            if (channel == OrtcDisconnected || channel == User.UserId)
                return;

            var request = LobbyMessage.GetDefault<UserFindRequest>();
            request.RoomId = channel;
            request.UserId = User.UserId;
            SendRPC(request.RoomId, request);
        }

        void _client_OnReconnecting()
        {
            State = ConnectionState.Reconnecting;
            OnState(State);
        }

        void _client_OnReconnected()
        {
            if (InLobby)
            {
                User.JoinedLobby = DateTime.UtcNow.Ticks;
                LobbyUsers.RemoveAll(o => o.UserId == User.UserId);
                LobbyUsers.Add(User);
                InLobby = true;
                OnLobbyEnter();

                SendRPC(LOBBY, User);

                Debug.Log("LobbyService EnterLobby");
            }

            if (InRoom)
            {
                User.JoinedRoom = DateTime.UtcNow.Ticks;
                RoomUsers.RemoveAll(o => o.UserId == User.UserId);
                RoomUsers.Add(User);
                InRoom = true;
                SetAuthority();
                OnRoomEnter(Room);
                SendRPC(Room.RoomId, User);

                //Update Room State
                var dto = LobbyMessage.GetDefault<RoomFindResponse>();
                dto.Room = Room;
                dto.Users = RoomUsers.ToArray();
                //TODO HOST
                SendRPC(LOBBY, dto);

                _pendingRoom = null;
                Debug.Log("LobbyService EnterRoom");
            }

            State = ConnectionState.Connected;
            OnState(State);
        }

        void _client_OnDisconnected()
        {
            LobbyUsers.Clear();
            RoomUsers.Clear();
            RoomAuthority = null;
            InLobby = false;
            InRoom = false;
            Room = null;
            SetAuthority();

            findCallback = null;

            State = ConnectionState.Disconnected;
            OnState(State);

            if (connectCallback != null)
                connectCallback(ConnectionState.Disconnected);

            connectCallback = null;

        }

        void _client_OnConnected()
        {
            _client.Subscribe(OrtcDisconnected, true, OnOrtcMessage);
            _client.Subscribe(User.UserId, true, OnOrtcMessage);
        }

        void _client_OnException(Exception ex)
        {
            Debug.LogException(ex);


            if (roomJoinCallback != null)
                roomJoinCallback(false);

            if (lobbyJoinCallback != null)
                lobbyJoinCallback(false);

            connectCallback = null;
            roomJoinCallback = null;
            lobbyJoinCallback = null;
        }

        void OnOrtcMessage(string channel, string message)
        {
            if (channel == OrtcDisconnected)
            {
                var model = JsonUtility.FromJson<OrtcAnnouncement>(message);

                //Send via messenger. Routed below
                LobbyMessenger.Publish(channel, model, typeof(OrtcAnnouncement));
            }
            else
            {
                var proxy = message.Split(new[] { Seperator }, StringSplitOptions.None);
                var type = LobbyMessage.GetTypeFromKey(int.Parse(proxy[0]));
                var mjson = proxy[1];

                if (type == null)
                    return;

                Debug.Log("LobbyService:OnRoomMessage " + type.Name);

                var model = JsonUtility.FromJson(mjson, type);

                //Send via messenger. Routed below
                LobbyMessenger.Publish(channel, model, type);
            }

        }

        #endregion

        #region Message Routes

        void MapRoutes()
        {
            LobbyMessenger<OrtcAnnouncement>.Subscribe(OnOrtcAnnouncement);
            LobbyMessenger<UserDetails>.Subscribe(OnUserDetails);
            LobbyMessenger<UserLeaveMessage>.Subscribe(OnUserLeaveMessage);
            LobbyMessenger<UserFindRequest>.Subscribe(OnUserFindRequest);
            LobbyMessenger<UserFindResponse>.Subscribe(UserFindResponse);
            LobbyMessenger<RoomDetails>.Subscribe(OnRoomDetails);
            LobbyMessenger<RoomFindRequest>.Subscribe(OnRoomFindRequest);
            LobbyMessenger<RoomFindResponse>.Subscribe(OnFindRoomResponse);
        }

        void OnOrtcAnnouncement(string channel, OrtcAnnouncement message)
        {
            if (channel != OrtcDisconnected)
                return;

            //Remove user from rooms
            var lobbyu = LobbyUsers.FirstOrDefault(o => o.UserId == message.cm);
            if (lobbyu != null)
            {
                Debug.Log("LobbyUserRemoved - " + lobbyu.UserName);
                LobbyUsers.Remove(lobbyu);
                OnLobbyUserRemove(lobbyu);
            }

            var roomu = RoomUsers.FirstOrDefault(o => o.UserId == message.cm);
            if (roomu != null)
            {
                Debug.Log("RoomUserRemoved - " + roomu.UserName);
                RoomUsers.Remove(roomu);
                OnRoomUserRemove(roomu);
                SetAuthority();
            }
        }

        // Users

        void OnUserDetails(string channel, UserDetails model)
        {
            if (channel == LOBBY)
            {
                LobbyUsers.RemoveAll(o => o.UserId == model.UserId);
                Debug.Log("LobbyUserAdded - " + model.UserName);
                if (model.UserId == User.UserId)
                {
                    LobbyUsers.Add(User);
                    OnLobbyUserAdd(User);
                }
                else
                {
                    LobbyUsers.Add(model);
                    OnLobbyUserAdd(model);
                }

            }
            else
            {
                Debug.Log("RoomUserAdded - " + model.UserName);
                RoomUsers.RemoveAll(o => o.UserId == model.UserId);
                if (model.UserId == User.UserId)
                {
                    RoomUsers.Add(User);
                    OnRoomUserAdd(User);
                    SetAuthority();
                }
                else
                {
                    RoomUsers.Add(model);
                    OnRoomUserAdd(model);
                    SetAuthority();
                }
            }
        }

        void OnUserLeaveMessage(string channel, UserLeaveMessage model)
        {
            var first = LobbyUsers.FirstOrDefault(o => o.UserId == model.UserId);
            if (first != null)
            {
                Debug.Log("LobbyUserRemoved - " + first.UserName);
                LobbyUsers.Remove(first);
                OnLobbyUserRemove(first);
            }
            var second = RoomUsers.FirstOrDefault(o => o.UserId == model.UserId);
            if (second != null)
            {
                Debug.Log("RoomUserRemoved - " + second.UserName);
                RoomUsers.Remove(second);
                OnRoomUserRemove(second);
                SetAuthority();
            }
        }

        void OnUserFindRequest(string channel, UserFindRequest model)
        {

            if (model.UserId == User.UserId)
                return;

            // Send response
            var dto = LobbyMessage.GetDefault<UserFindResponse>();
            dto.User = User;
            dto.RoomId = model.RoomId;
            SendRPC(dto.RoomId, dto);
        }

        void UserFindResponse(string channel, UserFindResponse model)
        {
            if (model.RoomId == LOBBY)
            {
                Debug.Log("LobbyUserAdded - " + model.User.UserName);
                LobbyUsers.RemoveAll(o => o.UserId == model.User.UserId);
                LobbyUsers.Add(model.User);
                OnLobbyUserAdd(model.User);
            }
            else if (Room != null && model.RoomId == Room.RoomId)
            {
                Debug.Log("RoomUserAdded - " + model.User.UserId);
                RoomUsers.RemoveAll(o => o.UserId == model.User.UserId);
                RoomUsers.Add(model.User);
                SetAuthority();
                OnRoomUserAdd(model.User);
            }
        }

        // Rooms

        void OnRoomDetails(string channel, RoomDetails model)
        {
            //From Room Channel
            Debug.Log("RoomUpdated - " + model.RoomName);
            Room = model;
            OnRoomUpdate(model);
        }

        void OnRoomFindRequest(string channel, RoomFindRequest model)
        {
            if (InRoom)
            {
                //send is authority
                if (IsAuthority())
                {
                    Debug.Log("SendingRoomDetails - " + Room.RoomName);

                    var details = LobbyMessage.GetDefault<RoomFindResponse>();
                    details.Room = Room;
                    details.Users = RoomUsers.ToArray();

                    SendRPC(model.UserId, details);
                }
            }
        }

        void OnFindRoomResponse(string channel, RoomFindResponse model)
        {
            Debug.Log("RoomFound - " + model.Room.RoomName + " " + model.Users.Length);
            OnRoomFound(model);
            if (findCallback != null)
                findCallback(model);
        }

        #endregion
    }
}