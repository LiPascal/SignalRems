using SignalRems.Core.Interfaces;
using SignalRems.Server.Data;

namespace SignalRems.Server.Extensions;

public static class WebApplicationExtensions
{
    public static IServiceCollection AddSignalRemsService(this IServiceCollection service)
    {
        service.AddSignalR().AddMessagePackProtocol();
        service.AddSingleton<IPublisherService, PublisherService>();
        service.AddSingleton<ContextManager>();
        return service;
    }

    public static void MapSignalRemsHub(this WebApplication app, string endpoint)
    {
        app.MapHub<PubSubHub>(endpoint);
    }
}