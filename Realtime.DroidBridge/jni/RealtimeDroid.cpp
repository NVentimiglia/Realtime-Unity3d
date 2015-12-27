#include "RealtimeDroid.h"
#include <jni.h>
#include <stdlib.h>
#include <android/log.h>

extern "C" {

    void logd(const char*);

    JNIEXPORT jint JNICALL JNI_OnLoad(JavaVM *jvm, void *reserved){
        return JNI_VERSION_1_6;
    }

    void logd(const char* msg)
    {
        __android_log_print(ANDROID_LOG_DEBUG, "RealtimeDroid", "%s",  msg);
    }

    // C# Subscribe method handlers

    void RegisterOpenedDelegate(NativeOpenedDelegate callback) {
        onOpenedCallback = callback;
    }
    void RegisterClosedDelegate(NativeClosedDelegate callback) {
        onClosedCallback = callback;
    }
    void RegisterMessageDelegate(NativeMessageDelegate callback) {
        onMessageCallback = callback;
    }
    void RegisterLogDelegate(NativeLogDelegate callback) {
        onLogCallback = callback;
    }
    void RegisterErrorDelegate(NativeErrorDelegate callback) {
        onErrorCallback = callback;
    }

    // Implement the Java to C# Event

    JNIEXPORT void JNICALL Java_realtime_droidbridge_BridgeClient_RaiseOpened(JNIEnv *env, jobject o, jint id) {
        if (onOpenedCallback != NULL)
        {
            onOpenedCallback(id);
        }
    }

    JNIEXPORT void JNICALL Java_realtime_droidbridge_BridgeClient_RaiseClosed(JNIEnv *env, jobject o, jint id) {
        if (onClosedCallback != NULL)
        {
            onClosedCallback(id);
        }
    }

    JNIEXPORT void JNICALL Java_realtime_droidbridge_BridgeClient_RaiseMessage(JNIEnv *env, jobject o, jint id, jstring m)  {
        if (onMessageCallback != NULL)
        {
            onMessageCallback(id, env->GetStringUTFChars(m, 0));
        }
    }

    JNIEXPORT void JNICALL Java_realtime_droidbridge_BridgeClient_RaiseLog(JNIEnv *env, jobject o, jint id, jstring m)  {
        if (onLogCallback != NULL)
        {
            onLogCallback(id, env->GetStringUTFChars(m, 0));
        }
    }

    JNIEXPORT void JNICALL Java_realtime_droidbridge_BridgeClient_RaiseError(JNIEnv *env, jobject o, jint id, jstring m)  {
        if (onErrorCallback != NULL)
        {
            onErrorCallback(id, env->GetStringUTFChars(m, 0));
        }
    }
}
