using Microsoft.Extensions.DependencyInjection;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Utils;

namespace SignalRems.Client.Extensions;

public static class ServicesExtensions
{
    public static IServiceCollection AddSignalRemsClient(this IServiceCollection service, bool useMessagePack = false)
    {
        SerializeUtil.UseMessagePack = useMessagePack;
        service.AddTransient<ISubscriberClient, SubscriberClient>();
        service.AddTransient<IRpcClient, RpcClient>();

        return service;
    }
}