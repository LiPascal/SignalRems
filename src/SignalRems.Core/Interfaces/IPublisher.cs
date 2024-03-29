﻿namespace SignalRems.Core.Interfaces;

public interface IPublisher<in T> : IDisposable where T : class, new()
{
    void Publish(T entity);
    void Publish(IEnumerable<T> entities);
    void Delete(T entity);
    void Delete(IEnumerable<T> entities);

    string Topic { get; }
}