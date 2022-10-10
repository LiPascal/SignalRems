using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using SignalRems.Core.Attributes;
using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;
using SignalRems.Core.Utils;
using SignalRems.Server.Data;
using SignalRems.Server.Exceptions;
using SignalRems.Server.Hubs;

namespace SignalRems.Server;

internal interface IPublisherWorker
{
    void DispatchCommand(SubscriptionCommand subscriptionCommand);
    bool IsIdle { get; }
    void Work();
}

internal class Publisher<T, TKey> : IPublisher<T>, IPublisherWorker where T : class, new() where TKey : notnull
{
    private readonly ILogger<Publisher<T, TKey>> _logger;

    private readonly Dictionary<string, SubscriptionContext> _subscriptions = new();
    private readonly List<T> _buffer = new();
    private readonly List<T> _deleteBuffer = new();

    private readonly Dictionary<TKey, T> _cache = new();
    private readonly Func<T, TKey> _keyGetter;
    private readonly IHubContext<PubSubHub> _hubContext;
    private readonly IClientCollection<SubscriptionClient> _clients;
    private readonly ConcurrentQueue<SubscriptionCommand> _pendingCommands = new();
    private bool _isDirty = false;

    public Publisher(ILogger<Publisher<T, TKey>> logger, IHubContext<PubSubHub> hubContext, IClientCollection<SubscriptionClient> clients,  string topic)
    {
        _logger = logger;
        _hubContext = hubContext;
        _clients = clients;
        Topic = topic;

        var keyPropertyInfos = typeof(T).GetProperties()
            .Where(x => x.GetCustomAttributes(typeof(KeyAttribute), true).Any()).ToArray();
        if (keyPropertyInfos.Length != 1)
        {
            throw new InvalidPubSubEntityException(keyPropertyInfos.Length, typeof(T));
        }

        if (keyPropertyInfos[0].PropertyType != typeof(TKey))
        {
            throw new InvalidPubSubEntityException(
                $"Type {typeof(T).Name} property {keyPropertyInfos[0].Name} is not type {typeof(TKey).Name}.");
        }

        var getter = keyPropertyInfos[0].GetGetMethod();

        if (getter == null)
        {
            throw new InvalidPubSubEntityException(
                $"Type {typeof(T).Name} property {keyPropertyInfos[0].Name} doesn't have public getter.");
        }

        _keyGetter = entity => GetKey(getter, entity);
    }

    #region interface IPublisher<T>

    public void Publish(T entity)
    {
        lock (_buffer)
        {
            _buffer.Add(entity);
        }

        _isDirty = true;
    }

    public void Publish(IEnumerable<T> entities)
    {
        lock (_buffer)
        {
            _buffer.AddRange(entities);
        }

        _isDirty = true;
    }

    public void Delete(T entity)
    {
        lock (_deleteBuffer)
        {
            _deleteBuffer.Add(entity);
        }

        lock (_buffer)
        {
            _buffer.Add(entity);
        }

        _isDirty = true;
    }

    public void Delete(IEnumerable<T> entities)
    {
        var array = entities.ToArray();
        lock (_deleteBuffer)
        {
            _deleteBuffer.AddRange(array);
        }

        lock (_buffer)
        {
            _buffer.AddRange(array);
        }

        _isDirty = true;
    }

    public string Topic { get; }

    #endregion

    #region interface IPublisherWorker

    public void DispatchCommand(SubscriptionCommand subscriptionCommand)
    {
        _pendingCommands.Enqueue(subscriptionCommand);
        _isDirty = true;
    }

    public bool IsIdle { get; private set; } = true;

    public void Work()
    {
        if (!_isDirty)
        {
            return;
        }

        IsIdle = false;
        _isDirty = false;
        Task.Run(async () =>
        {
            bool updated;
            do
            {
                updated = false;
                while (_pendingCommands.TryDequeue(out var command))
                {
                    await DispatchCommandAsync(command);
                    updated = true;
                }

                updated = await DoPublishAsync() || updated;
            } while (updated);

            IsIdle = true;
        });
    }

    #endregion

    #region private methods

