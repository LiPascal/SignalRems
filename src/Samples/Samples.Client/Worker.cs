using Samples.Model;
using SignalRems.Core.Interfaces;

namespace Samples.Client;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ISubscriptionHandler<Person> _personHandler;
    private readonly ISubscriberClient _subscriberClient;
    private readonly IRpcClient _rpcClient;

    public Worker(IServiceScopeFactory factory, ILogger<Worker> logger)
    {
        _logger = logger;
        var provider = factory.CreateScope().ServiceProvider;
        _personHandler = provider.GetRequiredService<ISubscriptionHandler<Person>>();
        _subscriberClient = provider.GetRequiredService<ISubscriberClient>();
        _rpcClient = provider.GetRequiredService<IRpcClient>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _subscriberClient.ConnectAsync("https://localhost:7198", "/signalr/ems/example/pubsub", stoppingToken);
        await _rpcClient.ConnectAsync("https://localhost:7198", "/signalr/ems/example/rpc", stoppingToken);

        var subscription = await _subscriberClient.SubscribeAsync("Message", _personHandler, p=> p.Age > 60);
        int i = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var request1 = new GetUserAgeRequest() { UserId = i.ToString() };
            var request2 = new GetUserNameRequest() { UserId = i.ToString() };
            _logger.LogInformation("Checking user({user})'s age", request1.UserId);
            var response1 = await _rpcClient.SendAsync<GetUserAgeRequest, GetUserAgeResponse>(request1);
            _logger.LogInformation("User({user})'s age is {age}", request1.UserId, response1.UserAge);
            _logger.LogInformation("Checking user({user})'s name", request2.UserId);
            var response2 = await _rpcClient.SendAsync<GetUserNameRequest, GetUserNameResponse>(request2);
            _logger.LogInformation("User({user})'s age is {name}", request2.UserId, response2.UserName);

            ++i;
            await Task.Delay(1000, stoppingToken);
        }
        subscription.Dispose();
    }    
}