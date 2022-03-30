﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SignalRems.Core.Events;
using SignalRems.Core.Interfaces;

namespace SignalRems.Client;

public abstract class ClientBase : IClient
{
    protected ClientBase(ILogger logger)
    {
        Logger = logger;
    }

    #region interface IClient

    public async Task ConnectAsync(string url, string endpoint, CancellationToken token)
    {
        ChangeConnectionStatus(ConnectionStatus.Connecting);
        if (Connection != null)
        {
            throw new InvalidOperationException("Connection already connected");
        }

        var address = url + endpoint;
        Connection = new HubConnectionBuilder()
            .WithUrl(address)
            .WithAutomaticReconnect(new RetryPolicy(Logger, address))
            .AddMessagePackProtocol()
            .Build();
        Connection.Reconnecting += ConnectionOnReconnecting;
        Connection.Reconnected += ConnectionOnReconnected;
        Connection.Closed += ConnectionOnClosed;
        var retryAfter = 1;
        while (true)
        {
            try
            {
                await Connection.StartAsync(token);
                ChangeConnectionStatus(ConnectionStatus.Connected);
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
                Logger.LogError(e, "Failed to connect server. Retry after {sec} seconds", retryAfter);
                await Task.Delay(retryAfter * 1000, new CancellationToken());
                retryAfter = Math.Min(retryAfter * 2, 60);
            }
        }
    }

    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

    public ConnectionStatus ConnectionStatus { get; private set; }

    public void Dispose()
    {
        DoDispose();
        GC.SuppressFinalize(this);
    }

    #endregion


    #region protected

    protected HubConnection? Connection { get; private set; }

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
        var handler = ConnectionStatusChanged;
        ConnectionStatus = status;
        handler?.Invoke(this, new ConnectionStatusChangedEventArgs(status));
    }

    #endregion
}