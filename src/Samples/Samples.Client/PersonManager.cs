using Samples.Model;
using SignalRems.Core.Interfaces;

namespace Samples.Client;

internal class PersonManager : ISubscriptionHandler<Person>
{
    private readonly ILogger<PersonManager> _logger;

    public PersonManager(ILogger<PersonManager> logger)
    {
        _logger = logger;
    }

    public void OnSnapshotBegin()
    {
        _logger.LogInformation("Snapshot Begin.");
    }

    public void OnMessageReceived(Person message)
    {
        _logger.LogInformation("Receive Id = {Id}, Name = {Name}, Age = {Age}", message.Id, message.Name, message.Age);
    }

    public void OnSnapshotEnd()
    {
        _logger.LogInformation("Snapshot End.");
    }

    public void OnError(string error)
    {
        _logger.LogInformation("OnError {message}", error);
    }

    public void OnReset()
    {
        _logger.LogInformation("Reset");
    }
}