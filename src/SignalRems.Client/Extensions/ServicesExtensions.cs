using System.Text.Json.Serialization;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Utils;

namespace SignalRems.Client.Extensions;

public static class ServicesExtensions
{
    /// <summary>
    /// Registers the SignalRems client using System.Text.Json serialization.
    /// </summary>
    public static IServiceCollection AddSignalRemsJsonClient(this IServiceCollection service, int maxParallelTaskCount = 128,
        params JsonConverter[] converters)
        => service.AddSignalRemsClientCore(useMessagePack: false, maxParallelTaskCount, messagePackResolver: null, converters);

    /// <summary>
    /// Registers the SignalRems client using MessagePack serialization. Pass one or more
    /// <paramref name="messagePackResolvers"/> to customize serialization; when none are supplied a contractless
    /// resolver is used (matching System.Text.Json behavior for plain POCOs).
    /// </summary>
    public static IServiceCollection AddSignalRemsMessagePackClient(this IServiceCollection service, int maxParallelTaskCount = 128,
        params IFormatterResolver[] messagePackResolvers)
        => service.AddSignalRemsClientCore(useMessagePack: true, maxParallelTaskCount, CombineResolvers(messagePackResolvers), Array.Empty<JsonConverter>());

    private static IServiceCollection AddSignalRemsClientCore(this IServiceCollection service, bool useMessagePack, int maxParallelTaskCount,
        IFormatterResolver? messagePackResolver, params JsonConverter[] converters)
    {
        SerializeUtil.UseMessagePack = useMessagePack;
        SerializeUtil.Converters = converters;
        SerializeUtil.ConfigureMessagePack(messagePackResolver);

        service.AddTransient<ISubscriberClient, SubscriberClient>();
        service.AddTransient<IRpcClient, RpcClient>(p=> new RpcClient(p.GetService<ILogger<RpcClient>>() ?? throw new Exception("No log provided."), maxParallelTaskCount));
        return service;
    }

    private static IFormatterResolver? CombineResolvers(IFormatterResolver[] resolvers) => resolvers.Length switch
    {
        0 => null,
        1 => resolvers[0],
        _ => CompositeResolver.Create(resolvers),
    };
}