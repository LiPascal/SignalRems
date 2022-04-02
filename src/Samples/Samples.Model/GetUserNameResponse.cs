using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SignalRems.Core.Interfaces;

namespace Samples.Model;

public class GetUserNameResponse : IRpcResponse
{
    public string? RequestId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? UserName { get; set; }
}