using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using SignalRems.Core.Models;
using SignalRems.Server.Data;

namespace SignalRems.Server.Hubs;

internal class PubSubHub : Hub
{
    private readonly ILogger<PubSubHub> _logger;
    public PubSubHub(ILogger<PubSubHub> logger)
    {
        _logger = logger;
    }

    #region override

    public override async Task OnConnectedAsync()
    {
        SubscriptionClient.Clients[Context.ConnectionId] = new SubscriptionClient(Context.ConnectionId);
        await base.OnConnectedAsync();
        var feature = Context.Features.Get<IHttpConnectionFeature>();
        _logger.LogInformation("Established new connection with {0}, ip = {1}, port = {2}", Context.ConnectionId, feature?.RemoteIpAddress, feature?.RemotePort);
    }

    public override async Task OnDisconnectedAsync(Exception? e)
    {
        SubscriptionClient.Clients[Context.ConnectionId].IsConnected = false;
        await base.OnDisconnectedAsync(e);
        _logger.LogInformation("Connection {0} lost", Context.ConnectionId);
    }

    #endregion

    #region api

    // ReSharper disable once UnusedMember.Global
    public async Task<string?> GetSnapshotAndSubscribe(string subscriptionId, string topic, string filterJson)
    {
        var clientSubscriptionContext = GetClientSubscriptionContext(subscriptionId, Context.ConnectionId, topic);
        if (clientSubscriptionContext.IsSubscribing)
        {
            return "The subscription has started already";
        }

        var tcs = new TaskCompletionSource<string?>();
        SubscriptionClient.Clients[Context.ConnectionId].PendingCommands.Enqueue(new SubscriptionCommand(clientSubscriptionContext,
            Command.GetSnapshotAndSubscribe, tcs, filterJson));
        return await tcs.Task;
    }

    // ReSharper disable once UnusedMember.Global
    public async Task<string?> GetSnapshot(string subscriptionId, string topic, string filterJson)
    {
        var clientSubscriptionContext = GetClientSubscriptionContext(subscriptionId, Context.ConnectionId, topic);
        var tcs = new TaskCompletionSource<string?>();
        SubscriptionClient.Clients[Context.ConnectionId].PendingCommands.Enqueue(new SubscriptionCommand(clientSubscriptionContext,
            Command.GetSnapshot, tcs, filterJson));
        return await tcs.Task;
    }

    // ReSharper disable once UnusedMember.Global
    public async Task<string?> Subscribe(string subscriptionId, string topic, string filterJson)
    {
        var clientSubscriptionContext = GetClientSubscriptionContext(subscriptionId, Context.ConnectionId, topic);
        if (clientSubscriptionContext.IsSubscribing)
        {
            return "The subscription has started already";
        }

        var tcs = new TaskCompletionSource<string?>();
        SubscriptionClient.Clients[Context.ConnectionId].PendingCommands.Enqueue(new SubscriptionCommand(clientSubscriptionContext,
            Command.Subscribe, tcs, filterJson));
        return await tcs.Task;
    }

    // ReSharper disable once UnusedMember.Global
    public async Task<string?> UnSubscribe(string subscriptionId)
    {
        if (!SubscriptionContext.Subscriptions.TryGetValue(subscriptionId, out var clientSubscriptionContext) || !clientSubscriptionContext.IsSubscribing)
        {
            return "Invalid action, the subscription is not running";
        }

        var tcs = new TaskCompletionSource<string?>();
        SubscriptionClient.Clients[Context.ConnectionId].PendingCommands.Enqueue(new SubscriptionCommand(clientSubscriptionContext,
            Command.UnSubscribe, tcs));
        return await tcs.Task;
    }

    #endregion

    #region private

    private static SubscriptionContext GetClientSubscriptionContext(string subscriptionId, string clientId, string topic)
    {
        var client = SubscriptionClient.Clients[clientId];
        var subscriptionContext = SubscriptionContext.Subscriptions.GetOrAdd(subscriptionId, t => new SubscriptionContext(subscriptionId, client.ClientId, topic));

        if (subscriptionContext.ClientId != clientId)
        {
            if (SubscriptionClient.Clients.ContainsKey(subscriptionContext.ClientId))
            {
                SubscriptionClient.Clients[subscriptionContext.ClientId].SubscriptionContexts.TryRemove(subscriptionId, out _);
            }
            //It happens when client reconnect;
            subscriptionContext.ClientId = clientId;
            client.SubscriptionContexts[subscriptionId] = subscriptionContext;
        }
        return subscriptionContext;
    }
    #endregion
}