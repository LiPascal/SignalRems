namespace SignalRems.Core.Interfaces;

public interface ISubscriptionHandler<in T> where T : class, new()
{
    void OnSnapshotBegin();

    void OnMessageReceived(T message);

    void OnSnapshotEnd();

    void OnException(Exception e);

    void OnReset();
}