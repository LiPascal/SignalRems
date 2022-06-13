using Samples.Server;
using SignalRems.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalRemsService();
builder.Services.AddSingleton<UserInfoQueryHandler>();
builder.Services.AddHostedService<Worker>();
var app = builder.Build();
app.MapSignalRemsPublisherHub("/signalr/ems/example/pubsub");
app.MapSignalRemsRpcHub("/signalr/ems/example/rpc");
app.Run();
