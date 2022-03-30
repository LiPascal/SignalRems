using System.Linq.Expressions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalRems.Core.Events;
using SignalRems.Core.Interfaces;

namespace SignalRems.Client;

internal sealed class SubscriberClient : ClientBase, ISubscriberClient
{
    private readonly List<ISubscription> _subscriptions = new();

    public SubscriberClient(ILogger<SubscriberClient> logger) : base(logger)
    {
    }


    #region interface ISubscriberClient

    public async Task<IDisposable> SubscribeAsync<T>(string topic, ISubscriptionHandler<T> handler,
        Expression<Func<T, bool>>? filter = null) where T : class, new()
    {
        if (Connection == null)
        {
            throw new InvalidOperationException("Not connected to server");
        }

        var subscription = new Subscription<T>(Connection, topic, handler, filter);
        await subscription.StartAsync();
        lock (subscription)
        {
            if (Connection == null)
            {
                subscription.Dispose();
                return subscription;
            }

            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    #endregion

    #region override

    protected override void DoDispose()
    {
        base.DoDispose();
        List<IDisposable> toDisposables = new();
        lock (_subscriptions)
        {
            toDisposables.AddRange(_subscriptions);
        }

        foreach (var disposable in toDisposables)
        {
            disposable.Dispose();
        }
    }

    protected override async Task ConnectionOnReconnected(string? newId)
    {
        await base.ConnectionOnReconnected(newId);
        ISubscription[] subscriptions;
        lock (_subscriptions)
        {
            subscriptions = _subscriptions.ToArray();
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Reset();
            await subscription.ReStartAsync();
        }
    }

    protected override async Task ConnectionOnClosed(Exception? exception)
    {
        await base.ConnectionOnClosed(exception);

        ISubscription[] subscriptions;
        lock (_subscriptions)
        {
            subscriptions = _subscriptions.ToArray();
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Closed();
        }
    }

    #endregion
}