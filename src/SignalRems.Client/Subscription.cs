using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using SignalRems.Client.Exceptions;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;

namespace SignalRems.Client;

internal class Subscription<T> : ISubscription where T : class, new()
{
    private readonly HubConnection _connection;
    private readonly string _topic;
    private readonly IMessageHandler<T> _handler;
    private readonly Expression<Func<T, bool>>? _filter;
    private readonly string _subscriptionId = Guid.NewGuid().ToString();
    private readonly List<IDisposable> _listeners = new();
    private bool _isSubscribing;

    public Subscription(HubConnection connection, string topic, IMessageHandler<T> handler, Expression<Func<T, bool>>? filter)
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
        
        var exception = await _connection.InvokeAsync<Exception?>(Command.GetSnapshotAndSubscribe, _subscriptionId, _topic, FilterUtil.ToFilterString(_filter));
        if (exception != null)
        {
            _handler.OnException(exception);
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

        _handler.OnException(new ServerCloseException());
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

        var exception = await _connection.InvokeAsync<Exception?>(Command.UnSubscribe, _subscriptionId);
        if (exception != null)
        {
            _handler.OnException(exception);
        }
    }

    
}