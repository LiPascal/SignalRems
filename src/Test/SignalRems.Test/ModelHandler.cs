using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SignalRems.Core.Interfaces;

namespace SignalRems.Test
{
    public class ModelHandler: ISubscriptionHandler<Model>
    {
        private readonly ConcurrentBag<Model> _models = new();
        public void OnSnapshotBegin()
        {
            Debug.WriteLine($"OnSnapshotBegin");
        }

        public void OnMessageReceived(Model message)
        {
            Debug.WriteLine($"OnMessageReceived");
            _models.Add(message);
        }

        public void OnSnapshotEnd()
        {
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

        public IReadOnlyCollection<Model> Models => _models;
    }
}
