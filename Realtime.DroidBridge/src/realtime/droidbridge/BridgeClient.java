/**
 * Created by Nicholas Ventimiglia on 11/27/2015.
 * nick@avariceonline.com
 *
 *  Android Websocket bridge application. Beacause Mono Networking sucks.
 *  Unity talks with BridgeClient (java) and uses a C Bridge to raise events.
 *  .NET Methods <-->  BridgeClient (Java) -> Bridge (C) -> .net Event
 */

package realtime.droidbridge;

import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import java.net.URI;
import java.net.URISyntaxException;

import realtime.websocket.WebSocket;
import realtime.websocket.WebSocketEventHandler;
import realtime.websocket.WebSocketException;
import realtime.websocket.WebSocketMessage;

public class BridgeClient
{

    // region Native / Static

    static
    {
        System.loadLibrary("RealtimeDroid");
    }

    @SuppressWarnings("JniMissingFunction")
    private native int  RaiseOpened     (int connectionID);
    @SuppressWarnings("JniMissingFunction")
    private native void RaiseClosed     (int connectionId);
    @SuppressWarnings("JniMissingFunction")
    private native void RaiseMessage    (int connectionId, String message);
    @SuppressWarnings("JniMissingFunction")
    private native void RaiseLog        (int connectionId, String message);
    @SuppressWarnings("JniMissingFunction")
    private native void RaiseError      (int connectionId, String message);

    public static BridgeClient GetInstance()
    {
        return new BridgeClient();
    }

    // endregion

    // region fields
    private static int counter = 0;
    public final int instanceId;
    private Handler mHandler;
    private WebSocket mConnection;
    private static String TAG = "realtime.droidbridge";
    // endregion

    // region Public methods

    public BridgeClient()
    {
        mHandler = new Handler(Looper.getMainLooper());
        instanceId = counter++;
    }

    // Support for multiple socket instances
    public int GetId()
    {
        return instanceId;
    }

    // connect websocket
    public void Open(final String wsuri)
    {
        try {
            URI connectionUri = new URI(wsuri);
            mConnection = new WebSocket(connectionUri);
            addSocketEventsListener();
            mConnection.connect();
        } catch (WebSocketException e) {
            RaiseError(instanceId, e.getMessage());
        } catch (URISyntaxException e) {
            RaiseError(instanceId, e.getMessage());
        }
    }


    private void addSocketEventsListener() {
        mConnection.setEventHandler(new WebSocketEventHandler() {

            @Override
            public void onOpen() {
                RaiseOpened(instanceId);
            }

            @Override
            public void onMessage(WebSocketMessage socketMessage) {
                try {
                    RaiseMessage(instanceId, socketMessage.getText());
                } catch (Exception e) {
                    RaiseError(instanceId, e.getMessage());
                }
            }

            @Override
            public void onClose() {
                RaiseClosed(instanceId);
                mConnection = null;
            }

            @Override
            public void onForcedClose() {
                RaiseClosed(instanceId);
                mConnection = null;
            }

            @Override
            public void onPing() {

            }

            @Override
            public void onPong() {

            }

            @Override
            public void onException(Exception error) {
                RaiseMessage(instanceId, error.getMessage());
            }
        });
    }

    // disconnect websocker
    public void Close()
    {
        if(mConnection == null)
            return;

        mHandler.post(new Runnable()
        {
            @Override
            public void run()
            {
                mConnection.close(true);
            }
        });
    }

    // send a message
    public void Send(final String message)
    {
        if(mConnection == null)
            return;

        mHandler.post(new Runnable()
        {
            @Override
            public void run()
            {
                mConnection.send(message);
            }
        });
    }

    private void Log(final String args)
    {
        Log.d(TAG, args);
        mHandler.post(new Runnable()
        {
            @Override
            public void run()
            {
                RaiseLog(instanceId, args);
            }
        });
    }

    private void Error(final String args)
    {
        Log.e(TAG, args);
        mHandler.post(new Runnable()
        {
            @Override
            public void run()
            {
                RaiseError(instanceId, String.format("Error: %s", args));
            }
        });
    }

    // endregion
}
