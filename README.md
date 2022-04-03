# [SignalR EMS](https://github.com/LiPascal/SignalRems)
## Description
SignalRems is one Enterprise Messaging System (EMS) implemented by .NET Standard SignalR. It provides two communication models, RPC and PUB/SUB. This library will provide strong typed API to help client application communicate with server. 
It is using Newtonsoft.json or MessagePack for serialization/deserialization. 
In PUB/SUB mode, it subscribes with filter of Lambda expression. 
## Dependency
This libiary is built on [SingalR](https://dotnet.microsoft.com/en-us/apps/aspnet/signalr), hence the server application must be using "Microsoft.NET.Sdk.Web" SDK. The client side could use any .net 6.0 SDK. 
## API usage
### RPC Server
1. Configure WebApplication builder; 
``` C#
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalRemsService(); // Using Json serialization/deserialization
// builder.Services.AddSignalRemsService(true); // MessagePack serialization/deserialization
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
services.AddSignalRemsClient(); // Using Json serialization/deserialization
// services.AddSignalRemsClient(true); // MessagePack serialization/deserialization
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
builder.Services.AddSignalRemsService();
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
services.AddSignalRemsClient();
```
2. Make connection and call. It supports Lambda expression as filter. This Lambda expression must be able to be explained from both server and client. 
``` C#
ISubscriptionHandler<Person> handler;
await _subscriberClient.ConnectAsync("https://localhost:7198", "/signalr/ems/example/pubsub", stoppingToken);
var subscription = await _subscriberClient.SubscribeAsync("Message", handler, p=> p.Age > 60);
```
Please note, one client can only used to do one subscription. The dispose method will stop the subscrition and disconnect from server. We can get multiple client instance from DI container to subscribe with differnet topic/filter. 
## License
This project is open source and follows MIT license. 

