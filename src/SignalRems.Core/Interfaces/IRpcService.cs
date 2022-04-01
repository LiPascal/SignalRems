using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalRems.Core.Interfaces
{
    public interface IRpcService : IDisposable
    {
        void RegisterHandler<TRequest, TResponse>(IRpcHandler<TRequest, TResponse> handler) where TRequest : IRpcRequest where TResponse : IRpcResponse;

        void RegisterHandler<TRequest, TResponse>(Func<TRequest, Task<TResponse>> handleFunc) where TRequest : IRpcRequest where TResponse : IRpcResponse;

        void Start();
    }
}
