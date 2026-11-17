# [SignalR EMS](https://github.com/LiPascal/SignalRems)
## Description
SignalRems is one Enterprise Messaging System (EMS) implemented by .NET Standard SignalR. It provides two communication models, RPC and PUB/SUB. This library will provide strong typed API to help client application communicate with server. 
It is using System.Text.Json or MessagePack for serialization/deserialization. 
In PUB/SUB mode, it subscribes with filter of Lambda expression. 
## Dependency
This libiary is built on [SingalR](https://dotnet.microsoft.com/en-us/apps/aspnet/signalr), hence the server application must be using "Microsoft.NET.Sdk.Web" SDK. The client side could use any .net SDK. 
Currently this project runs under dotnet core 8.0 and above. 
## API usage
### RPC Server
1. Configure WebApplication builder; 
``` C#
var builder = WebApplication.CreateBuilder(args);
// Using Json serialization/deserialization
builder.Services.AddSignalRemsJsonService(); 

// Or, using MessagePack serialization/deserialization
// builder.Services.AddSignalRemsMessagePackService(); 
```
2. Setup endpoint for app instance:
``` C#
var app = builder.Build();
app.MapSignalRemsRpcHub("/signalr/ems/example/rpc");
```
3. Register RPC handler in IRpcService instance. IRpcService instance is singleton and can be get from dependency injection, handler implements IRpcHandler<,> interface to handle the RPC logic; 
``` C#
IRpcService rpcService;
IRpcHandler<GetUserNameRequest, GetUserNameResponse> handler;
rpcService.RegisterHandler<GetUserNameRequest, GetUserNameResponse>(handler);
```
### RPC Client
1. Config RPC client into dependency injection; 
``` C#
services.AddSignalRemsJsonClient(); // Using Json serialization/deserialization
// services.AddSignalRemsMessagePackClient(); // MessagePack serialization/deserialization
```
2. Make connection and call:
``` C#
await _rpcClient.ConnectAsync("https://localhost:7198", "/signalr/ems/example/rpc", stoppingToken);
var request = new GetUserNameRequest() { UserId = i.ToString() };
var response = await _rpcClient.SendAsync<GetUserNameRequest, GetUserNameResponse>(request);
```
3. Connection loss and automatic retry
Replies are routed back to the connection that sent the request, so a request still waiting for its response when the connection drops can no longer be answered. By default the client sends such a request again automatically once it is connected again, and the caller only observes the eventual response. Each resend is logged as a warning: `Connection lost before the response of request ... arrived ... the request will be resent after reconnecting` followed by `Resending request ... (attempt N)`.

Retrying is transparent for the caller, and the server handler is told when a delivery may be a duplicate: a resent request carries `PossibleResend = true`. Request types opt in to the flag by declaring the optional `IRpcRequest.PossibleResend` property, which the server sets before `HandleRequest` runs (types that don't declare it are unaffected):
``` C#
public class CreateOrderRequest : IRpcRequest
{
    public string RequestId { get; set; }
    // Set by the server on resends, no need to initialize it on the client.
    public bool PossibleResend { get; set; }
}
```
The client cannot know whether the handler already ran before the connection dropped, so the flag means "may be a duplicate". Handlers that are not idempotent should check it (for example return the existing result by `RequestId`) — note that this also covers the case where the server itself restarted. Callers that would rather fail than repeat a call can turn the retry off:
``` C#
// Returns immediately with Success = false, Error = "Connection was lost before the response was received"
var response = await _rpcClient.SendAsync<GetUserNameRequest, GetUserNameResponse>(request, autoRetry: false);
```
The client also reconnects on its own when the server closes or restarts the connection (exponential backoff, capped at 60 seconds). A call that is started while the client is disconnected still fails immediately with `Server is not connected`; only a request that was already sent is waited for and resent.
### PUB/SUB Server
1. Configure WebApplication builder; 
``` C#
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalRemsJsonService();
```
2. Setup endpoint for app instance:
``` C#
var app = builder.Build();
app.MapSignalRemsPublisherHub("/signalr/ems/example/pubsub");
``` 
3. Create publisher from IPublisherService, IPublisherService is singleton instance; 
``` C#
IPublisherService publisherService;
var publisher =  _publisherService.CreatePublisher<Person, int>("Message");
```
4. Publish item to topic
``` C#
publisher.Publish(new Person() { Id = id, Age = random.Next(95), Name = $"Person_{id:000}" });
```
### PUB/SUB Client
1. Config subscriber client into dependency injection; 
``` C#
services.AddSignalRemsJsonClient();
```
2. Make connection and call. It supports Lambda expression as filter. This Lambda expression must be able to be explained from both server and client. 
``` C#
ISubscriptionHandler<Person> handler;
await _subscriberClient.ConnectAsync("https://localhost:7198", "/signalr/ems/example/pubsub", stoppingToken);
// Subscribe people whose age is over 60.
var subscription = await _subscriberClient.SubscribeAsync("Message", _personHandler, p=> p.Age > 60);

// Or, subscribe people with given Id 1, 2, 3, as one client can only make one subscription, this example shows in comment. 
// var subscriptionByKeys = await _subscriberClient.SubscribeWithKeysAsync("Message", _personHandler, 1, 2, 3);
```
Please note, if using subscription without keys, one client can only be used to do one subscription. The dispose method will stop the subscription and disconnect from server. We can get multiple client instances from DI container to subscribe with differnet topic/filter. 
If using subscription by keys mode, the client can be used to subscribe/unsubscribe by keys multiple times, but it can't subscribe without given keys. 
## Serialization
Both RPC and PUB/SUB support two formats, `System.Text.Json` (default) and `MessagePack`. Pick the format when registering each application, but note the server and the client of a connection **must use the same format** (`AddSignalRemsJsonService` + `AddSignalRemsJsonClient`, or `AddSignalRemsMessagePackService` + `AddSignalRemsMessagePackClient`).

### Json
Register and optionally pass any custom `JsonConverter`s (forwarded to the serializer):
``` C#
// server
builder.Services.AddSignalRemsJsonService();
// server, with a custom converter and hub options
builder.Services.AddSignalRemsJsonService(o => o.MaximumReceiveMessageSize = 1024 * 128, new MyJsonConverter());

// client
services.AddSignalRemsJsonClient();
// client, with a custom converter
services.AddSignalRemsJsonClient(128, new MyJsonConverter());
```

### MessagePack
Plain POCOs work out of the box: a contractless resolver is used by default so MessagePack behaves like `System.Text.Json`. Types annotated with `[MessagePackObject]` / `[MessagePackFormatter]` are honored, and `DateTime` is round-tripped exactly (kind/ticks preserved) just like with Json.

Pass one or more custom `IFormatterResolver`s when you need custom formatters. Multiple resolvers are combined with `CompositeResolver` and evaluated in order (the first one wins):
``` C#
// server
builder.Services.AddSignalRemsMessagePackService();
// server, with custom resolvers (note: configHub is the first parameter)
builder.Services.AddSignalRemsMessagePackService(null, MyResolver.Instance, NativeGuidResolver.Instance);

// client
services.AddSignalRemsMessagePackClient();
// client, with custom resolvers
services.AddSignalRemsMessagePackClient(128, MyResolver.Instance, NativeGuidResolver.Instance);
```
> Parameter order: `AddSignalRemsJsonClient(maxParallelTaskCount = 128, params JsonConverter[] converters)` and `AddSignalRemsMessagePackClient(maxParallelTaskCount = 128, params IFormatterResolver[] messagePackResolvers)`; on the server the first optional parameter is `Action<HubOptions>? configHub`.

## License
This project is open source and follows MIT license. 

