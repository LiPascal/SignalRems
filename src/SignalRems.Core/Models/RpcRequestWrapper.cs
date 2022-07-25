using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using MessagePack;
using SignalRems.Core.Interfaces;

[assembly: InternalsVisibleTo("SignalRems.Server")]
[assembly: InternalsVisibleTo("SignalRems.Client")]
namespace SignalRems.Core.Models;

[MessagePackObject]
internal class RpcRequestWrapper: RpcWrapperBase
{
    [Key(2)]
    public bool CompressInResult { get; set; } = false;

    public string ReplyOnTopic { get; set; } = null!;
}