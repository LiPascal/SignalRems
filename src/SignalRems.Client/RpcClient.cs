using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
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
    private const string ConnectionLostError = "Connection was lost before the response was received";
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
        bool compressInRequest = false, bool compressInResult = false, bool autoRetry = true)
        where TRequest : class, IRpcRequest, new() where TResponse : class, IRpcResponse, new()
    {
        if (!IsConnected)
        {
            return NotConnected<TResponse>(request);
        }

        var flowControlTaken = false;
        try
        {
            if(_maxParallelTaskCount > 0)
            {
                if (_flowControlSemphore.CurrentCount == 0)
                {
                    Logger.LogWarning("Max parallel task count reached, this call may be queued for a while, req id = {0}", request.RequestId);
                }
                await _flowControlSemphore.WaitAsync().ConfigureAwait(false);
                flowControlTaken = true;
            }

            var reqObj = await SerializeUtil.SerializeAsync<TRequest, RpcRequestWrapper>(request, compressInRequest).ConfigureAwait(false);
            reqObj.CompressInResult = compressInResult;
            // Resent attempts keep the same correlation id so they are recognizable as the same request in the logs.
            var correlationId = Interlocked.Increment(ref _messageCounter);
            reqObj.CorrelationId = correlationId;

            for (var attempt = 0;; ++attempt)
            {
                if (!IsConnected)
                {
                    if (!autoRetry || !await WaitForConnectedAsync().ConfigureAwait(false))
                    {
                        return ConnectionLost<TResponse>(request);
                    }
                }

                var connection = Connection;
                // A repeated attempt may already have run on the server, the handler decides what to do about it.
                reqObj.PossibleResend = attempt > 0;
                var tcs = new TaskCompletionSource<RpcResultWrapper>(TaskCreationOptions.RunContinuationsAsynchronously);
                _resultTcs[correlationId] = tcs;
                try
                {
                    if (attempt == 0)
                    {
                        Logger.Log($"Sending request {typeof(TRequest).ToLogName()}, CorrelationId={correlationId}:", reqObj, level);
                    }
                    else
                    {
                        Logger.LogWarning("Resending request {type} (attempt {attempt}), CorrelationId={correlationId}",
                            typeof(TRequest).ToLogName(), attempt, correlationId);
                    }

                    await connection!.InvokeAsync(Command.Send, reqObj, typeof(TRequest).FullName, typeof(TResponse).FullName).ConfigureAwait(false);
                    // The reply is delivered on a separate hub message, so it is lost if the connection drops in between.
                    // FailPendingRequests completes this task in that case instead of leaving the caller hanging forever.
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
                catch (Exception e) when (autoRetry && IsConnectionLost(e))
                {
                    // The reply can no longer arrive on this connection; resend it once the client is connected again.
                    Logger.LogWarning(
                        "Connection lost before the response of request {id} arrived (CorrelationId={correlationId}, error={error}), the request will be resent after reconnecting",
                        request.RequestId, correlationId, e.Message);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Get error when sending request {id}: {error}", request.RequestId,
                        e.Message);
                    return new TResponse { RequestId = request.RequestId, Success = false, Error = e.Message };
                }
                finally
                {
                    _resultTcs.TryRemove(correlationId, out _);
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Get error when sending request {id}: {error}", request.RequestId,
                e.Message);
            return new TResponse { RequestId = request.RequestId, Success = false, Error = e.Message };
        }
        finally
        {
            if (flowControlTaken)
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

    protected override Task ConnectionOnReconnecting(Exception? arg)
    {
        FailPendingRequests(ConnectionLostError);
        return base.ConnectionOnReconnecting(arg);
    }

    protected override Task ConnectionOnClosed(Exception? exception)
    {
        FailPendingRequests(ConnectionLostError);
        return base.ConnectionOnClosed(exception);
    }

    protected override void DoDispose()
    {
        FailPendingRequests(ConnectionLostError);
        base.DoDispose();
    }

    private void HandleReply(RpcResultWrapper resultWrapper)
    {
        if (_resultTcs.TryRemove(resultWrapper.CorrelationId, out var tcs))
        {
            tcs.TrySetResult(resultWrapper);
        }
    }

    /// <summary>
    /// Replies are routed by connection id on the server side, so any request still waiting for a reply when the
    /// connection is lost will never be answered. Failing them here releases both the caller and its flow control
    /// slot; callers with auto retry enabled then resend the request on the next connection.
    /// </summary>
    private void FailPendingRequests(string error)
    {
        // ConcurrentDictionary tolerates removal while being enumerated. TryRemove is also the claim: whoever removes
        // the entry owns it, so a reply that HandleReply took concurrently is left alone.
        foreach (var correlationId in _resultTcs.Keys)
        {
            if (_resultTcs.TryRemove(correlationId, out var pending))
            {
                Logger.LogWarning("Request CorrelationId={correlationId} was dropped: {error}", correlationId, error);
                if (pending.TrySetException(new RpcConnectionLostException(error)))
                {
                    // Mark the failure as observed right where it is raised: the owner of the task may have already
                    // left before awaiting it (e.g. InvokeAsync threw when the connection died), and an unobserved
                    // faulted task surfaces later as TaskScheduler.UnobservedTaskException.
                    _ = pending.Task.Exception;
                }
            }
        }
    }

    private static bool IsConnectionLost(Exception e) =>
        e is RpcConnectionLostException or InvalidOperationException or OperationCanceledException
            or IOException or SocketException or HttpRequestException;

    private static TResponse NotConnected<TResponse>(IRpcRequest request)
        where TResponse : class, IRpcResponse, new()
        => new() { RequestId = request.RequestId, Success = false, Error = NotConnectedError };

    private static TResponse ConnectionLost<TResponse>(IRpcRequest request)
        where TResponse : class, IRpcResponse, new()
        => new() { RequestId = request.RequestId, Success = false, Error = ConnectionLostError };
}