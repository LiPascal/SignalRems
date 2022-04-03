using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;

namespace SignalRems.Server.Data;

internal class RemoteCallerCommand
{
    public RemoteCallerCommand(RpcRequestWrapper rpcRequest, TaskCompletionSource<RpcResultWrapper> response, string requestType, string responseType)
    {
        RpcRequest = rpcRequest;
        Response = response;
        RequestType = requestType;
        ResponseType = responseType;
    }

    public RpcRequestWrapper RpcRequest { get; }
    public TaskCompletionSource<RpcResultWrapper> Response { get; }
    public string RequestType { get; }
    public string ResponseType { get; }
}