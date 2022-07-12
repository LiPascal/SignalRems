using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalRems.Core.Interfaces;

namespace SignalRems.Client;

internal sealed class SubscriberClient : ClientBase, ISubscriberClient
{
    private ISubscription? _subscription;
    private int _subscriptionCount = 0;
    private readonly IServiceProvider _serviceCollection;

    public SubscriberClient(ILogger<SubscriberClient> logger, IServiceProvider serviceCollection) : base(logger)
    {
        _serviceCollection = serviceCollection;
    }


    #region interface ISubscriberClient

    public async Task<IDisposable> SubscribeAsync<T>(string topic, ISubscriptionHandler<T> handler,
        Expression<Func<T, bool>>? filter = null) where T : class, new()
    {
        if (Connection == null)
        {
            throw new InvalidOperationException("Not connected to server");
        }

        if (Interlocked.Exchange(ref _subscriptionCount, 1) != 0)
        {
            throw new InvalidOperationException("One client instance can only subscribe once");
        }

        await ConnectionCompleteTask;

        Logger.LogInformation("Subscribe topic = {topic}, type={type}", topic, typeof(T));
        _subscription = new Subscription<T>(_serviceCollection.GetService<ILogger<Subscription<T>>>() ?? throw new InvalidOperationException(), Connection, topic, handler, filter);
        if (Connection == null)
        {
            _subscription.Dispose();
            return _subscription;
        }
        await _subscription.StartAsync();
       
        return _subscription;
    }

    #endregion

    #region override

    protected override void DoDispose()
    {
        _subscription?.Dispose();
        base.DoDispose();       
    }

    protected override async Task ConnectionOnReconnected(string? newId)
    {
        await base.ConnectionOnReconnected(newId);
        _subscription?.Reset();
        await (_subscription?.ReStartAsync() ?? Task.CompletedTask);
    }

    protected override async Task ConnectionOnClosed(Exception? exception)
    {
        await base.ConnectionOnClosed(exception);
        _subscription?.Closed();
    }

    #endregion
}