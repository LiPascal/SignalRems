using System;
using System.Collections.Concurrent;
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
            Task.FromResult(new TestResponse()));
        var rpcClient = TestEnvironment.ClientServiceProvider.GetService<IRpcClient>();
        DisposeActions.Push(() => rpcClient.Dispose());
        await rpcClient.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.RpcEndPoint,
            CancellationToken.None);
        var result = await rpcClient.SendAsync<TestRequest, TestResponse>(new TestRequest() { RequestId = "Test1" });
        Assert.That(result, Is.Not.Null);
        Assert.IsTrue(result.Success);
        Assert.AreEqual("Test1", result.RequestId);
        Assert.That(result.Error, Is.Null);
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
        var result = await rpcClient.SendAsync<TestRequest, TestResponse>(new TestRequest() { RequestId = "Test1" });
        Assert.That(result, Is.Not.Null);
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Test1", result.RequestId);
        Assert.IsTrue(result.Error.StartsWith("Error in Test"));
    }
}