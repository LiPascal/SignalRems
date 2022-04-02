using Microsoft.Extensions.DependencyInjection;
using SignalRems.Core.Interfaces;

namespace SignalRems.Client.Extensions;

public static class ServicesExtensions
{
    public static IServiceCollection AddSignalRemsClient(this IServiceCollection service)
    {
        service.AddTransient<ISubscriberClient, SubscriberClient>();
        service.AddTransient<IRpcClient, RpcClient>();
        return service;
    }
}