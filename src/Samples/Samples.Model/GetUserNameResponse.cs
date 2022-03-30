using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using SignalRems.Core.Interfaces;

namespace Samples.Model;

[MessagePackObject]
public class GetUserNameResponse : IRpcResponse
{
    [Key(0)]
    public string? RequestId { get; set; }
    [Key(1)]
    public bool Success { get; set; }
    [Key(2)]
    public string? Error { get; set; }
    [Key(3)]
    public string? UserName { get; set; }
}