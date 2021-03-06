using System.Collections.Concurrent;

namespace SignalRems.Server.Data;

internal class SubscriptionContext
{
    public static ConcurrentDictionary<string, SubscriptionContext> Subscriptions { get; } = new();

    public SubscriptionContext(string subscriptionId,string clientId, string topic)
    {
        SubscriptionId = subscriptionId;
        ClientId = clientId;
        Topic = topic;
    }

    public string SubscriptionId { get; }
    public string ClientId { get; set; }
    public string Topic { get; }
    public bool IsSubscribing { get; set; }
    public object? Filter { get; set; }
}