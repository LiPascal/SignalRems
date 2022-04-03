using System.Runtime.CompilerServices;
using MessagePack;
using Newtonsoft.Json;
using SignalRems.Core.Interfaces;

[assembly: InternalsVisibleTo("SignalRems.Server")]
[assembly: InternalsVisibleTo("SignalRems.Client")]
namespace SignalRems.Core.Models;

[MessagePackObject]
internal class RpcRequestWrapper: IRpcMessageWrapper
{
    [IgnoreMember] 
    public string? JsonPayload { get; set; }

    [Key(0)]
    [JsonIgnore]
    public byte[]? BinaryPayload { get; set; }
}