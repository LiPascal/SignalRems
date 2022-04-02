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

        var requestJson = JsonUtil.ToJson(request);
        Logger.LogInformation("Send Request to {url}: {req_id} {req}", Url, request.RequestId, requestJson);
        var result = await Connection.InvokeAsync<RpcResult>(Command.Send, requestJson,
            typeof(TRequest).FullName, typeof(TResponse).FullName).ConfigureAwait(false);

        var error = result?.Error;
        var responseJson = result?.Result;
        TResponse? response;

        if (error != null || responseJson == null || (response = JsonUtil.FromJson<TResponse>(responseJson)) == null)
        {
            Logger.LogError("Get error when sending request {id}: {error}, {response}", request.RequestId,
                error ?? UnknownError, responseJson);
            return new TResponse { RequestId = request.RequestId, Success = false, Error = error ?? UnknownError };
        }

        Logger.LogInformation("Receive response for request {id}:{response}", request.RequestId, responseJson);
        return response;
    }
}