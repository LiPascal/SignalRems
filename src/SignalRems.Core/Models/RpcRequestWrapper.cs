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
    [Key(20)]
    public bool CompressInResult { get; set; } = false;    

    /// <summary>
    /// True when the client resent the request because a previous attempt lost its connection, so the handler may
    /// have already run for the same request.
    /// </summary>
    [Key(21)]
    public bool PossibleResend { get; set; }
}