using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalRems.Core.Interfaces;

namespace SignalRems.Client;

internal sealed class SubscriberClient : ClientBase, ISubscriberClient
{
    private ISubscription? _subscription;
    private ISubscription? _subscriptionByKeys;
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

        if (_subscriptionByKeys != null)
        {
            throw new InvalidOperationException("Keys client can't use this method.");
        }

        await ConnectionCompleteTask;

        Logger.LogInformation("Subscribe topic = {topic}, type={type}", topic, typeof(T));
        _subscription =
            new Subscription<T>(
                _serviceCollection.GetService<ILogger<Subscription<T>>>() ?? throw new InvalidOperationException(),
                Connection, topic, handler, filter);
        if (Connection == null)
        {
            _subscription.Dispose();
            return _subscription;
        }

        await _subscription.StartAsync();

        return _subscription;
    }

    public async Task<IDisposable> SubscribeWithKeysAsync<T, TKey>(string topic, ISubscriptionHandler<T> handler,
        params TKey[] keys) where T : class, new()
    {
        await CheckStatusAsync();
        if (_subscriptionByKeys == null)
        {
            Logger.LogInformation("Subscribe topic = {topic} with keys, type={type}", topic, typeof(T));
            _subscriptionByKeys = new SubscriptionByKeys<T, TKey>(
                _serviceCollection.GetService<ILogger<SubscriptionByKeys<T, TKey>>>() ??
                throw new InvalidOperationException(), Connection!, topic, handler, keys);
            if (Connection == null)
            {
                _subscriptionByKeys.Dispose();
                return _subscriptionByKeys;
            }

            await _subscriptionByKeys.StartAsync();
        }
        else
        {
            await ((ISubscriptionByKeys<TKey>)_subscriptionByKeys).AddKeysAsync(keys);
        }

        return _subscriptionByKeys;
    }

    public async Task<IDisposable> UnSubscribeWithKeysAsync<T, TKey>(string topic, ISubscriptionHandler<T> handler,
        params TKey[] keys) where T : class, new()
    {
        await CheckStatusAsync();
        if (_subscriptionByKeys == null)
        {
            throw new InvalidOperationException("Subscription didn't start.");
        }

        await ((ISubscriptionByKeys<TKey>)_subscriptionByKeys).RemoveKeysAsync(keys);
        return _subscriptionByKeys;
    }

    #endregion

    #region override

    protected override void DoDispose()
    {
        Subscription?.Dispose();
        base.DoDispose();
    }

    protected override async Task ConnectionOnReconnected(string? newId)
    {
        await base.ConnectionOnReconnected(newId);
        Subscription?.Reset();
        await (Subscription?.ReStartAsync() ?? Task.CompletedTask);
    }

    protected override async Task ConnectionOnClosed(Exception? exception)
    {
        await base.ConnectionOnClosed(exception);
        Subscription?.Closed();
    }

    #endregion

    #region private

    private ISubscription? Subscription => _subscription ?? _subscriptionByKeys;

    private async Task CheckStatusAsync()
    {
        if (Connection == null)
        {
            throw new InvalidOperationException("Not connected to server");
        }

        if (_subscription != null)
        {
            throw new InvalidOperationException("Only keys client can use this method.");
        }

        await ConnectionCompleteTask;
    }

    #endregion
}