using System.Linq.Expressions;
using SignalRems.Core.Events;

namespace SignalRems.Core.Interfaces;

public interface ISubscriberClient: IDisposable
{
    Task ConnectAsync(string url, string endpoint, CancellationToken token);
    Task<IDisposable> SubscribeAsync<T>(string topic, IMessageHandler<T> handler, Expression<Func<T, bool>>? filter = null) where T : class, new();

    event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;
    ConnectionStatus ConnectionStatus { get; }
}