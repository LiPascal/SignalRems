using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger _logger;
    private bool _isSubscribing;
    private bool _disposed;

    public Subscription(ILogger<Subscription<T>> logger, HubConnection connection, string topic,
        ISubscriptionHandler<T> handler, Expression<Func<T, bool>>? filter)
    {
        _logger = logger;
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

            _logger.LogInformation("Get {0} items in snap short", snapshot.Length);
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
            var filter = FilterUtil.ToFilterString(_filter);
            _logger.LogInformation("Subscribing to topic {0}, filter = {1}", _topic, filter);
            var error = await _connection.InvokeAsync<string?>(Command.GetSnapshotAndSubscribe, _subscriptionId,
                _topic, FilterUtil.ToFilterString(_filter));
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("Get error when subscribe to topic {0}, filter = {1}, error = {2}", _topic, filter,
                    error);
                _handler.OnError(error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Get error when subscribe to topic {0}, error = {1}", _topic, ex.GetFullMessage());
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


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DoDispose();
    }

    private async void DoDispose()
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

        try
        {
            if (_connection.ConnectionId != null)
            {
                var error = await _connection.InvokeAsync<string?>(Command.UnSubscribe, _subscriptionId);
                if (error != null)
                {
                    _handler.OnError(error);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError("{msg}", e.Message);
            _handler.OnError(e.GetFullMessage());
        }
    }
}