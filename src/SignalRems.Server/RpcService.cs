using System.Collections.Concurrent;
using System.Diagnostics;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;
using SignalRems.Server.Interfaces;

namespace SignalRems.Server;

internal sealed class RpcService : IRpcService, IRpcServer
{
    private readonly ILogger<RpcService> _logger;

    private readonly ConcurrentDictionary<(string, string), Func<RpcRequestWrapper, Task<RpcResultWrapper>>> _handlers =
        new();

    public RpcService(ILogger<RpcService> logger)
    {
        _logger = logger;
    }
    public void RegisterHandler<TRequest, TResponse>(IRpcHandler<TRequest, TResponse> handler, LogLevel level = LogLevel.None)
        where TResponse : IRpcResponse where TRequest : IRpcRequest
    {
        RegisterHandler<TRequest, TResponse>(async request => await handler.HandleRequest(request), level);
    }

    public void RegisterHandler<TRequest, TResponse>(Func<TRequest, Task<TResponse>> handleFunc, LogLevel level = LogLevel.None)
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
            _logger.Log("Receive Request", reqObj, level);
            var req = await SerializeUtil.DeserializeAsync<TRequest, RpcRequestWrapper>(reqObj);
            Debug.Assert(req != null, nameof(req) + " != null");
            var result = await handleFunc(req);
            result.RequestId = req.RequestId;
            result.Success = string.IsNullOrEmpty(result.Error);
            var rsp = await SerializeUtil.SerializeAsync<TResponse, RpcResultWrapper>(result, reqObj.CompressInResult);
            _logger.Log("Reply with Response", rsp, level);
            return rsp;
        }

        _handlers[(requestType, responseType)] = Process;
        _logger.LogInformation("Register handler: {0}, {1}", requestType, responseType);
    }

   

    public async Task<RpcResultWrapper> ProcessAsync(RpcRequestWrapper request, string requestType, string responseType)
    {
        var key = (requestType, responseType);
        if (!_handlers.ContainsKey(key))
        {
            _logger.LogError($"Receive not support {requestType}/{responseType}");
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
            _logger.LogError(e, "Error when process request {0}", requestType);
            return new RpcResultWrapper() { Error = "Error when process request, message = " + e.GetFullMessage() };
        }
    }
}