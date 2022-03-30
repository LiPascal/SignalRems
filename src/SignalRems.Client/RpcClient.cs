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

namespace SignalRems.Client
{
    internal class RpcClient: ClientBase, IRpcClient
    {
        private readonly InvalidOperationException _invalidOperationException = new ("Server is not connected");
        private readonly Exception _unknownException = new ("UnknownError happened on server.");
        public RpcClient(ILogger<RpcClient> logger) : base(logger)
        {
        }

        public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request) where TRequest : IRpcRequest where TResponse : class, IRpcResponse, new()
        {
            if (Connection == null)
            {
                return new TResponse { RequestId = request.RequestId, Exception = _invalidOperationException };
            }

            var (rpcResponse, exception) = await Connection.InvokeAsync<(IRpcResponse?, Exception?)>(Command.Send, request, typeof(TRequest), typeof(TResponse));

            if (exception != null || rpcResponse == null)
            {
                return new TResponse { RequestId = request.RequestId, Exception = exception ?? _unknownException };
            }

            rpcResponse.RequestId = request.RequestId;
            return (TResponse)rpcResponse;
        }
    }
}
