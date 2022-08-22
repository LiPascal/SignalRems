using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;

namespace SignalRems.Client;

internal class Subscription<T> : SubscriptionBase<T> where T : class, new()
{
    private readonly Expression<Func<T, bool>>? _filter;
    public Subscription(ILogger<Subscription<T>> logger, HubConnection connection, string topic,
        ISubscriptionHandler<T> handler, Expression<Func<T, bool>>? filter) :
        base(logger, connection, topic, handler)
    {
        _filter = filter;
    }

    protected override async Task<string?> SendSubscribeCommand(string topic)
    {
        var filter = FilterUtil.ToFilterString(_filter);
        Logger.LogInformation("Subscribing to topic {0}, filter = {1}", topic, filter);
        return await Connection.InvokeAsync<string?>(Command.GetSnapshotAndSubscribe, SubscriptionId, topic,
            FilterUtil.ToFilterString(_filter));
    }
}