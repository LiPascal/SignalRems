using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.AspNetCore.SignalR.Client;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;

namespace SignalRems.Client;

internal class Subscription<T> : ISubscription where T : class, new()
{
    private readonly HubConnection _connection;
    private readonly string _topic;
    private readonly ISubscriptionHandler<T> _handler;
    private readonly Expression<Func<T, bool>>? _filter;
    private readonly string _subscriptionId = Guid.NewGuid().ToString();
    private readonly List<IDisposable> _listeners = new();
    private bool _isSubscribing;

    public Subscription(HubConnection connection, string topic, ISubscriptionHandler<T> handler, Expression<Func<T, bool>>? filter)
    {
        _connection = connection;
        _topic = topic;
        _handler = handler;
        _filter = filter;
    }

    public async Task StartAsync()
    {
        _isSubscribing = true;
        await ReStartAsync();
    }

    public async Task ReStartAsync()
    {
        if (!_isSubscribing)
        {
            return;
        }
        var snapshotDisposable = _connection.On<T[]>(Command.Snapshot, snapshot =>
        {
            _handler.OnSnapshotBegin();
            foreach (var item in snapshot)
            {
                _handler.OnMessageReceived(item);
            }

            _handler.OnSnapshotEnd();
        });
        var publishDisposable = _connection.On<T>(Command.Publish, item => { _handler.OnMessageReceived(item); });
        lock (_listeners)
        {
            foreach (var listener in _listeners)
            {
                listener.Dispose();
            }

            _listeners.Clear();
            _listeners.Add(snapshotDisposable);
            _listeners.Add(publishDisposable);
        }

        try
        {
            var error = await _connection.InvokeAsync<string?>(Command.GetSnapshotAndSubscribe, _subscriptionId,
                _topic, FilterUtil.ToFilterString(_filter));
            if (!string.IsNullOrEmpty(error))
            {
                _handler.OnError(error);
            }
        }
        catch (Exception ex)
        {
            _handler.OnError(ex.GetFullMessage());
        }

       
    }

    public void Reset()
    {
        if (_isSubscribing)
        {
            _handler.OnReset();
        }
    }

    public void Closed()
    {
        if (!_isSubscribing)
        {
            return;
        }

        _handler.OnError("Server is Closed");
        _isSubscribing = false;
    }

    public async void Dispose()
    {
        _isSubscribing = false;
        lock (_listeners)
        {
            foreach (var listener in _listeners)
            {
                listener.Dispose();
            }

            _listeners.Clear();
        }

        if (_connection.ConnectionId != null)
        {
            var error = await _connection.InvokeAsync<string?>(Command.UnSubscribe, _subscriptionId);
            if (error != null)
            {
                _handler.OnError(error);
            }
        }
    }

    
}