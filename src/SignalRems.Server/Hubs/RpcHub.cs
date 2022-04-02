using MessagePack;
using Microsoft.AspNetCore.SignalR;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Server.Data;

namespace SignalRems.Server.Hubs;

public class RpcHub : Hub
{
    #region override

    public override async Task OnConnectedAsync()
    {
        RemoteCallerClient.Clients[Context.ConnectionId] = new RemoteCallerClient(Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? e)
    {
        RemoteCallerClient.Clients[Context.ConnectionId].IsConnected = false;
        await base.OnDisconnectedAsync(e);
    }

    #endregion

    // ReSharper disable once UnusedMember.Global
    public async Task<RpcResult> Send(string request, string requestType, string responseType)
    {
        var tcs = new TaskCompletionSource<RpcResult>();
        var command = new RemoteCallerCommand(request, tcs, requestType, responseType);
        var client = RemoteCallerClient.Clients[Context.ConnectionId];
        client.PendingCommands.Enqueue(command);
        return await tcs.Task;
    }
}