using System.Collections.Concurrent;

namespace SignalRems.Server.Data;

internal class ClientStatus
{
    public ClientStatus(string clientId)
    {
        ClientId = clientId;
        IsConnected = true;
    }
    public string ClientId { get; }
    public bool IsConnected { get; set; }
    public ConcurrentDictionary<string, SubscriptionContext> SubscriptionContexts { get; set; } = new();
    public ConcurrentQueue<ClientCommand> PendingCommands { get; } = new();
}