using System.Text.Json.Serialization;
using MessagePack.Formatters;
using Microsoft.Extensions.DependencyInjection;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Utils;

namespace SignalRems.Client.Extensions;

public static class ServicesExtensions
{
    public static IServiceCollection AddSignalRemsClient(this IServiceCollection service, bool useMessagePack = false, params JsonConverter[] converters)
    {
        SerializeUtil.UseMessagePack = useMessagePack;
        SerializeUtil.Converters = converters;
        service.AddTransient<ISubscriberClient, SubscriberClient>();
        service.AddTransient<IRpcClient, RpcClient>();
        return service;
    }
}