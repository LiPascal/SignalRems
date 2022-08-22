using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;

namespace SignalRems.Client;

internal abstract class SubscriptionBase<T> : ISubscription where T : class, new()
{
    
    private readonly string _topic;
    private readonly ISubscriptionHandler<T> _handler;
    private readonly List<IDisposable> _listeners = new();
    private bool _isSubscribing;
    private bool _disposed;

    protected readonly string SubscriptionId = Guid.NewGuid().ToString();
    protected readonly HubConnection Connection;
    protected readonly ILogger Logger;

    protected SubscriptionBase(ILogger logger, HubConnection connection, string topic,
        ISubscriptionHandler<T> handler)
    {
        Logger = logger;
        Connection = connection;
        _topic = topic;
        _handler = handler;
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

        var snapshotDisposable = Connection.On<T[]>(Command.Snapshot, snapshot =>
        {
            _handler.OnSnapshotBegin();
            foreach (var item in snapshot)
            {
                _handler.OnMessageReceived(item);
            }

            Logger.LogInformation("Get {0} items in snap short", snapshot.Length);
            _handler.OnSnapshotEnd();
        });
        var publishDisposable = Connection.On<T>(Command.Publish, item => { _handler.OnMessageReceived(item); });
        var deleteDisposable =
            Connection.On<string>(Command.Delete, keyString => { _handler.OnMessageDelete(keyString); });
        lock (_listeners)
        {
            foreach (var listener in _listeners)
            {
                listener.Dispose();
            }

            _listeners.Clear();
            _listeners.Add(snapshotDisposable);
            _listeners.Add(publishDisposable);
            _listeners.Add(deleteDisposable);
        }

        try
        {
            var error = await SendSubscribeCommand(_topic);
            if (!string.IsNullOrEmpty(error))
            {
                Logger.LogError("Get error when subscribe to topic {0}, error = {1}", _topic, error);
                _handler.OnError(error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Get error when subscribe to topic {0}, error = {1}", _topic, ex.GetFullMessage());
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
            if (Connection.ConnectionId != null)
            {
                var error = await Connection.InvokeAsync<string?>(Command.UnSubscribe, SubscriptionId);
                if (error != null)
                {
                    _handler.OnError(error);
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError("{msg}", e.Message);
            _handler.OnError(e.GetFullMessage());
        }
    }

    protected abstract Task<string?> SendSubscribeCommand(string topic);
}