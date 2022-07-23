using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SignalRems.Core.Events;

namespace SignalRems.Core.Interfaces;

public interface IClient : IDisposable
{
    Task ConnectAsync(string url, string endpoint, CancellationToken token);
    ConnectionStatus ConnectionStatus { get; }
    event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;
    Task ConnectionCompleteTask { get; }
}