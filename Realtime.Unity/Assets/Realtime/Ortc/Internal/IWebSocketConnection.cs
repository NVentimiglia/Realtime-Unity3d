using System;

namespace Realtime.Ortc.Internal
{
    public delegate void OnOpenedDelegate();

    public delegate void OnClosedDelegate();

    public delegate void OnErrorDelegate(string error);

    public delegate void OnMessageDelegate(string message);

    public interface IWebSocketConnection : IDisposable
    {
        bool IsOpen { get; }

        void Open(string url);

        void Close();

        void Send(string message);
        
        event OnOpenedDelegate OnOpened;

        event OnClosedDelegate OnClosed;

        event OnErrorDelegate OnError;

        event OnMessageDelegate OnMessage;

    }
}