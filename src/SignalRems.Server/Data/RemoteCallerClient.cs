using System.Collections.Concurrent;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;

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

    public bool IsIdle { get; private set; } = true;

    public void ProcessPending(
        ConcurrentDictionary<(string, string), Func<RpcRequestWrapper, Task<RpcResultWrapper>>> handlers)
    {
        if (!PendingCommands.Any())
        {
            return;
        }

        IsIdle = false;
        Task.Run(async () =>
        {
            while (PendingCommands.TryDequeue(out var command))
            {
                var key = (command.RequestType, command.ResponseType);
                if (!handlers.ContainsKey(key))
                {
                    command.Response.SetResult(new RpcResultWrapper()
                        { Error = $"{command.RequestType}/{command.ResponseType} is not supported" });
                }
                else
                {
                    var handler = handlers[key];
                    try
                    {
                        var response = await handler(command.RpcRequest);
                        command.Response.SetResult(response);
                    }
                    catch (Exception e)
                    {
                        command.Response.SetResult(new RpcResultWrapper() { Error = e.GetFullMessage() });
                    }
                }
            }
            IsIdle = true;
        });
    }
}