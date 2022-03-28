using System.Linq.Expressions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalRems.Core.Events;
using SignalRems.Core.Interfaces;

namespace SignalRems.Client;

internal sealed class SubscriberClient : ISubscriberClient
{
    private HubConnection? _connection;
    private readonly ILogger<SubscriberClient> _logger;
    private readonly List<ISubscription> _subscriptions = new();

    public SubscriberClient(ILogger<SubscriberClient> logger)
    {
        _logger = logger;
        ConnectionStatus = ConnectionStatus.Disconnected;
    }


    #region interface ISubscriberClient

    public async Task ConnectAsync(string url, string endpoint, CancellationToken token)
    {
        ChangeConnectionStatus(ConnectionStatus.Connecting);
        if (_connection != null)
        {
            throw new InvalidOperationException("Connection already connected");
        }

        var address = url + endpoint;
        _connection = new HubConnectionBuilder()
            .WithUrl(address)
            .WithAutomaticReconnect(new RetryPolicy(_logger, address))
            .AddMessagePackProtocol()
            .Build();
        _connection.Reconnecting += ConnectionOnReconnecting;
        _connection.Reconnected += ConnectionOnReconnected;
        _connection.Closed += ConnectionOnClosed;
        var retryAfter = 1;
        while (true)
        {
            try
            {
                await _connection.StartAsync(token);
                ChangeConnectionStatus(ConnectionStatus.Connected);
                return;
            }
            catch when (token.IsCancellationRequested)
            {
                _connection = null;
                ChangeConnectionStatus(ConnectionStatus.Disconnected);
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to connect server. Retry after {sec} seconds", retryAfter);
                await Task.Delay(retryAfter * 1000, new CancellationToken());
                retryAfter = Math.Min(retryAfter * 2, 60);
            }
        }
    }

    public async Task<IDisposable> SubscribeAsync<T>(string topic, IMessageHandler<T> handler,
        Expression<Func<T, bool>>? filter = null) where T : class, new()
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("Not connected to server");
        }

        var subscription = new Subscription<T>(_connection, topic, handler, filter);
        await subscription.StartAsync();
        lock (subscription)
        {
            if (_connection == null)
            {
                subscription.Dispose();
                return subscription;
            }

            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    public ConnectionStatus ConnectionStatus { get; private set; }

    public async void Dispose()
    {
        List<IDisposable> toDisposables = new();
        if (_connection != null)
        {
            var connection = _connection;
            _connection = null;
            await connection.DisposeAsync();
        }

        lock (_subscriptions)
        {
            toDisposables.AddRange(_subscriptions);
        }

        foreach (var disposable in toDisposables)
        {
            disposable.Dispose();
        }
    }

    #endregion

    #region private

    private async Task ConnectionOnReconnected(string? arg)
    {
        _logger.LogWarning("Reconnected");
        ChangeConnectionStatus(ConnectionStatus.Connected);
        ISubscription[] subscriptions;
        lock (_subscriptions)
        {
            subscriptions = _subscriptions.ToArray();
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Reset();
            await subscription.ReStartAsync();
        }
    }

    private Task ConnectionOnReconnecting(Exception? arg)
    {
        _logger.LogWarning("Disconnected, retrying to re-connect");
        ChangeConnectionStatus(ConnectionStatus.Connecting);
        return Task.CompletedTask;
    }

    private Task ConnectionOnClosed(Exception? arg)
    {
        _logger.LogWarning("Lost connection to server.");
        ISubscription[] subscriptions;
        lock (_subscriptions)
        {
            subscriptions = _subscriptions.ToArray();
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Closed();
        }

        _connection = null;
        ChangeConnectionStatus(ConnectionStatus.Disconnected);
        return Task.CompletedTask;
    }

    private void ChangeConnectionStatus(ConnectionStatus status)
    {
        var handler = ConnectionStatusChanged;
        ConnectionStatus = status;
        handler?.Invoke(this, new ConnectionStatusChangedEventArgs(status));
    }

    #endregion
}