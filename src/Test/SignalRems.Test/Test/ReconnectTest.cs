using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using SignalRems.Client.Extensions;
using SignalRems.Core.Events;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Server.Extensions;
using SignalRems.Test.Data;

namespace SignalRems.Test.Test;

[TestFixture]
public class ReconnectTest
{
    private const string RpcEndPoint = "/reconnect-rpc";
    private const string ConnectionLostError = "Connection was lost before the response was received";
    private const string NotConnectedError = "Server is not connected";
    private WebApplication _serverApp;
    private ServiceProvider _clientProvider;
    private string _url;
    private AbortConnectionFilter _filter;

    [SetUp]
    public async Task Setup()
    {
        await TestEnvironment.EnvReady.Task;
        _url = $"http://127.0.0.1:{GetFreePort()}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        if (TestEnvironment.UseMessagePack)
        {
            builder.Services.AddSignalRemsMessagePackService(o => o.AddFilter<AbortConnectionFilter>());
        }
        else
        {
            builder.Services.AddSignalRemsJsonService(o => o.AddFilter<AbortConnectionFilter>(), Status.Convertor);
        }

        builder.Services.AddSingleton<AbortConnectionFilter>();
        builder.WebHost.UseUrls(_url);
        _serverApp = builder.Build();
        _serverApp.MapSignalRemsRpcHub(RpcEndPoint);
        await _serverApp.StartAsync();
        _filter = _serverApp.Services.GetRequiredService<AbortConnectionFilter>();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_clientProvider != null)
        {
            await _clientProvider.DisposeAsync();
        }

