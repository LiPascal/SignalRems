using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRems.Core.Interfaces;

public interface IRpcResponse
{
    string? RequestId { get; set; }
    bool Success { get; set; }
    string? Error { get; set; }
}