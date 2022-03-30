using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using SignalRems.Core.Events;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;

namespace SignalRems.Client;

internal class RpcClient : ClientBase, IRpcClient
{
    private const string NotConnectedError = "Server is not connected";
    private const string UnknownError = "UnknownError happened on server.";

    public RpcClient(ILogger<RpcClient> logger) : base(logger)
    {
    }

    public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request)
        where TRequest : class, IRpcRequest, new() where TResponse : class, IRpcResponse, new()
    {
        if (Connection == null)
        {
            return new TResponse { RequestId = request.RequestId, Success = false, Error = NotConnectedError };
        }

        Logger.LogInformation("Send Request to {url}: {req_id} {req}", Url, request.RequestId, request);
        var requestBytes = await MessagePackUtil.ToBinaryAsync(request);

        var (responseBytes, error) = await Connection.InvokeAsync<(byte[]?, string?)>(Command.Send, requestBytes,
            typeof(TRequest).FullName, typeof(TResponse).FullName);

        if (error != null || responseBytes == null)
        {
            Logger.LogError("Get error when sending request {id}: {error}", request.RequestId, error ?? UnknownError);
            return new TResponse { RequestId = request.RequestId, Success = false, Error = error ?? UnknownError };
        }

        var response = await MessagePackUtil.FromBinaryAsync<TResponse>(responseBytes);

        Logger.LogInformation("Receive response for request {id}:{response}", request.RequestId, response);
        response.RequestId = request.RequestId;
        response.Success = string.IsNullOrEmpty(response.Error);
        return response;
    }
}