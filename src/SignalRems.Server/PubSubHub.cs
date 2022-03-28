using Microsoft.AspNetCore.SignalR;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Server.Data;

namespace SignalRems.Server;

internal class PubSubHub : Hub
{
    private readonly ContextManager _contextManager;

    public PubSubHub(ContextManager contextManager)
    {
        _contextManager = contextManager;
    }

    #region override

    public override async Task OnConnectedAsync()
    {
        _contextManager.Clients[Context.ConnectionId] = new ClientStatus(Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? e)
    {
        _contextManager.Clients[Context.ConnectionId].IsConnected = false;
        await base.OnDisconnectedAsync(e);
    }

    #endregion

    #region api

    // ReSharper disable once UnusedMember.Global
    public async Task<Exception?> GetSnapshotAndSubscribe(string subscriptionId, string topic, string filterJson)
    {
        var clientSubscriptionContext = GetClientSubscriptionContext(subscriptionId, Context.ConnectionId, topic);
        if (clientSubscriptionContext.IsSubscribing)
        {
            return new InvalidOperationException("The subscription has started already");
        }

        var tcs = new TaskCompletionSource<Exception?>();
        _contextManager.Clients[Context.ConnectionId].PendingCommands.Enqueue(new ClientCommand(clientSubscriptionContext,
            Command.GetSnapshotAndSubscribe, tcs, filterJson));
        return await tcs.Task;
    }

    // ReSharper disable once UnusedMember.Global
    public async Task<Exception?> GetSnapshot(string subscriptionId, string topic, string filterJson)
    {
        var clientSubscriptionContext = GetClientSubscriptionContext(subscriptionId, Context.ConnectionId, topic);
        var tcs = new TaskCompletionSource<Exception?>();
        _contextManager.Clients[Context.ConnectionId].PendingCommands.Enqueue(new ClientCommand(clientSubscriptionContext,
            Command.GetSnapshot, tcs, filterJson));
        return await tcs.Task;
    }

    // ReSharper disable once UnusedMember.Global
    public async Task<Exception?> Subscribe(string subscriptionId, string topic, string filterJson)
    {
        var clientSubscriptionContext = GetClientSubscriptionContext(subscriptionId, Context.ConnectionId, topic);
        if (clientSubscriptionContext.IsSubscribing)
        {
            return new InvalidOperationException("The subscription has started already");
        }

        var tcs = new TaskCompletionSource<Exception?>();
        _contextManager.Clients[Context.ConnectionId].PendingCommands.Enqueue(new ClientCommand(clientSubscriptionContext,
            Command.Subscribe, tcs, filterJson));
        return await tcs.Task;
    }

    // ReSharper disable once UnusedMember.Global
    public async Task<Exception?> UnSubscribe(string subscriptionId)
    {
        if (!_contextManager.Subscriptions.TryGetValue(subscriptionId, out var clientSubscriptionContext) || !clientSubscriptionContext.IsSubscribing)
        {
            return new InvalidOperationException("Invalid action, the subscription is not running");
        }

        var tcs = new TaskCompletionSource<Exception?>();
        _contextManager.Clients[Context.ConnectionId].PendingCommands.Enqueue(new ClientCommand(clientSubscriptionContext,
            Command.UnSubscribe, tcs));
        return await tcs.Task;
    }

    #endregion

    #region private

    private SubscriptionContext GetClientSubscriptionContext(string subscriptionId, string clientId, string topic)
    {
        var client = _contextManager.Clients[clientId];
        var subscriptionContext = _contextManager.Subscriptions.GetOrAdd(subscriptionId, t => new SubscriptionContext(subscriptionId, client.ClientId, topic));

        if (subscriptionContext.ClientId != clientId)
        {
            if (_contextManager.Clients.ContainsKey(subscriptionContext.ClientId))
            {
                _contextManager.Clients[subscriptionContext.ClientId].SubscriptionContexts
                    .TryRemove(subscriptionId, out _);
            }
            //It happens when client reconnect;
            subscriptionContext.ClientId = clientId;
            client.SubscriptionContexts[subscriptionId] = subscriptionContext;
        }
        return subscriptionContext;
    }
    #endregion
}