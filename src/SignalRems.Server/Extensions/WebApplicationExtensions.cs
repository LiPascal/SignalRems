using System.Text.Json.Serialization;
using MessagePack;
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
    /// <summary>
    /// Registers the SignalRems server using System.Text.Json serialization.
    /// </summary>
    public static IServiceCollection AddSignalRemsJsonService(this IServiceCollection service, Action<HubOptions>? configHub = null,
        params JsonConverter[] converters)
        => service.AddSignalRemsServiceCore(configHub, useMessagePack: false, messagePackResolver: null, converters);

    /// <summary>
    /// Registers the SignalRems server using MessagePack serialization. Pass one or more
    /// <paramref name="messagePackResolvers"/> to customize serialization; when none are supplied a contractless
    /// resolver is used (matching System.Text.Json behavior for plain POCOs).
    /// </summary>
    public static IServiceCollection AddSignalRemsMessagePackService(this IServiceCollection service, Action<HubOptions>? configHub = null,
        params IFormatterResolver[] messagePackResolvers)
        => service.AddSignalRemsServiceCore(configHub, useMessagePack: true, CombineResolvers(messagePackResolvers), Array.Empty<JsonConverter>());

    private static IServiceCollection AddSignalRemsServiceCore(this IServiceCollection service, Action<HubOptions>? configHub, bool useMessagePack,
        IFormatterResolver? messagePackResolver, params JsonConverter[] converters)
    {
        SerializeUtil.UseMessagePack = useMessagePack;
        SerializeUtil.Converters = converters;
        SerializeUtil.ConfigureMessagePack(messagePackResolver);
        if (useMessagePack)
        {
            service.AddSignalR(o =>
            {
                configHub?.Invoke(o);
            }).AddMessagePackProtocol(o => o.SerializerOptions = SerializeUtil.ApplyToHubProtocol(o.SerializerOptions));
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

    private static IFormatterResolver? CombineResolvers(IFormatterResolver[] resolvers) => resolvers.Length switch
    {
        0 => null,
        1 => resolvers[0],
        _ => CompositeResolver.Create(resolvers),
    };


    public static void MapSignalRemsPublisherHub(this WebApplication app, string endpoint)
    {
        app.MapHub<PubSubHub>(endpoint);
    }

    public static void MapSignalRemsRpcHub(this WebApplication app, string endpoint)
    {
        app.MapHub<RpcHub>(endpoint);
    }
}