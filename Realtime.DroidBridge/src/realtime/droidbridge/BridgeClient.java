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

//https://github.com/koush/AndroidAsync
import com.koushikdutta.async.callback.CompletedCallback;
import com.koushikdutta.async.http.AsyncHttpClient;
import com.koushikdutta.async.http.WebSocket;

/*
import java.io.InvalidClassException;
import java.security.KeyManagementException;
import java.security.NoSuchAlgorithmException;
import javax.net.ssl.SSLContext;
*/

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
        /*
        if (wsuri.startsWith("wss")) {
            try {
                SSLContext sslContext = null;
                sslContext = SSLContext.getInstance("TLS");
                sslContext.init(null, null, null);

                AsyncHttpClient.getDefaultInstance().getSSLSocketMiddleware().setSSLContext(sslContext);
            } catch (NoSuchAlgorithmException | KeyManagementException e) {
                RaiseError(instanceId, e.getMessage());
            }
        }*/

        AsyncHttpClient.getDefaultInstance().websocket(wsuri, "TLSv1.2",
                new AsyncHttpClient.WebSocketConnectCallback() {
                    @Override
                    public void onCompleted(Exception ex, WebSocket webSocket) {
                        if (ex != null) {
                            Error(ex.toString());
                            return;
                        }

                        mConnection = webSocket;

                        mHandler.post(new Runnable() {
                            @Override
                            public void run() {
                                RaiseOpened(instanceId);
                            }
                        });

                        webSocket.setClosedCallback(new CompletedCallback() {
                            @Override
                            public void onCompleted(Exception e) {
                                mHandler.post(new Runnable() {
                                    @Override
                                    public void run() {
                                        RaiseClosed(instanceId);
                                        mConnection = null;
                                    }
                                });
                            }
                        });


                        webSocket.setStringCallback(new WebSocket.StringCallback() {
                            public void onStringAvailable(final String s) {
                                mHandler.post(new Runnable() {
                                    @Override
                                    public void run() {
                                        RaiseMessage(instanceId, s);
                                    }
                                });
                            }
                        });
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
                mConnection.close();
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
