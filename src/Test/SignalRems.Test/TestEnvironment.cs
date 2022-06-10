using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using SignalRems.Client.Extensions;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Utils;
using SignalRems.Server.Extensions;
using SignalRems.Test.Data;
using Interlocked = System.Threading.Interlocked;

namespace SignalRems.Test;

[SetUpFixture]
public class TestEnvironment
{
    public const string ServerUrl = "https://localhost:19906";
    public const string PubsubEndPoint = "/pubsub";
    public const string RpcEndPoint = "/rpc";
    public static IServiceProvider ServerServiceProvider;
    public static IServiceProvider ClientServiceProvider;

    public static TaskCompletionSource EnvReady = new();
    public static int ReadyCnt = 0;
    public static int ExpectedReadyCnt = 2;
    public static ConcurrentStack<Action> DisposeActions { get; set; } = new();

    public static bool UseMessagePack = true;

    [OneTimeSetUp]
    public void Init()
    {
        //use this command line to test MessagePack format 
        //dotnet test  SignalRems.Test.dll -e UseMessagePack="True"
        UseMessagePack = Environment.GetEnvironmentVariable("UseMessagePack") == "True";
        Trace.Listeners.Add(new ConsoleTraceListener());
        Task.Run(async () =>
        {
            var serverBuilder = WebApplication.CreateBuilder();
            serverBuilder.Services.AddSignalRemsService(UseMessagePack, Status.Convertor);
            var serverApp = serverBuilder.Build();
            ServerServiceProvider = serverApp.Services;
            serverApp.MapSignalRemsPublisherHub(PubsubEndPoint);
            serverApp.MapSignalRemsRpcHub(RpcEndPoint);
            serverApp.Lifetime.ApplicationStarted.Register(() =>
            {
                var publisherService = serverApp.Services.GetService<IPublisherService>();
                publisherService.Start();
                DisposeActions.Push(() => publisherService.Dispose());
                if (Interlocked.Increment(ref ReadyCnt) == ExpectedReadyCnt)
                {
                    EnvReady.SetResult();
                }
            });
            DisposeActions.Push(() =>
            {
                serverApp.DisposeAsync();
            });

            await serverApp.RunAsync(ServerUrl);
        });

        Task.Run(async () =>
        {
            var clientBuilder = Host.CreateDefaultBuilder().ConfigureServices(services =>
            {
                services.AddSignalRemsClient(UseMessagePack, Status.Convertor);
            });
            var clientApp = clientBuilder.Build();

            ClientServiceProvider = clientApp.Services;
            DisposeActions.Push(() => clientApp.StopAsync());
            if (Interlocked.Increment(ref ReadyCnt) == ExpectedReadyCnt)
            {
                EnvReady.SetResult();
            }

            await clientApp.RunAsync();
        });
    }

    [OneTimeTearDown]
    public void Dispose()
    {
        while (DisposeActions.TryPop(out var action))
        {
            action();
        }

        Trace.Flush();
    }
}