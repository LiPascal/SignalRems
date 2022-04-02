using SignalRems.Core.Interfaces;
using SignalRems.Core.Models;

namespace SignalRems.Server.Data;

internal class RemoteCallerCommand
{
    public RemoteCallerCommand(string rpcRequest, TaskCompletionSource<RpcResult> response, string requestType, string responseType)
    {
        RpcRequest = rpcRequest;
        Response = response;
        RequestType = requestType;
        ResponseType = responseType;
    }

    public string RpcRequest { get; }
    public TaskCompletionSource<RpcResult> Response { get; }
    public string RequestType { get; }
    public string ResponseType { get; }
}