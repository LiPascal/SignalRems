using SignalRems.Core.Interfaces;
using SignalRems.Server.Data;
using SignalRems.Server.Hubs;

namespace SignalRems.Server.Extensions;

public static class WebApplicationExtensions
{
    public static IServiceCollection AddSignalRemsService(this IServiceCollection service)
    {
        service.AddSignalR().AddMessagePackProtocol();
        service.AddSingleton<IPublisherService, PublisherService>();
        service.AddSingleton<IRpcService, RpcService>();
        return service;
    }

    public static void MapSignalRemsPublisherHub(this WebApplication app, string endpoint)
    {
        app.MapHub<PubSubHub>(endpoint);
    }

    public static void MapSignalRemsRpcHub(this WebApplication app, string endpoint)
    {
        app.MapHub<RpcHub>(endpoint);
    }
}