        if (_serverApp != null)
        {
            await _serverApp.StopAsync();
            await _serverApp.DisposeAsync();
        }
    }

    [Test]
    public async Task CallIsResentAfterReconnectAndMarkedAsPossibleDuplicateByDefault()
    {
        var resendFlags = new ConcurrentQueue<bool>();
        var invocations = 0;
        var rpcService = _serverApp.Services.GetRequiredService<IRpcService>();
        rpcService.RegisterHandler<TestRequest, TestResponse>(async r =>
        {
            resendFlags.Enqueue(r.PossibleResend);
            // Only the first attempt is slow enough to be in flight when the connection is aborted.
            if (Interlocked.Increment(ref invocations) == 1)
            {
                await Task.Delay(3000);
            }

            return new TestResponse() { Status = r.Status };
        });

        var rpcClient = await ConnectClientAsync(maxParallelTaskCount: 1);

        // The hub aborts the connection while this request is still being processed, so its reply is never delivered.
        var response = await CompleteWithin(
            rpcClient.SendAsync<TestRequest, TestResponse>(new TestRequest() { RequestId = "Retried", ProcessTime = 1 }),
            TimeSpan.FromSeconds(30), "the call which should be resent after the connection dropped");

        // The caller only observes the successful response of the resent request.
        Assert.That(response.Success, Is.True, response.Error);
        Assert.That(response.RequestId, Is.EqualTo("Retried"));
        Assert.That(_filter.SendCount, Is.GreaterThanOrEqualTo(2), "the client should have resent the request after reconnecting");
        // The handler runs for both deliveries and gets told which one may be a duplicate.
        Assert.That(resendFlags.ToArray(), Is.EqualTo(new[] { false, true }),
            "the first delivery is not a resend, the resent one must be flagged with PossibleResend");

        // The dropped attempt must not hold a flow control slot, otherwise every later call would wait forever.
        var afterReconnect = await CompleteWithin(
            rpcClient.SendAsync<TestRequest, TestResponse>(new TestRequest() { RequestId = "AfterReconnect" }),
            TimeSpan.FromSeconds(20), "a call made after the retry completed");
        Assert.That(afterReconnect.Success, Is.True, afterReconnect.Error);
        Assert.That(afterReconnect.RequestId, Is.EqualTo("AfterReconnect"));
    }

    [Test]
    public async Task CallFailsWhenAutoRetryIsDisabled()
    {
        var invocations = 0;
        var rpcService = _serverApp.Services.GetRequiredService<IRpcService>();
        rpcService.RegisterHandler<TestRequest, TestResponse>(async r =>
        {
            Interlocked.Increment(ref invocations);
            if (r.ProcessTime > 0)
            {
                await Task.Delay(r.ProcessTime);
            }

            return new TestResponse() { Status = r.Status };
        });

        var rpcClient = await ConnectClientAsync(maxParallelTaskCount: 1);

        var sendTask = rpcClient.SendAsync<TestRequest, TestResponse>(
            new TestRequest() { RequestId = "NoRetry", ProcessTime = 3000 }, autoRetry: false);
        // Wait until the request reached the server, so the connection loss happens while the reply is pending.
        await _filter.Aborted.WaitAsync(TimeSpan.FromSeconds(20));

        var response = await CompleteWithin(sendTask, TimeSpan.FromSeconds(20), "the call which must not be retried");
        Assert.That(response.Success, Is.False);
        Assert.That(response.Error, Is.EqualTo(ConnectionLostError));
        Assert.That(invocations, Is.EqualTo(1), "the request must not be sent again when auto retry is disabled");

        // The connection still recovers for later calls.
        var afterReconnect = await SendUntilSuccessAsync(rpcClient, "AfterReconnect");
        Assert.That(afterReconnect.Success, Is.True, afterReconnect.Error);
        Assert.That(afterReconnect.RequestId, Is.EqualTo("AfterReconnect"));
    }

    private async Task<IRpcClient> ConnectClientAsync(int maxParallelTaskCount)
    {
        var clientServices = new ServiceCollection();
        clientServices.AddLogging();
        if (TestEnvironment.UseMessagePack)
        {
            clientServices.AddSignalRemsMessagePackClient(maxParallelTaskCount);
        }
        else
        {
            clientServices.AddSignalRemsJsonClient(maxParallelTaskCount, Status.Convertor);
        }

        _clientProvider = clientServices.BuildServiceProvider();
        var rpcClient = _clientProvider.GetRequiredService<IRpcClient>();
        await rpcClient.ConnectAsync(_url, RpcEndPoint, CancellationToken.None);
        await rpcClient.ConnectionCompleteTask.WaitAsync(TimeSpan.FromSeconds(20));
        return rpcClient;
    }

    private static async Task<TestResponse> SendUntilSuccessAsync(IRpcClient rpcClient, string requestId)
    {
        var deadline = Stopwatch.StartNew();
        TestResponse response = null;
        while (deadline.Elapsed < TimeSpan.FromSeconds(30))
        {
            var result = await CompleteWithin(
                rpcClient.SendAsync<TestRequest, TestResponse>(new TestRequest() { RequestId = requestId }, autoRetry: false),
                TimeSpan.FromSeconds(20), "a call made after the connection was re-established");
            if (result.Success)
            {
                response = result;
                break;
            }

            if (result.Error != NotConnectedError && result.Error != ConnectionLostError)
            {
                Assert.Fail($"Unexpected error after reconnecting: {result.Error}");
            }

            await Task.Delay(200);
        }

        Assert.That(response, Is.Not.Null,
            $"Calls should succeed again after reconnecting, status = {rpcClient.ConnectionStatus}");
        return response;
    }

    private static async Task<T> CompleteWithin<T>(Task<T> task, TimeSpan timeout, string description)
    {
        try
        {
            return await task.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"Timed out waiting for {description}, the connection flow control or the pending call was never released.");
            return default;
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Aborts the first RPC connection shortly after a request was accepted, which simulates an abrupt network failure:
    /// the server keeps processing the request but the client can never receive the reply.
    /// </summary>
    private sealed class AbortConnectionFilter : IHubFilter
    {
        private int _aborted;
        private int _sendCount;
        private readonly TaskCompletionSource _abortedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Aborted => _abortedSignal.Task;

        /// <summary>
        /// Number of <c>Send</c> invocations received, including resends of the same logical request.
        /// </summary>
        public int SendCount => Volatile.Read(ref _sendCount);

        public async ValueTask<object> InvokeMethodAsync(HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object>> next)
        {
            var result = await next(invocationContext);
            if (invocationContext.HubMethodName == Command.Send)
            {
                Interlocked.Increment(ref _sendCount);
                if (Interlocked.Exchange(ref _aborted, 1) == 0)
                {
                    var context = invocationContext.Context;
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(500);
                        try
                        {
                            context.Abort();
                        }
                        catch (Exception)
                        {
                            // The connection may already be gone.
                        }
                        finally
                        {
                            _abortedSignal.TrySetResult();
                        }
                    });
                }
            }

            return result;
        }
    }
}
