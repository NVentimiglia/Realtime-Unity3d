//
//  iosBridge.m
//  iosBridge
//
//  Created by Nicholas Ventimiglia on 12/28/14.
//  Copyright (c) 2014 IBT. All rights reserved.
//

#import "IOSBridge.h"
#import "SRWebSocket.h"

// Static
static IOSBridge *instance = nil;

@implementation IOSBridge;

// Callbacks
@synthesize onOpenedCallback;
@synthesize onClosedCallback;
@synthesize onMessageCallback;
@synthesize onLogCallback;
@synthesize onErrorCallback;

// constructor


+(IOSBridge*) instance{
    if(instance == nil){
        NSLog(@"IOSBridge - alloc");
        instance = [[IOSBridge alloc] init];
    }
    return instance;
}

-(id)init
{
    NSLog(@"IOSBridge - init");
    self = [super init];
    _clients = [[NSMutableArray alloc] init];
    return self;
}

// static factory methods

-(void)Destroy:(int)clientId
{
    NSLog(@"IOSBridge - Destroy");
        
    SRWebSocket *client = [_clients objectAtIndex:clientId];
    
    [client close];
    
    client = nil;
    
    [_clients replaceObjectAtIndex:clientId withObject:[NSNull null]];
}

-(SRWebSocket*) getClient:(int) clientId{
    return [_clients objectAtIndex:clientId];
}

// instance methods

- (int)Create:(NSString *)uri
{
    NSLog(@"IOSBridge - Create : %@", uri);
    
    NSURL *url = [NSURL URLWithString: uri];
    
    SRWebSocket *ws =[[SRWebSocket alloc] initWithURLRequest:[NSURLRequest requestWithURL:url]];
    ws.delegate = self;
        
    [_clients addObject:ws];
    
    int clientId = (int)[_clients indexOfObject:ws];
    
    [ws open];
    
    return clientId;
}


-(void)Close:(int)clientId
{
    NSLog(@"IOSBridge - Close");
    
    SRWebSocket *ws = [instance getClient:clientId];
    
    [ws close];
}



-(void) Send :(int)clientId
          msg:(NSString *)msg
{
    SRWebSocket *ws = [instance getClient:clientId];
    
    [ws send:msg];
}

#pragma mark - SRWebSocketDelegate

- (void)webSocketDidOpen:(SRWebSocket *)webSocket;
{
    NSLog(@"IOSBridge - Opened");
    
    int clientId = (int)[_clients indexOfObject:webSocket];
    
    if(onOpenedCallback != NULL){
        onOpenedCallback(clientId);
    }
}

- (void)webSocket:(SRWebSocket *)webSocket didFailWithError:(NSError *)error;
{
    NSLog(@"IOSBridge - Error");
    
    int clientId = (int)[_clients indexOfObject:webSocket];
    
    if(onErrorCallback != NULL){
        onErrorCallback(clientId, [[error localizedDescription]UTF8String]);
    }
    
    if(onClosedCallback != NULL){
        onClosedCallback(clientId);
    }
}

- (void)webSocket:(SRWebSocket *)webSocket didReceiveMessage:(id)message;
{
    int clientId = (int)[_clients indexOfObject:webSocket];
    NSString *value = (NSString *)message;
    
    if(onMessageCallback != NULL){
        onMessageCallback(clientId, [value UTF8String]);
    }
}

- (void)webSocket:(SRWebSocket *)webSocket didCloseWithCode:(NSInteger)code reason:(NSString *)reason wasClean:(BOOL)wasClean;
{
    NSLog(@"IOSBridge - Closed with code");
    
    int clientId = (int)[_clients indexOfObject:webSocket];
    
    if(onClosedCallback != NULL){
        onClosedCallback(clientId);
    }
}

- (void)webSocket:(SRWebSocket *)webSocket didReceivePong:(NSData *)pongPayload;
{
    NSLog(@"IOSBridge - Websocket received pong");
}


#pragma mark Private methods

- (void) Log:(int) clientId text:(NSString*) text
{
    NSLog(@"IOSBridge %d : %@", clientId,  text);
    
    if(onLogCallback != NULL){
        onLogCallback(clientId, [text UTF8String]);
    }
}

@end

// utility

// Converts C style string to NSString
NSString* ToNSString (const char* string)
{
    if (string)
        return [NSString stringWithUTF8String: string];
    else
        return [NSString stringWithUTF8String: ""];
}

// Helper method to create C string copy
char* CopyString (const char* string)
{
    if (string == NULL)
        return NULL;
    
    char* res = (char*)malloc(strlen(string) + 1);
    strcpy(res, string);
    return res;
}

// Native

#ifdef __cplusplus
extern "C" {
#endif
    
    // Static
    
    void Init(){
        instance = [IOSBridge instance];
    }
    
    void Destroy(int clientId){
        [instance Destroy:clientId];
    }
    
    // Instance
    
    int Create(const char* path){
        return [instance Create:ToNSString(path)];
    }
    
    void Close(int clientId){
        [instance Close:clientId];
    }
    
    void Send(int clientId, const char* data){
        [instance Send:clientId msg:ToNSString(data)];
    }
    
    // Register Callbacks
    
    void RegisterOpenedDelegate(NativeOpenedDelegate callback) {
        instance.onOpenedCallback = callback;
    }
    void RegisterClosedDelegate(NativeClosedDelegate callback) {
        instance.onClosedCallback = callback;
    }
    void RegisterMessageDelegate(NativeMessageDelegate callback) {
        instance.onMessageCallback = callback;
    }
    void RegisterLogDelegate(NativeLogDelegate callback) {
        instance.onLogCallback = callback;
    }
    void RegisterErrorDelegate(NativeErrorDelegate callback) {
        instance.onErrorCallback = callback;
    }
    
    
#ifdef __cplusplus
}
#endif




