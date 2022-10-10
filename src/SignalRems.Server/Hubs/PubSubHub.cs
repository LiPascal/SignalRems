using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Server.Data;

namespace SignalRems.Server.Hubs;

internal class PubSubHub : Hub
{
    private readonly ILogger<PubSubHub> _logger;
    private readonly IClientCollection<SubscriptionClient> _subscriptionClients;
    private readonly IClientCollection<SubscriptionContext> _subscriptionContexts;

    public PubSubHub(IClientCollection<SubscriptionClient> subscriptionClients, 
        IClientCollection<SubscriptionContext> subscriptionContexts, 
        ILogger<PubSubHub> logger)
    {
        _logger = logger;
        _subscriptionClients = subscriptionClients;
        _subscriptionContexts = subscriptionContexts;
    }

    #region override

    public override async Task OnConnectedAsync()
    {
        _subscriptionClients[Context.ConnectionId] = new SubscriptionClient(Context.ConnectionId);
        await base.OnConnectedAsync();
        var feature = Context.Features.Get<IHttpConnectionFeature>();
        _logger.LogInformation("Established new connection with {0}, ip = {1}, port = {2}", Context.ConnectionId, feature?.RemoteIpAddress, feature?.RemotePort);
    }

    public override async Task OnDisconnectedAsync(Exception? e)
    {
        var client = _subscriptionClients[Context.ConnectionId];
        client.IsConnected = false;
        var subscription = _subscriptionContexts.Values.FirstOrDefault(x => x.ClientId == client.ClientId);
        if (subscription != null)
        {
            subscription.IsSubscribing = false;
        }
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
        _subscriptionClients[Context.ConnectionId].PendingCommands.Enqueue(new SubscriptionCommand(clientSubscriptionContext,
            Command.GetSnapshotAndSubscribe, tcs, filterJson));
        return await tcs.Task;
    }

    // ReSharper disable once UnusedMember.Global
    public async Task<string?> GetSnapshot(string subscriptionId, string topic, string filterJson)
    {
        var clientSubscriptionContext = GetClientSubscriptionContext(subscriptionId, Context.ConnectionId, topic);
        var tcs = new TaskCompletionSource<string?>();
        _subscriptionClients[Context.ConnectionId].PendingCommands.Enqueue(new SubscriptionCommand(clientSubscriptionContext,
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
        _subscriptionClients[Context.ConnectionId].PendingCommands.Enqueue(new SubscriptionCommand(clientSubscriptionContext,
            Command.Subscribe, tcs, filterJson));
        return await tcs.Task;
    }

    public async Task<string?> SubscribeWithKeys(string subscriptionId, string topic, string keys)
    {
        var clientSubscriptionContext = GetClientSubscriptionContext(subscriptionId, Context.ConnectionId, topic);
        if (clientSubscriptionContext.IsSubscribing)
        {
            return "The subscription has started already";
        }
        var tcs = new TaskCompletionSource<string?>();
        _subscriptionClients[Context.ConnectionId].PendingCommands.Enqueue(new SubscriptionCommand(clientSubscriptionContext,
            Command.SubscribeWithKeys, tcs, keys));
        return await tcs.Task;
    }

    public async Task<string?> AddSubscriptionKeys(string subscriptionId, string keys)
    {
        if (!_subscriptionContexts.ContainsKey(subscriptionId))
        {
            return "Subscription doesn't exists";
        }
        var clientSubscriptionContext = _subscriptionContexts[subscriptionId]; 
        if (!clientSubscriptionContext.IsSubscribing)
        {
            return "The subscription has not started.";
        }
        var tcs = new TaskCompletionSource<string?>();
        _subscriptionClients[Context.ConnectionId].PendingCommands.Enqueue(new SubscriptionCommand(clientSubscriptionContext,
            Command.AddSubscriptionKeys, tcs, keys));
        return await tcs.Task;
    }

    public async Task<string?> RemoveSubscriptionKeys(string subscriptionId, string keys)
    {
        if (!_subscriptionContexts.ContainsKey(subscriptionId))
        {
            return "Subscription doesn't exists";
        }
        var clientSubscriptionContext = _subscriptionContexts[subscriptionId];
        if (!clientSubscriptionContext.IsSubscribing)
        {
            return "The subscription has not started.";
        }
        var tcs = new TaskCompletionSource<string?>();
        _subscriptionClients[Context.ConnectionId].PendingCommands.Enqueue(new SubscriptionCommand(clientSubscriptionContext,
            Command.RemoveSubscriptionKeys, tcs, keys));
        return await tcs.Task;
    }

    // ReSharper disable once UnusedMember.Global
    public async Task<string?> UnSubscribe(string subscriptionId)
    {
        if (!_subscriptionContexts.TryGetValue(subscriptionId, out var clientSubscriptionContext) || !clientSubscriptionContext.IsSubscribing)
        {
            return "Invalid action, the subscription is not running";
        }

        var tcs = new TaskCompletionSource<string?>();
        _subscriptionClients[Context.ConnectionId].PendingCommands.Enqueue(new SubscriptionCommand(clientSubscriptionContext,
            Command.UnSubscribe, tcs));
        return await tcs.Task;
    }

    #endregion

    #region private

    private SubscriptionContext GetClientSubscriptionContext(string subscriptionId, string clientId, string topic)
    {
        var client = _subscriptionClients[clientId];
        var subscriptionContext = _subscriptionContexts.GetOrAdd(subscriptionId, t =>
        {
            _logger.LogInformation("Create new subscription client SubscriptionId={0} ClientId={1} Topic={2}", subscriptionId, client.ClientId, topic);
            return new SubscriptionContext(subscriptionId, client.ClientId, topic);
        });

        if (subscriptionContext.ClientId != clientId)
        {
            if (_subscriptionClients.ContainsKey(subscriptionContext.ClientId))
            {
                _subscriptionClients[subscriptionContext.ClientId].SubscriptionContexts.TryRemove(subscriptionId, out _);
            }
            //It happens when client reconnect;
            subscriptionContext.ClientId = clientId;
            subscriptionContext.IsSubscribing = false;
            client.SubscriptionContexts[subscriptionId] = subscriptionContext;
        }
        return subscriptionContext;
    }
    #endregion
}