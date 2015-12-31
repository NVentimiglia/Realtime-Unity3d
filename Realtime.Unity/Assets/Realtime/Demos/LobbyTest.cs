// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------
using System;
using System.Linq;
using Foundation.Terminal;
using Realtime.Lobby;
using Realtime.Ortc;
using Realtime.Ortc.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Realtime.Demos
{
    /// <summary>
    /// Demo Client using the Ortc CLient
    /// </summary>
    [AddComponentMenu("Realtime/Demos/LobbyTest")]
    public class LobbyTest : MonoBehaviour
    {
        [Serializable]
        public class CustomRPC : LobbyMessage
        {
            public string Message;
        }

        /// <summary>
        /// 
        /// </summary>
        public string URL = "http://ortc-developers.realtime.co/server/2.1";

        /// <summary>
        /// Identities your channel group
        /// </summary>
        public string ApplicationKey = "";

        /// <summary>
        /// Identities your channel group
        /// </summary>
        public string PrivateKey = "";

        private IOrtcClient _ortc;
        private LobbyService _lobby;
        private RoomDetails _lastRoom;

        void Awake()
        {
            RealtimeProxy.ConfirmInit();
            _ortc = new UnityOrtcClient();
            _ortc.ClusterUrl = URL;
            _lobby = LobbyService.Init(_ortc, ApplicationKey, PrivateKey, URL, true);

            _lobby.OnRoomFound += _lobby_OnRoomFound;
            _lobby.OnState += _lobby_OnState;

            _lobby.Subscribe<CustomRPC>(OnCustomRPC);
        }

        private void _lobby_OnState(LobbyService.ConnectionState obj)
        {
            if (obj != LobbyService.ConnectionState.Connected)
                TerminalModel.LogImportant(obj);
        }

        private void _lobby_OnRoomFound(RoomFindResponse obj)
        {
            _lastRoom = obj.Room;
        }

        void OnCustomRPC(string channel, CustomRPC model)
        {
            Debug.Log("Got Custom RPC " + model.Message);
        }

        protected void Start()
        {
            LoadCommands();
        }

        void LoadCommands()
        {
            TerminalModel.Add(new TerminalCommand
            {
                Label = "Connect",
                Method = Connect
            });

            TerminalModel.Add(new TerminalCommand
            {
                Label = "Disconnect",
                Method = Disconnect
            });

            //

            TerminalModel.Add(new TerminalCommand
            {
                Label = "Join Lobby",
                Method = SubscribeLobby
            });

            TerminalModel.Add(new TerminalCommand
            {
                Label = "Leave Lobby",
                Method = UnsubscribeLobby
            });

            TerminalModel.Add(new TerminalCommand
            {
                Label = "Print Lobby",
                Method = PrintLobby
            });

            TerminalModel.Add(new TerminalCommand
            {
                Label = "Find Rooms",
                Method = FindRooms
            });


            TerminalModel.Add(new TerminalCommand
            {
                Label = "Join Room",
                Method = Subscribe
            });

            TerminalModel.Add(new TerminalCommand
            {
                Label = "Create Room",
                Method = CreateRoom
            });

            TerminalModel.Add(new TerminalCommand
            {
                Label = "Leave Room",
                Method = Unsubscribe
            });

            TerminalModel.Add(new TerminalCommand
            {
                Label = "Print Room",
                Method = PrintRoom
            });
            //

            TerminalModel.Add(new TerminalCommand
            {
                Label = "Lobby Chat",
                Method = ChatLobby
            });
            TerminalModel.Add(new TerminalCommand
            {
                Label = "Room Chat",
                Method = ChatRoom
            });
            TerminalModel.Add(new TerminalCommand
            {
                Label = "User Chat",
                Method = ChatUser
            });

            TerminalModel.Add(new TerminalCommand
            {
                Label = "Custom RPC",
                Method = DoRPC
            });
            //
        }

        #region methods

        void Connect()
        {
            var user = new UserDetails
            {
                UserId = Guid.NewGuid().ToString(),
                UserName = Application.platform + " " + UnityEngine.Random.Range(0, 1000),
            };

            _lobby.Connect("AuthKey", user, state =>
            {
                TerminalModel.LogImportant("Connected !");
            });
        }

        void Disconnect()
        {
            _lobby.Disconnect();
        }

        void SubscribeLobby()
        {
            _lobby.JoinLobby(result =>
            {
                TerminalModel.LogImportant(result ? "In Lobby !" : "Error");
            });
        }

        void UnsubscribeLobby()
        {
            _lobby.LeaveLobby();
        }

        void PrintLobby()
        {
            TerminalModel.Log("");
            TerminalModel.LogImportant("Lobby Users :");
            foreach (var user in _lobby.LobbyUsers)
            {
                if (user.Equals(_lobby.User))
                {
                    Debug.Log(user.UserName + " (Self)");
                }
                else
                {
                    Debug.Log(user.UserName);
                }
            }
        }

        void PrintRoom()
        {
            if (_lobby.InRoom)
            {
                TerminalModel.Log("");
                TerminalModel.LogImportant("Room Users : " + _lobby.Room.RoomName);
                foreach (var user in _lobby.RoomUsers)
                {
                    if (_lobby.IsAuthority(user))
                    {
                        if (user.Equals(_lobby.User))
                        {
                            Debug.Log(user.UserName + " (Authority, Self)");
                        }
                        else
                        {
                            Debug.Log(user.UserName + " (Authority)");
                        }
                    }
                    else if (user.Equals(_lobby.User))
                    {
                        Debug.Log(user.UserName + " (Self)");
                    }
                    else
                    {
                        Debug.Log(user.UserName);
                    }
                }

            }
        }

        void FindRooms()
        {
            _lobby.FindRooms(room =>
            {
                Debug.Log("Found Room : " + room.Room.RoomName);
            });
        }

        void Subscribe()
        {
            if (_lastRoom == null)
            {
                Debug.LogWarning("No rooms found.");
                return;
            }
            _lobby.JoinRoom(_lastRoom, result =>
            {
                TerminalModel.LogImportant(result ? "In Room !" : "Error");
            });
        }

        void CreateRoom()
        {
            _lobby.CreateRoom(_lobby.User.UserName, SceneManager.GetActiveScene().name, true, result =>
            {
                TerminalModel.LogImportant(result ? "Room Created !" : "Error");
            });
        }

        void Unsubscribe()
        {
            _lobby.LeaveRoom();
        }

        void ChatUser()
        {
            var rand = _lobby.LobbyUsers.FirstOrDefault(o => o.UserId != _lobby.User.UserId);
            if (rand != null)
            {
                _lobby.SendUserChat(rand.UserId, "Hello From " + _lobby.User.UserName);
            }
        }
        void ChatLobby()
        {
            _lobby.SendLobbyChat("Hello From " + _lobby.User.UserName);
        }

        void ChatRoom()
        {
            _lobby.SendRoomChat("Hello From " + _lobby.User.UserName);
        }

        void DoRPC()
        {
            _lobby.SendLobbyRPC(new CustomRPC { Message = _lobby.User.UserName });
        }

        #endregion
    }


}
