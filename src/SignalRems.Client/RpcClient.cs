using System;
using System.Collections.Concurrent;
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
    private readonly int _maxParallelTaskCount;
    private const string NotConnectedError = "Server is not connected";
    private const string UnknownError = "UnknownError happened on server.";
    private long _messageCounter = 0;
    private IDisposable? _replySubscription;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<RpcResultWrapper>> _resultTcs = new();
    private readonly SemaphoreSlim _flowControlSemphore;    

    public RpcClient(ILogger<RpcClient> logger, int maxParallelTaskCount) : base(logger)
    {
        _maxParallelTaskCount = maxParallelTaskCount;
        var cnt = Math.Max(1, maxParallelTaskCount);
        _flowControlSemphore =  new SemaphoreSlim(cnt);        
    }

    public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, LogLevel level = LogLevel.None,
        bool compressInRequest = false, bool compressInResult = false)
        where TRequest : class, IRpcRequest, new() where TResponse : class, IRpcResponse, new()
    {
        if (Connection is not { State: HubConnectionState.Connected })
        {
            return new TResponse { RequestId = request.RequestId, Success = false, Error = NotConnectedError };
        }

        try
        {
            if(_maxParallelTaskCount > 0)
            {
                if (_flowControlSemphore.CurrentCount == _maxParallelTaskCount)
                {
                    Logger.LogWarning($"Max parallel task count reached, this call may be queued for a while, req id = {0}", request.RequestId);
                }
                await _flowControlSemphore.WaitAsync();
            }            
            var reqObj = await SerializeUtil.SerializeAsync<TRequest, RpcRequestWrapper>(request, compressInRequest);
            reqObj.CompressInResult = compressInResult;
            reqObj.CorrelationId = Interlocked.Increment(ref _messageCounter);
            Logger.Log($"Sending request {typeof(TRequest).ToLogName()}, CorrelationId={reqObj.CorrelationId}:", reqObj, level);
            var tcs = new TaskCompletionSource<RpcResultWrapper>(TaskCreationOptions.RunContinuationsAsynchronously);
            _resultTcs[reqObj.CorrelationId] = tcs;
            await Connection.InvokeAsync(Command.Send, reqObj, typeof(TRequest).FullName, typeof(TResponse).FullName).ConfigureAwait(false);
            var result = await tcs.Task.ConfigureAwait(false);
            Logger.Log($"Receive response {typeof(TResponse).ToLogName()}:", result, level);
            var error = result.Error;
            TResponse? response;

            if (error != null || (response = await SerializeUtil.DeserializeAsync<TResponse, RpcResultWrapper>(result)) == null)
            {
                Logger.LogError("Get error when sending request {id}: {error}", request.RequestId,
                    error ?? UnknownError);
                return new TResponse { RequestId = request.RequestId, Success = false, Error = error ?? UnknownError };
            }

            return response;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Get error when sending request {id}: {error}", request.RequestId,
                e.Message);
            return new TResponse { RequestId = request.RequestId, Success = false, Error = e.Message };
        }
        finally
        {
            if (_maxParallelTaskCount > 0)
            {
                _flowControlSemphore.Release();
            }                
        }
    }

    protected override void OnConnectionCreated(HubConnection connection)
    {
        var disposable = Connection?.On<RpcResultWrapper>(Command.ReplyTopic, HandleReply);
        Interlocked.Exchange(ref _replySubscription, disposable)?.Dispose();
    }

    private void HandleReply(RpcResultWrapper resultWrapper)
    {
        if (_resultTcs.TryRemove(resultWrapper.CorrelationId, out var tcs))
        {
            tcs.TrySetResult(resultWrapper);
        }
    }
}