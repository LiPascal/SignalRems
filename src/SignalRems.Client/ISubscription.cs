using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRems.Client;

internal interface ISubscription: IDisposable
{
    Task StartAsync();
    Task ReStartAsync();
    void Reset();
    void Closed();
}