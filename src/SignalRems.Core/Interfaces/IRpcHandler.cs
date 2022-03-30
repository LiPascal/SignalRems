namespace SignalRems.Core.Interfaces;

public interface IRpcHandler<in TRequest, TResponse> where TRequest : IRpcRequest where TResponse : IRpcResponse
{
    Task<TResponse> HandleRequest(TRequest request);
}