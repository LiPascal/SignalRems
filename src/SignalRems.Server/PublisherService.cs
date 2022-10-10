using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using SignalRems.Core.Interfaces;
using SignalRems.Server.Data;
using SignalRems.Server.Hubs;

namespace SignalRems.Server;

internal class PublisherService : IPublisherService
{
    private bool _running;
    private Task? _serviceTask;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClientCollection<SubscriptionClient> _clients;
    private readonly ILogger<PublisherService> _logger;
    private readonly ConcurrentDictionary<string, IPublisherWorker> _publishers = new();

    public PublisherService(IServiceProvider serviceProvider, IClientCollection<SubscriptionClient> clients, ILogger<PublisherService> logger)
    {
        _serviceProvider = serviceProvider;
        _clients = clients;
        _logger = logger;
    }

    public IPublisher<T> CreatePublisher<T, TKey>(string topic) where T : class, new() where TKey : notnull
    {
        var logger = _serviceProvider.GetService<ILogger<Publisher<T, TKey>>>();
        var hubContext = _serviceProvider.GetService<IHubContext<PubSubHub>>();
        Debug.Assert(hubContext != null, nameof(hubContext) + " != null");
        Debug.Assert(logger != null, nameof(logger) + " != null");
        var publisher = new Publisher<T, TKey>(logger, hubContext, _clients, topic);
        _publishers[topic] = publisher;
        _logger.LogInformation("Created publisher for topic {topic}, type = {type}", topic, typeof(T));
        return publisher;
    }

    public void Start()
    {
        if (_serviceTask != null)
        {
            return;
        }

        _running = true;
        _serviceTask = Task.Run(async () =>
        {
            while (_running)
            {
                var updated = false;
                foreach (var client in _clients.Values.Where(x => x.IsConnected))
                {
                    while (client.PendingCommands.TryDequeue(out var command))
                    {
                        if (_publishers.ContainsKey(command.Context.Topic))
                        {
                            _publishers[command.Context.Topic].DispatchCommand(command);
                            _logger.LogInformation(
                                "Dispatch command {command.CommandName} to topic {subscription.Topic}",
                                command.CommandName, command.Context.Topic);
                        }
                        else
                        {
                            command.CompleteSource.SetResult("Server doesn't support this topic");
                        }

                        updated = true;
                    }
                }

                foreach (var publisher in _publishers.Values.Where(p => p.IsIdle))
                {
                    publisher.Work();
                }

                if (!updated)
                {
                    await Task.Delay(10);
                }
            }
        });
    }

    public async void Dispose()
    {
        _running = false;
        if (_serviceTask == null)
        {
            return;
        }

        await _serviceTask;
        _serviceTask = null;
    }
}