using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SignalRems.Core.Interfaces;
using SignalRems.Test.Data;
using SignalRems.Test.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SignalRems.Test.Test;

[TestFixture]
public class PubsubTests
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
    public async Task BasicPubsubTest()
    {
        var handler = new ModelHandler();
        var model = new Model
        {
            Id = 5, 
            Name = "ABC",
            CreateTime = DateTime.Now, 
            Marks = new List<double> { 3.14, 2.17, 0.03 }, 
            Status = Status.Done
        };
        var topic = "BasicPubsubTestTopic";
        var publisherService = TestEnvironment.ServerServiceProvider.GetService<IPublisherService>();
        var publisher = publisherService.CreatePublisher<Model, int>(topic);
        publisher.Publish(model);

        DisposeActions.Push(() => publisher.Dispose());
        var client = TestEnvironment.ClientServiceProvider.GetService<ISubscriberClient>();
        await client.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.PubsubEndPoint, CancellationToken.None);
        await client.SubscribeAsync(topic, handler);

        DisposeActions.Push(() => client.Dispose());
        await TestUtil.WaitForConditionAsync(() => handler.Models.Count == 1, 2000);
        Assert.AreEqual(1, handler.Models.Count);
        var model2 = handler.Models.First();
        Assert.AreEqual(model.Id, model2.Id);
        Assert.AreEqual(model.Name, model2.Name);
        Assert.AreEqual(model.CreateTime, model2.CreateTime);
        Assert.IsTrue(model.Marks.SequenceEqual(model2.Marks));
        Assert.AreEqual(model.Status, Status.Done);
    }

    [Test]
    public async Task PubsubFilterTest()
    {
        var model1 = new Model { Id = 5, Name = "ABC", CreateTime = DateTime.Now };
        var model2 = new Model { Id = 6, Name = "XYZ", CreateTime = DateTime.Now };
        var topic = "PubsubFilterTest";
        var publisherService = TestEnvironment.ServerServiceProvider.GetService<IPublisherService>();
        var publisher = publisherService.CreatePublisher<Model, int>(topic);
        publisher.Publish(model1);
        publisher.Publish(model2);
        DisposeActions.Push(() => publisher.Dispose());

        var handler1 = new ModelHandler();
        var client1 = TestEnvironment.ClientServiceProvider.GetService<ISubscriberClient>();
        await client1.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.PubsubEndPoint, CancellationToken.None);
        await client1.SubscribeAsync(topic, handler1);
        DisposeActions.Push(() => client1.Dispose());

        var handler2 = new ModelHandler();
        var client2 = TestEnvironment.ClientServiceProvider.GetService<ISubscriberClient>();
        await client2.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.PubsubEndPoint, CancellationToken.None);
        await client2.SubscribeAsync(topic, handler2, m => m.Name.StartsWith("A"));
        DisposeActions.Push(() => client2.Dispose());

        await TestUtil.WaitForConditionAsync(() => handler1.Models.Count == 2, 2000);
        await TestUtil.WaitForConditionAsync(() => handler2.Models.Count == 1, 2000);

        Assert.AreEqual(2, handler1.Models.Count);
        Assert.AreEqual(1, handler2.Models.Count);
    }


    [Test]
    public async Task PubsubSnapshotTest()
    {
        var topic = "PubsubSnapshotTest";
        var publisherService = TestEnvironment.ServerServiceProvider.GetService<IPublisherService>();
        var publisher = publisherService.CreatePublisher<Model, int>(topic);
        DisposeActions.Push(() => publisher.Dispose());
        for (int i = 0; i < 5; ++i)
        {
            var model = new Model { Id = i, Name = $"Model_{i}", CreateTime = DateTime.Now };
            publisher.Publish(model);
        }

        var handler1 = new ModelHandler();
        var client1 = TestEnvironment.ClientServiceProvider.GetService<ISubscriberClient>();
        await client1.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.PubsubEndPoint, CancellationToken.None);
        await client1.SubscribeAsync(topic, handler1);
        DisposeActions.Push(() => client1.Dispose());
        await TestUtil.WaitForConditionAsync(() => handler1.Models.Count == 5, 2000);
        Assert.AreEqual(5, handler1.Models.Count);

        var handler2 = new ModelHandler();
        var client2 = TestEnvironment.ClientServiceProvider.GetService<ISubscriberClient>();
        await client2.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.PubsubEndPoint, CancellationToken.None);
        await client2.SubscribeAsync(topic, handler2);
        DisposeActions.Push(() => client2.Dispose());
        await TestUtil.WaitForConditionAsync(() => handler2.Models.Count == 5, 2000);
        Assert.AreEqual(5, handler1.Models.Count);
        Assert.AreEqual(5, handler2.SnapShotCount);
    }


    [Test]
    public async Task PubsubDeleteTest()
    {
        var model1 = new Model { Id = 5, Name = "ABC", CreateTime = DateTime.Now };
        var model2 = new Model { Id = 6, Name = "XYZ", CreateTime = DateTime.Now };
        var topic = "PubsubDeleteTest";
        var publisherService = TestEnvironment.ServerServiceProvider.GetService<IPublisherService>();
        var publisher = publisherService.CreatePublisher<Model, int>(topic);
        publisher.Publish(model1);
        publisher.Publish(model2);
        DisposeActions.Push(() => publisher.Dispose());

        var handler1 = new ModelHandler();
        var client1 = TestEnvironment.ClientServiceProvider.GetService<ISubscriberClient>();
        await client1.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.PubsubEndPoint, CancellationToken.None);
        await client1.SubscribeAsync(topic, handler1);
        DisposeActions.Push(() => client1.Dispose());

        var handler2 = new ModelHandler();
        var client2 = TestEnvironment.ClientServiceProvider.GetService<ISubscriberClient>();
        await client2.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.PubsubEndPoint, CancellationToken.None);
        await client2.SubscribeAsync(topic, handler2, m => m.Name.StartsWith("A"));
        DisposeActions.Push(() => client2.Dispose());

        await TestUtil.WaitForConditionAsync(() => handler1.Models.Count == 2, 2000);
        await TestUtil.WaitForConditionAsync(() => handler2.Models.Count == 1, 2000);

        publisher.Delete(model1);

        await TestUtil.WaitForConditionAsync(() => handler1.Models.Count == 1, 2000);
        await TestUtil.WaitForConditionAsync(() => handler2.Models.Count == 0, 2000);

        Assert.AreEqual(1, handler1.Models.Count);
        Assert.AreEqual(0, handler2.Models.Count);
        Assert.AreEqual(1, handler1.DeletedKeys.Count);
        Assert.AreEqual(1, handler2.DeletedKeys.Count);

        publisher.Delete(model2);

        await TestUtil.WaitForConditionAsync(() => handler1.Models.Count == 0, 2000);
        await TestUtil.WaitForConditionAsync(() => handler2.Models.Count == 0, 2000);

        Assert.AreEqual(0, handler1.Models.Count);
        Assert.AreEqual(0, handler2.Models.Count);
        Assert.AreEqual(2, handler1.DeletedKeys.Count);
        Assert.AreEqual(1, handler2.DeletedKeys.Count);
    }
}