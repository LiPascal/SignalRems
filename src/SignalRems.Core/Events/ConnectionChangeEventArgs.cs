using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRems.Core.Events;

public class ConnectionStatusChangedEventArgs : EventArgs
{
    public ConnectionStatusChangedEventArgs(ConnectionStatus status)
    {
        Status = status;
    }

    public ConnectionStatus Status { get; private set; }
}

public enum ConnectionStatus { Disconnected, Connecting, Connected}