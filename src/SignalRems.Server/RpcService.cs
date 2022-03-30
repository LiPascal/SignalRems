using System.Collections.Concurrent;
using SignalRems.Core.Interfaces;
using SignalRems.Server.Data;
using SignalRems.Server.Exceptions;

namespace SignalRems.Server;

internal sealed class RpcService : IRpcService
{
    private readonly ConcurrentDictionary<(Type, Type), Func<IRpcRequest, Task<IRpcResponse>>> _handlers = new();
    private bool _running = true;
    private bool _started = false;

    public void RegisterHandler<TRequest, TResponse>(IRpcHandler<TRequest, TResponse> handler)
        where TResponse : IRpcResponse where TRequest : IRpcRequest
    {
        async Task<IRpcResponse> Process(IRpcRequest request)
        {
            var req = (TRequest)request;
            return await handler.HandleRequest(req);
        }

        _handlers[(typeof(TRequest), typeof(TResponse))] = Process;
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
                        command.Response.SetResult((null, InvalidRequestException.Instance));
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
                                command.Response.SetResult((null, e));
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