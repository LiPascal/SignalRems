﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack.Resolvers;
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
    private bool _disposed = false;
    private TaskCompletionSource _connectionCompletionSource = new();

    protected ClientBase(ILogger logger)
    {
        Logger = logger;
    }

    #region interface IClient

    public Task ConnectAsync(string url, string endpoint, CancellationToken token)
    {
        ChangeConnectionStatus(ConnectionStatus.Connecting);
        if (Connection != null)
        {
            throw new InvalidOperationException("Connection already connected");
        }

        Url = url + endpoint;
        var builder = new HubConnectionBuilder()
            .WithUrl(Url)
            .WithAutomaticReconnect(new RetryPolicy(Logger, Url));
        builder = builder.AddJsonProtocol(config =>
        {
            foreach (var converter in SerializeUtil.Converters)
            {
                config.PayloadSerializerOptions.Converters.Add(converter);
            }
        });
        Connection = builder.Build();
        Connection.Reconnecting += ConnectionOnReconnecting;
        Connection.Reconnected += ConnectionOnReconnected;
        Connection.Closed += ConnectionOnClosed;
        var retryAfter = 1;
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await Connection.StartAsync(token);
                    ChangeConnectionStatus(ConnectionStatus.Connected);
                    Logger.LogInformation("Connected to {0}, {1}", url, endpoint);
                    return;
                }
                catch when (token.IsCancellationRequested)
                {
                    Connection = null;
                    ChangeConnectionStatus(ConnectionStatus.Disconnected);
                    return;
                }
                catch (Exception e)
                {
                    Logger.LogError("Failed to connect server. Retry after {sec} seconds: {msg}", retryAfter, e.Message);
                    await Task.Delay(retryAfter * 1000, new CancellationToken());
                    retryAfter = Math.Min(retryAfter * 2, 60);
                }
            }
        }, token);
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
        DoDispose();
        GC.SuppressFinalize(this);
    }

    #endregion


    #region protected

    protected HubConnection? Connection { get; private set; }

    protected string? Url { get; private set; }

    protected ILogger Logger { get; }

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
        ChangeConnectionStatus(ConnectionStatus.Disconnected);
        Connection = null;
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

    private void ChangeConnectionStatus(ConnectionStatus status)
    {
        if (status == ConnectionStatus)
        {
            return;
        }

        if (ConnectionStatus == ConnectionStatus.Connected)
        {
            Interlocked.Exchange(ref _connectionCompletionSource, new TaskCompletionSource());
        }
        var handler = ConnectionStatusChanged;
        ConnectionStatus = status;
        if (ConnectionStatus == ConnectionStatus.Connected)
        {
            _connectionCompletionSource.SetResult();
        }
        handler?.Invoke(this, new ConnectionStatusChangedEventArgs(status));
    }

    #endregion
}