
/**
 * Created by Nicholas Ventimiglia on 11/27/2015.
 * nick@avariceonline.com
 *
 *  Android Websocket bridge application. Beacause Mono Networking sucks.
 *  Unity talks with BridgeClient (java) and uses a C Bridge to raise events.
 *  .NET Methods <-->  BridgeClient (Java) -> Bridge (C) --> .net Event
 */

#include <jni.h>
#include <string.h>

#ifndef _Included_realtime_droidbridge
#define _Included_realtime_droidbridge

extern "C" {

    // Define Delegate Type
    typedef void (*NativeOpenedDelegate)  (int id);
    typedef void (*NativeClosedDelegate)  (int id);
    typedef void (*NativeMessageDelegate) (int id, const char *);
    typedef void (*NativeLogDelegate)     (int id, const char *);
    typedef void (*NativeErrorDelegate)   (int id, const char *);

    // Reserve space for Delegate instances
    NativeOpenedDelegate  onOpenedCallback;
    NativeClosedDelegate  onClosedCallback;
    NativeMessageDelegate onMessageCallback;
    NativeLogDelegate     onLogCallback;
    NativeErrorDelegate   onErrorCallback;

    // C# Subscribe method handlers
    void RegisterOpenedDelegate     (NativeOpenedDelegate callback);
    void RegisterClosedDelegate     (NativeClosedDelegate callback);
    void RegisterMessageDelegate    (NativeMessageDelegate callback);
    void RegisterLogDelegate        (NativeLogDelegate callback);
    void RegisterErrorDelegate      (NativeErrorDelegate callback);

    // Java Publish Method
    JNIEXPORT void JNICALL Java_realtime_droidbridge_BridgeClient_RaiseOpened   (JNIEnv *env, jobject o, jint id);
    JNIEXPORT void JNICALL Java_realtime_droidbridge_BridgeClient_RaiseClosed   (JNIEnv *env, jobject o, jint id);
    JNIEXPORT void JNICALL Java_realtime_droidbridge_BridgeClient_RaiseMessage  (JNIEnv *env, jobject o, jint id, jstring m);
    JNIEXPORT void JNICALL Java_realtime_droidbridge_BridgeClient_RaiseLog      (JNIEnv *env, jobject o, jint id, jstring m);
    JNIEXPORT void JNICALL Java_realtime_droidbridge_BridgeClient_RaiseError    (JNIEnv *env, jobject o, jint id, jstring m);

}
#endif