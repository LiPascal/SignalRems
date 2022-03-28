using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRems.Core.Interfaces;

public interface IPublisherService : IDisposable
{
    IPublisher<T> CreatePublisher<T, TKey>(string topic) where T : class, new() where TKey : notnull;
    void Start();
}