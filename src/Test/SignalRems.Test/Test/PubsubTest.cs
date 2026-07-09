using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SignalRems.Core.Interfaces;
using SignalRems.Test.Data;
using SignalRems.Test.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
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
        Assert.That(handler.Models.Count, Is.EqualTo(1));
        var model2 = handler.Models.First();
        Assert.That(model2.Id, Is.EqualTo(model.Id));
        Assert.That(model2.Name, Is.EqualTo(model.Name));
        Assert.That(model2.CreateTime, Is.EqualTo(model.CreateTime));
        Assert.That(model.Marks.SequenceEqual(model2.Marks), Is.True);
        Assert.That(Status.Done, Is.EqualTo(model.Status));
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
        await client2.SubscribeAsync(topic, handler2, m => m.Name.StartsWith('A'));
        DisposeActions.Push(() => client2.Dispose());

        await TestUtil.WaitForConditionAsync(() => handler1.Models.Count == 2, 2000);
        await TestUtil.WaitForConditionAsync(() => handler2.Models.Count == 1, 2000);

        Assert.That(handler1.Models.Count, Is.EqualTo(2));
        Assert.That(handler2.Models.Count, Is.EqualTo(1));

        model1.Name = "BBC";
        publisher.Publish(model1);

        await TestUtil.WaitForConditionAsync(() => handler1.Models.Count == 2, 2000);
        await TestUtil.WaitForConditionAsync(() => handler2.Models.Count == 0, 2000);

        Assert.That(handler1.Models.Count, Is.EqualTo(2));
        Assert.That(handler2.Models.Count, Is.EqualTo(0));

        model2.Name = "AYZ";
        publisher.Publish(model2);

        await TestUtil.WaitForConditionAsync(() => handler1.Models.Count == 2, 2000);
        await TestUtil.WaitForConditionAsync(() => handler2.Models.Count == 1, 2000);

        Assert.That(handler1.Models.Count, Is.EqualTo(2));
        Assert.That(handler2.Models.Count, Is.EqualTo(1));

        model2.Name = "CYZ";
        publisher.Publish(model2);

        await TestUtil.WaitForConditionAsync(() => handler1.Models.Count == 2, 2000);
        await TestUtil.WaitForConditionAsync(() => handler2.Models.Count == 0, 2000);

        Assert.That(handler1.Models.Count, Is.EqualTo(2));
        Assert.That(handler2.Models.Count, Is.EqualTo(0));
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
        Assert.That(handler1.Models.Count, Is.EqualTo(5));

        var handler2 = new ModelHandler();
        var client2 = TestEnvironment.ClientServiceProvider.GetService<ISubscriberClient>();
        await client2.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.PubsubEndPoint, CancellationToken.None);
        await client2.SubscribeAsync(topic, handler2);
        DisposeActions.Push(() => client2.Dispose());
        await TestUtil.WaitForConditionAsync(() => handler2.Models.Count == 5, 2000);
        Assert.That(handler1.Models.Count, Is.EqualTo(5));
        Assert.That(handler2.SnapShotCount, Is.EqualTo(5));
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

        Assert.That(handler1.Models.Count, Is.EqualTo(1));
        Assert.That(handler2.Models.Count, Is.EqualTo(0));
        Assert.That(handler1.DeletedKeys.Count, Is.EqualTo(1));
        Assert.That(handler2.DeletedKeys.Count, Is.EqualTo(1));

        publisher.Delete(model2);

        await TestUtil.WaitForConditionAsync(() => handler1.Models.Count == 0, 2000);
        await TestUtil.WaitForConditionAsync(() => handler2.Models.Count == 0, 2000);

        Assert.That(handler1.Models.Count, Is.EqualTo(0));
        Assert.That(handler2.Models.Count, Is.EqualTo(0));
        Assert.That(handler1.DeletedKeys.Count, Is.EqualTo(2));
        Assert.That(handler2.DeletedKeys.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task PubsubWithKeysTest()
    {
        var handler = new ModelHandler();
        var model1 = new Model { Id = 105, Name = "ABC", CreateTime = DateTime.Now };
        var model2 = new Model { Id = 106, Name = "XYZ", CreateTime = DateTime.Now };
        var model3 = new Model { Id = 107, Name = "XYZ", CreateTime = DateTime.Now };
        var model4 = new Model { Id = 108, Name = "XYZ", CreateTime = DateTime.Now };
        var topic = "PubsubWithKeysTest";
        var publisherService = TestEnvironment.ServerServiceProvider.GetService<IPublisherService>();
        var publisher = publisherService.CreatePublisher<Model, int>(topic);
        publisher.Publish(model1);

        DisposeActions.Push(() => publisher.Dispose());
        var client = TestEnvironment.ClientServiceProvider.GetService<ISubscriberClient>();
        await client.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.PubsubEndPoint, CancellationToken.None);
        await client.SubscribeWithKeysAsync(topic, handler, 105, 106);

        DisposeActions.Push(() => client.Dispose());
        await TestUtil.WaitForConditionAsync(() => handler.Models.Count == 1, 2000);
        Assert.That(handler.Models.Count, Is.EqualTo(1));

        publisher.Publish(model2);
        await TestUtil.WaitForConditionAsync(() => handler.Models.Count == 2, 2000);
        Assert.That(handler.Models.Count, Is.EqualTo(2));
		
        publisher.Publish(model3);
        await TestUtil.WaitForConditionAsync(() => handler.Models.Count == 2, 2000);
        Assert.That(handler.Models.Count, Is.EqualTo(2));

        await client.SubscribeWithKeysAsync(topic, handler, 107, 108);
        await TestUtil.WaitForConditionAsync(() => handler.Models.Count == 3, 2000);
        Assert.That(handler.Models.Count, Is.EqualTo(3));

        await client.UnSubscribeWithKeysAsync(topic, handler, 105, 108);
        await TestUtil.WaitForConditionAsync(() => handler.Models.Count == 2, 2000);
        Assert.That(handler.Models.Count, Is.EqualTo(2));

        publisher.Publish(model4);
        await TestUtil.WaitForConditionAsync(() => handler.Models.Count == 2, 2000);
        Assert.That(handler.Models.Count, Is.EqualTo(2));

    }

    [Test]
    public async Task MultiClientsTest()
    {       
        var client_cnt = 8;
        var handlers = new ModelHandler[client_cnt];
        var model1 = new Model { Id = 15, Name = "ABC", CreateTime = DateTime.Now };
        var model2 = new Model { Id = 16, Name = "XYZ", CreateTime = DateTime.Now };
        var model3 = new Model { Id = 17, Name = "ABC", CreateTime = DateTime.Now };
        var model4 = new Model { Id = 18, Name = "XYZ", CreateTime = DateTime.Now };
        var topic = "MultiClientsTest";
        var publisherService = TestEnvironment.ServerServiceProvider.GetService<IPublisherService>();
        var publisher = publisherService.CreatePublisher<Model, int>(topic);
        DisposeActions.Push(() => publisher.Dispose());

        publisher.Publish(model1);        
        for(int i = 0; i < client_cnt; ++i)
        {
            var local = i;
            var handler = handlers[local] = new ModelHandler();
            var client = TestEnvironment.ClientServiceProvider.GetService<ISubscriberClient>();
            await client.ConnectAsync(TestEnvironment.ServerUrl, TestEnvironment.PubsubEndPoint, CancellationToken.None);
            if(i % 2 == 0)
            {
                await client.SubscribeAsync(topic, handler);
            }
            else
            {
                await client.SubscribeAsync(topic, handler, m => m.Name.StartsWith('X'));
            }            
            DisposeActions.Push(() => client.Dispose());
        }
        
        for (int i = 0; i < client_cnt; ++i)
        {
            var local = i;           
            if (i % 2 == 0)
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 1, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(1));
            }
            else
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 0, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(0));
            }
        }

        publisher.Publish(model2);

        for (int i = 0; i < client_cnt; ++i)
        {
            var local = i;
            if (i % 2 == 0)
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 2, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(2));
            }
            else
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 1, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(1));
            }
        }

        publisher.Publish(model3);
        publisher.Publish(model4);

        for (int i = 0; i < client_cnt; ++i)
        {
            var local = i;
            if (i % 2 == 0)
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 4, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(4));
            }
            else
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 2, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(2));
            }
        }

        publisher.Delete(model2);
        for (int i = 0; i < client_cnt; ++i)
        {
            var local = i;
            if (i % 2 == 0)
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 3, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(3));
            }
            else
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 1, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(1));
            }
        }

        model1.Name = "XABC";
        publisher.Publish(model1);

        for (int i = 0; i < client_cnt; ++i)
        {
            var local = i;
            if (i % 2 == 0)
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 3, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(3));
            }
            else
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 2, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(2));
            }
        }

        model1.Name = "ABC";
        publisher.Publish(model1);

        for (int i = 0; i < client_cnt; ++i)
        {
            var local = i;
            if (i % 2 == 0)
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 3, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(3));
            }
            else
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 1, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(1));
            }
        }

        publisher.Delete(model1);
        for (int i = 0; i < client_cnt; ++i)
        {
            var local = i;
            if (i % 2 == 0)
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 2, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(2));
            }
            else
            {
                await TestUtil.WaitForConditionAsync(() => handlers[local].Models.Count == 1, 2000);
                Assert.That(handlers[local].Models.Count, Is.EqualTo(1));
            }
        }
    }    
}