    private async Task DispatchCommandAsync(SubscriptionCommand subscriptionCommand)
    {
        if (!_subscriptions.ContainsKey(subscriptionCommand.Context.SubscriptionId))
        {
            _subscriptions[subscriptionCommand.Context.SubscriptionId] = subscriptionCommand.Context;
        }

        var context = subscriptionCommand.Context;

        var needsSnapshot = false;
        Func<T, bool>? filter = null;
        var keys = Array.Empty<TKey>();
        try
        {
            switch (subscriptionCommand.CommandName)
            {
                case Command.GetSnapshot:
                    needsSnapshot = true;
                    filter = FilterUtil.ToFilter<T>(subscriptionCommand.Parameters[0] as string);
                    break;
                case Command.GetSnapshotAndSubscribe:
                    needsSnapshot = true;
                    context.IsSubscribing = true;
                    context.Filter = filter = FilterUtil.ToFilter<T>(subscriptionCommand.Parameters[0] as string);
                    break;
                case Command.UnSubscribe:
                    context.IsSubscribing = false;
                    _subscriptions.Remove(subscriptionCommand.Context.SubscriptionId);
                    break;
                case Command.Subscribe:
                    context.IsSubscribing = true;
                    filter = FilterUtil.ToFilter<T>(subscriptionCommand.Parameters[0] as string);
                    break;
                case Command.SubscribeWithKeys:
                    context.IsSubscribing = true;
                    keys = KeyWrapper<TKey>.FromJson(subscriptionCommand.Parameters[0] as string)?.Keys;
                    break;
                case Command.AddSubscriptionKeys:
                case Command.RemoveSubscriptionKeys:
                    keys = KeyWrapper<TKey>.FromJson(subscriptionCommand.Parameters[0] as string)?.Keys;
                    break;
                default:
                    subscriptionCommand.CompleteSource.SetResult("Should not happen here, unknown action");
                    return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when process subscribe command");
            _subscriptions.Remove(subscriptionCommand.Context.SubscriptionId);
            subscriptionCommand.CompleteSource.SetResult(ex.GetFullMessage());
            return;
        }

        if (needsSnapshot && _clients[context.ClientId].IsConnected)
        {
            var snapshot = (filter == null ? _cache.Values : _cache.Values.Where(filter)).ToArray();
            try
            {
                var client = _hubContext.Clients.Client(context.ClientId);
                await client.SendAsync(Command.Snapshot, snapshot);
                subscriptionCommand.CompleteSource.SetResult(null);
                _logger.LogInformation("Sending snapshot to client {0}", context.ClientId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Get error when sending snapshot.", ex);
                subscriptionCommand.CompleteSource.SetResult($"Get error when sending snapshot.{ex.GetFullMessage()}");
            }
        }
        else if (keys != null && keys.Any() && _clients[context.ClientId].IsConnected)
        {
            if (!keys.All(x => x is TKey))
            {
                subscriptionCommand.CompleteSource.SetResult($"The key is not type {typeof(TKey).Name}");
                return;
            }

            try
            {
                var client = _hubContext.Clients.Client(context.ClientId);
                if (subscriptionCommand.CommandName == Command.RemoveSubscriptionKeys)
                {
                    foreach (var key in keys.Where(_cache.ContainsKey))
                    {
                        await client.SendAsync(Command.Delete, key.ToString());
                    }

                    context.Keys = context.Keys.OfType<TKey>().Except(keys).OfType<object>().ToArray();
                }
                else
                {
                    var elements = keys.Where(_cache.ContainsKey).Select(x => _cache[x]).ToArray();
                    if (elements.Any())
                    {
                        if (subscriptionCommand.CommandName == Command.AddSubscriptionKeys)
                        {
                            foreach (var element in elements)
                            {
                                await client.SendAsync(Command.Publish, element);
                            }

                            context.Keys = context.Keys.OfType<TKey>().Concat(keys).Distinct()
                                .OfType<object>().ToArray();
                        }
                        else
                        {
                            await client.SendAsync(Command.Snapshot, elements);
                            context.Keys = keys.OfType<object>().ToArray();
                        }
                    }
                }

                subscriptionCommand.CompleteSource.SetResult(null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Get error when sending snapshot.", ex);
                subscriptionCommand.CompleteSource.SetResult($"Get error when sending snapshot.{ex.GetFullMessage()}");
            }
        }
        else
        {
            subscriptionCommand.CompleteSource.SetResult(null);
        }
    }

    public async ValueTask<bool> DoPublishAsync()
    {
        var updated = false;

        T[] buffer;
        T[] toDelete;
        lock (_buffer)
        {
            buffer = _buffer.ToArray();
            _buffer.Clear();
        }

        lock (_deleteBuffer)
        {
            toDelete = _deleteBuffer.ToArray();
            _deleteBuffer.Clear();
        }

        if (buffer.Any())
        {
            foreach (var entity in buffer)
            {
                var key = _keyGetter(entity);
                var isDelete = false;
                if (toDelete.Contains(entity))
                {
                    if (!_cache.ContainsKey(key))
                    {
                        continue;
                    }

                    _cache.Remove(key);
                    isDelete = true;
                }
                else
                {
                    _cache[key] = entity;
                }


                var contexts = _subscriptions.Values.Where(x => x.IsSubscribing);
                foreach (var context in contexts)
                {
                    if (!_clients[context.ClientId].IsConnected)
                    {
                        _subscriptions.Remove(context.SubscriptionId);
                        continue;
                    }

                    if (context.Filter is Func<T, bool> filter && !filter(entity))
                    {
                        continue;
                    }

                    if (context.Keys.Any() && !context.Keys.Any(x => Equals(x, key)))
                    {
                        continue;
                    }

                    try
                    {
                        if (isDelete)
                        {
                            await _hubContext.Clients.Client(context.ClientId)
                                .SendAsync(Command.Delete, key.ToString());
                        }
                        else
                        {
                            await _hubContext.Clients.Client(context.ClientId)
                                .SendAsync(Command.Publish, entity);
                        }
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
        if (key == null)
        {
            throw new InvalidPubSubEntityException("Key property can not be null");
        }

        return (TKey)key;
    }

    public void Dispose()
    {
    }
}