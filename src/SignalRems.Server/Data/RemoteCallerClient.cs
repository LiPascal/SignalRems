using System.Collections.Concurrent;
using SignalRems.Core.Interfaces;

namespace SignalRems.Server.Data;

internal class RemoteCallerClient
{
    public static ConcurrentDictionary<string, RemoteCallerClient> Clients { get; } = new();

    public RemoteCallerClient(string clientId)
    {
        ClientId = clientId;
        IsConnected = true;
    }

    public string ClientId { get; }
    public bool IsConnected { get; set; }
    public ConcurrentQueue<RemoteCallerCommand> PendingCommands { get; } = new();
}