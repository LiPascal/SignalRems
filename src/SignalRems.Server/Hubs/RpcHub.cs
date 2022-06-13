using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using SignalRems.Core.Models;
using SignalRems.Server.Data;
using SignalRems.Server.Interfaces;

namespace SignalRems.Server.Hubs;

internal class RpcHub : Hub
{
    private readonly IRpcServer _rpcServer;
    private readonly ILogger<RpcHub> _logger;

    public RpcHub(IRpcServer rpcServer, ILogger<RpcHub> logger)
    {
        _rpcServer = rpcServer;
        _logger = logger;
    }

    #region override

    public override async Task OnConnectedAsync()
    {
        RemoteCallerClient.Clients[Context.ConnectionId] = new RemoteCallerClient(Context.ConnectionId);
        await base.OnConnectedAsync();
        var feature = Context.Features.Get<IHttpConnectionFeature>();
        _logger.LogInformation("Established new connection with {0}, ip = {1}, port = {2}", Context.ConnectionId, feature?.RemoteIpAddress, feature?.RemotePort);
    }

    public override async Task OnDisconnectedAsync(Exception? e)
    {
        RemoteCallerClient.Clients[Context.ConnectionId].IsConnected = false;
        await base.OnDisconnectedAsync(e);
        _logger.LogInformation("Connection {0} lost", Context.ConnectionId);
    }

    #endregion

    // ReSharper disable once UnusedMember.Global
    public async Task<RpcResultWrapper> Send(RpcRequestWrapper request, string requestType, string responseType)
    {
        return await _rpcServer.ProcessAsync(request, requestType, responseType);
    }
}