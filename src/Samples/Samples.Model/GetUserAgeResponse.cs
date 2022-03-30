using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SignalRems.Core.Interfaces;

namespace Samples.Model;

public class GetUserAgeResponse : IRpcResponse
{
    public string? RequestId { get; set; }
    public int UserAge { get; set; }
    public Exception? Exception { get; set; }
}