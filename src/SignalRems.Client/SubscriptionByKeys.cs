using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;

namespace SignalRems.Client;

internal class SubscriptionByKeys<T, TKey> : SubscriptionBase<T>, ISubscriptionByKeys<TKey> where T : class, new()
{
    private readonly List<TKey> _keys = new();

    public SubscriptionByKeys(ILogger<SubscriptionByKeys<T, TKey>> logger, HubConnection connection, string topic,
        ISubscriptionHandler<T> handler, params TKey[] keys) :
        base(logger, connection, topic, handler)
    {
        foreach (var key in keys)
        {
            _keys.Add(key);
        }
    }

    protected override async Task<string?> SendSubscribeCommand(string topic)
    {
        Logger.LogInformation("Subscribing to topic {0} by keys {1}, id = {2}", topic, string.Join(";", _keys),
            SubscriptionId);
        return await Connection.InvokeAsync<string?>(Command.SubscribeWithKeys, SubscriptionId, topic,
            new KeyWrapper<TKey>(_keys.ToArray()).ToJson());
    }

    public async Task<string?> AddKeysAsync(params TKey[] keys)
    {
        Logger.LogInformation("Adding keys to subscription {0} by keys {1}", SubscriptionId, string.Join(";", _keys));
        TKey[] newKeys;
        lock (_keys)
        {
            newKeys = keys.Except(_keys).ToArray();
        }

        if (newKeys.Length == 0)
        {
            return null;
        }

        var error = await Connection.InvokeAsync<string?>(Command.AddSubscriptionKeys, SubscriptionId,
            new KeyWrapper<TKey>(newKeys.ToArray()).ToJson());
        if (error != null)
        {
            return error;
        }

        lock (_keys)
        {
            _keys.AddRange(newKeys);
        }

        return null;
    }

    public async Task<string?> RemoveKeysAsync(params TKey[] keys)
    {
        Logger.LogInformation("Removing keys to subscription {0} by keys {1}", SubscriptionId, string.Join(";", _keys));
        TKey[] toRemove;
        lock (_keys)
        {
            toRemove = keys.Where(x => _keys.Contains(x)).ToArray();
        }

        if (toRemove.Length == 0)
        {
            return null;
        }

        var error = await Connection.InvokeAsync<string?>(Command.RemoveSubscriptionKeys, SubscriptionId,
            new KeyWrapper<TKey>(toRemove.ToArray()).ToJson());
        if (error != null)
        {
            return error;
        }

        lock (_keys)
        {
            foreach (var key in toRemove)
            {
                _keys.Remove(key);
            }
        }

        return null;
    }
}