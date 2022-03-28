using Samples.Model;
using SignalRems.Core.Interfaces;

namespace Samples.Client;

public class Worker : BackgroundService
{
    private readonly IMessageHandler<Person> _personHandler;
    private readonly ISubscriberClient _client;

    public Worker(IServiceScopeFactory factory)
    {
        var provider = factory.CreateScope().ServiceProvider;
        _personHandler = provider.GetRequiredService<IMessageHandler<Person>>();
        _client = provider.GetRequiredService<ISubscriberClient>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _client.ConnectAsync("https://localhost:7198", "/signalr/ems/example", stoppingToken);
        var subscription = await _client.SubscribeAsync("Message", _personHandler, p=> p.Age > 60);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
        subscription.Dispose();
    }    
}