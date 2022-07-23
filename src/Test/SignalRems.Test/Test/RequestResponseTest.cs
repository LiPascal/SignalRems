using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SignalRems.Core.Interfaces;
using SignalRems.Test.Data;


namespace SignalRems.Test.Test;

[TestFixture]
public class RequestResponseTest
{
    private static readonly ConcurrentStack<Action> DisposeActions = new();

    [SetUp]
    public async Task Setup()
    {
        await TestEnvironment.EnvReady.Task;
    }

    [TearDown]
    public void TearDown()
    {
        while (DisposeActions.TryPop(out var action))
        {
            action();
        }
    }

    [Test]
    public async Task BasicRpcTest()
    {
        var rpcService = TestEnvironment.ServerServiceProvider.GetService<IRpcService>();
        rpcService.RegisterHandler<TestRequest, TestResponse>(r =>
            Task.FromResult(new TestResponse() { Status = r.Status}));
        var rpcClient = TestEnvironment.ClientServiceProvider.GetService<IRpcClient>();
        DisposeActions.Push(() => rpcClient.Dispose());
        await rpcClient.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.RpcEndPoint,
            CancellationToken.None);
        await rpcClient.ConnectionCompleteTask;
        var result = await rpcClient.SendAsync<TestRequest, TestResponse>(new TestRequest() { RequestId = "Test1", Status = Status.Done});
        Assert.That(result, Is.Not.Null);
        Assert.IsTrue(result.Success);
        Assert.AreEqual("Test1", result.RequestId);
        Assert.That(result.Error, Is.Null);
        Assert.AreEqual(Status.Done, result.Status);
    }

    [Test]

    public async Task RpcExceptionTest()
    {
        var rpcService = TestEnvironment.ServerServiceProvider.GetService<IRpcService>();
        rpcService.RegisterHandler<TestRequest, TestResponse>(r => throw new Exception("Error in Test"));
        var rpcClient = TestEnvironment.ClientServiceProvider.GetService<IRpcClient>();
        DisposeActions.Push(() => rpcClient.Dispose());
        await rpcClient.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.RpcEndPoint,
            CancellationToken.None);
        await rpcClient.ConnectionCompleteTask;
        var result = await rpcClient.SendAsync<TestRequest, TestResponse>(new TestRequest() { RequestId = "Test1" });
        Assert.That(result, Is.Not.Null);
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Test1", result.RequestId);
        Assert.IsTrue(result.Error.StartsWith("Exception:\n Error in Test"));
    }

    [Test]
    public async Task MultipleRpcTest()
    {
        var rpcService = TestEnvironment.ServerServiceProvider.GetService<IRpcService>();
        rpcService.RegisterHandler<TestRequest, TestResponse>(async r =>
        {
            if (r.ProcessTime > 0)
            {
                await Task.Delay(r.ProcessTime);
            }
            return new TestResponse();
        });
        var rpcClient1 = TestEnvironment.ClientServiceProvider.GetService<IRpcClient>();
        var rpcClient2 = TestEnvironment.ClientServiceProvider.GetService<IRpcClient>();
        DisposeActions.Push(() => rpcClient1.Dispose());
        DisposeActions.Push(() => rpcClient2.Dispose());
        await rpcClient1.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.RpcEndPoint,
            CancellationToken.None);
        await rpcClient2.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.RpcEndPoint,
            CancellationToken.None);
        await rpcClient1.ConnectionCompleteTask;
        await rpcClient2.ConnectionCompleteTask;
        List<Task<TestResponse>> responses1 = new();
        List<Task<TestResponse>> responses2 = new();
        for (int i = 0; i < 5; ++i)
        {
            responses1.Add(rpcClient1.SendAsync<TestRequest, TestResponse>(new TestRequest() { RequestId = $"Test1_{i}", ProcessTime = 500}));
        }
        for (int i = 0; i < 2; ++i)
        {
            responses2.Add(rpcClient2.SendAsync<TestRequest, TestResponse>(new TestRequest() { RequestId = $"Test2_{i}", ProcessTime = 100 }));
        }

        var delay200 = Task.Delay(200);
        var delay500 = Task.Delay(500);
        var delay1000 = Task.Delay(1000);
        var delay3000 = Task.Delay(3000);
        var firstTask = await Task.WhenAny(responses1.Concat(responses2).ToArray());
        // The second client's task is quicker, should return early. 
        Assert.IsTrue(responses2.Contains(firstTask));
        // The second client's first task should complete within 200 ms. 
        Assert.IsFalse(delay200.IsCompleted);
        await Task.WhenAll(responses2.ToArray());
        // The second client's all tasks should complete within 500 ms. 
        Assert.IsFalse(delay500.IsCompleted);
        await Task.WhenAny(responses1.ToArray());
        // The first client's first task should complete within 1000 ms. 
        Assert.IsFalse(delay1000.IsCompleted);
        // but not all tasks are completed; 
        Assert.IsFalse(responses1.All(x => x.IsCompleted));
        await Task.WhenAll(responses1.ToArray());
        // The first client's tasks should use more than 1000 ms. 
        Assert.IsTrue(delay1000.IsCompleted);
        // The first client's tasks should complete within 3000 ms. 
        Assert.IsFalse(delay3000.IsCompleted);
        await delay3000;
    }

    [Test]
    public async Task CompressInRequestRpcTest()
    {
        var rpcService = TestEnvironment.ServerServiceProvider.GetService<IRpcService>();
        rpcService.RegisterHandler<TestRequest, TestResponse>(r =>
            Task.FromResult(new TestResponse() { Status = r.Status }));
        var rpcClient = TestEnvironment.ClientServiceProvider.GetService<IRpcClient>();
        DisposeActions.Push(() => rpcClient.Dispose());
        await rpcClient.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.RpcEndPoint,
            CancellationToken.None);
        await rpcClient.ConnectionCompleteTask;
        var result = await rpcClient.SendAsync<TestRequest, TestResponse>(new TestRequest() { RequestId = "Test1", Status = Status.Done }, compressInRequest:true);
        Assert.That(result, Is.Not.Null);
        Assert.IsTrue(result.Success);
        Assert.AreEqual("Test1", result.RequestId);
        Assert.That(result.Error, Is.Null);
        Assert.AreEqual(Status.Done, result.Status);
    }
    [Test]
    public async Task CompressInResponseRpcTest()
    {
        var rpcService = TestEnvironment.ServerServiceProvider.GetService<IRpcService>();
        rpcService.RegisterHandler<TestRequest, TestResponse>(r =>
            Task.FromResult(new TestResponse() { Status = r.Status }));
        var rpcClient = TestEnvironment.ClientServiceProvider.GetService<IRpcClient>();
       
        DisposeActions.Push(() => rpcClient.Dispose());
        await rpcClient.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.RpcEndPoint,
            CancellationToken.None);
        await rpcClient.ConnectionCompleteTask;
        var result = await rpcClient.SendAsync<TestRequest, TestResponse>(new TestRequest() { RequestId = "Test1", Status = Status.Done }, compressInResult: true);
        Assert.That(result, Is.Not.Null);
        
        Assert.IsTrue(result.Success);
        Assert.AreEqual("Test1", result.RequestId);
        Assert.That(result.Error, Is.Null);
        Assert.AreEqual(Status.Done, result.Status);
    }
}