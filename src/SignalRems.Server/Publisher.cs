using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using SignalRems.Core.Attributes;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;
using SignalRems.Server.Data;
using SignalRems.Server.Exceptions;

namespace SignalRems.Server;

internal interface IPublisherWorker
{
    Task DispatchCommandAsync(ClientCommand command);
    ValueTask<bool> WorkAsync();
}

internal class Publisher<T, TKey> : IPublisher<T>, IPublisherWorker where T : class, new() where TKey : notnull
{
    private readonly ILogger<Publisher<T, TKey>> _logger;

    private readonly Dictionary<string, SubscriptionContext> _subscriptions = new();
    private readonly ContextManager _clientManager;
    private readonly List<T> _buffer = new();
    private readonly Dictionary<TKey, T> _cache = new();
    private readonly Func<T, TKey> _keyGetter;
    private readonly IHubContext<PubSubHub> _hubContext;

    public Publisher(ILogger<Publisher<T, TKey>> logger, IHubContext<PubSubHub> hubContext, ContextManager clientManager, string topic)
    {
        _logger = logger;
        _hubContext = hubContext;
        _clientManager = clientManager;
        Topic = topic;

        var keyPropertyInfos = typeof(T).GetProperties()
            .Where(x => x.GetCustomAttributes(typeof(KeyAttribute), true).Any()).ToArray();
        if (keyPropertyInfos.Length != 1) throw new InvalidPubSubEntityException(keyPropertyInfos.Length, typeof(T));

        if (keyPropertyInfos[0].PropertyType != typeof(TKey))
            throw new InvalidPubSubEntityException(
                $"Type {typeof(T).Name} property {keyPropertyInfos[0].Name} is not type {typeof(TKey).Name}.");

        var getter = keyPropertyInfos[0].GetGetMethod();

        if (getter == null)
            throw new InvalidPubSubEntityException(
                $"Type {typeof(T).Name} property {keyPropertyInfos[0].Name} doesn't have public getter.");

        _keyGetter = entity => Publisher<T, TKey>.GetKey(getter, entity);
    }

    #region interface IPublisher<T>
    public void Publish(T entity)
    {
        lock (_buffer)
        {
            _buffer.Add(entity);
        }
    }

    public void Publish(IEnumerable<T> entities)
    {
        lock (_buffer)
        {
            _buffer.AddRange(entities);
        }
    }

    public string Topic { get; }


    #endregion

    #region interface IPublisherWorker

    public async Task DispatchCommandAsync(ClientCommand command)
    {
        if (!_subscriptions.ContainsKey(command.Context.SubscriptionId))
        {
            _subscriptions[command.Context.SubscriptionId] = command.Context;
        }

        var context = command.Context;

        var needsSnapshot = false;
        Func<T, bool>? filter = null;
        try
        {
            switch (command.CommandName)
            {
                case Command.GetSnapshot:
                    needsSnapshot = true;
                    filter = FilterUtil.ToFilter<T>(command.Parameters[0] as string);
                    break;
                case Command.GetSnapshotAndSubscribe:
                    needsSnapshot = true;
                    context.IsSubscribing = true;
                    context.Filter = filter = FilterUtil.ToFilter<T>(command.Parameters[0] as string);
                    break;
                case Command.UnSubscribe:
                    context.IsSubscribing = false;
                    _subscriptions.Remove(command.Context.SubscriptionId);
                    break;
                case Command.Subscribe:
                    context.IsSubscribing = true;
                    filter = FilterUtil.ToFilter<T>(command.Parameters[0] as string);
                    break;
                default:
                    command.CompleteSource.SetResult(new InvalidOperationException("Should not happen here, unknown action"));
                    return;
            }
        }
        catch (Exception ex)
        {
            _subscriptions.Remove(command.Context.SubscriptionId);
            command.CompleteSource.SetResult(ex);
            return;
        }

        if (needsSnapshot && _clientManager.Clients[context.ClientId].IsConnected)
        {
            var snapshot = (filter == null ? _cache.Values : _cache.Values.Where(filter)).ToArray();
            try
            {
                var client = _hubContext.Clients.Client(context.ClientId);
                await client.SendAsync(Command.Snapshot, snapshot);
                command.CompleteSource.SetResult(null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Get error when sending snapshot.", ex);
                command.CompleteSource.SetResult(new Exception("Get error when sending snapshot."));
            }
        }
        else
        {
            command.CompleteSource.SetResult(null);
        }
    }

    public async ValueTask<bool> WorkAsync()
    {
        var updated = false;

        T[] toPub;
        lock (_buffer)
        {
            toPub = _buffer.ToArray();
            _buffer.Clear();
        }

        if (toPub.Any())
        {
            foreach (var entity in toPub)
            {
                var key = _keyGetter(entity);
                _cache[key] = entity;
                var contexts = _subscriptions.Values.Where(x => x.IsSubscribing);
                foreach (var context in contexts)
                {
                    if (!_clientManager.Clients[context.ClientId].IsConnected)
                    {
                        _subscriptions.Remove(context.SubscriptionId);
                        continue;
                    }
                    if (context.Filter is Func<T, bool> filter && !filter(entity))
                    {
                        continue;
                    }
                    try
                    {
                        await _hubContext.Clients.Client(context.ClientId).SendAsync(Command.Publish, entity);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Get error when publishing message", ex);
                    }
                }

            }

            updated = true;
        }


        return updated;
    }

    #endregion
    

   

    private static TKey GetKey(MethodBase getter, T entity)
    {
        var key = getter.Invoke(entity, null);
        if (key == null) throw new InvalidPubSubEntityException("Key property can not be null");
        return (TKey)key;
    }
    
    public void Dispose()
    {
    }

    
}