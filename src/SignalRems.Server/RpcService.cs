using System.Collections.Concurrent;
using System.Diagnostics;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;
using SignalRems.Server.Interfaces;

namespace SignalRems.Server;

internal sealed class RpcService : IRpcService, IRpcServer
{
    private readonly ConcurrentDictionary<(string, string), Func<RpcRequestWrapper, Task<RpcResultWrapper>>> _handlers =
        new();

    public void RegisterHandler<TRequest, TResponse>(IRpcHandler<TRequest, TResponse> handler)
        where TResponse : IRpcResponse where TRequest : IRpcRequest
    {
        RegisterHandler<TRequest, TResponse>(async request => await handler.HandleRequest(request));
    }

    public void RegisterHandler<TRequest, TResponse>(Func<TRequest, Task<TResponse>> handleFunc)
        where TRequest : IRpcRequest where TResponse : IRpcResponse
    {
        var requestType = typeof(TRequest).FullName;
        var responseType = typeof(TResponse).FullName;
        if (responseType == null || requestType == null)
        {
            throw new NotSupportedException("Invalid request/response type");
        }

        async Task<RpcResultWrapper> Process(RpcRequestWrapper reqObj)
        {
            var req = await SerializeUtil.DeserializeAsync<TRequest, RpcRequestWrapper>(reqObj);
            Debug.Assert(req != null, nameof(req) + " != null");
            var result = await handleFunc(req);
            result.RequestId = req.RequestId;
            result.Success = string.IsNullOrEmpty(result.Error);
            return await SerializeUtil.SerializeAsync<TResponse, RpcResultWrapper>(result);
        }

        _handlers[(requestType, responseType)] = Process;
    }

   

    public async Task<RpcResultWrapper> ProcessAsync(RpcRequestWrapper request, string requestType, string responseType)
    {
        var key = (requestType, responseType);
        if (!_handlers.ContainsKey(key))
        {
            return new RpcResultWrapper() { Error = $"{requestType}/{responseType} is not supported" };
        }

        var handler = _handlers[key];
        try
        {
            var response = await handler(request);
            return response;
        }
        catch (Exception e)
        {
            return new RpcResultWrapper() { Error = e.GetFullMessage() };
        }
    }
}