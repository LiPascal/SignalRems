using System.Text.Json.Serialization;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.SignalR;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;
using SignalRems.Server.Data;
using SignalRems.Server.Hubs;
using SignalRems.Server.Interfaces;

namespace SignalRems.Server.Extensions;

public static class WebApplicationExtensions
{
    public static IServiceCollection AddSignalRemsService(this IServiceCollection service, Action<HubOptions>? configHub = null,  bool useMessagePack = false,
        params JsonConverter[] converters)
    {
        SerializeUtil.UseMessagePack = useMessagePack;
        SerializeUtil.Converters = converters;
        if (useMessagePack)
        {
            service.AddSignalR(o =>
            {
                configHub?.Invoke(o);
            }).AddMessagePackProtocol();
        }
        else
        {
            service.AddSignalR(o =>
            {
                configHub?.Invoke(o);
            }).AddJsonProtocol(config =>
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
        service.AddSingleton<IClientCollection<RemoteCallerClient>, ClientCollection<RemoteCallerClient>>();
        service.AddSingleton<IClientCollection<SubscriptionClient>, ClientCollection<SubscriptionClient>>();
        service.AddSingleton<IClientCollection<SubscriptionContext>, ClientCollection<SubscriptionContext>>();
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