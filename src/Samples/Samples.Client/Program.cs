using Samples.Client;
using Samples.Model;
using SignalRems.Client.Extensions;
using SignalRems.Core.Interfaces;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddSignalRemsClient();
        services.AddTransient<ISubscriptionHandler<Person>, PersonManager>();
    })
    .Build();

await host.RunAsync();
