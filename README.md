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

