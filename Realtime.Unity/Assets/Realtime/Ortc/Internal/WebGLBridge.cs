#if UNITY_WEBGL 
using System;
using System.Text;
using System.Collections;
using System.Runtime.InteropServices;

namespace Realtime.Ortc.Internal
{
    public class WebGLBridge
    {
		private Uri mUrl;
	
		public WebGLBridge(Uri url)
		{
			mUrl = url;
	
			string protocol = mUrl.Scheme;
			if (!protocol.Equals("ws") && !protocol.Equals("wss"))
				throw new ArgumentException("Unsupported protocol: " + protocol);
		}
	
		public void SendString(string str)
		{
			Send(Encoding.UTF8.GetBytes (str));
		}
	
		public string RecvString()
		{
			byte[] retval = Recv();
			if (retval == null)
				return null;
			return Encoding.UTF8.GetString (retval);
		}
	
		[DllImport("__Internal")]
		private static extern int SocketCreate (string url);
	
		[DllImport("__Internal")]
		private static extern int SocketState (int socketInstance);
	
		[DllImport("__Internal")]
		private static extern void SocketSend (int socketInstance, byte[] ptr, int length);
	
		[DllImport("__Internal")]
		private static extern void SocketRecv (int socketInstance, byte[] ptr, int length);
	
		[DllImport("__Internal")]
		private static extern int SocketRecvLength (int socketInstance);
	
		[DllImport("__Internal")]
		private static extern void SocketClose (int socketInstance);
	
		[DllImport("__Internal")]
		private static extern int SocketError (int socketInstance, byte[] ptr, int length);
	
		int m_NativeRef = 0;
	
		public void Send(byte[] buffer)
		{
			SocketSend (m_NativeRef, buffer, buffer.Length);
		}
	
		public byte[] Recv()
		{
			int length = SocketRecvLength (m_NativeRef);
			if (length == 0)
				return null;
			byte[] buffer = new byte[length];
			SocketRecv (m_NativeRef, buffer, length);
			return buffer;
		}
	
		public IEnumerator Connect()
		{
			m_NativeRef = SocketCreate (mUrl.ToString());
	
			while (SocketState(m_NativeRef) == 0)
				yield return 0;
		}
	
		public void Close()
		{
			SocketClose(m_NativeRef);
		}
	
		public string error
		{
			get {
				const int bufsize = 1024;
				byte[] buffer = new byte[bufsize];
				int result = SocketError (m_NativeRef, buffer, bufsize);
	
				if (result == 0)
					return null;
	
				return Encoding.UTF8.GetString (buffer);				
			}
		}
    }
}
#endif