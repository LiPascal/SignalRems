using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRems.Core.Interfaces
{
    public interface IRpcRequest
    {
        string RequestId { get; }
    }
}
