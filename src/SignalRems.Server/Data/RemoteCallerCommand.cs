using SignalRems.Core.Interfaces;

namespace SignalRems.Server.Data;

internal class RemoteCallerCommand
{
    public RemoteCallerCommand(IRpcRequest rpcRequest, TaskCompletionSource<(IRpcResponse?, Exception?)> response, Type requestType, Type responseType)
    {
        RpcRequest = rpcRequest;
        Response = response;
        RequestType = requestType;
        ResponseType = responseType;
    }

    public IRpcRequest RpcRequest { get; }
    public TaskCompletionSource<(IRpcResponse?, Exception?)> Response { get; }
    public Type RequestType { get; }
    public Type ResponseType { get; }
}