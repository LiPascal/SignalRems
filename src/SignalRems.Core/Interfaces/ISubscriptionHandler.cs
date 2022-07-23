namespace SignalRems.Core.Interfaces;

public interface ISubscriptionHandler<in T> where T : class, new()
{
    void OnSnapshotBegin();

    void OnMessageReceived(T message);

    void OnMessageDelete(string keyString);

    void OnSnapshotEnd();

    void OnError(string error);

    void OnReset();
}