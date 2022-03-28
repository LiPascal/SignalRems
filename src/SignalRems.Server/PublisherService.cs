using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using SignalRems.Core.Interfaces;
using SignalRems.Server.Data;

namespace SignalRems.Server;

internal class PublisherService : IPublisherService
{
    private bool _running = true;
    private bool _started = false;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PublisherService> _logger;
    private readonly ConcurrentDictionary<string, IPublisherWorker> _publishers = new();
    private readonly ContextManager _contextManager;

    public PublisherService(IServiceProvider serviceProvider, ILogger<PublisherService> logger,
        ContextManager contextManager)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _contextManager = contextManager;
    }

    public IPublisher<T> CreatePublisher<T, TKey>(string topic) where T : class, new() where TKey : notnull
    {
        var logger = _serviceProvider.GetService<ILogger<Publisher<T, TKey>>>();
        var hubContext = _serviceProvider.GetService<IHubContext<PubSubHub>>();
        Debug.Assert(hubContext != null, nameof(hubContext) + " != null");
        Debug.Assert(logger != null, nameof(logger) + " != null");
        var publisher = new Publisher<T, TKey>(logger, hubContext, _contextManager, topic);
        _publishers[topic] = publisher;
        _logger.LogInformation("Created publisher for topic {topic}, type = {type}", topic, typeof(T));
        return publisher;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        Task.Run(async () =>
        {
            while (_running)
            {
                var updated = false;
                foreach (var client in _contextManager.Clients.Values.Where(x => x.IsConnected))
                {
                    while (client.PendingCommands.TryDequeue(out var command))
                    {
                        if (_publishers.ContainsKey(command.Context.Topic))
                        {
                            await _publishers[command.Context.Topic].DispatchCommandAsync(command).ConfigureAwait(true);
                            _logger.LogInformation("Dispatch command {command.CommandName} to topic {subscription.Topic}", command.CommandName, command.Context.Topic);
                        }
                        else
                        {
                            command.CompleteSource.SetResult(new NotSupportedException("Server doesn't support this topic"));
                        }
                        updated = true;
                    }
                }

                await Parallel.ForEachAsync(_publishers.Values, async (publisher, c) =>
                {
                    if (await publisher.WorkAsync())
                    {
                        updated = true;
                    }
                }).ConfigureAwait(true);

                if (!updated)
                {
                    await Task.Delay(10);
                }
            }
        });
    }

    public void Dispose()
    {
        _running = false;
    }
}