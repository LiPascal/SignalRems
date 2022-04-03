using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using Newtonsoft.Json;
using SignalRems.Core.Interfaces;


[assembly: InternalsVisibleTo("SignalRems.Server")]
[assembly: InternalsVisibleTo("SignalRems.Client")]
namespace SignalRems.Core.Models;

[MessagePackObject]
internal class RpcResultWrapper: IRpcMessageWrapper
{
    public RpcResultWrapper()
    {
    }

    [IgnoreMember]
    public string? JsonPayload { get; set; }

    [Key(0)]
    [JsonIgnore]
    public byte[]? BinaryPayload { get; set; }
    public string? Error { get; set; }
}