using SignalRems.Core.Models;

namespace SignalRems.Server.Interfaces
{
    internal interface IRpcServer
    {
        Task<RpcResultWrapper> ProcessAsync(RpcRequestWrapper request, string requestType, string responseType);
    }
}
