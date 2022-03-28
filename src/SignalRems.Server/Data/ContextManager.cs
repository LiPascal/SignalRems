using System.Collections.Concurrent;

namespace SignalRems.Server.Data;

internal class ContextManager
{
    public ConcurrentDictionary<string, ClientStatus> Clients { get; } = new();
    public ConcurrentDictionary<string, SubscriptionContext> Subscriptions { get; } = new();
}