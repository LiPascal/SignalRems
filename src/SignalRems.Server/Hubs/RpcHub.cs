using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Server.Data;
using SignalRems.Server.Interfaces;

namespace SignalRems.Server.Hubs;

internal class RpcHub : Hub
{
    private readonly IRpcServer _rpcServer;
    private readonly IHubContext<RpcHub> _hubContext;
    private readonly ILogger<RpcHub> _logger;
    private readonly IClientCollection<RemoteCallerClient> _clients;


    public RpcHub(IRpcServer rpcServer, IHubContext<RpcHub> hubContext, IClientCollection<RemoteCallerClient> clients, ILogger<RpcHub> logger)
    {
        _rpcServer = rpcServer;
        _hubContext = hubContext;
        _logger = logger;
        _clients = clients;
    }

    #region override

    public override async Task OnConnectedAsync()
    {
        _clients[Context.ConnectionId] = new RemoteCallerClient(Context.ConnectionId);
        await base.OnConnectedAsync();
        var feature = Context.Features.Get<IHttpConnectionFeature>();
        _logger.LogInformation("Established new connection with {0}, ip = {1}, port = {2}", Context.ConnectionId,
            feature?.RemoteIpAddress, feature?.RemotePort);
    }

    public override async Task OnDisconnectedAsync(Exception? e)
    {
        _clients[Context.ConnectionId].IsConnected = false;
        await base.OnDisconnectedAsync(e);
        _logger.LogInformation("Connection {0} lost", Context.ConnectionId);
    }

    #endregion

    // ReSharper disable once UnusedMember.Global
    public void Send(RpcRequestWrapper request, string requestType, string responseType)
    {
        var id = Context.ConnectionId;
        Task.Run(async () =>
        {
            var result = await _rpcServer.ProcessAsync(request, requestType, responseType);
            if (_clients.TryGetValue(id, out var caller) && caller!.IsConnected)
            {
                var client = _hubContext.Clients.Client(id);
                _logger.LogDebug("Reply message on topic {0}", request.ReplyOnTopic);
                await client.SendAsync(request.ReplyOnTopic, result);
            }
            else
            {
                _logger.LogWarning("client disconnected, discard result for on topic {0}", request.ReplyOnTopic);
            }
        });
    }
}