using System.Linq.Expressions;
using SignalRems.Core.Events;

namespace SignalRems.Core.Interfaces;

public interface ISubscriberClient: IClient
{
    Task<IDisposable> SubscribeAsync<T>(string topic, ISubscriptionHandler<T> handler, Expression<Func<T, bool>>? filter = null) where T : class, new();

}