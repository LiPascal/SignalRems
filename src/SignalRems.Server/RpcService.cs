using System.Collections.Concurrent;
using MessagePack;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Utils;
using SignalRems.Server.Data;
using SignalRems.Server.Exceptions;

namespace SignalRems.Server;

internal sealed class RpcService : IRpcService
{
    private readonly ConcurrentDictionary<(string, string), Func<byte[], Task<byte[]>>> _handlers = new();
    private bool _running = true;
    private bool _started = false;

    public void RegisterHandler<TRequest, TResponse>(IRpcHandler<TRequest, TResponse> handler)
        where TResponse : IRpcResponse where TRequest : IRpcRequest
    {
        RegisterHandler<TRequest, TResponse>(async request => await handler.HandleRequest(request));
    }

    public void RegisterHandler<TRequest, TResponse>(Func<TRequest, Task<TResponse>> handleFunc) where TRequest : IRpcRequest where TResponse : IRpcResponse
    {
        var requestType = typeof(TRequest).FullName;
        var responseType = typeof(TResponse).FullName;
        if (responseType == null || requestType == null)
        {
            throw new NotSupportedException("Invalid requestBytes/response type");
        }
        async Task<byte[]> Process(byte[] requestBytes)
        {
            var req = await MessagePackUtil.FromBinaryAsync<TRequest>(requestBytes);
            var result = await handleFunc(req);
            return await MessagePackUtil.ToBinaryAsync(result);
        }

        _handlers[(requestType, responseType)] = Process;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        Task.Run(async () =>
        {
            while (_running)
            {
                var updated = false;
                foreach (var client in RemoteCallerClient.Clients.Values)
                {
                    if (!client.PendingCommands.TryDequeue(out var command))
                    {
                        continue;
                    }

                    var key = (command.RequestType, command.ResponseType);
                    if (!_handlers.ContainsKey(key))
                    {
                        command.Response.SetResult((null, $"{command.RequestType}/{command.ResponseType} is not supported"));
                    }
                    else
                    {
                        var handler = _handlers[key];
                        await Task.Run(async () =>
                        {
                            try
                            {
                                var response = await handler(command.RpcRequest);
                                command.Response.SetResult((response, null));
                            }
                            catch (Exception e)
                            {
                                command.Response.SetResult((null, e.Message));
                            }
                        });
                    }

                    updated = true;
                }

                if (!updated)
                {
                    await Task.Delay(10);
                }
            }
        });
    }

    public void Dispose()
    {
        _running = false;
    }
}