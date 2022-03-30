using SignalRems.Core.Interfaces;

namespace SignalRems.Server.Data;

internal class RemoteCallerCommand
{
    public RemoteCallerCommand(byte[] rpcRequest, TaskCompletionSource<(byte[]?, string?)> response, string requestType, string responseType)
    {
        RpcRequest = rpcRequest;
        Response = response;
        RequestType = requestType;
        ResponseType = responseType;
    }

    public byte[] RpcRequest { get; }
    public TaskCompletionSource<(byte[]?, string?)> Response { get; }
    public string RequestType { get; }
    public string ResponseType { get; }
}