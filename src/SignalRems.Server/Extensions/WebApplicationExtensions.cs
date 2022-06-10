using System.Text.Json.Serialization;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Utils;
using SignalRems.Server.Data;
using SignalRems.Server.Hubs;
using SignalRems.Server.Interfaces;

namespace SignalRems.Server.Extensions;

public static class WebApplicationExtensions
{
    public static IServiceCollection AddSignalRemsService(this IServiceCollection service, bool useMessagePack = false,params JsonConverter[] converters)
    {
        SerializeUtil.UseMessagePack = useMessagePack;
        if (useMessagePack)
        {
            service.AddSignalR().AddMessagePackProtocol();
        }
        else
        {
            service.AddSignalR().AddJsonProtocol(config =>
            {
                foreach (var converter in converters)
                {
                    config.PayloadSerializerOptions.Converters.Add(converter);
                }
               
            });
        }

        service.AddSingleton<IPublisherService, PublisherService>();
        service.AddSingleton<RpcService>();
        service.AddSingleton<IRpcService, RpcService>(provider => provider.GetService<RpcService>()!);
        service.AddSingleton<IRpcServer, RpcService>(provider => provider.GetService<RpcService>()!);
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