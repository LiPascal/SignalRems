using Samples.Server;
using SignalRems.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalRemsService();
builder.Services.AddHostedService<Worker>();
var app = builder.Build();
app.MapSignalRemsHub("/signalr/ems/example");
app.Run();
