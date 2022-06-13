using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SignalRems.Core.Interfaces;

public interface IRpcClient : IClient
{
    Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, LogLevel level = LogLevel.None)
        where TRequest : class, IRpcRequest, new()
        where TResponse : class, IRpcResponse, new();
}