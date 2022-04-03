using SignalRems.Core.Interfaces;
using SignalRems.Core.Utils;
using SignalRems.Server.Data;
using SignalRems.Server.Hubs;

namespace SignalRems.Server.Extensions;

public static class WebApplicationExtensions
{
    public static IServiceCollection AddSignalRemsService(this IServiceCollection service, bool useMessagePack = false)
    {
        SerializeUtil.UseMessagePack = useMessagePack;
        if (useMessagePack)
        {
            service.AddSignalR().AddMessagePackProtocol();
        }
        else
        {
            service.AddSignalR();
        }
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