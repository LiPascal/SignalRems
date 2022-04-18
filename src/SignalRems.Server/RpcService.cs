using System.Collections.Concurrent;
using System.Diagnostics;
using MessagePack;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;
using SignalRems.Server.Data;
using SignalRems.Server.Exceptions;

namespace SignalRems.Server;

internal sealed class RpcService : IRpcService
{
    private readonly ConcurrentDictionary<(string, string), Func<RpcRequestWrapper, Task<RpcResultWrapper>>> _handlers =
        new();

    private bool _running;
    private Task? _serviceTask;

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

    public void Start()
    {
        if (_serviceTask != null)
        {
            return;
        }

        _running = true;
        _serviceTask = Task.Run(async () =>
        {
            while (_running)
            {
                foreach (var client in RemoteCallerClient.Clients.Values.Where(x => x.IsIdle && x.IsConnected))
                {
                    client.ProcessPending(_handlers);
                }

                await Task.Delay(10);
            }
        });
    }

    public async void Dispose()
    {
        _running = false;
        if (_serviceTask == null)
        {
            return;
        }

        await _serviceTask;
        _serviceTask = null;
    }
}