using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#if UNITY_WSA && !UNITY_EDITOR
using Windows.Foundation;
using Windows.ApplicationModel;
using Windows.Storage;
#endif

namespace Realtime.Lobby
{
    /// <summary>
    /// A user in the lobby system
    /// </summary>
    [Serializable]
    public class UserDetails : LobbyMessage, IEquatable<UserDetails>
    {
        /// <summary>
        /// uuid
        /// </summary>
        public string UserId;
        /// <summary>
        /// friendly name
        /// </summary>
        public string UserName;

        public long JoinedLobby = -1;
        public long JoinedRoom = -1;

        public bool Equals(UserDetails other)
        {
            return other != null && UserId == other.UserId;
        }
    }

    /// <summary>
    /// A room that may be joined. May be authoritative or public
    /// </summary>
    [Serializable]
    public class RoomDetails : LobbyMessage
    {
        /// <summary>
        /// uuid
        /// </summary>
        public string RoomId;

        /// <summary>
        /// friendly name
        /// </summary>
        public string RoomName;
    }

    /// <summary>
    /// Request for other users
    /// </summary>
    [Serializable]
    public class UserFindRequest : LobbyMessage
    {
        public string UserId;
        public string RoomId;
    }


    /// <summary>
    /// Request for other users
    /// </summary>
    [Serializable]
    public class UserFindResponse : LobbyMessage
    {
        public UserDetails User;
        public string RoomId;
    }

    /// <summary>
    /// Request to join a room
    /// </summary>
    [Serializable]
    public class RoomFindRequest : LobbyMessage
    {
        public string UserId;
    }


    /// <summary>
    /// Request to join a room
    /// </summary>
    [Serializable]
    public class RoomFindResponse : LobbyMessage
    {
        public RoomDetails Room;
        public UserDetails[] Users = new UserDetails[0];
    }

    /// <summary>
    /// User leave
    /// </summary>
    [Serializable]
    public class UserLeaveMessage : LobbyMessage
    {
        public string RoomId;
        public string UserId;
    }

    /// <summary>
    /// A simple DTO
    /// </summary>
    [Serializable]
    public class ChatMessage : LobbyMessage
    {
        public string UserId;
        public string Content;
    }

    /// <summary>
    /// Utility for serializing messages
    /// </summary>
    [Serializable]
    public partial class LobbyMessage
    {
        static Dictionary<int, Type> KeyMap = new Dictionary<int, Type>();
        static Dictionary<Type, int> TypeMap = new Dictionary<Type, int>();
        static Dictionary<Type, object> Defaults = new Dictionary<Type, object>();

        /// <summary>
        /// Key lookup
        /// </summary>
        /// <param name="messageType"></param>
        /// <returns></returns>
        public static int GetTypeKey(Type id)
        {
            if (TypeMap.ContainsKey(id))
                return TypeMap[id];
            return 0;
        }

        /// <summary>
        /// type lookup
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static Type GetTypeFromKey(int id)
        {
            if (KeyMap.ContainsKey(id))
                return KeyMap[id];
            return null;
        }

        /// <summary>
        /// default message
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetDefault<T>() where T : LobbyMessage
        {
            return Defaults[typeof(T)] as T;
        }
    }

    public partial class LobbyMessage
    {
#if UNITY_WSA && !UNITY_EDITOR
        static LobbyMessage()
        {
            DoLoadInternal();
        }

        static async void DoLoadInternal()
        {


            // Find assemblies.
            StorageFolder folder = Package.Current.InstalledLocation;

            var loadedAssemblies = new List<Assembly>();

            var folderFilesAsync = await folder.GetFilesAsync().AsTask();

            foreach (var file in folderFilesAsync)
            {
                if (file.FileType == ".dll" || file.FileType == ".exe")
                {
                    try
                    {
                        var filename = file.Name.Substring(0, file.Name.Length - file.FileType.Length);
                        AssemblyName name = new AssemblyName { Name = filename };
                        Assembly asm = Assembly.Load(name);
                        loadedAssemblies.Add(asm);
                    }
                    catch (BadImageFormatException)
                    {
                        // Thrown reflecting on C++ executable files for which the C++ compiler stripped the relocation addresses (such as Unity dlls): http://msdn.microsoft.com/en-us/library/x4cw969y(v=vs.110).aspx
                    }
                }
            }

            var assemblies = loadedAssemblies.OrderBy(o => o.FullName).ToArray();
            var types = assemblies.SelectMany(t => t.ExportedTypes.Where(o => o.GetType().GetTypeInfo().IsSubclassOf(typeof(LobbyMessage))));
            int i = 1;
            KeyMap = types.ToDictionary(k => i++, v => v);
            TypeMap = KeyMap.ToDictionary(k => k.Value, v => v.Key);
            Defaults = KeyMap.ToDictionary(k => k.Value, o => Activator.CreateInstance(o.Value));
        }

#else
        static LobbyMessage()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var types = assemblies.SelectMany(t => t.GetTypes().Where(o => o.IsSubclassOf(typeof(LobbyMessage))));
            int i = 1;
            KeyMap = types.ToDictionary(k => i++, v => v);
            TypeMap = KeyMap.ToDictionary(k => k.Value, v => v.Key);
            Defaults = KeyMap.ToDictionary(k => k.Value, o => Activator.CreateInstance(o.Value));
        }
#endif
    }
}
