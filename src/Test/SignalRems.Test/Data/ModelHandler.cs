using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using SignalRems.Core.Interfaces;

namespace SignalRems.Test.Data;

public class ModelHandler : ISubscriptionHandler<Model>
{
    private readonly ConcurrentDictionary<int, Model> _models = new();
    private readonly ConcurrentBag<int> _deleted = new();

    public void OnSnapshotBegin()
    {
        Debug.WriteLine($"OnSnapshotBegin");
    }

    public void OnMessageReceived(Model message)
    {
        Debug.WriteLine($"OnMessageReceived");
        _models[message.Id] = message;
    }

    public void OnMessageDelete(string keyString)
    {
        var key = int.Parse(keyString);
        _models.Remove(key, out _);
        _deleted.Add(key);
    }

    public void OnSnapshotEnd()
    {
        SnapShotCount = _models.Count;
        Debug.WriteLine($"OnSnapshotEnd");
    }

    public void OnError(string error)
    {
        Debug.WriteLine($"OnError {error}");
    }

    public void OnReset()
    {
        Debug.WriteLine($"OnReset");
    }

    public ICollection<Model> Models => _models.Values;

    public IReadOnlyCollection<int> DeletedKeys => _deleted;

    public int SnapShotCount { get; private set; }
}