//
//  iosBridge.h
//  iosBridge
//
//  Created by Nicholas Ventimiglia on 7/17/14.
//  Copyright (c) 2014 IBT. All rights reserved.
//

#import <Foundation/Foundation.h>
#import "SocketRocket/SRWebSocket.h"


// Define Callbacks
#ifdef __cplusplus
extern "C" {
#endif
    // Define Delegate Type
    typedef void (*NativeOpenedDelegate)  (int id);
    typedef void (*NativeClosedDelegate)  (int id);
    typedef void (*NativeMessageDelegate) (int id, const char *);
    typedef void (*NativeLogDelegate)     (int id, const char *);
    typedef void (*NativeErrorDelegate)   (int id, const char *);
    
#ifdef __cplusplus
}
#endif

//External API
@interface IOSBridge : NSObject <SRWebSocketDelegate>
{
    NSMutableArray* _clients;
}

// Reserve space for Delegate instances
@property NativeOpenedDelegate  onOpenedCallback;
@property NativeClosedDelegate  onClosedCallback;
@property NativeMessageDelegate onMessageCallback;
@property NativeLogDelegate     onLogCallback;
@property NativeErrorDelegate   onErrorCallback;

// Static

+(IOSBridge*)instance;

// Methods

- (void) Destroy:(int)clientId;

- (int) Create:(NSString*)uri;

- (void) Close:(int)clientId;

- (void) Send:(int)clientId
          msg:(NSString*)msg;
@end
