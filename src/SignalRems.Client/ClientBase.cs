using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.CompilerServices;
using SignalRems.Core.Events;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Utils;

namespace SignalRems.Client;

public abstract class ClientBase : IClient
{
    private volatile bool _disposed = false;
    private int _connectLoopRunning;
    private CancellationTokenSource _lifetimeCts = new();
    private TaskCompletionSource _connectionCompletionSource = new();

    protected ClientBase(ILogger logger)
    {
        Logger = logger;
    }

    #region interface IClient

    public Task ConnectAsync(string url, string endpoint, CancellationToken token)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClientBase));
        }

        ChangeConnectionStatus(ConnectionStatus.Connecting);
        if (Connection != null)
        {
            throw new InvalidOperationException("Connection already connected");
        }

        Url = url + endpoint;
        // The lifetime source combines the caller's token with Dispose(): cancelling either one stops connecting and
        // releases calls that are waiting for a reconnect. Replace the previous one only after cancelling it, so a
        // second ConnectAsync call (possible after a connect loop gave up) also stops the previous lifetime's waiters
        // and unlinks it from the caller's token.
        var previousLifetime = _lifetimeCts;
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        previousLifetime.Cancel();
        var builder = new HubConnectionBuilder()
            .WithUrl(Url)
            .WithAutomaticReconnect(new RetryPolicy(Logger, Url));
        if (SerializeUtil.UseMessagePack)
        {
            builder = builder.AddMessagePackProtocol(o => o.SerializerOptions = SerializeUtil.ApplyToHubProtocol(o.SerializerOptions));
        }
        else
        {
            builder = builder.AddJsonProtocol(config =>
            {
                foreach (var converter in SerializeUtil.Converters)
                {
                    config.PayloadSerializerOptions.Converters.Add(converter);
                }
            });
        }
        Connection = builder.Build();
        OnConnectionCreated(Connection);
        Connection.Reconnecting += ConnectionOnReconnecting;
        Connection.Reconnected += ConnectionOnReconnected;
        Connection.Closed += ConnectionOnClosed;
        EnsureConnectionStarted(Connection, _lifetimeCts.Token);
        return Task.CompletedTask;
    }

    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

    public ConnectionStatus ConnectionStatus { get; private set; }

    public Task ConnectionCompleteTask => _connectionCompletionSource.Task;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetimeCts.Cancel();
        DoDispose();
        GC.SuppressFinalize(this);
    }

    #endregion


    #region protected

    protected HubConnection? Connection { get; private set; }

    protected string? Url { get; private set; }

    protected ILogger Logger { get; }

    protected bool IsConnected => !_disposed
                                  && ConnectionStatus == ConnectionStatus.Connected
                                  && Connection is { State: HubConnectionState.Connected };

    /// <summary>
    /// Waits until the connection can send messages again. Returns <c>false</c> when the client was disposed or the
    /// lifetime token passed to <see cref="ConnectAsync"/> was cancelled before a connection could be re-established.
    /// </summary>
    protected async Task<bool> WaitForConnectedAsync()
    {
        while (!_disposed)
        {
            if (IsConnected)
            {
                return true;
            }

            var completionSource = _connectionCompletionSource;
            try
            {
                if (completionSource.Task.IsCompleted)
                {
                    // Status and hub state are reported by different sources, give them a moment to agree.
                    await Task.Delay(50, _lifetimeCts.Token).ConfigureAwait(false);
                }
                else
                {
                    await completionSource.Task.WaitAsync(_lifetimeCts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    protected virtual void OnConnectionCreated(HubConnection connection)
    {

    }

    protected virtual Task ConnectionOnReconnected(string? newId)
    {
        Logger.LogWarning("Reconnected");
        ChangeConnectionStatus(ConnectionStatus.Connected);
        return Task.CompletedTask;
    }

    protected virtual Task ConnectionOnReconnecting(Exception? arg)
    {
        Logger.LogWarning("Disconnected, retrying to re-connect");
        ChangeConnectionStatus(ConnectionStatus.Connecting);
        return Task.CompletedTask;
    }

    protected virtual Task ConnectionOnClosed(Exception? exception)
    {
        Logger.LogWarning("Lost connection to server.");
        var connection = Connection;
        if (_disposed || connection == null)
        {
            Connection = null;
            ChangeConnectionStatus(ConnectionStatus.Disconnected);
            return Task.CompletedTask;
        }

        // SignalR stops trying once a connection is closed, but the connection instance can be started again.
        // Keeping the same instance alive preserves the registered handlers, so the client stays usable.
        ChangeConnectionStatus(ConnectionStatus.Connecting);
        EnsureConnectionStarted(connection, _lifetimeCts.Token);
        return Task.CompletedTask;
    }

    protected virtual async void DoDispose()
    {
        if (Connection == null)
        {
            return;
        }

        var connection = Connection;
        Connection = null;
        await connection.DisposeAsync();
    }

    #endregion

    #region private

    private void EnsureConnectionStarted(HubConnection connection, CancellationToken token)
    {
        if (_disposed || Interlocked.Exchange(ref _connectLoopRunning, 1) == 1)
        {
            return;
        }

        Task.Run(async () =>
        {
            var connected = false;
            try
            {
                connected = await StartConnectionAsync(connection, token).ConfigureAwait(false);
            }
            finally
            {
                // Release the guard before dropping the connection, otherwise a ConnectAsync called at this moment
                // would see the flag still set and never start a new attempt.
                Interlocked.Exchange(ref _connectLoopRunning, 0);
                if (!connected && !_disposed && ReferenceEquals(Connection, connection))
                {
                    Connection = null;
                    ChangeConnectionStatus(ConnectionStatus.Disconnected);
                }
            }
        }, CancellationToken.None);
    }

    private async Task<bool> StartConnectionAsync(HubConnection connection, CancellationToken token)
    {
        var retryAfter = 1;
        while (!_disposed && ReferenceEquals(Connection, connection))
        {
            try
            {
                await connection.StartAsync(token).ConfigureAwait(false);
                ChangeConnectionStatus(ConnectionStatus.Connected);
                Logger.LogInformation("Connected to {0}", Url);
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                Logger.LogWarning("Connecting to {0} was cancelled", Url);
                break;
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to connect server. Retry after {sec} seconds: {msg}", retryAfter, e.Message);
                try
                {
                    await Task.Delay(retryAfter * 1000, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                retryAfter = Math.Min(retryAfter * 2, 60);
            }
        }

        return false;
    }

    private void ChangeConnectionStatus(ConnectionStatus status)
    {
        if (status == ConnectionStatus)
        {
            return;
        }

        if (ConnectionStatus == ConnectionStatus.Connected)
        {
            Interlocked.Exchange(ref _connectionCompletionSource, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        }
        var handler = ConnectionStatusChanged;
        ConnectionStatus = status;
        if (ConnectionStatus == ConnectionStatus.Connected)
        {
            _connectionCompletionSource.TrySetResult();
        }
        handler?.Invoke(this, new ConnectionStatusChangedEventArgs(status));
    }

    #endregion
}