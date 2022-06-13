using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SignalRems.Core.Interfaces;

public interface IRpcService
{
    void RegisterHandler<TRequest, TResponse>(IRpcHandler<TRequest, TResponse> handler, LogLevel level = LogLevel.None)
        where TRequest : IRpcRequest where TResponse : IRpcResponse;

    void RegisterHandler<TRequest, TResponse>(Func<TRequest, Task<TResponse>> handleFunc,
        LogLevel level = LogLevel.None) where TRequest : IRpcRequest where TResponse : IRpcResponse;
}