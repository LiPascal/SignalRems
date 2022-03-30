using System.Collections.Concurrent;
using SignalRems.Core.Interfaces;

namespace SignalRems.Server.Data;

internal class SubscriptionClient
{
    public static ConcurrentDictionary<string, SubscriptionClient> Clients { get; } = new();
    public SubscriptionClient(string clientId)
    {
        ClientId = clientId;
        IsConnected = true;
    }
    public string ClientId { get; }
    public bool IsConnected { get; set; }
    public ConcurrentDictionary<string, SubscriptionContext> SubscriptionContexts { get; set; } = new();
    public ConcurrentQueue<SubscriptionCommand> PendingCommands { get; } = new();
}