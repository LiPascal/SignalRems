using Microsoft.AspNetCore.SignalR;
using SignalRems.Core.Models;
using SignalRems.Server.Data;
using SignalRems.Server.Interfaces;

namespace SignalRems.Server.Hubs;

internal class RpcHub : Hub
{
    private readonly IRpcServer _rpcServer;

    public RpcHub(IRpcServer rpcServer)
    {
        _rpcServer = rpcServer;
    }

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
    public async Task<RpcResultWrapper> Send(RpcRequestWrapper request, string requestType, string responseType)
    {
        return await _rpcServer.ProcessAsync(request, requestType, responseType);
    }
}